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
    public class TyroneCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public TyroneCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "TyroneCharterTownshipMI",
                CityName = "Tyrone Charter Township",
                CityUrl = "http://www.tyronetownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("TyroneCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {

            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'documents/')]");
                Regex reg = new Regex(@"\d{4}-\d{2}");

                foreach (var r in list)
                {
                    var dateStr = r.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    if (string.IsNullOrEmpty(dateStr))
                    {
                        continue;

                    }
                    Match m = reg.Match(dateStr);
                    if (m.Success)
                    {
                        dateStr = m.Groups[0].ToString();
                    }
                    else
                    {
                        dateStr = "2016-02";
                    }
                    var title = r.ParentNode.ParentNode.PreviousSibling.PreviousSibling.InnerText;
                    if (title.IndexOf("Meeting Minutes") == 0)
                    {
                        title = "Regular Board";
                    }
                    if (title.IndexOf("Tyrone Township") == 0)
                    {
                        title = "Township";
                    }
                    if (title.IndexOf("Planning Commission") == 0)
                    {
                        title = "Planning Commission";
                    }
                    if (title.IndexOf("Board of Appeals") == 0)
                    {
                        title = "Board of Appeals";
                    }
                    DateTime meetingDate = DateTime.MinValue;
                    bool dateConvert = false;
                    string[] formats = { "yyyy-MM" };
                    if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(dateStr + "-date format incorrect...");
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                     this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, title, "pdf", meetingDate, ref docs, ref queries);
                }

            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }
    }
}
