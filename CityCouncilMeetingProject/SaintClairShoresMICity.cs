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
    public class SaintClairShoresMICity : City
    {
        private List<string> docUrls = null;

        public SaintClairShoresMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "SaintClairShoresMICity",
                CityName = "Saint Clair Shores",
                CityUrl = "http://www.ci.saint-clair-shores.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SaintClairShoresMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            
            for(int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
            {
                foreach(string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string yearUrl = string.Format(url.Split('*')[1], year);

                    HtmlDocument doc = web.Load(yearUrl);
                    HtmlNodeCollection meetingNodes = doc.DocumentNode.SelectNodes("//tr[@class='catAgendaRow']");

                    if(meetingNodes != null)
                    {
                        foreach(HtmlNode meetingNode in meetingNodes)
                        {
                            string meetingDateText = dateReg.Match(meetingNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = meetingNode.SelectSingleNode(".//a[contains(@href,'Minutes')]");
                            HtmlNode agendaNode = meetingNode.SelectSingleNode(".//li/a[contains(@href,'Agenda')]");
                            Dictionary<string, string> urlDic = new Dictionary<string, string>();


                            if(minuteNode != null)
                            {
                                if (minuteNode.Attributes.Contains("class") && minuteNode.Attributes["class"].Value == "html")
                                {
                                    urlDic.Add(this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value, "doc");
                                }
                                else
                                {
                                    urlDic.Add(this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value, "pdf");
                                }
                            }

                            if(agendaNode != null)
                            {
                                if (agendaNode.Attributes.Contains("class") && agendaNode.Attributes["class"].Value == "html")
                                {
                                    urlDic.Add(this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value, "doc");
                                }
                                else
                                {
                                    urlDic.Add(this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value, "pdf");
                                }
                            }

                            if(urlDic == null)
                            {
                                Console.WriteLine("No files today");
                                continue;
                            }

                            foreach(string key in urlDic.Keys)
                            {
                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == key);

                                if(localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.DocType = category;
                                    localdoc.DocSource = key;
                                    localdoc.Important = false;
                                    localdoc.Checked = false;
                                    localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}_{3}.{4}",
                                        this.localDirectory,
                                        category,
                                        meetingDate.ToString("yyyy-MM-dd"),
                                        Guid.NewGuid().ToString(),
                                        urlDic[key]);

                                    try
                                    {
                                        c.Headers.Add("user-agent", "chrome");
                                        c.DownloadFile(key, localdoc.DocLocalPath);
                                    }
                                    catch (Exception ex)
                                    { }

                                    docs.Add(localdoc);
                                }
                                else
                                {
                                    Console.WriteLine("This file already downloaded...");
                                }

                                this.ReadText(false, localdoc.DocLocalPath, ref localdoc); ;
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                if(qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.CityId = this.cityEntity.CityId;
                                    qr.DocId = localdoc.DocId;
                                    qr.MeetingDate = meetingDate;
                                    qr.SearchTime = DateTime.Now;
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
}
