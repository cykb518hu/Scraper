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
    public class KingsleyVillageMI : City
    {
        private List<string> docUrls = null;

        public KingsleyVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "KingsleyVillageMI",
                CityName = "Kingsley",
                CityUrl = "http://www.villageofkingsley.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("KingsleyVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            StringBuilder yearBuilder = new StringBuilder();

            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(i.ToString());
                yearBuilder.Append('|');
            }

            Regex yearReg = new Regex(string.Format("({0})", yearBuilder.ToString().Trim('|')));
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection councilNodes = doc.DocumentNode.SelectNodes("//h2[text()='Village Council']/parent::td//a[contains(@href,'pdf')]");
            HtmlNodeCollection planningNodes = doc.DocumentNode.SelectNodes("//h3[text()='Planning Commission']/parent::td//a[contains(@href,'pdf')]");

            if(councilNodes != null)
            {
                foreach(HtmlNode councilNode in councilNodes)
                {
                    string councilUrl = this.cityEntity.CityUrl + councilNode.Attributes["href"].Value;
                    if (yearReg.IsMatch(councilNode.InnerText))
                    {
                        DateTime meetingDate = DateTime.MinValue;
                        this.ExtractADoc(c, councilUrl, "City Council", "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }

            if(planningNodes != null)
            {
                foreach(HtmlNode planningNode in planningNodes)
                {
                    Regex dateReg = new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{2}");
                    string meetingDateText = dateReg.Match(planningNode.InnerText).ToString();
                    DateTime meetingDate = DateTime.ParseExact(meetingDateText, "M/d/yy", null);
                    string planningUrl = this.cityEntity.CityUrl + planningNode.Attributes["href"].Value;

                    if(meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early, skip...");
                        continue;
                    }

                    this.ExtractADoc(c, planningUrl, "Planning", "pdf", meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
