//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class OshtemoCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public OshtemoCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "OshtemoCharterTownshipMI",
                CityName = "Oshtemo Charter Township",
                CityUrl = "http://www.oshtemo.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OshtemoCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);

                HtmlNodeCollection filesNodes = doc.DocumentNode.SelectNodes(".//a[contains(@href,'uploads')]");

                if (filesNodes != null)
                {
                    foreach (HtmlNode fileNode in filesNodes)
                    {
                        string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();

                        if (string.IsNullOrEmpty(meetingDateText))
                        {
                            continue;
                        }

#if debug
                        bool isMatch = dateReg.IsMatch(fileNode.InnerText);
                        if (isMatch)
                        {
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Not match {0} on {1}...", fileNode.InnerText, categoryUrl);
                            continue;
                        }
#endif

                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        string fileUrl = fileNode.Attributes["href"].Value;
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }

            }
        }
    }
}
