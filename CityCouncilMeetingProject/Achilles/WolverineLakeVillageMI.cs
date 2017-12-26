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
    public class WolverineLakeVillageMI : City
    {
        private List<string> docUrls = null;

        public WolverineLakeVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WolverineLakeVillageMI",
                CityName = "WolverineLake",
                CityUrl = "https://www.wolverinelake.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WolverineLakeVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            StringBuilder yearBuilder = new StringBuilder();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(i.ToString());
                yearBuilder.Append('|');
            }

            Regex yearReg = new Regex(string.Format("({0})", yearBuilder.ToString().Trim('|')));

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");

                foreach (var r in list)
                {
                    string meetingDateText = dateReg.Match(r.InnerText).ToString();
                    DateTime meetingDate;
                    if (!DateTime.TryParse(meetingDateText, out meetingDate))
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
                   // Console.WriteLine(string.Format("url:{0},category:{1}", this.cityEntity.CityUrl + r.Attributes["href"].Value, category));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
