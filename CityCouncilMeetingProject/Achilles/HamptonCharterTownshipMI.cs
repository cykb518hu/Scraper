using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Globalization;

namespace CityCouncilMeetingProject
{
    public class HamptonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public HamptonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HamptonCharterTownshipMI",
                CityName = "Hampton Charter Township",
                CityUrl = "http://www.hamptontownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HamptonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {

            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'LinkClick.aspx')]");
                foreach (var r in list)
                {

                    string[] formats = { "MM-d-yy", "MM-dd-yy", "M-d-yy", "M-dd-yy" };
                    var dateStr = r.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    DateTime meetingDate = DateTime.MinValue;
                    bool dateConvert = false;
                    if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine("date formart incorrect");
                        continue;
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    var link = this.cityEntity.CityUrl + r.Attributes["href"].Value;
                    var data = c.DownloadData(link);
                    var docType = "pdf";
                    if (c.ResponseHeaders["Content-Type"].IndexOf("word") > -1)
                    {
                        docType = "doc";
                    }
                    this.ExtractADoc(c,  link, category, docType, meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }
    }
}
