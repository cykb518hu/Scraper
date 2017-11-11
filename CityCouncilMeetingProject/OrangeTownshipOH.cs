using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class OrangeTownshipOH : City
    {
        private List<string> docUrls = null;

        public OrangeTownshipOH()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "OrangeTownshipOH",
                CityName = "Orange Township",
                CityUrl = "http://orangetwp.org",
                StateCode = "OH"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OrangeTownshipOH_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]{4}");
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//*[@class='catAgendaRow']");

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[contains(@href,'/Minutes/')]");
                            if (minuteNode != null)
                            {
                                string minuteUrl = this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                                this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            var agendaNodes = entryNode.SelectNodes(".//a[contains(@href,'/Agenda/')]");
                            var agendaNode = agendaNodes == null ? null : agendaNodes.FirstOrDefault(t => t.Attributes["href"].Value.Contains("?") == false);
                            if (agendaNode != null)
                            {
                                string agendaUrl = this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                                this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            var packetNode = agendaNodes == null ? null : agendaNodes.FirstOrDefault(t => t.Attributes["href"].Value.Contains("packet"));
                            if (packetNode != null)
                            {
                                string packetUrl = this.cityEntity.CityUrl + packetNode.Attributes["href"].Value;
                                this.ExtractADoc(c, packetUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
