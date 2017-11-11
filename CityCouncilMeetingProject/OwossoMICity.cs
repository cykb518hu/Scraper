//#define debug
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
    public class OwossoMICity : City
    {
        private List<string> docUrls = null;

        public OwossoMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "OwossoMICity",
                CityName = "Owosso",
                CityUrl = "http://www.ci.owosso.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OwossoMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            string entryXpath = "//div[@class='minutesAgendas']";
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entriesNodes = doc.DocumentNode.SelectNodes(entryXpath);

                if (entriesNodes != null)
                {
                    foreach(HtmlNode entryNode in entriesNodes)
                    {
                        HtmlNode dateNode = entryNode.SelectSingleNode("./h1/a");
                        string meetingDateText = dateNode.InnerText.Split('-').FirstOrDefault().Trim((char)32, (char)160);
                        meetingDateText = dateReg.Match(meetingDateText).ToString();
#if debug
                        try
                        {
                            DateTime.Parse(dateNode.InnerText);
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        catch
                        {
                            Console.WriteLine("Not match {0}...", dateNode.InnerText);
                            continue;
                        }
#endif
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("DEBUG:{0}...", meetingDateText);
                        Console.ResetColor();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip it...");
                            continue;
                        }

                        HtmlNode agendaNode = entryNode.SelectSingleNode(".//a[text()='Agenda']");
                        HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[text()='Minutes']");
                        HtmlNode packetNode = entryNode.SelectSingleNode(".//a[text()='Packet']");

                        if(agendaNode != null)
                        {
                            string agendaUrl = this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                            this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }

                        if(minuteNode != null)
                        {
                            string minuteUrl = this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                            this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }

                        if(packetNode != null)
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
