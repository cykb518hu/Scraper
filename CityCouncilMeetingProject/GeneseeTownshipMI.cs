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
    public class GeneseeTownshipMI : City
    {
        private List<string> docUrls = null;

        public GeneseeTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "GeneseeTownshipMI",
                CityName = "Genesee",
                CityUrl = "http://www.geneseetwp.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GeneseeTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection meetingNodes = doc.DocumentNode.SelectNodes("//article[@class='page-post clearfix']/p/a[@href]");

            if(meetingNodes != null)
            {
                foreach(HtmlNode meetingNode in meetingNodes)
                {
                    string meetingUrl = meetingNode.Attributes["href"].Value;
                    string meetingDateText = dateReg.Match(meetingNode.InnerText).ToString();
                    DateTime meetingDate = DateTime.Parse(meetingDateText);

                    if(meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip...");
                        continue;
                    }
                    string fileType = meetingUrl.Contains(".pdf") ? "pdf" :
                        meetingUrl.Contains(".docx") ? "docx" : "doc";
                    this.ExtractADoc(c, meetingUrl, "council", fileType, meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
