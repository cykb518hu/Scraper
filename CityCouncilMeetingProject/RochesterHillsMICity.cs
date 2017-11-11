using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace CityCouncilMeetingProject
{
    public class RochesterHillsMICity : City
    {
        private string docUrl = null;

        public RochesterHillsMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RochesterHillsMICity",
                CityName = "RochesterHills",
                CityUrl = "http://www.rochesterhills.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrl = File.ReadLines("RochesterHillsMICity_Urls.txt").FirstOrDefault();
        }

        public void DownloadCouncilPdfFiles()
        {
            string[] categories = { "City Council", "Planning Commission", "Zoning Board of Appeals" };
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            ChromeDriver cd = new ChromeDriver();
            cd.Navigate().GoToUrl(this.docUrl);
            System.Threading.Thread.Sleep(10000);

            foreach (string category in categories)
            {
                List<int> years = new List<int>();
                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    years.Add(i);
                }

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(cd.PageSource);

                List<string> tagsToSearch = doc.DocumentNode.SelectNodes(string.Format("//li[starts-with(text(),'{0}')]", category))
                    .Select(t => t.InnerText).ToList();

                foreach (string tag in tagsToSearch)
                {
                    Console.WriteLine("Working on [{0}]...", tag);
                    foreach (int year in years)
                    {
                        IWebElement tagLabelEle = cd.FindElementById("ctl00_ContentPlaceHolder1_lstName_Input");
                        tagLabelEle.Click();
                        System.Threading.Thread.Sleep(1000);
                        IWebElement tagEle = cd.FindElementByXPath(string.Format("//li[text()='{0}']", tag));
                        tagEle.Click();
                        System.Threading.Thread.Sleep(2000);
                        IWebElement yearLabelEle = cd.FindElementByXPath("//*[contains(text(),'Date: ')]");
                        try
                        {
                            yearLabelEle.Click();
                        }
                        catch
                        {

                        }

                        System.Threading.Thread.Sleep(1000);
                        IWebElement yearEle = cd.FindElementByXPath(string.Format("//span[text()='{0}']", year));
                        yearEle.Click();
                        System.Threading.Thread.Sleep(2000);

                        doc.LoadHtml(cd.PageSource);
                        HtmlNodeCollection docCollection = doc.DocumentNode.SelectNodes("//table[@id='ctl00_ContentPlaceHolder1_gridCalendar_ctl00']/tbody/tr");

                        if (docCollection != null && docCollection.Count > 0)
                        {
                            foreach (HtmlNode docNode in docCollection)
                            {
                                string meetingDateText = docNode.SelectSingleNode("./td").InnerText;

                                if (meetingDateText == "No records to display.")
                                {
                                    continue;
                                }

                                DateTime meetingDate = DateTime.Parse(meetingDateText);
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }
                                HtmlNode agendaNode = docNode.SelectSingleNode(".//a[text()='Agenda']");
                                HtmlNode minuteNode = docNode.SelectSingleNode(".//a[text()='Minutes']");
                                Dictionary<string, string> docUrlDic = new Dictionary<string, string>();

                                if (agendaNode != null)
                                {
                                    string agendaUrl = agendaNode.Attributes["href"].Value;
                                    agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : "http://roch.legistar.com/" + agendaUrl;
                                    string fileType = string.Empty;
                                    string tagText = agendaNode.PreviousSibling.PreviousSibling.Attributes["src"].Value;
                                    if (tagText.Contains("HTML"))
                                    {
                                        fileType = "html";
                                    }
                                    else if (tagText.Contains("PDF"))
                                    {
                                        fileType = "pdf";
                                    }
                                    docUrlDic.Add("Agenda_" + fileType, agendaUrl.Replace("&amp;", "&"));
                                }

                                if (minuteNode != null)
                                {
                                    string minuteUrl = minuteNode.Attributes["href"].Value;
                                    minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : "http://roch.legistar.com/" + minuteUrl;
                                    string fileType = string.Empty;
                                    string tagText = minuteNode.PreviousSibling.PreviousSibling.Attributes["src"].Value;
                                    if (tagText.Contains("HTML"))
                                    {
                                        fileType = "html";
                                    }
                                    else if (tagText.Contains("PDF"))
                                    {
                                        fileType = "pdf";
                                    }
                                    docUrlDic.Add("Minute_" + fileType, minuteUrl.Replace("&amp;", "&"));
                                }

                                foreach (string key in docUrlDic.Keys)
                                {
                                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrlDic[key]);
                                    if (localdoc == null)
                                    {
                                        localdoc = new Documents();
                                        localdoc.DocId = Guid.NewGuid().ToString();
                                        localdoc.CityId = this.cityEntity.CityId;
                                        localdoc.Important = false;
                                        localdoc.Checked = false;
                                        localdoc.DocType = category;
                                        localdoc.DocSource = docUrlDic[key];

                                        if (key.Contains("html"))
                                        {
                                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.html", localDirectory, key, docUrlDic[key].Split('=').LastOrDefault());

                                            try
                                            {
                                                c.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");
                                                string html = c.DownloadString(docUrlDic[key]);
                                                File.WriteAllText(localdoc.DocLocalPath, html);
                                                HtmlDocument agendaDoc = new HtmlDocument();
                                                agendaDoc.LoadHtml(html);
                                                localdoc.DocBodyDic.Add(1, agendaDoc.DocumentNode.InnerText);
                                            }
                                            catch (Exception ex)
                                            {

                                            }
                                        }
                                        else
                                        {
                                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf", localDirectory, key, docUrlDic[key].Split('=').LastOrDefault());

                                            try
                                            {
                                                c.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");
                                                c.DownloadFile(docUrlDic[key], localdoc.DocLocalPath);
                                            }
                                            catch (Exception ex)
                                            {
                                            }

                                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                                        }

                                        docs.Add(localdoc);
                                    }
                                    else
                                    {
                                        Console.WriteLine("This file already downloaded....");

                                        if (localdoc.DocLocalPath.ToLower().Contains("pdf"))
                                        {
                                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                                        }
                                        else
                                        {
                                            string html = File.ReadAllText(localdoc.DocLocalPath);
                                            HtmlDocument pageContent = new HtmlDocument();
                                            pageContent.LoadHtml(html);
                                            localdoc.DocBodyDic.Add(1, pageContent.DocumentNode.InnerText);
                                        }
                                    }

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
                }
            }

            cd.Quit();
            cd = null;
        }
    }
}
