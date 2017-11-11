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
    public class DelhiCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public DelhiCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "DelhiCharterTownshipMI",
                CityName = "Delhi",
                CityUrl = "http://www.delhitownship.com/index.html",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("DelhiCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNode agendaNode = doc.DocumentNode.SelectSingleNode("//a[text()='Agenda']");
                
                if(agendaNode != null)
                {
                    HtmlNode councilContainerNode = doc.DocumentNode.SelectNodes("//a[@name='Delhi_Charter_Township_Board_of_Trustees']/ancestor::table")
                    .LastOrDefault();
                    HtmlNode planningContainerNode = doc.DocumentNode.SelectNodes("//a[@name='Delhi_Charter_Township_Planning_Commission']/ancestor::table")
                        .LastOrDefault();
                    HtmlNodeCollection councilNodes = councilContainerNode.SelectNodes(".//tr[position()>1]//a[contains(@href,'.pdf')]");
                    HtmlNodeCollection planningNodes = planningContainerNode == null ? null : planningContainerNode.SelectNodes(".//a[contains(@href,'.pdf')]");
                    
                    if(councilNodes != null)
                    {
                        string category = "City Council";

                        foreach(HtmlNode councilNode in councilNodes)
                        {
                            string councilUrl = councilNode.Attributes["href"].Value;
                            councilUrl = councilUrl.StartsWith("http") ? councilUrl :
                                this.cityEntity.CityUrl.Replace(this.cityEntity.CityUrl.Split('/').LastOrDefault(), string.Empty) + councilUrl;
                            DateTime meetingDate = DateTime.MinValue;
                            this.ExtractADoc(c, councilUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }

                    if(planningNodes != null)
                    {
                        string category = "Planning";

                        foreach (HtmlNode planningNode in planningNodes)
                        {
                            string planningUrl = planningNode.Attributes["href"].Value;
                            planningUrl = planningUrl.StartsWith("http") ? planningUrl :
                                this.cityEntity.CityUrl.Replace(this.cityEntity.CityUrl.Split('/').LastOrDefault(), string.Empty) + planningUrl;
                            DateTime meetingDate = DateTime.MinValue;
                            this.ExtractADoc(c, planningUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
                else
                {
                    var titleNodes = doc.DocumentNode.SelectNodes("//*[@color='#FFFFFF']");

                    if(titleNodes != null)
                    {
                        StringBuilder yearBuilder = new StringBuilder();

                        for(int i = dtStartFrom.Year; i < DateTime.Now.Year; i++)
                        {
                            yearBuilder.AppendFormat("{0}|", i);
                        }

                        Regex yearReg = new Regex(yearBuilder.ToString().Trim('|'));
                        var councilTitleNodes = titleNodes.Where(t => t.InnerText.Contains("Board of Trustee"));
                        var planningTitleNodes = titleNodes.Where(t => t.InnerText.Contains("Planning Commission"));

                        if(councilTitleNodes != null && councilTitleNodes.Count() > 0)
                        {
                            string category = "council";
                            foreach(HtmlNode councilTitleNode in councilTitleNodes)
                            {
                                if (yearReg.IsMatch(councilTitleNode.InnerText))
                                {
                                    Console.WriteLine("DEBUG:{0}", councilTitleNode.InnerText);
                                    var yearContainerNode = councilTitleNode.Ancestors().FirstOrDefault(t => t.OriginalName == "table");

                                    HtmlNodeCollection yearDocNodes = yearContainerNode == null ? null :
                                        yearContainerNode.SelectNodes(".//a[contains(@href,'pdf')]");

                                    if(yearDocNodes != null)
                                    {
                                        foreach(HtmlNode yearDocNode in yearDocNodes)
                                        {
                                            string yearDocurl = yearDocNode.Attributes["href"].Value;
                                            yearDocurl = yearDocurl.StartsWith("http") ? yearDocurl :
                                                this.cityEntity.CityUrl.Replace(this.cityEntity.CityUrl.Split('/').LastOrDefault(), string.Empty) + yearDocurl;
                                            string meetingDateText = yearDocNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160);
                                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "MM/dd/yy", null);
                                            if (meetingDate < this.dtStartFrom)
                                            {
                                                Console.WriteLine("Too early, skip...");
                                                break;
                                            }
                                            this.ExtractADoc(c, yearDocurl, category, "pdf", meetingDate, ref docs, ref queries);
                                        }
                                    }
                                }
                            }
                        }

                        if(planningTitleNodes != null && planningTitleNodes.Count() > 0)
                        {
                            string category = "planning";
                            foreach (HtmlNode planningTitleNode in planningTitleNodes)
                            {
                                if (yearReg.IsMatch(planningTitleNode.InnerText))
                                {
                                    var yearContainerNode = planningTitleNode.Ancestors().FirstOrDefault(t => t.OriginalName == "table");

                                    HtmlNodeCollection yearDocNodes = yearContainerNode == null ? null :
                                        yearContainerNode.SelectNodes(".//a[contains(@href,'pdf')]");

                                    if (yearDocNodes != null)
                                    {
                                        foreach (HtmlNode yearDocNode in yearDocNodes)
                                        {
                                            string yearDocurl = yearDocNode.Attributes["href"].Value;
                                            yearDocurl = yearDocurl.StartsWith("http") ? yearDocurl :
                                                this.cityEntity.CityUrl.Replace(this.cityEntity.CityUrl.Split('/').LastOrDefault(), string.Empty) + yearDocurl;
                                            string meetingDateText = yearDocNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160);
                                            DateTime meetingDate = DateTime.ParseExact(meetingDateText, "MM/dd/yy", null);

                                            if(meetingDate < this.dtStartFrom)
                                            {
                                                break;
                                            }

                                            this.ExtractADoc(c, yearDocurl, category, "pdf", meetingDate, ref docs, ref queries);
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
