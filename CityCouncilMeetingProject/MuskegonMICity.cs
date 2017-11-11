using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class MuskegonMICity : City
    {
        private List<string> docUrls = null;

        public MuskegonMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MuskegonMICity",
                CityName = "Muskegon",
                CityUrl = "http://www.muskegon-mi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("MuskegonMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];

                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[@class='table table-bordered table-striped table-responsive']//tr[position()>2]");

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            HtmlNode dateNode = entryNode.SelectSingleNode("./td");
                            string meetingDateText = dateNode.InnerText;
                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M-d-yy", null);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode docNode = entryNode.SelectSingleNode(".//a[contains(@href,'pdf')]");
                            string docLink = docNode == null ? string.Empty : docNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(docLink))
                            {
                                docLink = this.cityEntity.CityUrl + docLink;
                                this.ExtractADoc(c, docLink, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
