using System;
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
    public class PennfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PennfieldCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PennfieldCharterTownshipMI",
                CityName = "Pennfield Charter Township",
                CityUrl = "http://www.pennfieldtwp.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PennfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
             //var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            var dateReg = new Regex("[A-Za-z]+ [0-9]{0,2}");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var year = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'LinkClick.aspx')]");
                foreach (var r in list)
                {
                    var dateStr = r.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    if (dateReg.IsMatch(dateStr))
                    {
                        dateStr = dateReg.Match(dateStr).ToString();
                    }
                    dateStr = dateStr + "," + year;
                    string[] formats = { "MMMM,yyyy", "MMMM d,yyyy", "MMMM dd,yyyy" };
                    
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
                    var category = "";
                    try
                    {
                        var idStr = r.Attributes["id"].Value.Substring(0, r.Attributes["id"].Value.IndexOf("_Links"));
                        category = doc.DocumentNode.SelectSingleNode("//span[contains(@id,'" + idStr + "')]").InnerText.Replace("\r", "").Replace("\n", "").Trim();
                        if (category.IndexOf("Township") > -1)
                        {
                            category = "Township";
                        }
                        if (category.IndexOf("PC") > -1 || category.IndexOf("Planning Commission") > -1)
                        {
                            category = "Planning Commission";
                        }
                        if (category.IndexOf("ZBA") > -1 || category.IndexOf("Zoning Board of Appeals") > -1)
                        {
                            category = "Zoning Board of Appeals";
                        }
                    }
                    catch
                    {
                        Console.WriteLine("can not find category...");
                        continue;
                    }
                    var link = this.cityEntity.CityUrl + r.Attributes["href"].Value;
                    var data = c.DownloadData(link);
                    var docType = "pdf";
                    if (c.ResponseHeaders["Content-Type"].IndexOf("word") > -1)
                    {
                        docType = "doc";
                    }
                    this.ExtractADoc(c, link, category, docType, meetingDate, ref docs, ref queries);
                }

            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }
    }
}
