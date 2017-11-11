using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class NorthvilleTownshipMI : City
    {
        private List<string> docUrls = null;

        public NorthvilleTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "NorthvilleTownshipMI",
                CityName = "Northville Township",
                CityUrl = "http://38.106.4.236/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("NorthvilleTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[@class='tableData']/tbody/tr[position()>1]");

                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        string meetingDateText = entryNode.SelectSingleNode("./td").InnerText;
                        meetingDateText = HttpUtility.HtmlDecode(meetingDateText).Trim((char)32, (char)160, '\r', '\n');
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            continue;
                        }

                        HtmlNode minuteNode = entryNode.SelectSingleNode(".//a[contains(text(),'Minute')]");
                        HtmlNode agendaNode = entryNode.SelectSingleNode(".//a[contains(text(),'Agenda')]");

                        if (minuteNode != null)
                        {
                            string minuteUrl = minuteNode.Attributes["href"].Value.StartsWith("http") ?
                                minuteNode.Attributes["href"].Value :
                                this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value;
                            this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }

                        if (agendaNode != null)
                        {
                            string agendaUrl = agendaNode.Attributes["href"].Value.StartsWith("http") ?
                                agendaNode.Attributes["href"].Value :
                                this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value;
                            this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
