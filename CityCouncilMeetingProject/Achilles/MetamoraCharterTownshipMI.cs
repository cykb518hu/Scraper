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
    public class MetamoraCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public MetamoraCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MetamoraCharterTownshipMI",
                CityName = "Metamora Charter Township",
                CityUrl = "http://metamoratownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MetamoraCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            //var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
           
            foreach (string url in this.docUrls)
            {
                var subCategory = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'go.php?id')]");
                foreach (var r in list)
                {

                    string meetingDateText = DateStr(r);
                  
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
                    subCategory = CategoryStr(r);
                   // Console.WriteLine(string.Format("date:{0},category:{1}", meetingDateText, subCategory));
                    this.ExtractADoc(c, r.Attributes["href"].Value, subCategory, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }

        public string DateStr(HtmlNode node )
        {
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            string meetingDateText = "";
            HtmlNode item = null;
            while (node.ParentNode != null)
            {
                if (node.ParentNode.Name.ToLower() == "p")
                {
                    item = node.ParentNode;
                    break;
                }
                else
                {
                    node = node.ParentNode;
                }
            }
            if (item != null)
            {
                meetingDateText = dateReg.Match(item.InnerText).ToString();
            }
            return meetingDateText;
        }
        public string CategoryStr(HtmlNode node)
        {
            string result = "";
            HtmlNode item = null;
            while (node.ParentNode != null)
            {
                if (node.ParentNode.Name.ToLower() == "td")
                {
                    item = node.ParentNode;
                    break;
                }
                else
                {
                    node = node.ParentNode;
                }
            }
            if (item != null)
            {
                result = item.FirstChild.NextSibling.InnerText;
            }
            return result.Replace("&nbsp;","").Trim();
        }
    }
}
