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
    public class HamptonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public HamptonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HamptonCharterTownshipMI",
                CityName = "Hampton Charter Township",
                CityUrl = "http://www.hamptontownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HamptonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
      
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/"+ category + "/')]");
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
                var str = "http://www.hamptontownship.org/LinkClick.aspx?fileticket=77JEAWMuTn8%3d&tabid=3062&portalid=56&mid=6876";
                 this.ExtractADoc(c, str, "ZOB", "pdf", DateTime.Now, ref docs, ref queries);
                //}
            }
        }
    }
}
