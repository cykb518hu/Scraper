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
    public class CharlotteMICity : City
    {
        private List<string> docUrls = null;

        public CharlotteMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "CharlotteMICity",
                CityName = "Charlotte MI",
                CityUrl = "http://www.charlottemi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("CharlotteMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
             //var docs = new List<Documents>();
             //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");


            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table/tbody/tr[contains(@class,'row')]");
                int number = 1;
                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        if(number==1)
                        {
                            number++;
                            continue;
                        }
                        string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                        DateTime meetingDate;
                        if (!DateTime.TryParse(meetingDateText, out meetingDate))
                        {
                            Console.WriteLine("date format incorrect...");
                            continue;
                        }

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip");
                            continue;
                        }
                        if(category== "Recreation Co-Op")
                        {
                            HtmlNode spMinuteNode = entryNode.SelectNodes("td")[1].SelectSingleNode("a");
                            string spMinuteUrl = spMinuteNode == null ? string.Empty : spMinuteNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(spMinuteUrl))
                            {
                                this.ExtractADoc(c, spMinuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                               // Console.WriteLine(string.Format("url:{0}", spMinuteUrl));
                            }
                            continue;
                        }
                        HtmlNode minuteNode = entryNode.SelectNodes("td")[2].SelectSingleNode("a");
                        string minuteUrl = minuteNode == null ? string.Empty : minuteNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(minuteUrl))
                        {
                            this.ExtractADoc(c, minuteUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            //Console.WriteLine(string.Format("url:{0}", minuteUrl));
                        }

                        HtmlNode agendaNode = entryNode.SelectNodes("td")[1].SelectSingleNode("a"); //entryNode.SelectSingleNode("//a[contains(@href,'Agenda')]");
                        string agendaUrl = agendaNode == null ? string.Empty :agendaNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(agendaUrl))
                        {
                            if (agendaUrl.Contains(".pdf"))
                            {
                                //Console.WriteLine(string.Format("sub aganda-url:{0}", agendaUrl));
                                 this.ExtractADoc(c, agendaUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                            else
                            {
                                HtmlDocument agendaDoc = web.Load(agendaUrl);
                                HtmlNodeCollection agendaFileNodes = agendaDoc.DocumentNode.SelectNodes("//ul[contains(@class,'attachment_list')]/li/a[contains(@href,'.pdf')]");

                                if (agendaFileNodes != null)
                                {
                                    foreach (HtmlNode fileNode in agendaFileNodes)
                                    {
                                        string fileUrl = fileNode.Attributes["href"].Value;

                                        if (fileUrl.Contains(".pdf"))
                                        {
                                           // Console.WriteLine(string.Format("sub aganda-url:{0}", fileUrl));
                                             this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }
    }
}
