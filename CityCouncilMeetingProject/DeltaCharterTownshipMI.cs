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
    public class DeltaCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public DeltaCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "DeltaCharterTownshipMI",
                CityName = "Delta",
                CityUrl = "http://www.deltami.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("DeltaCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    HtmlNode yearNode = doc.DocumentNode.SelectSingleNode(string.Format("//a[text()='{0}']", i));

                    if (yearNode != null)
                    {
                        string yearUrl =  categoryUrl.Split('?').FirstOrDefault() + yearNode.Attributes["href"].Value;
                        HtmlDocument yearDoc = web.Load(yearUrl);
                        
                        if (category == "City Council")
                        {
                            HtmlNodeCollection monthsNodes = yearDoc.DocumentNode.SelectNodes("//div[contains(@class,'bex-cell')]//a[@href]");

                            Console.WriteLine("DEBUG: {0}...", monthsNodes == null);
                            if(monthsNodes != null)
                            {
                                Console.WriteLine("{0} Months...", monthsNodes.Count);
                                foreach(HtmlNode monthNode in monthsNodes)
                                {
                                    string monthUrl = categoryUrl.Split('?').FirstOrDefault() + monthNode.Attributes["href"].Value;
                                    HtmlDocument monthDoc = web.Load(monthUrl);
                                    HtmlNodeCollection docNodes = monthDoc.DocumentNode.SelectNodes("//div[@class='bex-table']//a[contains(@href,'.')]");

                                    if(docNodes != null)
                                    {
                                        foreach(HtmlNode docNode in docNodes)
                                        {
                                            Regex dateReg1 = new Regex("([a-zA-Z]+[\\s]{0,2}[0-9]{1,2}[\\s]{0,2}[0-9]{4}|[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4})");
                                            string meetingDateText = dateReg1.Match(docNode.InnerText).ToString();
                                            Console.ForegroundColor = ConsoleColor.Yellow;
                                            Console.WriteLine("DEBUG: {0}; {1}...", docNode.InnerText, meetingDateText);
                                            Console.ResetColor();
                                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                                            if (meetingDate < this.dtStartFrom)
                                            {
                                                Console.WriteLine("Too early, skip...");
                                                continue;
                                            }

                                            string meetingUrl = categoryUrl.Split('?').FirstOrDefault() + docNode.Attributes["href"].Value;
                                            this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            HtmlNodeCollection docNodes = yearDoc.DocumentNode.SelectNodes("//div[@class='bex-table']//a[contains(@href,'.')]");

                            if(docNodes != null)
                            {
                                foreach(HtmlNode docNode in docNodes)
                                {
                                    string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                                    DateTime meetingDate = DateTime.Parse(meetingDateText);
                                    if (meetingDate < this.dtStartFrom)
                                    {
                                        Console.WriteLine("Too early, skip...");
                                        continue;
                                    }
                                    string meetingUrl = categoryUrl.Split('?').FirstOrDefault() + docNode.Attributes["href"].Value;
                                    this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
