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
    public class MiltonAntrimTownshipMI : City
    {
        private List<string> docUrls = null;

        public MiltonAntrimTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MiltonAntrimTownshipMI",
                CityName = "Milton-Antrim Charter Township",
                CityUrl = "http://www.miltontownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MiltonAntrimTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    if(string.IsNullOrWhiteSpace(r.InnerText))
                    {
                        continue;
                    }
                    var dateConvert = false;
                    var newDateStr = "";
                    if (category == "Park Commission")
                    {
                        newDateStr = r.InnerText.Replace(" ","").Substring(0, 4);
                    }
                    else
                    {
                        var dateStr = r.Attributes["href"].Value;
                        dateStr = dateStr.Replace("spec-", "");
                        var year = dateStr.Substring(0, 4);
                        var subDate = dateStr.Substring(dateStr.IndexOf("/") + 1, dateStr.IndexOf(".") - dateStr.IndexOf("/") - 1).ToUpper();
                        if (subDate.ToCharArray().Any(char.IsDigit))
                        {
                            newDateStr = year + "-" + subDate;    
                        }
                        else
                        {
                            newDateStr = year;
                        }
                       
                      
                    }
                    DateTime meetingDate = DateTime.MinValue;
                    string[] formats = { "yyyy-MMMdd", "yyyy-MMMd","yyyy" };
                    if (DateTime.TryParseExact(newDateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(r.InnerText + ":" + newDateStr);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    //Console.WriteLine(string.Format("datestr:{0},meeting:{1}", r.Attributes["href"].Value, meetingDate.ToString("yyyy-MM-dd")));
                     this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    }
}
