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
    public class WaylandMICity : City
    {
        private List<string> docUrls = null;

        public WaylandMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WaylandMICity",
                CityName = "Wayland MI",
                CityUrl = "http://www.charlottemi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WaylandMICity_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}"), " ");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+ [\\s]{0,2}[0-9]{4}"), "MMMM yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{1}-[0-9]{2}"), "MM-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{2}"), "M-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{1}-[0-9]{2}"), "M-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{8}"), "MMddyyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+. [0-9]{2} [\\s]{0,2}[0-9]{4}"), "MMM dd yyyy");
            foreach (string url in this.docUrls)
            {
                var category = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    if (r.FirstChild.Name == "img")
                    {
                        continue;
                    }
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;
                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        string format = dateRegFormatDic[dateRegKey];
                        string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
                        if (string.IsNullOrEmpty(format))
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
                        foreach (var dateRegKey in dateRegFormatDic.Keys)
                        {

                            string format = dateRegFormatDic[dateRegKey];
                            string meetingDateText = dateRegKey.Match(r.ParentNode.NextSibling.NextSibling.InnerText).ToString();
                            if (string.IsNullOrEmpty(format))
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
                    Console.WriteLine(string.Format("datestr:{0}", meetingDate.ToString("yyyy-MM-dd")));
                   // this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    }
}
