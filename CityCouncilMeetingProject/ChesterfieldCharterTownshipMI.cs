using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class ChesterfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public ChesterfieldCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ChesterfieldCharterTownshipMI",
                CityName = "Chesterfield Charter Township",
                CityUrl = "http://www.chesterfieldtwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("ChesterfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            string[] tags = { "Board", "Planning", "ZBA" };
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<string> targetUrls = new List<string>();

            foreach (string docUrl in this.docUrls)
            {
                if (docUrl.Contains("{0}"))
                {
                    for (int i = this.dtStartFrom.Year; i < DateTime.Now.Year; i++)
                    {
                        targetUrls.Add(string.Format(docUrl, i));
                    }
                }
                else
                {
                    targetUrls.Add(docUrl);
                }
            }

            foreach (string url in targetUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNode yearNode = doc.DocumentNode.SelectSingleNode("//h2[contains(text(),'Agenda and Minutes Overview')]");
                string year = yearNode.InnerText.Split('-').LastOrDefault().Trim((char)32, (char)160);
                foreach (string key in tags)
                {
                    string category = string.Empty;

                    if (key == "Board")
                    {
                        category = "council";
                    }
                    else
                    {
                        category = key;
                    }

                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes(string.Format("//a[contains(@href,'/{0}/')]", key));

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            string month = entryNode.InnerText.Split('-').FirstOrDefault().Trim((char)32, (char)160, '\r', '\t', '\n');
                            string dateText = string.Format("{0}, {1}", month, year);
                            Console.WriteLine("DEBUG:{0}...", dateText);
                            DateTime meetingDate = DateTime.Now;
                            bool t = DateTime.TryParse(dateText, out meetingDate);

                            if (!t)
                            {
                                continue;
                            }

                            string docUrl = url.Replace(url.Split('/').LastOrDefault(), string.Empty) + entryNode.Attributes["href"].Value;
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
