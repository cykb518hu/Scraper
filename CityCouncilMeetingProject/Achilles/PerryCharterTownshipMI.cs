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
    public class PerryCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PerryCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PerryCharterTownshipMI",
                CityName = "Perry Charter Township",
                CityUrl = "http://www.alpenatownship.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PerryCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
           // var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}");
            List<int> yearList = new List<int>();
            for (var i = 2016; i <= DateTime.Now.Year; i++)
            {
                yearList.Add(i);
            }
            foreach (string url in this.docUrls)
            {
            
                var category = url.Split('*')[0];
                var count = 0;
                foreach (var y in yearList)
                {
                    var subUrl = url.Split('*')[1];
                    if (subUrl.IndexOf("{0}") < 0 && count > 0)
                    {
                        break;
                    }
                    else
                    {
                        subUrl = string.Format(subUrl, y);
                    }
                    count++;
                   
                    try
                    {
                        HtmlDocument doc = web.Load(subUrl);
                        HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                        foreach (var r in list)
                        {
                            string meetingDateText = dateReg.Match(r.Attributes["href"].Value).ToString();
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
                            subUrl = subUrl.Substring(0, subUrl.LastIndexOf('/') + 1);
                            //Console.WriteLine(string.Format("url:{0},category:{1}", subUrl + r.Attributes["href"].Value, category));
                            this.ExtractADoc(c, subUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                        
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine(subUrl + " ---- invalid");
                    }

                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }
    }
}
