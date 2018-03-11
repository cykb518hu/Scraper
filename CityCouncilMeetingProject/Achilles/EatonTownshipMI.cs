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
    public class EatonTownshipMI : City
    {
        private List<string> docUrls = null;

        public EatonTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "EatonTownshipMI",
                CityName = "Eaton Charter Township",
                CityUrl = "http://www.eatontownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("EatonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
           // var docs = this.LoadDocumentsDoneSQL();
           // var queries = this.LoadQueriesDoneSQL();
             var docs = new List<Documents>();
             var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+ [\\s]{0,2}[0-9]{4}"), "MMMM yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{4}"), "MMM yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{4}"), "yyyy");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/LinkClick.aspx?')]");
                foreach (var r in list)
                {
                    var dateConvert = false;
                    var dateStr = r.InnerText.Replace(".", "").Replace("  ", "");
                    DateTime meetingDate = DateTime.MinValue;
                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        string format = dateRegFormatDic[dateRegKey];
                        string meetingDateText = dateRegKey.Match(dateStr).ToString();
                        if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                            break;
                        }

                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(dateStr);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                   // Console.WriteLine(string.Format("datestr:{0},category:{1}", meetingDate.ToString("yyyy-MM-dd"), category));
                     this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    }
}
