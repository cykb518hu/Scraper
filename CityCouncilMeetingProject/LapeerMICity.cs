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
    public class LapeerMICity : City
    {
        private List<string> docUrls = null;

        public LapeerMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LapeerMICity",
                CityName = "Lapeer",
                CityUrl = "http://www.ci.lapeer.mi.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("LapeerMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument categoryDoc = web.Load(categoryUrl);
                HtmlNodeCollection fileNodes = categoryDoc.DocumentNode.SelectNodes("//div[@id='RZdocument_center']//a[contains(@href,'.pdf')]");
                
                if(fileNodes != null)
                {
                    foreach(HtmlNode fileNode in fileNodes)
                    {
                        if(fileNode.SelectSingleNode("./img") != null)
                        {
                            continue;
                        }

                        string meetingUrl = fileNode.Attributes["href"].Value;
                        meetingUrl = meetingUrl.StartsWith("http") ? meetingUrl : this.cityEntity.CityUrl + meetingUrl;
                        string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("DEBUG: {0} - {1}", fileNode.InnerText, meetingDateText);
                        Console.ResetColor();

                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early...");
                            continue;
                        }

                        this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
