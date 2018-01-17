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
    public class MusseyCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public MusseyCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MusseyCharterTownshipMI",
                CityName = "Mussey Charter Township",
                CityUrl = "http://www.roscommontownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MusseyCharterTownshipMI_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}"), "MMMM dd, yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]"), "MMMM, yyyy");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;

                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/webapp/GetFile?fid=')]");
                foreach (var r in list)
                {

                    var meetingDateStr = r.InnerText;
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;

                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        meetingDateStr = dateRegKey.Match(r.InnerText).ToString();
                        if(!string.IsNullOrWhiteSpace(meetingDateStr))
                        {
                            string format = dateRegFormatDic[dateRegKey];
                            if (DateTime.TryParseExact(meetingDateStr, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }
                        }
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(meetingDateStr);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    Console.WriteLine(string.Format("meeting date:{0},category:{1}", meetingDateStr, category));
                    //this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, ".pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }

    }
}
