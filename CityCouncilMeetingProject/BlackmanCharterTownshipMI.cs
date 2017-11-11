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
    public class BlackmanCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public BlackmanCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BlackmanCharterTownshipMI",
                CityName = "Blackman",
                CityUrl = "http://www.blackmantwp.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BlackmanCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{1,2}[-\\s]{1,2}[0-9]{1,2}[-\\s]{1,2}[0-9]{4}");
            
            foreach(string url in this.docUrls)
            {
                string categoryUrl = url.Split('*')[1];
                string category = url.Split('*')[0];

                for(int i = this.dtStartFrom.Year;i <= DateTime.Now.Year; i++)
                {
                    categoryUrl = string.Format(categoryUrl, i);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection docNodeList = doc.DocumentNode.SelectNodes("//a[@class='doc']");

                    if(docNodeList != null)
                    {
                        foreach(HtmlNode docNode in docNodeList)
                        {
                            if (!docNode.InnerText.Contains(i.ToString()) || docNode.InnerText.Contains("Park"))
                            {
                                Console.WriteLine("Not this year or not council meeting...");
                                continue;
                            }

                            string docUrl = "http://ecode360.com" + docNode.Attributes["href"].Value;
                            string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "MM dd yyyy", null);

                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Early...");
                                continue;
                            }

                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
