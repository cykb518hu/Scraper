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
    public class EastChinaCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public EastChinaCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "EastChinaCharterTownshipMI",
                CityName = "East China Charter Township",
                CityUrl = "http://www.eastchinatownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("EastChinaCharterTownshipMI_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{4}"), "MM-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}"), "");
            foreach (string url in this.docUrls)
            {
                var subCategory = "";
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
                    subCategory = r.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode.SelectSingleNode(".//h3").InnerText.Replace("\r", "").Replace("\n", "").TrimEnd().TrimStart();
                    //Console.WriteLine(string.Format("url:{0},category:{1},datetime:{2}", r.Attributes["href"].Value, subCategory, meetingDate.ToString("yyyy-MM-dd")));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, subCategory, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }
    }
}
