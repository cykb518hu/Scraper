using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;

namespace CityCouncilMeetingProject
{
    public class HowellMICity : City
    {
        private List<string> docUrls = null;

        public HowellMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "HowellMICity",
                CityName = "Howell",
                CityUrl = "http://www.cityofhowell.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HowellMICityMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {

            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            //var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                string[] formats = { "MM.dd.yyyy", "MM-dd-yyyy" };
                foreach (var r in list)
                {
                    var fullStr = r.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    if (string.IsNullOrWhiteSpace(fullStr) || fullStr.IndexOf("20") < 0)
                    {
                        continue;
                    }
                    var dateStr = fullStr.Substring(0, fullStr.IndexOf(" "));
                    var category = fullStr.Substring(fullStr.IndexOf(" ") + 1);
                    category = Regex.Replace(category, "agendas", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "agenda", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "minutes", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "minute", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "Pakets", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "Paket", "", RegexOptions.IgnoreCase);
                    category = Regex.Replace(category, "PACKET", "", RegexOptions.IgnoreCase);
                    category = category.Trim();
                    DateTime meetingDate = DateTime.MinValue;
                    bool dateConvert = false;
                    if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(string.Format("date str:{0}", dateStr));
                        Console.WriteLine("date formart incorrect");
                        continue;
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                     //Console.WriteLine(string.Format("url:{0},category:{1}", dateStr, category));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    
    }
}
