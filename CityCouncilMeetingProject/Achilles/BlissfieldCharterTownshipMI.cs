using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Net.Http;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class BlissfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public BlissfieldCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BlissfieldCharterTownshipMI",
                CityName = "Blissfield Charter Township",
                CityUrl = "http://www.blissfieldtownship.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BlissfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            //var docs = this.LoadDocumentsDoneSQL();
           // var queries = this.LoadQueriesDoneSQL();
            var docs = new List<Documents>();
            var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();

            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{4}"), "MMMMM yyyy");

            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                if (list != null)
                {
                    foreach (var r in list)
                    {
                        DateTime meetingDate = DateTime.MinValue;
                        var dateConvert = false;
                        foreach (var dateRegKey in dateRegFormatDic.Keys)
                        {
                            string format = dateRegFormatDic[dateRegKey];
                            string meetingDateText = dateRegKey.Match(r.InnerText.Replace("  ", " ")).ToString();
                            if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }

                        }
                        if (!dateConvert)
                        {
                            Console.WriteLine(r.InnerText);
                            Console.WriteLine("date format incorrect...");
                            continue;
                        }

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early...");
                            continue;
                        }
                        //Console.WriteLine("date "+meetingDate.ToString("yyyy-MM-dd"));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);

                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }

}
