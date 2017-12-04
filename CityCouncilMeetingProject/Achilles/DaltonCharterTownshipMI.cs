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
    public class DaltonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public DaltonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "DaltonCharterTownshipMI",
                CityName = "Dalton Charter Township",
                CityUrl = "https://www.daltontownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("DaltonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
           // var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection linkList = doc.DocumentNode.SelectNodes("//option[contains(@value,'/wp-content/uploads/Minutes')]");

                foreach (var r in linkList)
                {
                    try
                    {
                        var docUrl = r.Attributes["value"].Value;

                        var type = docUrl.Substring(docUrl.LastIndexOf("/") + 1);
                        if (type.IndexOf("-") >= 0)
                        {
                            var dateStr = type.Substring(0, type.IndexOf("-"));
                            var category = type.Substring(type.IndexOf("-") + 1);
                            category = category.Replace("-", "").Replace("Meeting.pdf", "").Trim();
                            DateTime meetingDate = DateTime.MinValue;
                           
                            bool dateConvert = false;
                            if (DateTime.TryParseExact(dateStr, "yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                            }
                            if (!dateConvert)
                            {
                                Console.WriteLine(docUrl + "::" + dateStr);
                                Console.WriteLine("date formart incorrect");
                                continue;
                            }
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Early...");
                                continue;
                            }
                           // Console.WriteLine(string.Format("url:{0},category:{1}", docUrl, category));
                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("error:" + r.Attributes["value"].Value);
                        Console.WriteLine();
                    }
                }


            }
        }
    }
}
