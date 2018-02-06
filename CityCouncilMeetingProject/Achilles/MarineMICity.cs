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
    public class MarineMICity : City
    {
        private List<string> docUrls = null;

        public MarineMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MarineMICity",
                CityName = "Marine MI",
                CityUrl = "https://cityofmarinecity.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MarineMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
             //var docs = new List<Documents>();
             //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");


            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection sectionNodes = doc.DocumentNode.SelectNodes("//section");
                if (sectionNodes != null)
                {
                    foreach (HtmlNode section in sectionNodes)
                    {
                        HtmlNodeCollection urlList = section.SelectNodes(".//a[contains(@href,'/wp-content/uploads/')]");
                        if (urlList == null)
                        {
                            continue;
                        }
                        foreach (var r in urlList)
                        {
                            string meetingDateText = dateReg.Match(r.InnerText).ToString();

                            DateTime meetingDate;
                            if (!DateTime.TryParse(meetingDateText, out meetingDate))
                            {
                                Console.WriteLine(meetingDateText);
                                Console.WriteLine("date format incorrect...");
                                continue;
                            }
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Early...");
                                continue;
                            }
                            var category = section.Attributes["id"].Value;
                            //Console.WriteLine(string.Format("meeting date :{0},category:{1}", meetingDateText, section.Attributes["id"].Value));
                            this.ExtractADoc(c, r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                        }

                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }
}
