using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class ShelbyCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public ShelbyCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "ShelbyCharterTownshipMI",
                CityName = "Shelby Charter Township",
                CityUrl = "http://www.shelbytwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ShelbyCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            List<string> councilAgendaUrls = new List<string>();
            List<string> otherUrls = new List<string>();

            foreach (string url in this.docUrls)
            {
                for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
                {
                    if (url.Contains("{1}"))
                    {
                        string yearStart = string.Format("1/1/{0}", year);
                        string yearEnd = string.Format("12/31/{0}", year);
                        councilAgendaUrls.Add(string.Format(url, yearStart, yearEnd));
                    }
                    else
                    {
                        otherUrls.Add(string.Format(url, year));
                    }
                }
            }

            this.ExtractCouncilAgenda(councilAgendaUrls, ref docs, ref queries);
            this.ExtractOthers(otherUrls, ref docs, ref queries);
        }

        public void ExtractCouncilAgenda(List<string> urls, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");

            foreach (string url in urls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(listUrl);
                HtmlNodeCollection recordNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'Row MeetingRow')]");

                if (recordNodes != null)
                {
                    foreach (HtmlNode recordNode in recordNodes)
                    {
                        List<HtmlNode> docNodes = new List<HtmlNode>();
                        HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[text()='Agenda']");
                        if (agendaNode != null)
                        {
                            docNodes.Add(agendaNode);
                        }
                        HtmlNode agendePacketNode = recordNode.SelectSingleNode(".//a[text()='Agenda Packet']");
                        if (agendePacketNode != null)
                        {
                            docNodes.Add(agendePacketNode);
                        }

                        if (docNodes.Count == 0)
                        {
                            Console.WriteLine("No files found....");
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
                            string docUrl = "http://shelbytownmi.iqm2.com/Citizens/" + docNode.Attributes["href"].Value.Replace("&amp;", "&");
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocSource = docUrl;
                                localdoc.Important = false;
                                localdoc.Checked = false;
                                localdoc.DocType = "City Council";
                                localdoc.DocLocalPath = string.Format("{0}\\Council_{1}_{2}.pdf", this.localDirectory,
                                    docNode.InnerText, meetingDate.ToString("yyyy-MM-dd"));

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

        public void ExtractOthers(List<string> urls, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");

            foreach (string url in urls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];
                Console.WriteLine("Working on {0}...", listUrl);

                string json = c.DownloadString(listUrl);
                var jsonDoc = JsonConvert.DeserializeObject(json) as JToken;

                if (jsonDoc != null)
                {
                    var fileUrlsNodes = jsonDoc.SelectTokens("$..href");

                    if (fileUrlsNodes != null)
                    {
                        foreach (var fileUrlNode in fileUrlsNodes)
                        {
                            string fileUrl = "https://shelbytwpmi.documents-on-demand.com" + fileUrlNode.ToString();
                            string meetingDateText = dateReg.Match(fileUrl).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocSource = fileUrl;
                                localdoc.Important = false;
                                localdoc.Checked = false;
                                localdoc.DocType = category;
                                string localPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());
                                localdoc.DocLocalPath = localPath;

                                try
                                {
                                    c.DownloadFile(fileUrl, localPath);
                                }
                                catch { }

                                docs.Add(localdoc);
                            }
                            else
                            {
                                Console.WriteLine("This file already downloaded....");
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
                            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                            this.SaveMeetingResultsToSQL(docs, queries);
                        }
                    }
                }
            }
        }
    }
}
