using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    public class KalamazooMICity : City
    {
        private List<string> docUrls = null;

        public KalamazooMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "KalamazooMICity",
                CityName = "Kalamazoo",
                CityUrl = "http://www.kalamazoocity.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("KalamazooMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];

                if (category == "City Council")
                {
                    this.ExractCouncil(categoryUrl, ref docs, ref queries);
                }
                else
                {
                    this.ExtractOthers(categoryUrl, category, ref docs, ref queries);
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }

        public void ExractCouncil(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();
            cd.Navigate().GoToUrl(url);
            System.Threading.Thread.Sleep(3000);
            IWebElement rangeEle = cd.FindElementByXPath("//select[@class='agendasearch-input']");
            SelectElement rangeSelectEle = new SelectElement(rangeEle);
            rangeSelectEle.SelectByValue("cus");
            System.Threading.Thread.Sleep(3000);
            IWebElement dateStartEle = cd.FindElementById("ctl00_ContentPlaceHolder1_SearchAgendasMeetings_radCalendarFrom_dateInput");
            IWebElement dateEndEle = cd.FindElementById("ctl00_ContentPlaceHolder1_SearchAgendasMeetings_radCalendarTo_dateInput");
            dateStartEle.Clear();
            dateStartEle.SendKeys(this.dtStartFrom.ToString("M/d/yyyy"));
            dateEndEle.Clear();
            dateEndEle.SendKeys(DateTime.Now.ToString("M/d/yyyy"));
            System.Threading.Thread.Sleep(2000);
            IWebElement searchBtnEle = cd.FindElementById("ctl00_ContentPlaceHolder1_SearchAgendasMeetings_imageButtonSearch");
            searchBtnEle.Click();
            System.Threading.Thread.Sleep(2000);

            while (true)
            {
                try
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(cd.PageSource);

                    HtmlNodeCollection rowList = doc.DocumentNode.SelectNodes("//table[@id='ctl00_ContentPlaceHolder1_SearchAgendasMeetings_radGridMeetings_ctl00']/tbody/tr[contains(@class,'Row')]");
                    if (rowList != null)
                    {
                        foreach (HtmlNode rowNode in rowList)
                        {
                            HtmlNode meetingDateNode = rowNode.SelectSingleNode("./td");
                            string meetingText = meetingDateNode.InnerText;
                            DateTime meetingDate = DateTime.ParseExact(meetingText, "MM/dd/yy", null);

                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }

                            HtmlNodeCollection docNodeList = rowNode.SelectNodes(".//a[contains(@href,'ashx')]");

                            if (docNodeList != null)
                            {
                                foreach (HtmlNode docNode in docNodeList)
                                {
                                    string docUrl = docNode.Attributes["href"].Value;
                                    docUrl = docUrl.StartsWith("http") ? docUrl : url.Trim('#') + docUrl;
                                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                                    if (localdoc == null)
                                    {
                                        string tag = docUrl.Contains("Minute") ? "Minute" : "Agenda";
                                        localdoc = new Documents();
                                        localdoc.DocId = Guid.NewGuid().ToString();
                                        localdoc.CityId = this.cityEntity.CityId;
                                        localdoc.Checked = false;
                                        localdoc.Important = false;
                                        localdoc.DocType = "City Council";
                                        localdoc.DocSource = docUrl;
                                        string localFile = string.Format("{0}\\Council_{1}_{2}.pdf", this.localDirectory, tag, meetingDate.ToString("yyyy-MM-dd"));
                                        localdoc.DocLocalPath = localFile;

                                        try
                                        {
                                            c.DownloadFile(docUrl, localFile);
                                        }
                                        catch
                                        {
                                        }

                                        docs.Add(localdoc);
                                    }
                                    else
                                    {
                                        Console.WriteLine("This file already downloaded...");
                                    }

                                    this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                    if (qr == null)
                                    {
                                        qr = new QueryResult();
                                        qr.CityId = localdoc.CityId;
                                        qr.DocId = localdoc.DocId;
                                        qr.MeetingDate = meetingDate;
                                        qr.SearchTime = DateTime.Now;

                                        queries.Add(qr);
                                    }

                                    this.ExtractQueriesFromDoc(localdoc, ref qr);
                                    Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                                    this.SaveMeetingResultsToSQL(docs, queries);
                                }
                            }
                        }
                    }

                    IWebElement nextPageBtnEle = cd.FindElementByXPath("//a[@title='Next Page']");
                    nextPageBtnEle.Click();
                    System.Threading.Thread.Sleep(3000);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Reach last page...");
                    Console.ResetColor();
                    break;
                }
            }

            cd.Quit();
            cd = null;
        }

        public void ExtractOthers(string url, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]+");
            HtmlDocument listDoc = web.Load(url);
            HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");

            if (docNodeList != null)
            {
                foreach (HtmlNode docNode in docNodeList)
                {
                    string docUrl = docNode.Attributes["href"].Value;
                    docUrl = docUrl.StartsWith("http") ? docUrl : this.cityEntity.CityUrl + docUrl;
                    string meetingDateText = docNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160);
                    meetingDateText = dateReg.Match(meetingDateText).ToString();
                    DateTime meetingDate = DateTime.Parse(meetingDateText);

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip!");
                        continue;
                    }

                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                    if (localdoc == null)
                    {
                        localdoc = new Documents();
                        localdoc.DocId = Guid.NewGuid().ToString();
                        localdoc.CityId = this.cityEntity.CityId;
                        localdoc.Checked = false;
                        localdoc.Important = false;
                        localdoc.DocSource = docUrl;
                        localdoc.DocType = category;
                        localdoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, docUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());

                        try
                        {
                            c.DownloadFile(docUrl, localdoc.DocLocalPath);
                        }
                        catch
                        {
                        }

                        docs.Add(localdoc);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("This document already downloaded...");
                        Console.ResetColor();
                    }

                    this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                    if (qr == null)
                    {
                        qr = new QueryResult();
                        qr.CityId = localdoc.CityId;
                        qr.DocId = localdoc.DocId;
                        qr.SearchTime = DateTime.Now;

                        qr.MeetingDate = meetingDate;
                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localdoc, ref qr);
                    Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                }
            }
        }
    }
}
