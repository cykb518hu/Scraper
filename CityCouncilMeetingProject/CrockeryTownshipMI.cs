using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    public class CrockeryTownshipMI : City
    {
        private List<string> docUrls = null;

        public CrockeryTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "CrockeryTownshipMI",
                CityName = "Crockery",
                CityUrl = "http://www.crockery-township.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("CrockeryTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                HtmlDocument meetingHomeDoc = web.Load(url);
                HtmlNodeCollection fileNodes = meetingHomeDoc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");

                for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
                {
                    var targetNodes = fileNodes.Where(t => t.OuterHtml.Contains(year.ToString()));

                    if(targetNodes != null)
                    {
                        foreach(HtmlNode fileNode in targetNodes)
                        {
                            string nodeUrl = fileNode.Attributes["href"].Value;
                            nodeUrl = !nodeUrl.StartsWith("http") ? this.cityEntity.CityUrl + nodeUrl : nodeUrl;
                            DateTime meetingDate = DateTime.MinValue;
                            string meetingDateText = dateReg.Match(nodeUrl).ToString();

                            if (!string.IsNullOrEmpty(meetingDateText))
                            {
                                meetingDate = DateTime.Parse(meetingDateText);
                            }
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string category = nodeUrl.Contains("PC") || nodeUrl.Contains("Plan") ? "Planning" : "City Council";
                            this.ExtractADoc(c, nodeUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
