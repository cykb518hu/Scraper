//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class HamtramckMICity : City
    {
        private List<string> docUrls = null;

        public HamtramckMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "HamtramckMICity",
                CityName = "Hamtramck",
                CityUrl = "http://www.hamtramck.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HamtramckMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+_[0-9]{1,2}_[0-9]{4}");
            Regex dateReg1 = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);

                if(category == "City Council")
                {
                    HtmlNodeCollection filesNodes = doc.DocumentNode.SelectNodes("//div[@class='content']/table//tr");

                    if (filesNodes != null)
                    {
                        foreach(HtmlNode entryNode in filesNodes)
                        {
                            if(entryNode.SelectNodes("./td").Count == 1)
                            {
                                Console.WriteLine("Not meeting node...");
                                continue;
                            }

                            string meetingDateText = dateReg1.Match(entryNode.SelectSingleNode("./td").InnerText).ToString();
#if debug
                            try
                            {
                                DateTime.Parse(meetingDateText);
                                Console.WriteLine("No problem, continue");
                                continue;
                            }
                            catch
                            {
                                Console.WriteLine("Not match {0}...", meetingDateText);
                                continue;
                            }
#endif
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if(meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                break;
                            }

                            HtmlNodeCollection filesUrlsNodes = entryNode.SelectNodes(".//a[@href]");

                            foreach (HtmlNode fileUrlNode in filesUrlsNodes)
                            {
                                string fileUrl = categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), string.Empty) + fileUrlNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
                else
                {
                    HtmlNodeCollection filesNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");

                    if(filesNodes != null)
                    {
                        Console.WriteLine("Category {0}...", category);

                        foreach(HtmlNode fileNode in filesNodes)
                        {
                            string fileUrl = categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), string.Empty) + fileNode.Attributes["href"].Value;
                            string meetingDateText = dateReg.Match(fileUrl).ToString();

                            if (string.IsNullOrEmpty(meetingDateText))
                            {
                                continue;
                            }

#if debug
                            bool isMatch = dateReg.IsMatch(fileUrl);
                            if (isMatch)
                            {
                                Console.WriteLine("No problem, continue");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Not match {0}...", fileUrl);
                                continue;
                            }
#endif
                            string[] elements = meetingDateText.Split('_');
                            meetingDateText = string.Format("{0} {1}, {2}", elements[0], elements[1], elements[2]);
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if(meetingDate < this.dtStartFrom)
                            {
                                continue;
                            }

                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
