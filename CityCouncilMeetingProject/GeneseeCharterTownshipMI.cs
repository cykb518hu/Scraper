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
    public class GeneseeCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public GeneseeCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "GeneseeCharterTownshipMI",
                CityName = "Genesee Charter Township",
                CityUrl = "http://www.geneseetwp.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GeneseeCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("([a-zA-Z]+[\\s]{0,2}[0-9]{1,2},|[a-zA-Z]+[\\s]{0,2}[0-9]{1,2})[\\s]{0,2}[0-9]{4}");
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection entriesNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/wp-content/')]");

            if(entriesNodes != null)
            {
                foreach(HtmlNode entryNode in entriesNodes)
                {
                    string docUrl = entryNode.Attributes["href"].Value;
                    string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();

#if debug
                    //bool isMatch = dateReg.IsMatch(entryNode.InnerText);
                    //if (isMatch)
                    //{
                    //    Console.WriteLine("No problem, continue");
                    //    continue;
                    //}
                    //else
                    //{
                    //    Console.WriteLine("Not match {0}...", entryNode.InnerText);
                    //    continue;
                    //}

                    try
                    {
                        DateTime.Parse(meetingDateText);
                        Console.WriteLine("Working....");
                        continue;
                    }
                    catch
                    {
                        Console.WriteLine("Not working {0}...", meetingDateText);
                        continue;
                    }
#endif

                    DateTime meetingDate = DateTime.Parse(meetingDateText);

                    if(meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip...");
                        break;
                    }

                    string category = "City Council";
                    this.ExtractADoc(c, docUrl, category, "docx", meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
