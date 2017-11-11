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
    public class InksterMICity : City
    {
        private List<string> docUrls = null;

        public InksterMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "InksterMICity",
                CityName = "Inkster",
                CityUrl = "http://www.cityofinkster.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("InksterMICity_Urls.txt").ToList();
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
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument categoryDoc = web.Load(categoryUrl);
                    HtmlNodeCollection nodeList = categoryDoc.DocumentNode.SelectNodes("//tr[@class='catAgendaRow']");

                    if (nodeList != null)
                    {
                        foreach (HtmlNode meetingNode in nodeList)
                        {
                            string meetingDateText = dateReg.Match(meetingNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = meetingNode.SelectSingleNode(".//a[contains(@href,'Minute')]");
                            if (minuteNode != null)
                            {
                                string minuteUrl = minuteNode.Attributes["href"].Value;
                                minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                                this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode packetNode = meetingNode.SelectSingleNode(".//a[contains(@href,'packet')]");
                            if (packetNode != null)
                            {
                                string packetUrl = packetNode.Attributes["href"].Value;
                                packetUrl = packetUrl.StartsWith("http") ? packetUrl : this.cityEntity.CityUrl + packetUrl;
                                this.ExtractADoc(c, packetUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode agendaNode = meetingNode.SelectSingleNode(".//a[contains(@href,'Agenda')]");
                            if (agendaNode != null)
                            {
                                string agendaUrl = agendaNode.Attributes["href"].Value;
                                agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;

                                if (agendaUrl.Contains("html"))
                                {
                                    HtmlDocument agendaDoc = web.Load(agendaUrl);
                                    HtmlNodeCollection pdfFileNodes = agendaDoc.DocumentNode.SelectNodes("//a[contains(@href,'ViewFile')]");
                                    if(pdfFileNodes != null)
                                    {
                                        foreach(HtmlNode pdfNode in pdfFileNodes)
                                        {
                                            string pdfUrl = pdfNode.Attributes["href"].Value;
                                            pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : this.cityEntity.CityUrl + pdfUrl;
                                            this.ExtractADoc(c, pdfUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                        }
                                    }
                                }
                                else
                                {
                                    this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
