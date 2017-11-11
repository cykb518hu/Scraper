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
    public class FlintCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public FlintCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "FlintCharterTownshipMI",
                CityName = "Flint Charter Township",
                CityUrl = "http://www.flinttownship.org/Default.aspx?tabid=3600",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("FlintCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<string> yearUrls = new List<string>();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                string rangeStart = string.Format("1/1/{0}", i);
                string rangeEnd = string.Format("12/31/{0}", i);
                yearUrls.Add(string.Format(this.docUrls[0], rangeStart, rangeEnd));
            }

            foreach (string yearUrl in yearUrls)
            {
                HtmlDocument doc = web.Load(yearUrl);
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

                        if (category.Contains("Board of Trustee"))
                        {
                            category = "City Council";
                        }
                        else if (category.Contains("Board of Zoning Appeals"))
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
                            string docUrl = "http://flintchartermi.iqm2.com/Citizens/" + docNode.Attributes["href"].Value.Replace("&amp;", "&");
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
                                    category, docNode.InnerText, meetingDate.ToString("yyyy-MM-dd"));

                                try
                                {
                                    c.Headers.Add("user-agent", "chrome");
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
