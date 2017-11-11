using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class WaterfordCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public WaterfordCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WaterfordCharterTownshipMI",
                CityName = "Waterford Charter Township",
                CityUrl = "https://waterfordmi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WaterfordCharterTownshipMI_Urls.txt").ToList();
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
                HtmlDocument doc = new HtmlDocument(); //web.Load(url);

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
                        HtmlNode agendaNode = entryNode.SelectSingleNode(".//a[contains(@href,'html')]");
                        agendaNode = agendaNode == null ?
                            entryNode.SelectSingleNode(".//a[contains(@href,'Agenda')]") :
                            agendaNode;
                        string agendaUrl = agendaNode == null ? string.Empty : this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                        HtmlNode agendaPacketNode = entryNode.SelectSingleNode(".//a[contains(@href,'packet')]");
                        string agendaPacketUrl = agendaPacketNode == null ? string.Empty : this.cityEntity.CityUrl + agendaPacketNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(minuteUrl))
                        {
                            ExtractPdf(c, meetingDate, minuteUrl, category, string.Empty, ref docs, ref queries);
                        }
                        if (!string.IsNullOrEmpty(agendaPacketUrl))
                        {
                            ExtractPdf(c, meetingDate, agendaPacketUrl, category, string.Empty, ref docs, ref queries);
                        }
                        if (!string.IsNullOrEmpty(agendaUrl))
                        {
                            if (agendaUrl.Contains("html"))
                            {
                                ExtractHtml(web, c, meetingDate, category, agendaUrl, ref docs, ref queries);
                            }
                            else
                            {
                                ExtractPdf(c, meetingDate, agendaUrl, category, string.Empty, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }

        public void ExtractHtml(HtmlWeb web, WebClient c, DateTime meetingDate, string category, string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == url);

            if (localdoc == null)
            {
                localdoc = new Documents();
                localdoc.DocId = Guid.NewGuid().ToString();
                localdoc.CityId = this.cityEntity.CityId;
                localdoc.DocSource = url;
                localdoc.DocType = category;
                localdoc.Checked = false;
                localdoc.Important = false;
                localdoc.DocLocalPath = string.Format("{0}\\{1}_Agenda_{2}.html",
                    this.localDirectory,
                    category,
                    Guid.NewGuid().ToString());

                try
                {
                    string html = c.DownloadString(url);
                    File.WriteAllText(localdoc.DocLocalPath, html);
                }
                catch
                { }

                docs.Add(localdoc);
            }
            else
            {
                Console.WriteLine("This file already downloaded...");
            }

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(File.ReadAllText(localdoc.DocLocalPath));
            HtmlNodeCollection pdfFileNodes = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href,'ViewFile')]");
            localdoc.DocBodyDic.Clear();
            localdoc.DocBodyDic.Add(1, htmlDoc.DocumentNode.InnerText);
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

            if (pdfFileNodes != null)
            {
                foreach (HtmlNode pdfNode in pdfFileNodes)
                {
                    if (!pdfNode.InnerText.ToLower().Contains(".pdf"))
                    {
                        continue;
                    }

                    string pdfUrl = this.cityEntity.CityUrl + pdfNode.Attributes["href"].Value;
                    this.ExtractPdf(c, meetingDate, pdfUrl, category, pdfNode.InnerText, ref docs, ref queries);
                }
            }
        }

        public void ExtractPdf(WebClient c, DateTime meetingDate, string url, string category, string fileName, ref List<Documents> docs, ref List<QueryResult> queries)
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
                localdoc.DocLocalPath = string.IsNullOrEmpty(fileName) ?
                    string.Format("{0}\\{1}_{2}.pdf",
                    this.localDirectory,
                    category,
                    Guid.NewGuid().ToString()) :
                    string.Format("{0}\\{1}",
                    this.localDirectory,
                    fileName);

                try
                {
                    c.DownloadFile(url, localdoc.DocLocalPath);
                }
                catch (Exception ex) { }

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
