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
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                List<string> cutStr = new List<string>();
                cutStr.Add("Minutes");
                cutStr.Add("Meeting");
                cutStr.Add("Commission");
                cutStr.Add("Board");
                HtmlDocument doc = web.Load(url);
                HtmlNode div= doc.DocumentNode.SelectSingleNode("//div[@id='p7AP3rw_FC51']");
                HtmlNodeCollection typeList = div.SelectNodes("//div[contains(@class,'p7AP3trig')]");
                HtmlNodeCollection linkList = div.SelectNodes("//div[contains(@class,'p7ap3-col-wrapper')]");
                for (int i=0; i<typeList.Count;i++)
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
                        DateTime meetingDate = DateTime.MinValue;
                        bool dateConvert = false;
                        if (DateTime.TryParseExact(dateStr, "MM-d-yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        if (DateTime.TryParseExact(dateStr, "MM-dd-yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "M/d/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MM/d/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MMM. d, yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MMM. dd, yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                        }
                        else if (DateTime.TryParseExact(dateStr, "MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
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
                        Console.WriteLine("original datestr:" + dateStr + "|new date str" + meetingDate.ToString("yyyy-MM-dd"));
                        Console.WriteLine();
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
                        this.ExtractADoc(c, this.cityEntity.CityUrl + d.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
