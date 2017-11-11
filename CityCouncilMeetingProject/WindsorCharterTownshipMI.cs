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
    public class WindsorCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public WindsorCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WindsorCharterTownshipMI",
                CityName = "Windsor",
                CityUrl = "https://www.windsortownship.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WindsorCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            StringBuilder yearBuilder = new StringBuilder();

            for(int i = this.dtStartFrom.Year;i <= DateTime.Now.Year; i++)
            {
                yearBuilder.AppendFormat("{0}|", i);
            }

            Regex yearReg = new Regex(string.Format("({0})", yearBuilder.ToString().Trim('|')));

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = new HtmlDocument();
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string html = c.DownloadString(categoryUrl);
                doc.LoadHtml(html);
                HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//div[@class='tab-content']//a[contains(@href,'pdf')]");

                if(fileNodes != null)
                {
                    foreach(HtmlNode fileNode in fileNodes)
                    {
                        if (!yearReg.IsMatch(fileNode.InnerText))
                        {
                            Console.WriteLine("{0} not target file, skip...", fileNode.InnerHtml);
                            continue;
                        }

                        string fileUrl = fileNode.Attributes["href"].Value;
                        string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early...");
                            continue;
                        }

                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
