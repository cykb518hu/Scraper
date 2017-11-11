using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class FarmingtonHillsMICity : City
    {
        private List<string> docUrls = null;

        public FarmingtonHillsMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "FarmingtonHillsMICity",
                CityName = "FarmingtonHills",
                CityUrl = "http://www.ci.farmington-hills.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("FarmingtonHillsMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            Regex dateReg = new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{4}");
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument categoryDoc = web.Load(categoryUrl);
                HtmlNodeCollection docNodeList = categoryDoc.DocumentNode.SelectNodes("//table[@class='tablewithheadingresponsive']/tbody/tr");

                if(docNodeList != null)
                {
                    foreach(HtmlNode lineNode in docNodeList)
                    {
                        if(lineNode.SelectSingleNode("./th") != null)
                        {
                            continue;
                        }

                        string meetingDateText = dateReg.Match(lineNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNodeCollection docNodes = lineNode.SelectNodes(".//div[@class='pdf']/a");

                        if(docNodes != null)
                        {
                            foreach(HtmlNode docNode in docNodes)
                            {
                                string docLink = docNode.Attributes["href"].Value;
                                docLink = docLink.StartsWith("http") ? docLink : this.cityEntity.CityUrl + docLink;
                                Documents localDoc = docs.FirstOrDefault(t => t.DocSource == docLink);

                                if(localDoc == null)
                                {
                                    localDoc = new Documents();
                                    localDoc.DocSource = docLink;
                                    localDoc.DocId = Guid.NewGuid().ToString();
                                    localDoc.CityId = this.cityEntity.CityId;
                                    localDoc.Important = false;
                                    localDoc.Checked = false;
                                    localDoc.DocType = category;
                                    string tag = docNode.InnerText == "Agenda" ? "agenda" : "minute";
                                    string localFile = string.Format("{0}\\{1}_{2}_{3}.pdf", this.localDirectory, category, tag, meetingDate.ToString("yyyyMMdd"));
                                    localDoc.DocLocalPath = localFile;

                                    try
                                    {
                                        c.DownloadFile(docLink, localFile);
                                    }
                                    catch
                                    {
                                    }

                                    docs.Add(localDoc);
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("This file already downloaded...");
                                    Console.ResetColor();
                                }

                                this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                                if(qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.DocId = localDoc.DocId;
                                    qr.CityId = localDoc.CityId;
                                    qr.MeetingDate = meetingDate;
                                    qr.SearchTime = DateTime.Now;
                                    
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localDoc, ref qr);
                                Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                                this.SaveMeetingResultsToSQL(docs, queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
