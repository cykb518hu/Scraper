using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    class BattleCreekMICity : City
    {
        private List<string> docUrls = null;

        public BattleCreekMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BattleCreekMICity",
                CityName = "BattleCreek",
                CityUrl = "http://www.battlecreekmi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("BattleCreekMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                if (category == "City Commission")
                {
                    this.ExtractCityCommission(categoryUrl, ref docs, ref queries);
                }
                else
                {
                    this.ExtractOthers(categoryUrl, category, ref docs, ref queries);
                }
            }
        }

        public void ExtractCityCommission(string url, ref List<Documents> docs, ref List<QueryResult> queries)
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
                            DateTime meetingDate = DateTime.MinValue;
                            try
                            {
                                meetingDate = DateTime.ParseExact(meetingText, "MM/dd/yy", null);
                            }
                            catch
                            {
                            }

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
                                        string localFile = string.Format("{0}\\Council_{1}_{2}_{3}.pdf",
                                            this.localDirectory,
                                            tag,
                                            meetingDate.ToString("yyyy-MM-dd"),
                                            Guid.NewGuid().ToString());
                                        localdoc.DocLocalPath = localFile;

                                        try
                                        {
                                            c.DownloadFile(docUrl, localFile);
                                        }
                                        catch (Exception ex)
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
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            HtmlDocument listDoc = web.Load(url);
            HtmlNodeCollection recordNodes = listDoc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

            if (recordNodes != null && recordNodes.Count > 0)
            {
                foreach (HtmlNode recordNode in recordNodes)
                {
                    try
                    {
                        HtmlNode dateNode = recordNode.SelectSingleNode(".//strong");
                        string dateText = dateReg.Match(dateNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(dateText);
                        HtmlNode agendaNode = dateNode == null ?
                            recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                            .Where(t => !t.Attributes["href"].Value.Contains("html"))
                            .FirstOrDefault(t => t.Attributes["href"].Value
                            .ToLower().Contains("/agenda/")) :
                            dateNode.ParentNode;
                        string agendaUrl = agendaNode.Attributes["href"].Value;
                        agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                        HtmlNode minuteNode = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                            .FirstOrDefault(t => t.Attributes["href"].Value.ToLower().Contains("minutes"));
                        string minuteUrl = minuteNode == null ? string.Empty : minuteNode.Attributes["href"].Value;
                        List<string> fileUrls = new List<string>();
                        fileUrls.Add(agendaUrl);
                        if (!string.IsNullOrEmpty(minuteUrl))
                        {
                            minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                            fileUrls.Add(minuteUrl);
                        }

                        foreach (string fileUrl in fileUrls)
                        {
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);
                            string tag = fileUrl.ToLower().Contains("minute") ? "minute" : "agenda";

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.Checked = false;
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.DocSource = fileUrl;
                                localdoc.DocType = category;
                                string localFileName = string.Format("{0}\\{1}_{2}_{3}.pdf",
                                    this.localDirectory,
                                    category,
                                    meetingDate.ToString("yyyy-MM-dd"),
                                    tag);
                                try
                                {
                                    c.DownloadFile(fileUrl, localFileName);
                                }
                                catch
                                {
                                }

                                localdoc.DocLocalPath = localFileName;
                                docs.Add(localdoc);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("File already downloaded....");
                                Console.ResetColor();
                            }

                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                            if (qr == null)
                            {
                                qr = new QueryResult();
                                qr.CityId = this.cityEntity.CityId;
                                qr.DocId = localdoc.DocId;
                                qr.MeetingDate = meetingDate;
                                qr.SearchTime = DateTime.Now;
                                
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localdoc, ref qr);
                            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("DEBUG EXCEPTION:{0}", ex.ToString());
                        Console.WriteLine("DATA: {0}", recordNode.InnerHtml);
                    }
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
