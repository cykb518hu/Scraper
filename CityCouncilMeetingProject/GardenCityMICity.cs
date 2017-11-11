using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class GardenCityMICity : City
    {
        private List<string> docUrls = null;

        public GardenCityMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "GardenCityMICity",
                CityName = "GardenCity",
                CityUrl = "http://www.gardencitymi.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("GardenCityMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];

                HtmlDocument doc = web.Load(listUrl);
                HtmlNodeCollection docNodeList = doc.DocumentNode.SelectNodes("//table[@id='table2']/tbody/tr");

                if (docNodeList != null)
                {
                    foreach (HtmlNode docNode in docNodeList)
                    {
                        HtmlNode dateNode = docNode.SelectSingleNode(".//strong");
                        string meetingDateText = dateReg.Match(dateNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNode agendaNode = dateNode.ParentNode;
                        HtmlNode minuteNode = docNode.SelectSingleNode(".//a[contains(@href,'Minute')]");

                        Dictionary<string, string> urlDic = new Dictionary<string, string>();
                        urlDic.Add(this.cityEntity.CityUrl + agendaNode.Attributes["href"].Value, "agenda");

                        if (minuteNode != null)
                        {
                            urlDic.Add(this.cityEntity.CityUrl + minuteNode.Attributes["href"].Value, "minute");
                        }

                        foreach(string key in urlDic.Keys)
                        {
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == key);

                            if(localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.Checked = false;
                                localdoc.Important = false;
                                localdoc.DocType = category;
                                localdoc.DocSource = key;
                                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf",
                                    this.localDirectory,
                                    urlDic[key],
                                    Guid.NewGuid().ToString());

                                try
                                {
                                    c.DownloadFile(key, localdoc.DocLocalPath);
                                }
                                catch { }

                                docs.Add(localdoc);
                            }
                            else
                            {
                                Console.WriteLine("This file already downloaded...");
                            }

                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                            if(qr == null)
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
            }

        }
    }
}
