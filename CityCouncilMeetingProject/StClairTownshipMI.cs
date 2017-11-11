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
    public class StClairTownshipMI : City
    {
        private List<string> docUrls = null;

        public StClairTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "StClairTownshipMI",
                CityName = "St. Clair Township",
                CityUrl = "http://www.stclairtwp.org/home.html",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("StClairTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = new HtmlDocument();
                    string html = c.DownloadString(categoryUrl);
                    doc.LoadHtml(html);
                    //web.Load(categoryUrl);
                    HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'AccessKeyId')]");

                    if(docNodes != null)
                    {
                        foreach(HtmlNode docNode in docNodes)
                        {
                            string docUrl = "http:" + docNode.Attributes["href"].Value;
                            string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
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
