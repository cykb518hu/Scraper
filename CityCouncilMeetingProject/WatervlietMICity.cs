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
    public class WatervlietMICity : City
    {
        private List<string> docUrls = null;

        public WatervlietMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WatervlietMICity",
                CityName = "Watervliet",
                CityUrl = "http://www.watervliet.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WatervlietMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            Regex dateReg1 = new Regex("[a-zA-Z\\.]+[\\s]{0,2}[0-9]{1,2}[,]{0,1}[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[@summary='Archive Details']");

                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("DEBUG:{0}", entryNode.InnerText);
                        Console.ResetColor();
                        string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                        meetingDateText = string.IsNullOrEmpty(meetingDateText) ?
                            dateReg1.Match(entryNode.InnerText).ToString() :
                            meetingDateText;
                        meetingDateText = meetingDateText.Replace(".", string.Empty).Replace("Sept", "Sep");

                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                 
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        HtmlNode docTypeNode = entryNode.SelectSingleNode(".//img[@src]");
                        string docTypeIndicator = docTypeNode.Attributes["src"].Value.ToLower();
                        string docType = string.Empty;
                        if (docTypeIndicator.Contains("pdf"))
                        {
                            docType = "pdf";
                        }
                        else if (docTypeIndicator.Contains("word"))
                        {
                            docType = "doc";
                        }

                        if (!string.IsNullOrEmpty(docType))
                        {
                            HtmlNode docNode = entryNode.SelectSingleNode(".//a[@href]");
                            string entryUrl = docNode == null ? string.Empty :
                                this.cityEntity.CityUrl + "/" + docNode.Attributes["href"].Value.Trim('/');

                            if (!string.IsNullOrEmpty(entryUrl))
                            {
                                this.ExtractADoc(c, entryUrl, category, docType, meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
