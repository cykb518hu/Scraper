﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class HancockMICity : City
    {
        private List<string> docUrls = null;

        public HancockMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HancockMICity",
                CityName = "Hancock MI",
                CityUrl = "http://www.cityofhancock.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HancockMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
          //var docs = new List<Documents>();
          //  var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                var category = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'minutes/')]");
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
                    category = r.ParentNode.ParentNode.ParentNode.ParentNode.SelectNodes(".//tr")[0].SelectSingleNode(".//font").InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    this.ExtractADoc(c, this.cityEntity.CityUrl+ r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }
    }
}
