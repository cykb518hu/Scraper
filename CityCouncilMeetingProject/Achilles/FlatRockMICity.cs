using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class FlatRockMICity : City
    {
        private List<string> docUrls = null;

        public FlatRockMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "FlatRockMICity",
                CityName = "FlatRock MI",
                CityUrl = "http://www.flatrockmi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("FlatRockMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
             //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");


            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[contains(@id,'MATable')]/tbody/tr");
                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        string meetingDateText = dateReg.Match(entryNode.SelectNodes("td")[0].InnerText).ToString();
                        DateTime meetingDate;
                        if (!DateTime.TryParse(meetingDateText, out meetingDate))
                        {
                            
                            Console.WriteLine("date format incorrect...");
                            Console.WriteLine("meetingDateText");
                            continue;
                        }

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip");
                            continue;
                        }
                        var category = entryNode.SelectNodes("td")[3].InnerText.Replace("\r", "").Replace("\n", "").Trim();

                        HtmlNode agendaNode = entryNode.SelectNodes("td")[1].SelectSingleNode("a");
                        string agendaUrl = agendaNode == null ? string.Empty : agendaNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(agendaUrl))
                        {
                            this.ExtractADoc(c, this.cityEntity.CityUrl + agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            //Console.WriteLine(string.Format("url:{0}", agendaUrl));
                        }


                        HtmlNode minuteNode = entryNode.SelectNodes("td")[2].SelectSingleNode("a");
                        string minuteUrl = minuteNode == null ? string.Empty : minuteNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(minuteUrl))
                        {
                            this.ExtractADoc(c, this.cityEntity.CityUrl + agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            //Console.WriteLine(string.Format("url:{0}", minuteUrl));
                        }
             
                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }
}
