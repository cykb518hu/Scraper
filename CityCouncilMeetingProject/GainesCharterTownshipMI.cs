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
    public class GainesCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public GainesCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "GainesCharterTownshipMI",
                CityName = "Gaines Charter Township",
                CityUrl = "http://www.gainestownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GainesCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection tableNodes = doc.DocumentNode.SelectNodes("//table");

            for (int i = 1; i < 4; i++)
            {
                string category = string.Empty;
                if (i == 1)
                {
                    category = "Planning Commission";
                }
                else if (i == 2)
                {
                    category = "City Council";
                }
                else if (i == 3)
                {
                    category = "Zoning Board of Appeals";
                }

                HtmlNode contentNode = tableNodes[i];
                HtmlNodeCollection dataNodes = contentNode.SelectNodes(".//tr");

                if (dataNodes != null)
                {
                    foreach (HtmlNode dataNode in dataNodes)
                    {
                        HtmlNodeCollection urlsNodes = dataNode.SelectNodes(".//a[contains(@href,'.pdf')]");

                        if (urlsNodes == null)
                        {
                            continue;
                        }

                        HtmlNode dateNode = dataNode.SelectSingleNode("./td");
                        string meetingDateText = dateReg.Match(dateNode.InnerText).ToString();

#if debug
                        bool isMatch = dateReg.IsMatch(dateNode.InnerText);
                        if (isMatch)
                        {
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Not match {0}...", dateNode.InnerText);
                            continue;
                        }
#endif

                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        foreach (HtmlNode urlNode in urlsNodes)
                        {
                            string docUrl = this.cityEntity.CityUrl + "/" + urlNode.Attributes["href"].Value;
                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }

        }
    }
}
