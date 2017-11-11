using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    /// <summary>
    /// http://sciotownship.org/boards-commissions/planning-commission/planning-commission-agenda/
    /// http://sciotownship.org/boards-commissions/planning-commission/planning-commission-minutes/
    /// http://sciotownship.org/boards-commissions/zoning-board-of-appeals/agenda/
    /// http://sciotownship.org/boards-commissions/zoning-board-of-appeals/minutes/
    /// </summary>
    class ScioTownshipMI : City
    {
        private List<String> docUrls = null;

        public ScioTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "ScioTownshipMI",
                CityName = "Scio Township",
                CityUrl = "http://sciotownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            docUrls = File.ReadAllLines("ScioTownshipMI_Urls.txt").ToList();
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
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//div[@class='entry-content']/ul/li/a");

                if (docNodeList != null)
                {
                    Console.WriteLine("{0} dates...", docNodeList.Count);

                    foreach (HtmlNode docNode in docNodeList)
                    {
                        string pdfUrl = docNode.Attributes["href"].Value;

                        if (!pdfUrl.Contains(".pdf"))
                        {
                            HtmlDocument innerDoc = web.Load(pdfUrl);
                            HtmlNode innerPdfNode = innerDoc.DocumentNode.SelectSingleNode("//a[contains(@href,'.pdf')]");
                            pdfUrl = innerPdfNode == null ? string.Empty : innerPdfNode.Attributes["href"].Value;
                        }

                        if (string.IsNullOrEmpty(pdfUrl))
                        {
                            Console.WriteLine("No files...");
                            continue;
                        }

                        DateTime meetingDate = DateTime.MinValue;
                        try
                        {
                            if (docNode.InnerText.ToLower().Contains("invoice"))
                            {
                                continue;
                            }

                            meetingDate = DateTime.Parse(docNode.InnerText);
                        }
                        catch
                        {
                            string meetingText = string.Empty;
                            try
                            {
                                string[] meetingTagArray = docNode.InnerText.Split(' ');
                                meetingText = string.Join(" ", meetingTagArray.Take(3));
                                meetingDate = DateTime.Parse(meetingText);
                            }
                            catch
                            {
                                try
                                {
                                    string[] meetingTagArray = pdfUrl.Split('?').FirstOrDefault().Split('/').FirstOrDefault().Split('-');
                                    meetingText = string.Join("-", meetingTagArray.Take(3));
                                    meetingDate = DateTime.Parse(meetingText);
                                }
                                catch
                                {
                                    Console.WriteLine("Failed to parse meeting date");
                                }
                            }
                        }
                        
                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Earlier than {0}...", this.dtStartFrom);
                            continue;
                        }

                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                        if (localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            string tag = string.Empty;

                            if (url.Contains("planning-commission"))
                            {
                                tag = "Planning Commission";
                            }
                            else if (url.Contains("zoning-board-of-appeals"))
                            {
                                tag = "Zoning Board of Appeals";
                            }
                            else
                            {
                                tag = "Council";
                            }

                            localdoc.DocType = tag;
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocSource = pdfUrl;

                            string localPath = string.Format("{0}\\{1}", this.localDirectory, pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
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

