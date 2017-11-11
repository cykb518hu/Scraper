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
    public class VillageofDouglasMICity : City
    {
        private List<string> docUrls = null;

        public VillageofDouglasMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "VillageofDouglasMICity",
                CityName = "Village of Douglas",
                CityUrl = "https://ci.douglas.mi.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("VillageofDouglasMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection currentAgendas = doc.DocumentNode.SelectNodes("//div[@class='x-column x-sm vc x-1-1']/p//a[contains(@href,'.pdf')]");

            if (currentAgendas != null)
            {
                foreach (HtmlNode currentAgendaNode in currentAgendas)
                {
                    string category = string.Empty;
                    if (currentAgendaNode.InnerText.Contains("Council") || currentAgendaNode.ParentNode.InnerText.Contains("Council"))
                    {
                        category = "City Council";
                        string meetingUrl = currentAgendaNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                    else if (currentAgendaNode.InnerText.Contains("Planning"))
                    {
                        category = "Planning";
                        string meetingUrl = currentAgendaNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                    else if (currentAgendaNode.InnerText.Contains("Zoning"))
                    {
                        category = "Zoning";
                        string meetingUrl = currentAgendaNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            HtmlNode councilNode = doc.DocumentNode.SelectNodes("//div[@class='x-column x-sm vc x-1-6']")
                .FirstOrDefault(t => t.InnerText.ToLower().Contains("council"));
            HtmlNode planningNode = doc.DocumentNode.SelectNodes("//div[@class='x-column x-sm vc x-1-6']")
                .FirstOrDefault(t => t.InnerText.ToLower().Contains("planning"));
            HtmlNode zoningNode = doc.DocumentNode.SelectNodes("//div[@class='x-column x-sm vc x-1-6']")
                .FirstOrDefault(t => t.InnerText.ToLower().Contains("zoning"));

            if (councilNode != null)
            {
                HtmlNodeCollection councilMinutesNodes = councilNode.SelectNodes(".//a[contains(@href,'pdf')]");
                if (councilMinutesNodes != null)
                {
                    foreach(HtmlNode councilMinuteNode in councilMinutesNodes)
                    {
                        string meetingUrl = councilMinuteNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, "City Council", "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            if(planningNode != null)
            {
                HtmlNodeCollection planningMinutesNodes = planningNode.SelectNodes(".//a[contains(@href,'pdf')]");
                if(planningMinutesNodes != null)
                {
                    foreach(HtmlNode planningMinuteNode in planningMinutesNodes)
                    {
                        string meetingUrl = planningMinuteNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, "Planning", "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            if(zoningNode != null)
            {
                HtmlNodeCollection zoningMinuteNodes = zoningNode.SelectNodes(".//a[contains(href,'pdf')]");
                if(zoningMinuteNodes != null)
                {
                    foreach(var zoningMinuteNode in zoningMinuteNodes)
                    {
                        string meetingUrl = zoningMinuteNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, meetingUrl, "Zoning", "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
