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
    public class NegauneeMICity : City
    {
        private List<string> docUrls = null;

        public NegauneeMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "NegauneeMICity",
                CityName = "Negaunee MI",
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

            this.docUrls = File.ReadAllLines("NegauneeMICity_Urls.txt").ToList();
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
                var subUrl = url.Split('*')[1];
                var type = url.Split('*')[0];
                var category = "";

                HtmlDocument doc = web.Load(subUrl);
                var divNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'panel-last-child')]");
                HtmlNodeCollection list = divNode.SelectNodes(".//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    bool dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;
                    if (type == "Meeting")
                    {
                        Regex dateReg = new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}");
                        string meetingDateText = dateReg.Match(r.InnerText).ToString();
                        string[] formats = { "MM-dd-yy" };
                        if (DateTime.TryParseExact(meetingDateText, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                    }
                    else
                    {
                        Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
                        string meetingDateText = dateReg.Match(r.InnerText).ToString();
                        if (DateTime.TryParse(meetingDateText, out meetingDate))
                        {
                            dateConvert = true;
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
                    if (type == "Meeting")
                    {
                        var tableId = r.ParentNode.ParentNode.ParentNode.Attributes["id"].Value.Split('-')[1];

                        var span = doc.DocumentNode.SelectSingleNode("//span[contains(@id,'" + tableId + "')]");
                        if (span != null)
                        {
                            category = span.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                            category = System.Net.WebUtility.HtmlDecode(category);
                        }
                    }
                    else
                    {
                        category = type;
                    }

                    this.ExtractADoc(c, r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }

            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }
    }
}
