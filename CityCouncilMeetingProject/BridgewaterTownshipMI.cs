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
    public class BridgewaterTownshipMI : City
    {
        private List<string> docUrls = null;

        public BridgewaterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BridgewaterTownshipMI",
                CityName = "Bridgewater",
                CityUrl = "http://twp-bridgewater.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BridgewaterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}");
            List<string> years = new List<string>();
            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                years.Add(i.ToString());
            }

            string yearRegText = string.Format("({0})", string.Join("|", years));
            Regex yearReg = new Regex(yearRegText);

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");

                if(fileNodes != null)
                {
                    foreach(HtmlNode fileNode in fileNodes)
                    {
                        string fileUrl = fileNode.Attributes["href"].Value;
                        fileUrl = fileUrl.StartsWith("http") ? fileUrl : this.cityEntity.CityUrl + fileUrl;
                        string nodeText = System.Web.HttpUtility.HtmlDecode(fileNode.InnerText).Trim('\r', '\n', '\t', (char)32, (char)160);
                        if (string.IsNullOrEmpty(nodeText))
                        {
                            continue;
                        }

                        string meetingDateText = dateReg.Match(nodeText).ToString();
                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            if(yearReg.IsMatch(fileNode.ParentNode.InnerText))
                            {
                                dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
                                meetingDateText = dateReg.Match(fileNode.ParentNode.InnerText).ToString();
                            }
                        }

                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            continue;
                        }
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
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
