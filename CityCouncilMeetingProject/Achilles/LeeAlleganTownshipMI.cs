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
    public class LeeAlleganTownshipMI : City
    {
        private List<string> docUrls = null;

        public LeeAlleganTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LeeAlleganTownshipMI",
                CityName = "Lee-Allegan Township MI",
                CityUrl = "http://www.leetwp.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("LeeAlleganCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<string> urlList = new List<string>();
           // var docs = this.LoadDocumentsDoneSQL();
           // var queries = this.LoadQueriesDoneSQL();
            var docs = new List<Documents>();
            var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+ [\\s]{0,2}[0-9]{4}"), "MMMM yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}"), "");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                for (int i = 2016; i <= DateTime.Now.Year-1; i++)
                {
                    var  currentUrl = string.Format(subUrl, i);
                    HtmlDocument doc = web.Load(currentUrl);
                    HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                    if (list == null)
                    {
                        continue;
                    }
                    foreach (var r in list)
                    {
                        var dateConvert = false;
                        DateTime meetingDate = DateTime.MinValue;
                        foreach (var dateRegKey in dateRegFormatDic.Keys)
                        {
                            string format = dateRegFormatDic[dateRegKey];
                            string meetingDateText = dateRegKey.Match(r.InnerText.Replace("  "," ").Replace("Febuary", "February")).ToString();
                            if (string.IsNullOrWhiteSpace(format))
                            {
                                if (DateTime.TryParse(meetingDateText, out meetingDate))
                                {
                                    dateConvert = true;
                                    break;
                                }
                            }
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
                        if (urlList.Any(x => x == r.InnerText))
                        {
                            continue;
                        }
                        urlList.Add(r.InnerText);
                       // Console.WriteLine(string.Format("url:{0},date:{1}", r.InnerText, meetingDate.ToString("yyyy-MM-dd")));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                    }

                }
               
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }
}
