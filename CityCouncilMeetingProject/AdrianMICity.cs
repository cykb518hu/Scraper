using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;

namespace CityCouncilMeetingProject
{
    public class AdrianMICity : City
    {
        private List<string> docUrls = null;

        public AdrianMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "AdrianMICity",
                CityName = "Adrian",
                CityUrl = "http://adriancity.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("AdrianMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                cd.Navigate().GoToUrl(url);
                System.Threading.Thread.Sleep(10000);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(cd.PageSource);
                HtmlNodeCollection fileNodes = null;
                string category = string.Empty;
                if (!url.Contains("planning-commission"))
                {
                    category = "City Council";
                    fileNodes = doc.DocumentNode.SelectNodes("//a[@class='agendasminutesitem']");
                }
                else
                {
                    fileNodes = doc.DocumentNode.SelectNodes("//div[@class='interiorcontenteditabletext']/table");
                }

                foreach (HtmlNode entryNode in fileNodes)
                {
                    if (category == "City Council")
                    {
                        string meetingUrl = entryNode.Attributes["href"].Value;
                        string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                    else
                    {
                        if (fileNodes.IndexOf(entryNode) == 0)
                        {
                            category = "Planning Commission";
                        }
                        else
                        {
                            category = "Zoning Board of Appeals";
                        }

                        HtmlNodeCollection meetingNodes = entryNode.SelectNodes(".//a[@class='pdf']");

                        if (meetingNodes != null)
                        {
                            foreach (HtmlNode meetingNode in meetingNodes)
                            {
                                string meetingDocUrl = meetingNode.Attributes["href"].Value;
                                string meetingDateText = dateReg.Match(meetingNode.InnerText).Value;
                                DateTime meetingDate = DateTime.Parse(meetingDateText);

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }

                                this.ExtractADoc(c, meetingDocUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }

            cd.Quit();
        }
    }
}
