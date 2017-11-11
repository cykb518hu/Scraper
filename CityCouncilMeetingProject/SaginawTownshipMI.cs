using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class SaginawTownshipMI : City
    {
        private List<string> docUrls = null;

        public SaginawTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SaginawTownshipMI",
                CityName = "SaginawTownship",
                CityUrl = "http://www.saginawtownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SaginawTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            //2017-01-09
            Regex dateReg1 = new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string docListUrl = url.Split('*')[1];

                HtmlDocument listDoc = web.Load(docListUrl);
                HtmlNodeCollection fileNodeCollection = listDoc.DocumentNode.SelectNodes("//div[@id='file_name']/a[@href]");

                if(fileNodeCollection != null)
                {
                    foreach(HtmlNode fileNode in fileNodeCollection)
                    {
                        string fileUrl = fileNode.Attributes["href"].Value;
                        fileUrl = fileUrl.StartsWith("http") ? fileUrl : this.cityEntity.CityUrl + "/" + fileUrl;
                        string meetingText = dateReg.Match(fileUrl).ToString();
                        meetingText = string.IsNullOrEmpty(meetingText) ? dateReg.Match(fileNode.InnerText).ToString() : meetingText;
                        meetingText = string.IsNullOrEmpty(meetingText) ? dateReg1.Match(fileUrl).Value : meetingText;
                        DateTime meetingDate = dateReg1.IsMatch(fileUrl) ? DateTime.ParseExact(meetingText, "yyyy-MM-dd", null) : DateTime.MinValue;
                        bool isDate = meetingDate == DateTime.MinValue ?
                            DateTime.TryParse(meetingText, out meetingDate)
                            : true;

                        if(!isDate || meetingDate < this.dtStartFrom || fileUrl.Contains("2015"))
                        {
                            Console.WriteLine("Earlier than {0}...", this.dtStartFrom.ToString("yyyy-MM-dd"));
                            continue;
                        }

                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if(localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.DocId = Guid.NewGuid().ToString();
                            localDoc.Checked = false;
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.DocSource = fileUrl;
                            localDoc.DocType = category;
                            localDoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());

                            try
                            {
                                c.DownloadFile(fileUrl, localDoc.DocLocalPath);
                            }
                            catch
                            {
                            }

                            docs.Add(localDoc);
                        }
                        else
                        {
                            Console.WriteLine("{0} already downloaded...", fileUrl);
                        }

                        this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                        if(qr == null)
                        {
                            qr = new QueryResult();
                            qr.CityId = localDoc.CityId;
                            qr.DocId = localDoc.DocId;
                            qr.MeetingDate = meetingDate;
                            qr.SearchTime = DateTime.Now;
                            
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localDoc, ref qr);
                        Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                    }
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
