using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class SaginawMICity : City 
    {
        private List<string> docUrls = null;

        public SaginawMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "SaginawMICity",
                CityName = "Saginaw",
                CityUrl = "http://www.saginaw-mi.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SaginawMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();

            foreach (string url in this.docUrls)
            {
                HtmlDocument listDoc = web.Load(url);
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//div[contains(@id,'vid')]//ul/li/a[@href]");

                if (docNodeList != null)
                {
                    Console.WriteLine("{0} files...", docNodeList.Count);

                    foreach (HtmlNode docNode in docNodeList)
                    {
                        string pdfName = docNode.InnerText.Trim('\t', '\r', '\n', (char)32, (char)160);
                        string tag = pdfName.ToLower().Contains("agenda") ? "agenda" : "minute";
                        string pdfUrl = docNode.Attributes["href"].Value;
                        pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://www.saginaw-mi.com" + pdfUrl;
                        DateTime meetingDate = DateTime.Parse(pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault().Replace(".pdf", string.Empty));

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("{0} earlier than {1}. Skip...", meetingDate, dtStartFrom);
                            continue;
                        }

                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                        if (localdoc == null)
                        {
                            string category = "Council";
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.DocType = category;
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocSource = pdfUrl;

                            string localPath = string.Format("{0}\\{1}_{2}", this.localDirectory, tag, pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
                            localPath = HttpUtility.UrlDecode(localPath);
                            localdoc.DocLocalPath = localPath;

                            try
                            {
                                c.DownloadFile(pdfUrl, localPath);
                            }
                            catch
                            {
                            }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("This document already downloaded...");
                            Console.ResetColor();
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.DocId = localdoc.DocId;
                            qr.CityId = localdoc.CityId;
                            qr.MeetingDate = meetingDate;
                            qr.SearchTime = DateTime.Now;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} documents saved...", docs.Count);
                        Console.WriteLine("{0} query results saved...", queries.Count);
                    }
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
