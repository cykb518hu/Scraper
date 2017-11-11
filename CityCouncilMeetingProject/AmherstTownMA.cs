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
    public class AmherstTownMA : City
    {
        private List<string> docUrls = null;

        public AmherstTownMA()
        {
            cityEntity = new CityInfo()
            {
                CityId = "AmherstTownMA",
                CityName = "Amherst",
                CityUrl = "https://www.amherstma.gov/",
                StateCode = "MA"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("AmherstTownMA_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            string targetUrl = string.Format(this.docUrls[0],
               this.dtStartFrom.Month.ToString().PadLeft(2, '0'),
               this.dtStartFrom.Day.ToString().PadLeft(2, '0'),
               this.dtStartFrom.Year,
               DateTime.Now.Month.ToString().PadLeft(2, '0'),
               DateTime.Now.Day.ToString().PadLeft(2, '0'),
               DateTime.Now.Year);
            ChromeDriver cd = new ChromeDriver();
            cd.Navigate().GoToUrl(targetUrl);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(cd.PageSource);
            cd.Quit();
            cd = null;
            HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//a[starts-with(@href,'Archive.aspx?ADID=')]");
            Console.WriteLine("In total {0} docs to download...", fileNodes.Count);
            List<HtmlNode> targetNodes = fileNodes.Where(t => t.InnerText.ToLower().Contains("minute")
                || t.InnerText.ToLower().Contains("agenda")).ToList();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("(0|1)[0-9]{1}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}[,]{0,1}[\\s]{0,2}[0-9]{4}"), string.Empty);
            dateRegFormatDic.Add(new Regex("(0|1)[0-9]{1}-[0-9]{1,2}-[0-9]{4}"), string.Empty);
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}(\\/|\\.)[0-9]{1,2}(\\/|\\.)[0-9]{4}"), string.Empty);
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}"), string.Empty);
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{1,2}"), string.Empty);
            dateRegFormatDic.Add(new Regex("(0|1)[0-9]{1}\\/[0-9]{2}\\/[0-9]{2}"), "MM/dd/yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{2}"), "M/d/yy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}(st|rd|nd|th),[\\s]{1}[0-9]{4}"), "st|rd|nd|th");
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}\\.[0-9]{1,2}\\.[0-9]{2}"), "M.d.yy");
            
            foreach(HtmlNode targetNode in targetNodes)
            {
                var parentNode = targetNode.ParentNode;
                var containerNode = parentNode == null ? null : parentNode.ParentNode;
                var categoryNode = containerNode == null ? null : containerNode.PreviousSibling.PreviousSibling;
                string category = categoryNode == null ? string.Empty :  categoryNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160);
                string docType = parentNode.Attributes["style"].Value.Contains("iconword") ? "doc" : "pdf";
                string fileUrl = this.cityEntity.CityUrl + targetNode.Attributes["href"].Value;
                DateTime meetingDate = DateTime.MinValue;

                foreach(var dateRegKey in dateRegFormatDic.Keys)
                {
                    string format = dateRegFormatDic[dateRegKey];
                    if (dateRegKey.IsMatch(targetNode.InnerText))
                    {
                        Console.WriteLine("Match date successfully...");
                        string meetingDateText = dateRegKey.Match(targetNode.InnerText).ToString();

                        if (string.IsNullOrEmpty(format))
                        {
                            meetingDate = DateTime.Parse(meetingDateText);
                        }
                        else if(format == "st|rd|nd|th")
                        {
                            meetingDateText = meetingDateText.Replace("st", string.Empty).Replace("rd", string.Empty).Replace("nd", string.Empty).Replace("th", string.Empty);
                            meetingDate = DateTime.Parse(meetingDateText);
                        }
                        else
                        {
                            meetingDate = DateTime.ParseExact(meetingDateText, format, null);
                        }

                        break;
                    }
                }

                if(meetingDate != DateTime.MinValue && meetingDate < this.dtStartFrom)
                {
                    Console.WriteLine("Too early");
                    continue;
                }

                this.ExtractADoc(c, fileUrl, category, docType, meetingDate, ref docs, ref queries);
            }
        }
    }
}
