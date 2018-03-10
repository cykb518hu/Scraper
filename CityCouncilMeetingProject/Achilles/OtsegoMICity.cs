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
    public class OtsegoMICity : City
    {
        private List<string> docUrls = null;

        public OtsegoMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "OtsegoMICity",
                CityName = "Otsego City",
                CityUrl = "http://www.cityofotsego.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OtsegoMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
           var queries = this.LoadQueriesDoneSQL();
            //var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
           
            dateRegFormatDic.Add(new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}"), "yyyy-MM-dd");
            foreach (string url in this.docUrls)
            {
                var category = "City Commission";
                HtmlDocument doc = web.Load(url);
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
                            string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
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
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);

                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }

}
