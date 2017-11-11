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
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    /// <summary>
    /// http://a2gov.legistar.com/Calendar.aspx
    /// </summary>
    public class AnnArborMICity : City
    {
        private string docUrl = string.Empty;

        public AnnArborMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "AnnArborMICity",
                CityName = "AnnArbor",
                CityUrl = "http://a2gov.legistar.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrl = File.ReadLines("AnnArborMICity_Urls.txt").FirstOrDefault();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();

            ChromeDriver cd = new ChromeDriver();
            cd.Navigate().GoToUrl(this.docUrl);
            System.Threading.Thread.Sleep(7);
            string[] categories = { "City Council", "Zoning Board of Appeals", "Planning Commission, City" };

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                Console.WriteLine("Working on year {0}...", i);
                IWebElement yearsInputEle = cd.FindElementByName("ctl00$ContentPlaceHolder1$lstYears");
                yearsInputEle.Click();
                System.Threading.Thread.Sleep(1000);
                IWebElement yearEle = cd.FindElementByXPath(string.Format("//div[@id='ctl00_ContentPlaceHolder1_lstYears_DropDown']//ul/li[text()='{0}']", i));
                yearEle.Click();
                System.Threading.Thread.Sleep(5000);

                foreach(string category in categories)
                {
                    Console.WriteLine("Working on category {0}...", category);
                    IWebElement categoryInputEle = cd.FindElementByName("ctl00$ContentPlaceHolder1$lstBodies");
                    categoryInputEle.Click();
                    System.Threading.Thread.Sleep(1000);
                    IWebElement categoryEle = cd.FindElementByXPath(string.Format("//div[@id='ctl00_ContentPlaceHolder1_lstBodies_DropDown']//ul//li[text()='{0}']", category));
                    categoryEle.Click();
                    System.Threading.Thread.Sleep(5000);

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(cd.PageSource);
                    HtmlNodeCollection recordNodes = doc.DocumentNode.SelectNodes("//table[@class='rgMasterTable']/tbody/tr");

                    foreach(HtmlNode recordNode in recordNodes)
                    {
                        HtmlNode dateNode = recordNode.SelectSingleNode("./td[@class='rgSorted']");
                        DateTime meetingDate = DateTime.Parse(dateNode.InnerText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[text()='Agenda']");
                        HtmlNode minuteNode = recordNode.SelectSingleNode(".//a[text()='Minutes']");

                        List<string> fileUrls = new List<string>();

                        if(agendaNode != null)
                        {
                            fileUrls.Add(string.Format("{0}/{1}", this.cityEntity.CityUrl, agendaNode.Attributes["href"].Value.TrimStart('.').Replace("&amp;", "&")));
                        }

                        if(minuteNode != null)
                        {
                            fileUrls.Add(string.Format("{0}/{1}", this.cityEntity.CityUrl, minuteNode.Attributes["href"].Value.TrimStart('.').Replace("&amp;", "&")));
                        }

                        foreach(string fileUrl in fileUrls)
                        {
                            Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                            if(localDoc == null)
                            {
                                localDoc = new Documents();
                                localDoc.DocId = Guid.NewGuid().ToString();
                                localDoc.CityId = this.cityEntity.CityId;
                                localDoc.Checked = false;
                                localDoc.DocType = category;
                                localDoc.DocSource = fileUrl;

                                string localPath = null;

                                if (fileUrl.Contains("M=A"))
                                {
                                    localPath = string.Format("{0}\\{1}_{2}_{3}_{4}.pdf", this.localDirectory, category.Replace(",", string.Empty), "Agenda", meetingDate.ToString("yyyy-MM-dd"), Guid.NewGuid().ToString());
                                }
                                else if (fileUrl.Contains("M=M"))
                                {
                                    localPath = string.Format("{0}\\{1}_{2}_{3}_{4}.pdf", this.localDirectory, category.Replace(",", string.Empty), "Minutes", meetingDate.ToString("yyyy-MM-dd"), Guid.NewGuid().ToString());
                                }

                                try
                                {
                                    c.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");
                                    c.DownloadFile(fileUrl, localPath);
                                }
                                catch (Exception ex)
                                {
                                }

                                localDoc.DocLocalPath = localPath;
                                docs.Add(localDoc);
                            }
                            else
                            {
                                Console.WriteLine("This document is already downloaded...");
                            }

                            this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                            if(qr == null)
                            {
                                qr = new QueryResult();
                                qr.DocId = localDoc.DocId;
                                qr.MeetingDate = meetingDate;
                                qr.SearchTime = DateTime.Now;
                                qr.CityId = localDoc.CityId;
                                
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localDoc, ref qr);
                            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                        }
                    }

                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }

            cd.Quit();
        }
    }
}
