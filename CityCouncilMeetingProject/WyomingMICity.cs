using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace CityCouncilMeetingProject
{
    public class WyomingMICity : City
    {
        private List<string> docUrls = null;
        public WyomingMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WyomingMICity",
                CityName = "Wyoming",
                CityUrl = "https://www.wyomingmi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WyomingMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();

            ChromeDriver cd = new ChromeDriver();

            cd.Navigate().GoToUrl(this.docUrls.FirstOrDefault());
            System.Threading.Thread.Sleep(5000);

            IWebElement startEle = cd.FindElementById("dnn_ctr524_ViewEventPlannerModule_EventListView_calStartDate");
            startEle.Clear();
            startEle.SendKeys(this.dtStartFrom.ToString("M/d/yyyy"));
            System.Threading.Thread.Sleep(1000);
            IWebElement endEle = cd.FindElementById("dnn_ctr524_ViewEventPlannerModule_EventListView_calEndDate");
            endEle.Clear();
            endEle.SendKeys(DateTime.Now.ToString("M/d/yyyy"));
            System.Threading.Thread.Sleep(1000);
            IWebElement searchBtnEle = cd.FindElementById("dnn_ctr524_ViewEventPlannerModule_EventListView_lnkSearch");
            searchBtnEle.Click();
            System.Threading.Thread.Sleep(6000);
            searchBtnEle = cd.FindElementById("dnn_ctr524_ViewEventPlannerModule_EventListView_lnkSearch");
            searchBtnEle.Click();
            System.Threading.Thread.Sleep(4000);
            int currentPage = 1;
            while (true)
            {
                HtmlDocument listDoc = new HtmlDocument();
                listDoc.LoadHtml(cd.PageSource);
                HtmlNodeCollection eventList = listDoc.DocumentNode.SelectNodes("//table[@id='dnn_ctr524_ViewEventPlannerModule_EventListView_grdEvents']//div[@class='eventListView col-sm-12']");

                if (eventList != null)
                {
                    foreach (HtmlNode eventNode in eventList)
                    {
                        HtmlNode meetingDateNode = eventNode.SelectSingleNode(".//meta[@itemprop='startDate']");
                        DateTime meetingDate = DateTime.Parse(meetingDateNode.Attributes["content"].Value);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNode eventUrlNode = eventNode.SelectSingleNode(".//a[@class='eventPlannerStandardButton eventListButton']");
                        string eventUrl = eventUrlNode == null ? string.Empty : eventUrlNode.Attributes["href"].Value;

                        HtmlNode eventTitleNode = eventNode.SelectSingleNode(".//span[@class='eventTitle']");
                        string eventTitle = eventTitleNode == null ? string.Empty : eventTitleNode.InnerText;
                        string category = string.Empty;

                        if (eventTitle.Contains("City Council"))
                        {
                            category = "City Council";
                        }
                        else if (eventTitle.Contains("Planning Commission"))
                        {
                            category = "Planning Commission";
                        }
                        else if (eventTitle.Contains("Zoning Board of Appeals"))
                        {
                            category = "Zoning Board of Appeals";
                        }

                        if (string.IsNullOrEmpty(category) == false)
                        {
                            HtmlDocument meetingDoc = web.Load(eventUrl);
                            HtmlNode agendaNode = meetingDoc.DocumentNode.SelectSingleNode("//a[text()='Full Agenda Link']");
                            agendaNode = (agendaNode == null || (agendaNode != null && string.IsNullOrEmpty(agendaNode.Attributes["href"].Value))) ? meetingDoc.DocumentNode.SelectSingleNode("//a[text()='Agenda Link']") : agendaNode;
                            HtmlNode minuteNode = meetingDoc.DocumentNode.SelectSingleNode("//a[text()='Meetings Minutes Link']");
                            string tag = string.Empty;

                            if (agendaNode != null)
                            {
                                tag = "agenda";
                                string agendaUrl = agendaNode.Attributes["href"].Value;
                                if (!string.IsNullOrEmpty(agendaUrl))
                                {
                                    agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                                    this.ExtractADoc(c, agendaUrl, category, tag, meetingDate, ref docs, ref queries);
                                }
                            }

                            if (minuteNode != null)
                            {
                                tag = "minute";
                                string minuteUrl = minuteNode.Attributes["href"].Value;
                                if (!string.IsNullOrEmpty(minuteUrl))
                                {
                                    minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                                    this.ExtractADoc(c, minuteUrl, category, tag, meetingDate, ref docs, ref queries);
                                }
                            }

                            this.SaveMeetingResultsToSQL(docs, queries);
                        }
                    }
                }

                currentPage++;
                try
                {
                    IWebElement pageEle = cd.FindElementByXPath(string.Format("//a[text()='{0}']", currentPage));
                    Console.WriteLine("Go to page {0}...", currentPage);
                    pageEle.Click();
                    System.Threading.Thread.Sleep(3000);
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No more pages, break!");
                    Console.ResetColor();
                    break;
                }
            }

            cd.Quit();
            cd = null;
        }

        private void ExtractADoc(WebClient c, string docUrl, string docType, string tag, DateTime meetingDate, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Documents localDoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

            if (localDoc == null)
            {
                localDoc = new Documents();
                localDoc.DocId = Guid.NewGuid().ToString();
                localDoc.CityId = this.cityEntity.CityId;
                localDoc.DocType = docType;
                localDoc.DocSource = docUrl;
                localDoc.Checked = false;
                localDoc.Important = false;
                localDoc.DocLocalPath = string.Format("{0}\\{1}_{2}", this.localDirectory, tag, docUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());

                try
                {
                    c.DownloadFile(docUrl, localDoc.DocLocalPath);
                }
                catch
                {
                }

                docs.Add(localDoc);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("This file already downloaded...");
                Console.ResetColor();
            }

            this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

            if (qr == null)
            {
                qr = new QueryResult();
                qr.DocId = localDoc.DocId;
                qr.CityId = localDoc.CityId;
                qr.MeetingDate = meetingDate;
                qr.SearchTime = DateTime.Now;

                queries.Add(qr);
            }

            this.ExtractQueriesFromDoc(localDoc, ref qr);
            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
        }
    }
}
