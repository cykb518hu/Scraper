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
    public class NilesMICity : City
    {
        private List<string> docUrls = null;

        public NilesMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "NilesMICity",
                CityName = "Niles",
                CityUrl = "http://www.nilesmi.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("NilesMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("(a|m)[0-9]{2}[0-1]{1}[0-9]{1}[0-9]{2}");

            foreach(string docUrl in this.docUrls)
            {
                string category = docUrl.Split('*')[0];
                string categoryUrl = docUrl.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");

                if(docNodes != null)
                {
                    foreach(HtmlNode docNode in docNodes)
                    {
                        string fileUrl = docNode.Attributes["href"].Value;
                        fileUrl = fileUrl.StartsWith("http") ? fileUrl : this.cityEntity.CityUrl + fileUrl;
                        string meetingDateText = dateReg.Match(fileUrl).ToString();
                        DateTime meetingDate = DateTime.MinValue;
                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Please check the meeting date...");
                            Console.ResetColor();
                            continue;
                        }
                        else
                        {
                            meetingDate = DateTime.ParseExact(meetingDateText.Trim('a', 'm'), "yyMMdd", null);
                        }

                        if(meetingDate < this.dtStartFrom)
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
