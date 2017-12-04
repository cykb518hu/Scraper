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
    public class SpartaCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public SpartaCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SpartaCharterTownshipMI",
                CityName = "Sparta Charter Township",
                CityUrl = "http://spartatownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SpartaCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            // var docs = new List<Documents>();
            // var queries = new List<QueryResult>();
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                List<string> cutStr = new List<string>();
                cutStr.Add("Minutes");
                cutStr.Add("Meeting");
                cutStr.Add("Commission");
                cutStr.Add("Board");
                HtmlDocument doc = web.Load(url);
                HtmlNode div = doc.DocumentNode.SelectSingleNode("//div[@id='p7AP3rw_FC51']");
                HtmlNodeCollection typeList = div.SelectNodes("//div[contains(@class,'p7AP3trig')]");
                HtmlNodeCollection linkList = div.SelectNodes("//div[contains(@class,'p7ap3-col-wrapper')]");
                for (int i = 0; i < typeList.Count; i++)
                {
                    var category = typeList[i].InnerText.Replace("\r", "").Replace("\n", "").TrimEnd().TrimStart();
                    HtmlNodeCollection docList = linkList[i].SelectSingleNode("p").SelectNodes("a");
                    foreach (var d in docList)
                    {
                        var dateStr = d.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                        foreach (var str in cutStr)
                        {
                            if (dateStr.IndexOf(str) > 0)
                            {
                                dateStr = dateStr.Substring(dateStr.IndexOf(str) + str.Length).Replace("(PDF)", "").Trim();
                                break;
                            }
                        }
                        string[] formats = { "MM-d-yy", "MM-d-yyyy", "MM-dd-yy", "M/d/yyyy", "MM/d/yyyy", "MM/dd/yyyy", "MMM. d, yyyy", "MMM. dd, yyyy", "MMMM d, yyyy", "MMMM dd, yyyy" };
                        DateTime meetingDate = DateTime.MinValue;
                        bool dateConvert = false;
                        if (DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }

                        if (!dateConvert)
                        {
                            dateStr = dateStr.Replace("Sept", "Sep");
                            if (DateTime.TryParseExact(dateStr, "MMM. dd, yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                            }
                        }
                        if (!dateConvert)
                        {
                            Console.WriteLine("date formart incorrect");
                            continue;
                        }
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early...");
                            continue;
                        }
                        //Console.WriteLine(string.Format("url:{0},category:{1}", d.Attributes["href"].Value, category));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + d.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
