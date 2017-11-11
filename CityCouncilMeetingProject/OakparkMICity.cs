using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class OakparkMICity : City
    {
        private List<string> docUrls = null;

        public OakparkMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "OakparkMICity",
                CityName = "Oakpark",
                CityUrl = "http://www.oakparkmi.gov/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("OakparkMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (String url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];
                HtmlDocument listDoc = web.Load(listUrl);
                HtmlNodeCollection listNodes = listDoc.DocumentNode.SelectNodes("//div[@class='post clearfix']/strong//a[contains(@href,'.pdf')]");

                if (listNodes != null)
                {
                    foreach (HtmlNode listNode in listNodes)
                    {
                        string fileUrl = this.cityEntity.CityUrl + listNode.Attributes["href"].Value;
                        string meetingDateText = dateReg.Match(listNode.InnerText).ToString();

                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            continue;
                        }

                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early...");
                            continue;
                        }

                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if(localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.Important = false;
                            localdoc.Checked = false;
                            localdoc.DocType = category;
                            localdoc.DocSource = fileUrl;
                            string localPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());
                            localdoc.DocLocalPath = localPath;

                            try
                            {
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch { }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.WriteLine("This document already downloaded....");
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if(qr == null)
                        {
                            qr = new QueryResult();
                            qr.CityId = localdoc.CityId;
                            qr.DocId = localdoc.DocId;
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        this.SaveMeetingResultsToSQL(docs, queries);
                        Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                    }
                }
            }
        }
    }
}
