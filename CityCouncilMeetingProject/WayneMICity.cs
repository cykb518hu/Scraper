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
    /// http://www.ci.wayne.mi.us/index.php/view-agenda-s-minutes/2-uncategorised/55-2016-agendas-minutes
    /// http://www.ci.wayne.mi.us/index.php/view-agenda-s-minutes
    /// </summary>
    public class WayneMICity : City
    {
        private string[] docUrls = null;

        public WayneMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WayneMICity",
                CityName = "Wayne",
                CityUrl = "http://www.ci.wayne.mi.us/index.php/view-agenda-s-minutes",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            docUrls = File.ReadAllLines("WayneMICity_Urls.txt");
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
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//div[@id='system']//table//tr[@valign='top']");

                if (docNodeList != null)
                {
                    Console.WriteLine("{0} dates...", docNodeList.Count);

                    foreach (HtmlNode docNode in docNodeList)
                    {
                        DateTime meetingDate = DateTime.MinValue;
                        try
                        {
                            string dateText = docNode.SelectSingleNode("./td").InnerText.Trim((char)32, (char)160);
                            if (!string.IsNullOrEmpty(dateText))
                            {
                                meetingDate = DateTime.Parse(dateText);
                            }
                        }
                        catch
                        {
                        }

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Earlier than {0}...", this.dtStartFrom);
                            continue;
                        }

                        HtmlNodeCollection pdfNodes = docNode.SelectNodes(".//a[@href]");

                        if (pdfNodes != null)
                        {
                            Console.WriteLine("{0} files at {1}...", pdfNodes.Count, meetingDate);

                            foreach (HtmlNode pdfNode in pdfNodes)
                            {
                                string pdfUrl = pdfNode.Attributes["href"].Value;
                                pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://www.ci.wayne.mi.us" + pdfUrl;

                                if (pdfUrl.Contains("youtu"))
                                {
                                    continue;
                                }

                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                                if (localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.DocType = "Council";
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.DocSource = pdfUrl;

                                    string localPath = string.Format("{0}\\{1}", this.localDirectory, pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
                                    localdoc.DocLocalPath = localPath;

                                    if (!File.Exists(localPath))
                                    {
                                        try
                                        {
                                            c.DownloadFile(pdfUrl, localPath);
                                        }
                                        catch
                                        {
                                        }
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
                    }
                }
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }
    }
}
