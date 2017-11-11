using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace CityCouncilMeetingProject
{
    public class BuchananMICity : City
    {
        private List<string> docUrls = null;

        public BuchananMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BuchananMICity",
                CityName = "Buchanan",
                CityUrl = "https://www.cityofbuchanan.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BuchananMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            ChromeDriver cd = new ChromeDriver();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2}[\\s]{0,1}[0-9]{4}");

            foreach (string docUrl in this.docUrls)
            {
                string category = docUrl.Split('*')[0];
                string categoryUrl = docUrl.Split('*')[1];
                cd.Navigate().GoToUrl(categoryUrl);
                System.Threading.Thread.Sleep(2000);
                HtmlDocument yearListDoc = new HtmlDocument();
                yearListDoc.LoadHtml(cd.PageSource);
                HtmlNodeCollection yearNodeList = yearListDoc.DocumentNode.SelectNodes("//div[@class='media']//a[@class='page-list__item']");

                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    Console.WriteLine("Working on year {0}...", i);
                    var yearNode = yearNodeList.FirstOrDefault(t => t.InnerText.Contains(i.ToString()));

                    if (yearNode != null)
                    {
                        string yearUrl = this.cityEntity.CityUrl + yearNode.Attributes["href"].Value;
                        cd.Navigate().GoToUrl(yearUrl);
                        System.Threading.Thread.Sleep(2000);
                        var fileList = cd.FindElementsByXPath("//a[@class='blog__post']");
                        Console.WriteLine("{0} nodes found...", fileList.Count);
                        int page = 1; 
                        do
                        {
                            bool breakNow = false;
                            var nextButtonEle = cd.FindElementByXPath("//a[@class='su_next']/parent::li");
                            if (nextButtonEle.GetAttribute("class") != "next")
                            {
                                Console.WriteLine("Reach last page...");
                                breakNow = true;
                            }

                            if (fileList.All(t =>
                                {
                                    string href = t.GetAttribute("href");
                                    string text = t.Text;
                                    return (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(text));
                                }))
                            {
                                Console.WriteLine("No doc...");
                                fileList = null;
                                continue;
                            }
                            
                            var fileUrlDic = new Dictionary<string, string>();
                            foreach (var ele in fileList)
                            {
                                string url = ele.GetAttribute("href");

                                if (!string.IsNullOrEmpty(url))
                                {
                                    fileUrlDic.Add(url, ele.Text);
                                }
                            }

                            foreach(string url in fileUrlDic.Keys)
                            {
                                string meetingDateText = dateReg.Match(fileUrlDic[url]).ToString();
                                Console.WriteLine("DEBUG: {0} - {1}", meetingDateText, fileUrlDic[url]);
                                DateTime meetingDate = DateTime.Parse(meetingDateText);

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }

                                var localdoc = docs.FirstOrDefault(t => t.DocSource == url);

                                if (localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.Checked = false;
                                    localdoc.Important = false;
                                    localdoc.DocType = category;
                                    localdoc.DocSource = url;
                                    string localPath = string.Format("{0}\\{1}_{2}_{3}.html",
                                        this.localDirectory,
                                        category,
                                        meetingDate.ToString("yyyy-MM-dd"),
                                        localdoc.DocId);
                                    localdoc.DocLocalPath = localPath;
                                    localdoc.Readable = true;
                                    docs.Add(localdoc);

                                    cd.Navigate().GoToUrl(url);
                                    System.Threading.Thread.Sleep(1000);
                                    var targetEle = cd.FindElementByXPath("//div[@class='su_bootstrap_safe su-content-wrapper']");
                                    string js = "document.documentElement.scrollTop=" + targetEle.Location.Y;
                                    ((IJavaScriptExecutor)cd).ExecuteScript(js);
                                    System.Threading.Thread.Sleep(1000);
                                    string meetingText = targetEle.Text;
                                    localdoc.DocBodyDic.Add(1, meetingText);
                                    File.WriteAllText(localdoc.DocLocalPath, meetingText);
                                }
                                else
                                {
                                    Console.WriteLine("This file already downloaded....");
                                    localdoc.DocBodyDic.Add(1, File.ReadAllText(localdoc.DocLocalPath));
                                }

                                var qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                if(qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.CityId = this.cityEntity.CityId;
                                    qr.DocId = localdoc.DocId;
                                    qr.SearchTime = DateTime.Now;
                                    qr.MeetingDate = meetingDate;
                                    qr.QueryId = Guid.NewGuid().ToString();
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localdoc, ref qr);
                                Console.WriteLine("{0} docs saved, {1} queries save...", docs.Count, queries.Count);
                            }

                            this.SaveMeetingResultsToSQL(docs, queries);
                            page++;

                            if (breakNow)
                            {
                                break;
                            }

                            Console.WriteLine("Go to page {0}...", page);
                            string newPage = yearUrl + "?page=" + page;
                            cd.Navigate().GoToUrl(newPage);
                            System.Threading.Thread.Sleep(2000);
                            fileList = cd.FindElementsByXPath("//a[@class='blog__post']");
                        }
                        while (fileList != null && fileList.Count > 0);
                    }
                }
            }

            cd.Quit();
            cd = null;
        }
    }
}
