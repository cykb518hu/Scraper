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
    public class AllenParkMICity : City
    {
        private List<string> docUrls = null;

        public AllenParkMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "AllenParkMICity",
                CityName = "Allen Park",
                CityUrl = "http://www.cityofallenpark.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("AllenParkMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection entriesNodes = doc.DocumentNode.SelectNodes("//table[@class='tablewithheadingresponsive']/tbody/tr[position()>1]");

                    if(entriesNodes != null)
                    {
                        foreach(HtmlNode entryNode in entriesNodes)
                        {
                            HtmlNode meetingDateNode = entryNode.SelectSingleNode("./td");
                            string meetingDateText = meetingDateNode.InnerText.Split('-').FirstOrDefault().Trim('\r', '\n', '\t', (char)32, (char)160);
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if(meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }

                            HtmlNode agendaNode = entryNode.SelectSingleNode(".//a[text()='Agenda']");
                            
                            if(agendaNode != null)
                            {
                                string agendaUrl = this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                                this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[text()='Minutes']");

                            if(minuteNode != null)
                            {
                                string minuteUrl = this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                                this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode agendaPacketNode = entryNode.SelectSingleNode(".//a[text()='Packet']");

                            if(agendaPacketNode != null)
                            {
                                string agendaPacketUrl = this.cityEntity.CityUrl + agendaPacketNode.Attributes["href"].Value;
                                this.ExtractADoc(c, agendaPacketUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
