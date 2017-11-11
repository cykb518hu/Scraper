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
    public class CommerceCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public CommerceCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "CommerceCharterTownshipMI",
                CityName = "Commerce Charter Township",
                CityUrl = "http://www.commercetwp.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("CommerceCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], year);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();

                            if (string.IsNullOrEmpty(meetingDateText))
                            {
                                continue;
                            }
#if debug
                            bool isMatch = dateReg.IsMatch(entryNode.InnerText);
                            if (isMatch)
                            {
                                Console.WriteLine("No problem, continue");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Not match...");
                                continue;
                            }
#endif
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string entryUrl = entryNode.Attributes["href"].Value;
                            this.ExtractADoc(c, entryUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
