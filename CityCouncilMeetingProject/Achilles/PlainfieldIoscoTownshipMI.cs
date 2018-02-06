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
    public class PlainfieldIoscoTownshipMI : City
    {
        private List<string> docUrls = null;

        public PlainfieldIoscoTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PlainfieldIoscoTownshipMI",
                CityName = "Plainfield Charter Township",
                CityUrl = "http://www.plainfieldtwpmi.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PlainfieldIoscoTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
           // var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{4} [0-9]{2} [0-9]{2}");
            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    bool dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;
                    string meetingDateText = dateReg.Match(r.InnerText).ToString();
                    if (DateTime.TryParseExact(meetingDateText, "yyyy MM dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(string.Format("date str:{0}", r.InnerText));
                        Console.WriteLine("date formart incorrect");
                        continue;
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                   // Console.WriteLine(string.Format("date:{0},category:{1}", meetingDate.ToString("yyyy-MM-dd"), category));
                    this.ExtractADoc(c, r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }
    }
}
