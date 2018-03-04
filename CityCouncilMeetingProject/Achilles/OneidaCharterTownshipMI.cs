using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Net.Http;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class OneidaCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public OneidaCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "OneidaCharterTownshipMI",
                CityName = "Oneida Charter Township",
                CityUrl = "http://www.oneidatownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OneidaCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/LinkClick.aspx')]");
                if (list == null || !list.Any())
                {
                    list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/Portals/')]");
                }
                foreach (var r in list)
                {
                    var fileType = "pdf";
                    var dateStr = r.InnerText;
                    if (dateStr.ToUpper().IndexOf("Canceled".ToUpper()) > 0)
                    {
                        continue;
                    }
                    string meetingDateText = dateReg.Match(dateStr).ToString();
                    DateTime meetingDate;
                    if (!DateTime.TryParse(meetingDateText, out meetingDate))
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
                        fileType = "docx";
                    }
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);

                }

            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }


    }


}
