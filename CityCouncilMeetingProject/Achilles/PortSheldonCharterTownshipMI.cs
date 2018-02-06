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
    public class PortSheldonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PortSheldonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PortSheldonCharterTownshipMI",
                CityName = "PortSheldon Charter Township",
                CityUrl = "http://www.portsheldontwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PortSheldonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            //var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}");
            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/wp-content/uploads/')]");
                if(list==null)
                {
                    continue;
                }
                foreach (var r in list)
                {
                    string meetingDateText = dateReg.Match(r.PreviousSibling.InnerText).ToString();
                    var year = r.ParentNode.PreviousSibling.PreviousSibling.InnerText.Split(' ')[0];
                    meetingDateText += ", " + year;
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
                   // Console.WriteLine(string.Format("url:{0},meeting date:{1}", r.Attributes["href"].Value, meetingDateText));
                    this.ExtractADoc(c, r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }
    }
}
