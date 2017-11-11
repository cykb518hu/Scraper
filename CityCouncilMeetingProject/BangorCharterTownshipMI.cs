using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class BangorCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public BangorCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "BangorCharterTownshipMI",
                CityName = "Bangor Charter Township",
                CityUrl = "http://www.bangortownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BangorCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}[,\\.]{1}[\\s]{0,2}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//div[@id='wsite-content']/div/div");

                if(entryNodes != null)
                {
                    foreach(HtmlNode entryNode in entryNodes)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("DEBUG:{0}.", entryNode.InnerText);
                        Console.ResetColor();
                        string meetingDateText = dateReg.Match(entryNode.InnerText).ToString().Replace(".", string.Empty);
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            break;
                        }

                        HtmlNode linkNode = entryNode.SelectSingleNode(".//a[@href]");
                        string link = linkNode == null ? string.Empty : this.cityEntity.CityUrl + linkNode.Attributes["href"].Value;

                        if (string.IsNullOrEmpty(link))
                        {
                            Console.WriteLine("No meeting...");
                            continue;
                        }

                        this.ExtractADoc(c, link, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
