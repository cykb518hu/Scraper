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
    public class KalkaskaVillageMI : City
    {
        private List<string> docUrls = null;

        public KalkaskaVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "KalkaskaVillageMI",
                CityName = "Thetford",
                CityUrl = "http://www.kalkaskavillage.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("KalkaskaVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];

                for (int i = 1; ; i++)
                {
                    Console.WriteLine("Working on page {0}...", i);
                    string categoryPagedUrl = i == 1 ? categoryUrl : string.Format("{0}&paged={1}", categoryUrl, i);
                    HtmlDocument listDoc = web.Load(categoryPagedUrl);
                    HtmlNode notFoundNode = listDoc.DocumentNode.SelectSingleNode("//section[@class='error-404 not-found']");

                    if (notFoundNode != null)
                    {
                        break;
                    }

                    HtmlNodeCollection entryNodes = listDoc.DocumentNode.SelectNodes("//article[contains(@id,'post')]");

                    if(entryNodes != null)
                    {
                        foreach(HtmlNode entryNode in entryNodes)
                        {
                            HtmlNode dateNode = entryNode.SelectSingleNode(".//time[contains(@class,'entry-date published')]");
                            string meetingDateText = dateNode.InnerText;
                            DateTime meetingDate = DateTime.Parse(meetingDateText);

                            if(meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Early! Skip....");
                                continue;
                            }

                            HtmlNode meetingUrlNode = dateNode == null ? null : dateNode.ParentNode;
                            string meetingUrl = meetingUrlNode == null ? string.Empty : meetingUrlNode.Attributes["href"].Value;

                            if (!string.IsNullOrEmpty(meetingUrl))
                            {
                                HtmlNode contentNode = entryNode.SelectSingleNode(".//div[@class='entry-content']");
                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == meetingUrl);

                                if(localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.DocType = category;
                                    localdoc.Checked = false;
                                    localdoc.Important = false;
                                    localdoc.Readable = true;
                                    localdoc.DocSource = meetingUrl;
                                    localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.html", this.localDirectory, category, Guid.NewGuid().ToString());
                                    File.WriteAllText(localdoc.DocLocalPath, contentNode.OuterHtml);
                                    docs.Add(localdoc); 
                                }
                                else
                                {
                                    Console.WriteLine("This file already downloaded...");
                                }

                                localdoc.DocBodyDic.Add(1, contentNode.InnerText);
                                QueryResult qr = queries.FirstOrDefault(q => q.DocId == localdoc.DocId);

                                if(qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.MeetingDate = meetingDate;
                                    qr.SearchTime = DateTime.Now;
                                    qr.QueryId = Guid.NewGuid().ToString();
                                    qr.DocId = localdoc.DocId;
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localdoc, ref qr);
                                Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                            }
                        }

                        this.SaveMeetingResultsToSQL(docs, queries);
                    }
                }
            }
        }
    }
}
