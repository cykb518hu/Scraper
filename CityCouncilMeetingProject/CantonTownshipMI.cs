using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class CantonTownshipMI : City
    {
        private List<string> docUrls = null;

        public CantonTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "CantonTownshipMI",
                CityName = "Canton Township",
                CityUrl = "https://www.canton-mi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("CantonTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<string> yearUrls = new List<string>();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    yearUrls.Add(string.Format(url, i));
                }
            }

            foreach (string yearUrl in yearUrls)
            {
                string category = yearUrl.Split('*')[0];
                string url = yearUrl.Split('*')[1];
                HtmlDocument doc = new HtmlDocument();//web.Load(url);

                try
                {
                    string html = this.GetHtml(url, string.Empty);
                    doc.LoadHtml(html);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("DEBUG:\r\n");
                    Console.WriteLine("URL:{0}...", url);
                    Console.WriteLine("EXCEPTION:{0}...", ex.ToString());
                    Console.ResetColor();
                }

                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//tr[@class='catAgendaRow']");

                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        HtmlNode dateNode = entryNode.SelectSingleNode(".//h4");
                        string dateText = dateNode == null
                            ? string.Empty :
                            dateReg.Match(dateNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(dateText);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[contains(@href,'Minutes')]");
                        string minuteUrl = minuteNode == null ? string.Empty : this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                        HtmlNode agendaNode = entryNode.SelectSingleNode(".//ol//a[contains(@href,'Agenda')]");
                        string agendaUrl = agendaNode == null ? string.Empty : this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                        HtmlNode agendaPacketNode = entryNode.SelectSingleNode(".//a[contains(@href,'packet')]");
                        string agendaPacketUrl = agendaPacketNode == null ? string.Empty : this.cityEntity.CityUrl + agendaPacketNode.Attributes["href"].Value;

                        string fileType = string.Empty;

                        if (!string.IsNullOrEmpty(minuteUrl))
                        {
                            fileType = minuteNode.OuterHtml.Contains("html") ? "docx" : "pdf";
                            ExtractPdf(c, meetingDate, minuteUrl, category, fileType, ref docs, ref queries);
                        }
                        if (!string.IsNullOrEmpty(agendaPacketUrl))
                        {
                            fileType = agendaPacketNode.OuterHtml.Contains("html") ? "docx" : "pdf";
                            ExtractPdf(c, meetingDate, agendaPacketUrl, category, fileType, ref docs, ref queries);
                        }
                        if (!string.IsNullOrEmpty(agendaUrl))
                        {
                            fileType = agendaNode.OuterHtml.Contains("html") ? "docx" : "pdf";
                            ExtractPdf(c, meetingDate, agendaUrl, category, fileType, ref docs, ref queries);
                        }
                    }
                }
            }
        }

        public void ExtractPdf(WebClient c, DateTime meetingDate, string url, string category, string fileType, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == url);

            if (localdoc == null)
            {
                localdoc = new Documents();
                localdoc.DocSource = url;
                localdoc.DocId = Guid.NewGuid().ToString();
                localdoc.CityId = this.cityEntity.CityId;
                localdoc.DocType = category;
                localdoc.Important = false;
                localdoc.Checked = false;
                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.{3}",
                    this.localDirectory,
                    category,
                    Guid.NewGuid().ToString(),
                    fileType); 

                try
                {
                    c.Headers.Add("user-agent", "chrome");
                    c.DownloadFile(url, localdoc.DocLocalPath);
                }
                catch (Exception ex)
                { }

                docs.Add(localdoc);
            }
            else
            {
                Console.WriteLine("This file already downloaded...");
            }

            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

            if (qr == null)
            {
                qr = new QueryResult();
                qr.CityId = localdoc.CityId;
                qr.DocId = localdoc.DocId;
                qr.SearchTime = DateTime.Now;
                qr.MeetingDate = meetingDate;
                queries.Add(qr);
            }

            this.ExtractQueriesFromDoc(localdoc, ref qr);
            Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
            this.SaveMeetingResultsToSQL(docs, queries);
        }
    }
}
