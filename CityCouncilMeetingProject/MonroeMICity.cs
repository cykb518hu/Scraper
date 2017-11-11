//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class MonroeMICity : City
    {
        private List<string> docUrls = null;

        public MonroeMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "MonroeMICity",
                CityName = "Monroe",
                CityUrl = "http://www.monroemi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MonroeMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]{4}");

            for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = url.Split('*')[1];

                    if (category == "City Council")
                    {
                        string rangeStart = string.Format("1/1/{0}", year);
                        string rangeEnd = string.Format("12/31/{0}", year);
                        HtmlDocument doc = web.Load(string.Format(categoryUrl, rangeStart, rangeEnd));
                        HtmlNodeCollection docCollection = doc.DocumentNode.SelectNodes("//div[contains(@class,'Row MeetingRow')]");

                        if (docCollection != null)
                        {
                            foreach (HtmlNode docNode in docCollection)
                            {
                                HtmlNode meetingDateNode = docNode.SelectSingleNode(".//div[@class='RowLink']/a");
#if debug
                                try
                                {
                                    DateTime.Parse(meetingDateNode.InnerText);
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("No problem...");
                                    Console.ResetColor();
                                    continue;
                                }
                                catch
                                {
                                    Console.WriteLine("Not match {0}...", meetingDateNode.InnerText);
                                    continue;
                                }
#endif

                                DateTime meetingDate = DateTime.Parse(meetingDateNode.InnerText);

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }

                                HtmlNode agendaNode = docNode.SelectSingleNode(".//a[text()='Agenda']");
                                string agendaUrl = agendaNode == null ? string.Empty : "http://monroecitymi.iqm2.com/Citizens/" + agendaNode.Attributes["href"].Value;
                                HtmlNode minuteNode = docNode.SelectSingleNode(".//a[text()='Minutes']");
                                string minuteUrl = minuteNode == null ? string.Empty : "http://monroecitymi.iqm2.com/Citizens/" + minuteNode.Attributes["href"].Value;
                                HtmlNode agendaPacketNode = docNode.SelectSingleNode(".//a[text()='Agenda Packet']");
                                string agendaPacketUrl = agendaPacketNode == null ? string.Empty : "http://monroecitymi.iqm2.com/Citizens/" + agendaPacketNode.Attributes["href"].Value;

                                if (string.IsNullOrEmpty(agendaUrl) == false && agendaUrl.Contains(".pdf"))
                                {
                                    this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }

                                if (string.IsNullOrEmpty(minuteUrl) == false && minuteUrl.Contains(".pdf"))
                                {
                                    this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }

                                if (string.IsNullOrEmpty(agendaPacketUrl) == false && agendaPacketUrl.Contains(".pdf"))
                                {
                                    this.ExtractADoc(c, agendaPacketUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }
                        }
                    }
                    else
                    {
                        HtmlDocument doc = web.Load(string.Format(categoryUrl, year));
                        HtmlNodeCollection docCollection = doc.DocumentNode.SelectNodes("//a[contains(@href,'ownload')]");

                        if (docCollection != null)
                        {
                            foreach (HtmlNode docNode in docCollection)
                            {
                                string docUrl = docNode.Attributes["href"].Value;
                                string meetingDateText = dateReg.Match(docNode.InnerText).ToString();

                                if (string.IsNullOrEmpty(meetingDateText))
                                {
                                    continue;
                                }

#if debug
                                try
                                {
                                    DateTime.Parse(meetingDateText);
                                    Console.WriteLine("No problem...");
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine(docUrl);
                                    Console.ResetColor();
                                    continue;
                                }
                                catch
                                {
                                    Console.WriteLine("Not match [{0}] on [{1}]...", meetingDateText, categoryUrl);
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine(docUrl);
                                    Console.ResetColor();
                                    continue;
                                }
#endif

                                if (docUrl.Contains("pdf"))
                                {
                                    DateTime meetingDate = DateTime.Parse(meetingDateText);
                                    this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
