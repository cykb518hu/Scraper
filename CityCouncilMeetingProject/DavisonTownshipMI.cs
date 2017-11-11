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
    public class DavisonTownshipMI : City
    {
        private List<string> docUrls = null;

        public DavisonTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "DavisonTownshipMI",
                CityName = "DavisonTownship",
                CityUrl = "http://www.davisontwp-mi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("DavisonTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(this.docUrls[0]);
            Regex dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{1,2}");
            HtmlNode councilNode = doc.GetElementbyId("u451");
            HtmlNode planningCommissionNode = doc.GetElementbyId("u463");
            HtmlNode zoningBoardOfAppealsNode = doc.GetElementbyId("u444");

            string category = string.Empty;
            HtmlNodeCollection entryNodes = null;
            if (councilNode != null)
            {
                category = "City Council";
                entryNodes = councilNode.SelectNodes(".//a[contains(@href,'assets')]");

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
                        try
                        {
                            DateTime.ParseExact(meetingDateText, "M-d-yy", null);
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        catch
                        {
                            Console.WriteLine("Not match {0}...", meetingDateText);
                            continue;
                        }
#endif

                        DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M-d-yy", null);

                        if (meetingDate < this.dtStartFrom)
                        {
                            continue;
                        }

                        string fileUrl = this.cityEntity.CityUrl + "/" + entryNode.Attributes["href"].Value;
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            if (planningCommissionNode != null)
            {
                category = "Planning Commission";
                entryNodes = planningCommissionNode.SelectNodes(".//a[contains(@href,'assets')]");

                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        string meetingDateText = dateReg.Match(entryNode.InnerText.Split(' ').FirstOrDefault()).ToString();
#if debug
                        try
                        {
                            DateTime.ParseExact(meetingDateText, "M-d-yy", null);
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        catch
                        {
                            Console.WriteLine("Not match {0}...", meetingDateText);
                            continue;
                        }
#endif
                        DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M-d-yy", null);

                        if (meetingDate < this.dtStartFrom)
                        {
                            continue;
                        }

                        string fileUrl = this.cityEntity.CityUrl + "/" + entryNode.Attributes["href"].Value;
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            if (zoningBoardOfAppealsNode != null)
            {
                category = "Zoning Board of Appeals";
                entryNodes = zoningBoardOfAppealsNode.SelectNodes(".//a[contains(@href,'assets')]");

                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        string meetingDateText = dateReg.Match(entryNode.InnerText.Split(' ').FirstOrDefault()).ToString();
#if debug
                        try
                        {
                            DateTime.ParseExact(meetingDateText, "M-d-yy", null);
                            Console.WriteLine("No problem, continue");
                            continue;
                        }
                        catch
                        {
                            Console.WriteLine("Not match {0}...", meetingDateText);
                            continue;
                        }
#endif

                        DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M-d-yy", null);

                        if (meetingDate < this.dtStartFrom)
                        {
                            continue;
                        }

                        string fileUrl = this.cityEntity.CityUrl + "/" + entryNode.Attributes["href"].Value;
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
