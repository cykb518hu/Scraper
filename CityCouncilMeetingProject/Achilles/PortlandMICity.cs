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
    public class PortlandMICity : City
    {
        private List<string> docUrls = null;

        public PortlandMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PortlandMICity",
                CityName = "Portland MI",
                CityUrl = "http://www.portland-michigan.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PortlandMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
           var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");


            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);

                HtmlNodeCollection urlList = doc.DocumentNode.SelectNodes(".//a[contains(@href,'/DocumentCenter/View/')]");
                if (urlList == null)
                {
                    continue;
                }
                string meetingDateText = "";
              
                foreach (var r in urlList)
                {
                    if (category == "City Council")
                    {
                        meetingDateText = GetDate(r, subUrl);
                    }
                    else
                    {
                        meetingDateText = dateReg.Match(r.InnerText).ToString();
                    }
                    DateTime meetingDate;
                    if (!DateTime.TryParse(meetingDateText, out meetingDate))
                    {
                        Console.WriteLine(meetingDateText);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }

                    // Console.WriteLine(string.Format("meeting date :{0},category:{1}", meetingDateText, category));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }

        public string GetDate(HtmlNode node,string url)
        {
            var dateStr = node.ParentNode.ParentNode.FirstChild.NextSibling.InnerText;
            if(!string.IsNullOrWhiteSpace(dateStr))
            {
                dateStr = dateStr.Replace("Special Meeting", "").Replace("Budget Workshop", "").Replace("Goal Session", "").Replace("-", "").Trim();
                dateStr = dateStr.Substring(0, dateStr.Length - 2);
            }

            var year = url.Substring(url.LastIndexOf("/") + 1, 4);

            return dateStr + ", " + year;

        }
    }
}
