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
    class GeorgetownCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public GeorgetownCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "GeorgetownCharterTownshipMI",
                CityName = "Georgetown Charter Township",
                CityUrl = "http://www.georgetown-mi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GeorgetownCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = url.Split('*')[1];
                    categoryUrl = string.Format(categoryUrl, i);

                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection recordNodes = doc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

                    if (recordNodes != null)
                    {
                        foreach (HtmlNode recordNode in recordNodes)
                        {
                            string meetingDateText = dateReg.Match(recordNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = recordNode.SelectSingleNode(".//a[contains(@href,'/Minutes/')]");
                            HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[contains(@href,'/Agenda/')]");
                            HtmlNode agendaPacketNode = recordNode.SelectSingleNode(".//a[contains(@href,'packet')]");

                            if (minuteNode != null)
                            {
                                string fileUrl = this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            if (agendaPacketNode != null)
                            {
                                string fileUrl = this.cityEntity.CityUrl + agendaPacketNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            if (agendaNode != null)
                            {
                                string fileUrl = this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;

                                if (fileUrl.Contains("html"))
                                {
                                    this.ExtractAgendas(web, fileUrl, category, meetingDate, ref docs, ref queries);
                                }
                                else
                                {
                                    this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExtractAgendas(HtmlWeb web, string docUrl, string category, DateTime meetingDate, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);
            HtmlDocument doc = new HtmlDocument();

            if(localdoc == null)
            {
                localdoc = new Documents();
                localdoc.DocId = Guid.NewGuid().ToString();
                localdoc.CityId = this.cityEntity.CityId;
                localdoc.DocSource = docUrl;
                localdoc.DocType = category;
                localdoc.Important = false;
                localdoc.Checked = false;
                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}_{3}.html",
                    this.localDirectory,
                    category,
                    meetingDate.ToString("yyyy-MM-dd"),
                    Guid.NewGuid().ToString());
                
                try
                {
                    doc = web.Load(docUrl);
                    doc.Save(localdoc.DocLocalPath);
                }
                catch
                { }

                docs.Add(localdoc);
            }
            else
            {
                Console.WriteLine("This document already downloaded...");
            }

            doc.LoadHtml(File.ReadAllText(localdoc.DocLocalPath));
            localdoc.DocBodyDic.Add(1, doc.DocumentNode.InnerText);
            HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/ViewFile/')]");
            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

            if (qr == null)
            {
                qr = new QueryResult();
                qr.DocId = localdoc.DocId;
                qr.CityId = this.cityEntity.CityId;
                qr.MeetingDate = meetingDate;
                qr.SearchTime = DateTime.Now;
                queries.Add(qr);
            }

            this.ExtractQueriesFromDoc(localdoc, ref qr);
            Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
            this.SaveMeetingResultsToSQL(docs, queries);

            if(fileNodes != null)
            {
                WebClient c = new WebClient();
                foreach(HtmlNode fileNode in fileNodes)
                {
                    string fileUrl = this.cityEntity.CityUrl + fileNode.Attributes["href"].Value;

                    if (fileUrl.Contains("pdf"))
                    {
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
