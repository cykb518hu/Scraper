using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class ClareMICity : City
    {
        private List<string> docUrls = null;

        public ClareMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ClareMICity",
                CityName = "Crockery",
                CityUrl = "http://www.cityofclare.org/index.html",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ClareMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            
            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                var fileNodes = doc.DocumentNode.SelectNodes("//select[starts-with(@name,'menu')]/option[@value]");

                if(fileNodes != null)
                {
                    foreach (HtmlNode fileNode in fileNodes)
                    {
                        string fileUrl = categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), string.Empty) + fileNode.Attributes["value"].Value;
                        string meetingDateText = fileNode.NextSibling.InnerText.Trim((char)32, (char)160, '\r', '\n', '\t').Split(' ').FirstOrDefault();

                        Console.WriteLine("DEBUG: {0}...", meetingDateText.Trim((char)32, (char)160, '\r', '\n', '\t'));
                        Console.WriteLine("DEBUG: {0}...", fileUrl);

                        if (!string.IsNullOrEmpty(meetingDateText))
                        {
                            var dateEles = meetingDateText.Split('/').Select(t => t.PadLeft(2, '0'));
                            meetingDateText = string.Join("/", dateEles);
                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "MM/dd/yy", null);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }

            }
        }
    }
}
