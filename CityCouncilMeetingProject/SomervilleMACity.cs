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
    public class SomervilleMACity : City
    {
        private List<string> docUrls = null;

        public SomervilleMACity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SomervilleMACity",
                CityName = "Somerville",
                CityUrl = "https://www.somervillema.gov/",
                StateCode = "MA"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SomervilleMACity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for(int i =0; ; i++)
            {
                Console.WriteLine("Working on page {0}...", i + 1);
                string page = i == 0 ? this.docUrls[0] : this.docUrls[0] + "?page=" + i;
                HtmlDocument pageDoc = web.Load(page);
                HtmlNodeCollection meetings = pageDoc.DocumentNode.SelectNodes("//table[@id='views-aggregator-datatable']/tbody/tr");

                if(meetings != null && meetings.Count > 0)
                {
                    foreach(HtmlNode meetingNode in meetings)
                    {
                        HtmlNode meetingDateNode = meetingNode.SelectSingleNode(".//span[@property='dc:date']");
                        DateTime meetingDate = DateTime.Parse(meetingDateNode.Attributes["content"].Value);
                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNode categoryNode = meetingNode.SelectSingleNode("./td[2]");
                        string category = categoryNode == null ? string.Empty : categoryNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160);
                        HtmlNode meetingDocNode = meetingNode.SelectSingleNode(".//span[@class='file']/a");
                        string meetingDocUrl = meetingDocNode == null ? string.Empty : "http:" + meetingDocNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(meetingDocUrl))
                        {
                            this.ExtractADoc(c, meetingDocUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Reach last page...");
                    break;
                }
            }
        }
    }
}
