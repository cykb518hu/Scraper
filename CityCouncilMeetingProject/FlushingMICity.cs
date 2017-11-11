using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    public class FlushingMICity : City
    {
        private List<string> docUrls = null;

        public FlushingMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "FlushingMICity",
                CityName = "Flushing",
                CityUrl = "http://www.flushingcity.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("FlushingMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            ChromeDriver cd = new ChromeDriver();
            cd.Navigate().GoToUrl(this.docUrls[0]);
            System.Threading.Thread.Sleep(3000);
            try
            {
                string pageHtml = cd.PageSource;
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);
                var containerEles = cd.FindElementsByXPath("//div[@id='dnn_RowNine_Grid3_Pane2']/div");

                if (containerEles != null)
                {
                    for (int i = 0; i < containerEles.Count; i++)
                    {
                        var yearContainer = containerEles[i];
                        var datesEle = yearContainer.FindElement(By.XPath(".//select"));
                        SelectElement dateSelect = new SelectElement(datesEle);
                        string id = datesEle.GetAttribute("id");
                        HtmlNode currentNode = doc.GetElementbyId(id);
                        HtmlNodeCollection dateNodes = currentNode.SelectNodes("./option");
                        foreach (HtmlNode childNode in dateNodes)
                        {
                            dateSelect.SelectByText(childNode.InnerText);
                            string meetingDateText = dateReg.Match(childNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                break;
                            }

                            var goButton = yearContainer.FindElement(By.XPath(".//*[text()='Go']"));
                            goButton.Click();
                            System.Threading.Thread.Sleep(2000);
                            string fileUrl = cd.Url;
                            string category = yearContainer.FindElement(By.Id("dnn_ctr14060_dnnTITLE4_titleLabel")).Text;
                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);

                            cd.Navigate().GoToUrl(this.docUrls[0]);
                            System.Threading.Thread.Sleep(3000);
                            containerEles = cd.FindElementsByXPath("//div[@id='dnn_RowNine_Grid3_Pane2']/div");
                            yearContainer = containerEles[i];
                            datesEle = yearContainer.FindElement(By.XPath(".//select"));
                            dateSelect = new SelectElement(datesEle);
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Not found such element [dnn_RowNine_Grid3_Pane2]...");
            }

        }
    }
}
