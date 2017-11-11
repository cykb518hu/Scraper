using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class PlymouthCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PlymouthCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "PlymouthCharterTownshipMI",
                CityName = "Plymouth Charter Township",
                CityUrl = "http://www.plymouthtwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PlymouthCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[0-9]{1,2}/[0-9]{1,2}/[0-9]{2}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entriesNodes = doc.DocumentNode.SelectNodes("//div[@class='post']/table[@style]//tr");

                if(entriesNodes != null)
                {
                    foreach(HtmlNode entryNode in entriesNodes)
                    {
                        string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.ParseExact(meetingDateText, "MM/dd/yy", null);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        HtmlNode agendaNode = entryNode.SelectSingleNode(".//a[text()='Agenda']");
                        HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[text()='Minutes']");

                        if(agendaNode != null)
                        {
                            string agendaUrl = this.cityEntity.CityUrl + "/" + agendaNode.Attributes["href"].Value;
                            this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }

                        if(minuteNode!= null)
                        {
                            string minuteUrl = this.cityEntity.CityUrl + "/" + minuteNode.Attributes["href"].Value;
                            this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
