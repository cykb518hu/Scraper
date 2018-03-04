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
    public class NottawaIsabellaTownshipMI : City
    {
        private List<string> docUrls = null;

        public NottawaIsabellaTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "NottawaIsabellaTownshipMI",
                CityName = "Nottawa-Isabella Charter Township",
                CityUrl = "http://nottawatwp.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("NottawaIsabellaTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            //dateRegFormatDic.Add(new Regex("[a-zA-Z]+ [\\s]{0,2}[0-9]{4}"), "MMMM yyyy");
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,2}[0-9]{1,2} [\\s]{0,2}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    var dataStr = r.InnerText.Replace(".", " ").Replace("-", " ").Replace("Sept", "Sep");
                    var dateConvert = false;
                    //DateTime meetingDate = DateTime.MinValue;
                    //foreach (var dateRegKey in dateRegFormatDic.Keys)
                    //{
                    //    string format = dateRegFormatDic[dateRegKey];
                    //    string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
                    //    if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    //    {
                    //        dateConvert = true;
                    //        break;
                    //    }

                    //}
                    string meetingDateText = dateReg.Match(dataStr).ToString();
                    DateTime meetingDate;
                    if (!DateTime.TryParse(meetingDateText, out meetingDate))
                    {
                        Console.WriteLine("date format incorrect...");
                        Console.WriteLine(dataStr);
                        continue;
                    }
                    //if (!dateConvert)
                    //{
                    //    Console.WriteLine(r.InnerText);
                    //    Console.WriteLine("date format incorrect...");
                    //    continue;
                    //}

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    //Console.WriteLine(string.Format("datestr:{0},meeting:{1}", dataStr, meetingDate.ToString("yyyy-MM-dd")));
                    this.ExtractADoc(c, r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    }
}
