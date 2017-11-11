using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;

namespace CityCouncilMeetingProject
{
    public class TraverseMICity : City
    {
        private List<string> docUrls = null;

        public TraverseMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "TraverseMICity",
                CityName = "Thetford",
                CityUrl = "http://www.traversecitymi.gov/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("TraverseMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            cd.Navigate().GoToUrl(this.docUrls[0]);
            System.Threading.Thread.Sleep(2000);

            while (true)
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(cd.PageSource);
                var entryList = doc.DocumentNode.SelectNodes("//div[@class='panel radius']");
                DateTime latestTime = DateTime.MaxValue;

                if(entryList != null)
                {
                    foreach(HtmlNode docNode in entryList)
                    {
                        HtmlNode catNode = docNode.SelectSingleNode("./h2");
                        string catText = catNode.InnerText;
                        string category = string.Empty;

                        if(catText.Contains("Planning Commission"))
                        {
                            category = "Planning";
                        }
                        else if (catText.Contains("Board of Zoning Appeals"))
                        {
                            category = "Zoning";
                        }
                        else if (catText.Contains("City Commission"))
                        {
                            category = "City Commission";
                        }
                        else
                        {
                            continue;
                        }

                        string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                        latestTime = meetingDate;
                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Reach last one...");
                            break;
                        }

                        HtmlNode agendaNode = docNode.SelectSingleNode(".//a[text()='Agenda']");
                        if(agendaNode != null)
                        {
                            string agendaUrl = agendaNode.Attributes["href"].Value;
                            agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                            this.ExtractADoc(c, agendaUrl, category, agendaUrl.Split('?').FirstOrDefault().Split('.').LastOrDefault(), meetingDate, ref docs, ref queries);
                        }

                        HtmlNode minuteNode = docNode.SelectSingleNode(".//a[text()='Minutes']");
                        if(minuteNode != null)
                        {
                            string minuteUrl = agendaNode.Attributes["href"].Value;
                            minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                            this.ExtractADoc(c, minuteUrl, category, minuteUrl.Split('?').FirstOrDefault().Split('.').LastOrDefault(), meetingDate, ref docs, ref queries);
                        }
                    }
                }

                if(latestTime < this.dtStartFrom)
                {
                    Console.WriteLine("Reach last page...");
                    break;
                }

                Console.WriteLine("Go to next page...");
                var nextPageEle = cd.FindElementByXPath("//*[text()='Next 25 Items>']");
                nextPageEle.Click();
                System.Threading.Thread.Sleep(4000);
            }

            cd.Quit();
            cd = null;
            GC.Collect();
        }
    }
}
