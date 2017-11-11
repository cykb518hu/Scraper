using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class FerndaleMICity : City
    {
        private List<string> docUrls = null;

        public FerndaleMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "FerndaleMICity",
                CityName = "Ferndale MI City",
                CityUrl = "http://www.ferndalemi.gov/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("FerndaleMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("([0-9]{1,2}[\\s]{0,2}[a-zA-Z]{1,}[\\s]{0,2}[0-9]{4}|[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}[\\s]{0,2}[0-9]{4}|[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4})");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();

            foreach (string url in this.docUrls)
            {
                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = url.Split('*')[1];
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection docNodeList = doc.DocumentNode.SelectNodes("//a[@class='document-link']");

                    if (docNodeList != null)
                    {
                        Regex anotherDateReg = new Regex("[0-9]{4}[\\s]{0,2}[0-9]{1,2}[\\s]{0,2}[0-9]{1,2}");
                        foreach (HtmlNode docNode in docNodeList)
                        {
                            string fileUrl =docNode.Attributes["href"].Value;
                            fileUrl = fileUrl.StartsWith("http") ? fileUrl : "https://ferndalemi.civicweb.net" + fileUrl;
                            string meetingDateText = anotherDateReg.Match(docNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.MinValue;
                            try
                            {
                                meetingDate= DateTime.Parse(meetingDateText);
                            }
                            catch
                            {
                                Console.WriteLine("Will parse meeting date again before saving...");
                            }

                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }

                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }

                    HtmlNode folderNode = doc.DocumentNode.SelectSingleNode(string.Format("//a[contains(text(),'{0}')]", i));

                    if (folderNode != null)
                    {
                        string folderUrl = folderNode.Attributes["href"].Value;
                        folderUrl = folderUrl.StartsWith("http") ? folderUrl : "https://ferndalemi.civicweb.net" + folderUrl;
                        doc = web.Load(folderUrl);
                        docNodeList = doc.DocumentNode.SelectNodes("//a[contains(text(),' Pdf')]");

                        if (docNodeList != null)
                        {
                            foreach (HtmlNode docNode in docNodeList)
                            {
                                string fileUrl = docNode.Attributes["href"].Value;
                                fileUrl = fileUrl.StartsWith("http") ? fileUrl : "https://ferndalemi.civicweb.net" + fileUrl;
                                string meetingDateText = dateReg.Match(docNode.InnerText).ToString();

                                if (string.IsNullOrEmpty(meetingDateText))
                                {
                                    Regex dayReg = new Regex("[0-9]{1,2}[\\s]{0,2}[a-zA-Z]+");
                                    string dayText = dayReg.Match(docNode.InnerText).ToString();
                                    if(string.IsNullOrEmpty(dayText) == false)
                                    {
                                        meetingDateText = dayText + " " + i;
                                    }
                                }

                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("DEBUG:{0}", meetingDateText);
                                Console.WriteLine("CATEGORY URL:{0}", categoryUrl);
                                Console.WriteLine("Folder URL:{0}", folderUrl);
                                Console.ResetColor();

                                DateTime meetingDate = DateTime.Parse(meetingDateText);
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
