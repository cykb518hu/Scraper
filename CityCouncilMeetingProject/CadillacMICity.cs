﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class CadillacMICity : City
    {
        private List<string> docUrls = null;

        public CadillacMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "CadillacMICity",
                CityName = "Cadillac",
                CityUrl = "http://www.cadillac-mi.net",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("CadillacMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");


            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                if (category == "City Council")
                {
                    for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                    {
                        string categoryUrl = string.Format(url.Split('*')[1], i);
                        this.ExtractCityCouncil(categoryUrl, category, dateReg, ref docs, ref queries);
                    }
                }
                else
                {
                    string categoryUrl = url.Split('*')[1];
                    this.ExtractOthers(categoryUrl, category, dateReg, ref docs, ref queries);
                }
            }
        }

        private void ExtractCityCouncil(string categoryUrl, string category, Regex dateReg, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            HtmlDocument doc = web.Load(categoryUrl);
            HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

            if (entryNodes != null)
            {
                foreach (HtmlNode entryNode in entryNodes)
                {
                    string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                    DateTime meetingDate = DateTime.MinValue;
                    try
                    {
                        meetingDate = DateTime.Parse(meetingDateText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception:{0}", ex.ToString());
                    }
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

        private void ExtractOthers(string categoryUrl, string category, Regex dateReg, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            HtmlDocument doc = web.Load(categoryUrl);
            HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//span[@class='archive']/a");

            if (entryNodes != null)
            {
                foreach (HtmlNode entryNode in entryNodes)
                {
                    string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();

                    if (entryNode.InnerText.Contains("All Archives"))
                    {
                        continue;
                    }

                    DateTime meetingDate = DateTime.MinValue;
                    try
                    {
                        meetingDate = DateTime.Parse(meetingDateText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception:{0}", ex.ToString());
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip...");
                        continue;
                    }

                    string docPageUrl = this.cityEntity.CityUrl.TrimEnd('/') + "/" + entryNode.Attributes["href"].Value.TrimStart('/');
                    string docUrl = string.Format("{0}{1}{2}", this.cityEntity.CityUrl, "/ArchiveCenter/ViewFile/Item/", docPageUrl.Split('=').LastOrDefault());
                    this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
