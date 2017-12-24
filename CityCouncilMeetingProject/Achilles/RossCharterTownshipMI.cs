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

    //county:Jackson
    public class RossCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public RossCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RossCharterTownshipMI",
                CityName = "Ross Charter Township",
                CityUrl = "http://www.ross-township.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RossCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex(@"\d{4}-\d{2}-\d{2}");
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                var category = "";
                foreach (var r in list)
                {
                    string meetingDateText = dateReg.Match(r.InnerText).ToString();
 
                    DateTime meetingDate;
                    if (!DateTime.TryParse(meetingDateText, out meetingDate))
                    {
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    if (r.Attributes["href"].Value.IndexOf("PARKS") > 0)
                    {
                        category = "Parks";
                    }
                    if (r.Attributes["href"].Value.IndexOf("PLANNING") > 0)
                    {
                        category = "Planning Commission";
                    }
                    if (r.Attributes["href"].Value.IndexOf("TWP") > 0)
                    {
                        category = "Township Board";
                    }
                    if (r.Attributes["href"].Value.IndexOf("ZBA") > 0)
                    {
                        category = "ZONING BOARD of APPEALS";
                    }
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }
    }
}
