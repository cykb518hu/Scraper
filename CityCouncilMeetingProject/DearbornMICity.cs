using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class DearbornMICity : City
    {
        private List<string> docUrls = null;

        public DearbornMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "DearbornMICity",
                CityName = "Dearborn",
                CityUrl = "http://www.cityofdearborn.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("DearbornMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[A-Za-z]+[\\.]{0,1}[\\s]{0,1}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = string.Empty;
                if (url.Contains("city-council"))
                {
                    category = "City Council";
                }
                else if (url.Contains("planning-commission"))
                {
                    category = "Planning Commission";
                }
                else if (url.Contains("zoning"))
                {
                    category = "Zoning Board of Appeals";
                }

                string tag = url.ToLower().Contains("agenda") ? "agenda" : "minute";

                HtmlDocument listDoc = web.Load(url);
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//div[@class='docman_document']");

                if (docNodeList != null && docNodeList.Count > 0)
                {
                    foreach (HtmlNode docNode in docNodeList)
                    {
                        HtmlNode dateAndUrlNode = docNode.SelectSingleNode(".//a[@class='koowa_header__title_link docman_track_download']");
                        string dateText = dateReg.Match(dateAndUrlNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.MinValue;
                        bool isDate = DateTime.TryParse(dateText, out meetingDate);
                        
                        string fileUrl = dateAndUrlNode.Attributes["href"].Value;
                        fileUrl = !fileUrl.StartsWith("http") ? this.cityEntity.CityUrl + fileUrl : fileUrl;

                        HtmlNode localFileNameNode = docNode.SelectSingleNode(".//p[@class='docman_download__filename']");
                        string localFileName = localFileNameNode == null ? Guid.NewGuid().ToString() : localFileNameNode.InnerText;

                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if (localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.DocId = Guid.NewGuid().ToString();
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.DocType = category;
                            localDoc.Checked = false;
                            localDoc.DocLocalPath = string.Format("{0}\\{1}_{2}", this.localDirectory, tag, localFileName);
                            localDoc.DocSource = fileUrl;

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
                            Console.WriteLine("This file already downloaded...");
                        }

                        this.ReadText(false, localDoc.DocLocalPath, ref localDoc);

                        if (meetingDate == DateTime.MinValue)
                        {
                            if (dateReg.IsMatch(localDoc.DocBodyDic[1]))
                            {
                                meetingDate = DateTime.Parse(dateReg.Match(localDoc.DocBodyDic[1]).ToString());
                            }
                        }

                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.DocId = localDoc.DocId;
                            qr.CityId = localDoc.CityId;
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;
                            
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
