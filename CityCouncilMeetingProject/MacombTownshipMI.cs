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
    class MacombTownshipMI : City
    {
        private List<string> docUrls = null;

        public MacombTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "MacombTownshipMI",
                CityName = "Macomb Township",
                CityUrl = "https://www.macomb-mi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MacombTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = url.Split('*')[1];
                    categoryUrl = string.Format(categoryUrl, i);

                    HtmlDocument doc = new HtmlDocument();//web.Load(categoryUrl);
                    string cookie = "CP_TrackBrowser={\"doNotShowLegacyMsg\":false,\"supportNewUI\":true,\"legacy\":false,\"isMobile\":false}; _pk_id.3071.5ea1=f41f08faa525b5f7.1502151243.1.1502151293.1502151243.; ASP.NET_SessionId=5xugtstpqi4lexcymroehekx; CP_IsMobile=false";
                    string html = this.GetHtml(categoryUrl, cookie);
                    doc.LoadHtml(html);
                    HtmlNodeCollection recordNodes = doc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

                    if (recordNodes != null)
                    {
                        foreach (HtmlNode recordNode in recordNodes)
                        {
                            string meetingDateText = dateReg.Match(recordNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode minuteNode = recordNode.SelectSingleNode(".//a[contains(@href,'/Minutes/')]");
                            HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[contains(@href,'/Agenda/')]");
                            HtmlNode agendaPacketNode = recordNode.SelectSingleNode(".//a[contains(@href,'packet')]");

                            if (minuteNode != null)
                            {
                                string fileType = minuteNode.Attributes.Contains("class") && minuteNode.Attributes["class"].Value == "html" ? "doc" : "pdf";
                                string fileUrl = this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                            }

                            if (agendaPacketNode != null)
                            {
                                string fileType = agendaPacketNode.Attributes.Contains("class") && minuteNode.Attributes["class"].Value == "html" ? "doc" : "pdf";
                                string fileUrl = this.cityEntity.CityUrl + agendaPacketNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                            }

                            if (agendaNode != null)
                            {
                                string fileType = agendaNode.Attributes.Contains("class") && minuteNode.Attributes["class"].Value == "html" ? "doc" : "pdf";
                                string fileUrl = this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                                this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
