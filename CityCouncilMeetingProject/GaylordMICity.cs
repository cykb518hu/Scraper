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
    public class GaylordMICity : City
    {
        private List<string> docUrls = null;

        public GaylordMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "GaylordMICity",
                CityName = "Gaylord",
                CityUrl = "http://cityofgaylord.com/index.cfm",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GaylordMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];

                for(int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
                {
                    string yearUrl = null;
                    if(year == DateTime.Now.Year)
                    {
                        yearUrl = string.Format(categoryUrl, year, string.Empty);
                    }
                    else
                    {
                        yearUrl = string.Format(categoryUrl, year, "minutesagendas");
                    }

                    HtmlDocument doc = web.Load(yearUrl);
                    HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//div[@id='textWindow']//a[contains(@href,'.pdf')]");
                   
                    if(fileNodes != null)
                    {
                        foreach (HtmlNode fileNode in fileNodes)
                        {
                            string fileUrl = fileNode.Attributes["href"].Value;
                            string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();
                            if (dateReg.IsMatch(fileNode.InnerText))
                            {
                                DateTime meetingDate = DateTime.Parse(meetingDateText);
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
}
