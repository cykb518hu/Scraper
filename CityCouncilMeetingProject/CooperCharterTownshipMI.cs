﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Globalization;

namespace CityCouncilMeetingProject
{
    public class CooperCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public CooperCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "CooperCharterTownshipMI",
                CityName = "Cooper Charter Township",
                CityUrl = "http://www.coopertwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("CooperCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
      
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/wp-content/uploads/')]");
                foreach (var r in list)
                {
                    var dateStr = r.InnerText.Replace("\r", "").Replace("\n", "").TrimEnd();
                    var test = dateStr;
                    dateStr = dateStr.Replace("&#8211;", "").Replace("-Special", "").Replace("Special", "").Replace("Joint", "").Replace("Meeting", "").Replace("-Mtg", "").Replace("Mtg", "").Replace("_old", "").Replace("draft", "").Replace("-Amendments", "").Trim();
                    DateTime meetingDate = DateTime.MinValue;
                    bool dateConvert = false;
                    string[] formats = { "yyyy-MM-dd", "MMMM d, yyyy", "MMMM dd, yyyy" };
                    if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine("date format incorrect...");
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }

                //if (category.Contains("pzc"))
                //{
                //    subCategory = "Planning Commission";
                //}
                //if (category.Contains("zba"))
                //{
                //    subCategory = "Zoning Board of Appeals";
                //}
                //if (category.Contains("bot"))
                //{
                //    subCategory = "Board of Trustees";
                //}
                //foreach (var r in list)
                //{
                //    DateTime meetingDate = DateTime.MinValue;
                //    try
                //    {
                //        meetingDate = DateTime.ParseExact(r.InnerText.Replace("\r", "").Replace("\n", "").TrimEnd().TrimStart(), "MM-dd-yy", null);
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine("date format incorrect...");
                //        continue;
                //    }
                //    if (meetingDate < this.dtStartFrom)
                //    {
                //        Console.WriteLine("Early...");
                //        continue;
                //    }
                // var str = "http://www.hamptontownship.org/LinkClick.aspx?fileticket=77JEAWMuTn8%3d&tabid=3062&portalid=56&mid=6876";
                //this.ExtractADoc(c, str, "ZOB", "pdf", DateTime.Now, ref docs, ref queries);
                //}
            }
        }
    }
}
