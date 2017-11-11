using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class HollyTownshipMI : City
    {
        private List<string> docUrls = null;

        public HollyTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "HollyTownshipMI",
                CityName = "Holly Township",
                CityUrl = "http://www.hollytownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HollyTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach(string docUrl in this.docUrls)
            {
                HtmlDocument doc = web.Load(docUrl);
                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/uploads/')]");

                if(docNodes != null)
                {
                    foreach(HtmlNode docNode in docNodes)
                    {
                        string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                        string[] items = meetingDateText.Split('-');
                        DateTime meetingDate = new DateTime(2000 + int.Parse(items.LastOrDefault()), int.Parse(items[0]), int.Parse(items[1]));
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        string category = string.Empty;
                        string fileUrl = docNode.Attributes["href"].Value;
                        if (fileUrl.Contains("TB"))
                        {
                            category = "City Council";
                        }
                        else if (fileUrl.Contains("PC"))
                        {
                            category = "Planning Commission";
                        }

                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
