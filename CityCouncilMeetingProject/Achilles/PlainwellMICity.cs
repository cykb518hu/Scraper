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
    public class PlainwellMICity : City
    {
        private List<string> docUrls = null;

        public PlainwellMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PlainwellMICity",
                CityName = "Plainwell MI",
                CityUrl = "http://www.plainwell.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PlainwellMICityMI_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[0-9]{2}/[0-9]{2}/[0-9]{4}"), "MM/dd/yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}/[0-9]{1}/[0-9]{4}"), "MM/d/yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}/[0-9]{2}/[0-9]{4}"), "M/dd/yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}/[0-9]{1}/[0-9]{4}"), "M/d/yyyy");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                for (int i = 2016; i <= DateTime.Now.Year; i++)
                {
                    var  currentUrl = string.Format(subUrl, i);
                    HtmlDocument doc = web.Load(currentUrl);
                    HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/getattachment/')]");
                    foreach (var r in list)
                    {
                        var dateStr = r.ParentNode.ParentNode.FirstChild.NextSibling.InnerText;
                        if(dateStr.IndexOf("Canceled")>0)
                        {
                            continue;
                        }
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
                       // Console.WriteLine(string.Format("url:{0},category:{1},date:{2}", r.Attributes["href"].Value, category, meetingDate.ToString("yyyy-MM-dd")));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                    }

                }
               
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }
}
