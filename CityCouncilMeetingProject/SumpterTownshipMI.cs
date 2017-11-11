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
    public class SumpterTownshipMI : City
    {
        private List<string> docUrls = null;

        public SumpterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SumpterTownshipMI",
                CityName = "Sumpter",
                CityUrl = "http://sumptertwp.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SumpterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[_]{1,3}[0-9]{1,2}[_]{1,3}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Contains("*") ? url.Split('*')[0] : string.Empty;
                string categoryUrl = url.Split('*').LastOrDefault();

                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//a[starts-with(@href,'uploads')]");

                if (string.IsNullOrEmpty(category))
                {
                    foreach(HtmlNode docNode in docNodes)
                    {
                        string fileUrl = this.cityEntity.CityUrl + docNode.Attributes["href"].Value;
                        string meetingDateText = dateReg.Match(fileUrl).ToString();
                        meetingDateText = meetingDateText.Replace("_", " ");

                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            Console.WriteLine("No meeting date....");
                            continue;
                        }
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        string fileType = string.Empty;

                        if (fileUrl.ToLower().Contains("pdf"))
                        {
                            fileType = "pdf";
                        }
                        else if (fileUrl.ToLower().Contains("docx"))
                        {
                            fileType = "docx";
                        }
                        else if (fileUrl.ToLower().Contains("doc"))
                        {
                            fileType = "doc";
                        }
                        else if (fileUrl.ToLower().Contains("htm"))
                        {
                            fileType = "html";
                        }

                        if (fileUrl.ToLower().Contains("boarding"))
                        {
                            category = "council";
                        }
                        else if (fileUrl.ToLower().Contains("planning"))
                        {
                            category = "Planning";
                        }
                        else if (fileUrl.ToLower().Contains("zoning"))
                        {
                            category = "Zoning";
                        }

                        this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                    }
                }
                else
                {
                    if(docNodes != null)
                    {
                        foreach(HtmlNode docNode in docNodes)
                        {
                            string fileUrl = this.cityEntity.CityUrl + docNode.Attributes["href"].Value;
                            string meetingDateText = dateReg.Match(fileUrl).ToString();
                            meetingDateText = meetingDateText.Replace("_", " ");

                            if (string.IsNullOrEmpty(meetingDateText))
                            {
                                Console.WriteLine("No meeting date....");
                                continue;
                            }
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if(meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string fileType = string.Empty;

                            if (fileUrl.ToLower().Contains("pdf"))
                            {
                                fileType = "pdf";
                            }
                            else if (fileUrl.ToLower().Contains("docx"))
                            {
                                fileType = "docx";
                            }
                            else if (fileUrl.ToLower().Contains("doc"))
                            {
                                fileType = "doc";
                            }
                            else if (fileUrl.ToLower().Contains("htm"))
                            {
                                fileType = "html";
                            }

                            this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
