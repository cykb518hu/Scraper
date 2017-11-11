using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    public class BurtonMICity : City
    {
        private List<string> docUrls = null;

        public BurtonMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BurtonMICity",
                CityName = "Burton",
                CityUrl = "http://www.burtonmi.gov/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("BurtonMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<string> rangeUrls = new List<string>();

            for (int i = dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                DateTime rangeStart = new DateTime(i, 1, 1); ;
                DateTime rangeEnd = new DateTime(i, 12, 31);
                string rangeUrl = string.Format("{0}?From={1}&To={2}",
                    this.docUrls[0],
                    rangeStart.ToString("M/d/yyyy"),
                    rangeEnd.ToString("M/d/yyyy"));
                rangeUrls.Add(rangeUrl);
            }

            foreach (string rangeUrl in rangeUrls)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Working on {0}...", rangeUrl);
                Console.ResetColor();

                HtmlDocument doc = web.Load(rangeUrl);
                HtmlNodeCollection recordNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'Row MeetingRow')]");

                if (recordNodes != null)
                {
                    foreach (HtmlNode recordNode in recordNodes)
                    {
                        List<HtmlNode> docNodes = new List<HtmlNode>();
                        HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[text()='Agenda']");
                        if (agendaNode != null && string.IsNullOrEmpty(agendaNode.Attributes["href"].Value) == false)
                        {
                            docNodes.Add(agendaNode);
                        }
                        HtmlNode minuteNode = recordNode.SelectSingleNode(".//a[text()='Minutes']");
                        if (minuteNode != null && string.IsNullOrEmpty(minuteNode.Attributes["href"].Value) == false)
                        {
                            docNodes.Add(minuteNode);
                        }
                        HtmlNode agendePacketNode = recordNode.SelectSingleNode(".//a[text()='Agenda Packet']");
                        if (agendePacketNode != null && string.IsNullOrEmpty(agendePacketNode.Attributes["href"].Value) == false)
                        {
                            docNodes.Add(agendePacketNode);
                        }

                        if (docNodes.Count == 0)
                        {
                            Console.WriteLine("No files found....");
                            continue;
                        }

                        HtmlNode meetingCategoryNode = recordNode.SelectSingleNode(".//div[@class='MainScreenText RowDetails']");
                        string category = meetingCategoryNode == null ? string.Empty : meetingCategoryNode.InnerText;

                        if (category.Contains("City Council"))
                        {
                            category = "City Council";
                        }
                        else if (category.Contains("Zoning Board of Appeals"))
                        {
                            category = "Zoning Board of Appeals";
                        }
                        else if (category.Contains("Planning Commission"))
                        {
                            category = "Planning Commission";
                        }
                        else
                        {
                            continue;
                        }

                        HtmlNode dateNode = recordNode.SelectSingleNode(".//div[@class='RowLink']/a");
                        string meetingDateText = dateNode.InnerText;
                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        foreach (HtmlNode docNode in docNodes)
                        {
                            string docUrl = "http://burtonmi.iqm2.com/Citizens/" + docNode.Attributes["href"].Value.Replace("&amp;", "&");
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocSource = docUrl;
                                localdoc.Important = false;
                                localdoc.Checked = false;
                                localdoc.DocType = category;
                                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}_{3}.pdf", this.localDirectory,
                                    category, docNode.InnerText, Guid.NewGuid().ToString());

                                try
                                {
                                    c.Headers.Add("user-agent", "Chrome");
                                    c.DownloadFile(docUrl, localdoc.DocLocalPath);
                                }
                                catch (Exception ex) { }

                                docs.Add(localdoc);
                            }
                            else
                            {
                                Console.WriteLine("This file already downloaded...");
                            }

                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                            if (qr == null)
                            {
                                qr = new QueryResult();
                                qr.CityId = this.cityEntity.CityId;
                                qr.DocId = localdoc.DocId;
                                qr.SearchTime = DateTime.Now;
                                qr.MeetingDate = meetingDate;
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localdoc, ref qr);
                            Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                            this.SaveMeetingResultsToSQL(docs, queries);
                        }
                    }
                }
            }
        }
    }
}
