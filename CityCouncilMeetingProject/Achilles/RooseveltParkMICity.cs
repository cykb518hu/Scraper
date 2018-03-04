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
    public class RooseveltParkMICity : City
    {
        private List<string> docUrls = null;

        public RooseveltParkMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RooseveltParkMICity",
                CityName = "Roosevelt Park MI",
                CityUrl = "http://rooseveltpark.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RooseveltParkMICityMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
           
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("[0-9]{2}[0-9]{2}[0-9]{4}"), "MMddyyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}[0-9]{2}[0-9]{2}"), "MMddyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{4}"), "MM-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{1}-[0-9]{4}"), "MM-d-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{4}"), "M-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{1}-[0-9]{4}"), "M-d-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{1}-[0-9]{2}"), "MM-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{2}"), "M-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{1}-[0-9]{2}"), "M-d-yy");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                if (subUrl.IndexOf("{0}") > 0)
                {
                    for (int i = 2016; i <= DateTime.Now.Year; i++)
                    {
                        var currentUrl = string.Format(subUrl, i);
                        HtmlDocument doc = web.Load(currentUrl);
                        HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'wp-content/uploads')]");
                        if (list == null || !list.Any())
                        {
                            continue;
                        }
                        foreach (var r in list)
                        {
                            var fileType = "pdf";
                            if (list == null || !list.Any())
                            {
                                continue;
                            }
                            var dateStr = r.InnerText;
                            var dateConvert = false;
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
                            if (r.Attributes["href"].Value.IndexOf("doc") > 0)
                            {
                                fileType = "doc";

                            }
                           // Console.WriteLine(string.Format("url:{0},category:{1},date:{2}", dateStr, category, meetingDate.ToString("yyyy-MM-dd")));
                            this.ExtractADoc(c, r.Attributes["href"].Value, category, fileType, meetingDate, ref docs, ref queries);
                        }

                    }

                    
                }
                else
                {
                    HtmlDocument doc = web.Load(subUrl);
                    HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'wp-content/uploads')]");
                    if (list == null || !list.Any())
                    {
                        continue;
                    }
                    foreach (var r in list)
                    {
                        var fileType = "pdf";
                        var dateStr = r.InnerText;
                        var dateConvert = false;
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
                        if (r.Attributes["href"].Value.IndexOf("doc") > 0)
                        {
                            fileType = "doc";

                        }
                        //Console.WriteLine(string.Format("url:{0},category:{1},date:{2}", r.InnerText, category, meetingDate.ToString("yyyy-MM-dd")));
                         this.ExtractADoc(c, r.Attributes["href"].Value, category, fileType, meetingDate, ref docs, ref queries);
                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }


    }
}
