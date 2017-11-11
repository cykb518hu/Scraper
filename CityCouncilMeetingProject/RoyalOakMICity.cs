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
    public class RoyalOakMICity : City
    {
        private List<string> docUrls = null;

        public RoyalOakMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RoyalOakMICity",
                CityName = "Royal Oak",
                CityUrl = "https://www.romi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RoyalOakMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = new HtmlDocument(); //web.Load(categoryUrl);

                    try
                    {
                        string html = this.GetHtml(categoryUrl, string.Empty);
                        doc.LoadHtml(html);
                    }
                    catch(Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("DEBUG:\r\n");
                        Console.WriteLine("URL:{0}...", categoryUrl);
                        Console.WriteLine("EXCEPTION:{0}...", ex.ToString());
                        Console.ResetColor();
                    }

                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[contains(@href,'/Minutes/')]");
                            string minuteUrl = minuteNode == null ? string.Empty :
                                this.cityEntity.CityUrl.TrimEnd('/') + minuteNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(minuteUrl))
                            {
                                this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode packetNode = entryNode.SelectNodes(".//a[contains(@href,'/Agenda/')]").FirstOrDefault(t => t.Attributes["href"].Value.Contains("packet"));
                            string packetUrl = packetNode == null ? string.Empty :
                                this.cityEntity.CityUrl.TrimEnd('/') + packetNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(packetUrl))
                            {
                                this.ExtractADoc(c, packetUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }

                            HtmlNode agendaNode = entryNode.SelectNodes(".//a[contains(@href,'/Agenda/')]").FirstOrDefault(t => t.Attributes["href"].Value.Contains("html"));
                            agendaNode = agendaNode == null ?
                                entryNode.SelectNodes(".//a[contains(@href,'/Agenda/')]").FirstOrDefault(t => t.Attributes["href"].Value.Contains("?") == false) :
                                agendaNode;
                            string agendaUrl = agendaNode == null ? string.Empty : this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(agendaUrl))
                            {
                                if (!agendaUrl.Contains("html"))
                                {
                                    this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                                else
                                {
                                    this.ExtractADoc(c, agendaUrl.Split('?').FirstOrDefault(), category, "pdf", meetingDate, ref docs, ref queries);

                                    HtmlDocument agendaDoc = web.Load(agendaUrl);
                                    HtmlNodeCollection agendaFileNodes = agendaDoc.DocumentNode.SelectNodes("//a[@href]");

                                    if (agendaFileNodes != null)
                                    {
                                        foreach (HtmlNode fileNode in agendaFileNodes)
                                        {
                                            string fileUrl = fileNode.Attributes["href"].Value;
                                            if (!fileUrl.StartsWith("http"))
                                            {
                                                fileUrl = this.cityEntity.CityUrl.Trim('/') + '/' + fileUrl.Trim('/');
                                            }

                                            if (fileNode.InnerText.ToLower().Contains("pdf"))
                                            {
                                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
