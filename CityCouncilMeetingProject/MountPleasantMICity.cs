using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class MountPleasantMICity : City
    {
        private List<string> docUrls = null;

        public MountPleasantMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MountPleasantMICity",
                CityName = "Mount Pleasant",
                CityUrl = "http://www.mt-pleasant.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MountPleasantMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{1,2}\\.[0-9]{1,2}\\.[0-9]{2}|[0-9]{1,2}\\.[0-9]{1,2}");
            StringBuilder yearBuilder = new StringBuilder();
            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(i.ToString());
                yearBuilder.Append("|");
            }
            Regex yearReg = new Regex(string.Format("({0})", yearBuilder.ToString().Trim('|')));

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument listDoc = web.Load(categoryUrl);
                var docNodes = listDoc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]")
                    .Where(t => t.InnerText.Contains("Upcoming"));

                if(docNodes != null)
                {
                    foreach(HtmlNode docNode in docNodes)
                    {
                        string fileUrl = docNode.Attributes["href"].Value;
                        fileUrl = !fileUrl.StartsWith("http") ? this.cityEntity.CityUrl + fileUrl.Trim('.') : fileUrl;
                        if (yearReg.IsMatch(fileUrl))
                        {
                            string meetingDateText = dateReg.Match(fileUrl).ToString();
                            Console.WriteLine("DEBUG:{0},{1}...", fileUrl, meetingDateText);
                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M.d.yy", null);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }

                var minutePageNode = listDoc.DocumentNode.SelectSingleNode("//a[text()='Meeting minutes']");

                if(minutePageNode != null)
                {
                    string minuteUrl = minutePageNode.Attributes["href"].Value;
                    minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), minuteUrl);
                    HtmlDocument minuteDoc = web.Load(minuteUrl);
                    HtmlNodeCollection minuteNodes = minuteDoc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");

                    if(minuteNodes!= null)
                    {
                        foreach(HtmlNode minuteNode in minuteNodes)
                        {
                            string fileUrl = minuteNode.Attributes["href"].Value;
                            fileUrl = !fileUrl.StartsWith("http") ? this.cityEntity.CityUrl + fileUrl.Trim('.') : fileUrl;
                            if (yearReg.IsMatch(fileUrl))
                            {
                                string meetingDateText = dateReg.Match(fileUrl).ToString();

                                if (meetingDateText.Split('.').Length == 2)
                                {
                                    string year = yearReg.Match(fileUrl).ToString();
                                    meetingDateText = string.Format("{0}.{1}", meetingDateText, year.Substring(2));
                                }
                                Console.WriteLine("DEBUG:{0},{1}...", fileUrl, meetingDateText);
                                DateTime meetingDate = string.IsNullOrEmpty(meetingDateText) ? DateTime.MinValue : DateTime.ParseExact(meetingDateText, "M.d.yy", null);
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
