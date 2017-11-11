using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class KentwoodMICity : City
    {
        private List<string> docUrls = null;

        public KentwoodMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "KentwoodMICity",
                CityName = "Kentwood",
                CityUrl = "http://www.ci.kentwood.mi.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("KentwoodMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]+");
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];

                HtmlDocument listDoc = web.Load(listUrl);
                List<HtmlNode> docNodes = new List<HtmlNode>();
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//img[contains(@src,'pdf')]/following-sibling::a");
                if (docNodeList == null || docNodeList.Count == 0)
                {
                    docNodeList = listDoc.DocumentNode.SelectNodes("//a[contains(@href,'/minutes/')]");
                    if (docNodeList != null)
                    {
                        docNodes.AddRange(docNodeList.ToList());
                    }
                    docNodeList = listDoc.DocumentNode.SelectNodes("//a[contains(@href,'/agendas/')]");
                    if (docNodeList != null)
                    {
                        docNodes.AddRange(docNodeList.ToList());
                    }
                }
                else
                {
                    docNodes.AddRange(docNodeList);
                }

                foreach (HtmlNode docNode in docNodes)
                {
                    string meetingDateText = docNode.InnerText.Trim('\r', '\n', (char)32, (char)160);
                    if (!dateReg.IsMatch(meetingDateText))
                    {
                        if (listUrl.Contains("ePacket"))
                        {
                            meetingDateText = docNode.InnerText.Split(' ').LastOrDefault();
                        }
                        else
                        {
                            continue;
                        }
                    }

                    DateTime meetingDate = listUrl.Contains("ePacket") ? DateTime.ParseExact(meetingDateText, "MM-dd-yy", null) : DateTime.Parse(meetingDateText);

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip!!!");
                        if (listUrl.Contains("ePacket"))
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    string docUrl = docNode.Attributes["href"].Value;
                    docUrl = docUrl.StartsWith("http") ? docUrl : this.cityEntity.CityUrl + docUrl;
                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                    if (localdoc == null)
                    {
                        localdoc = new Documents();
                        localdoc.DocId = Guid.NewGuid().ToString();
                        localdoc.CityId = this.cityEntity.CityId;
                        localdoc.Important = false;
                        localdoc.Checked = false;
                        localdoc.DocType = category;
                        localdoc.DocSource = docUrl;

                        string localPath = string.Empty;
                        if (listUrl.Contains("ePacket"))
                        {
                            localPath = string.Format("{0}\\ePacket_{1}.pdf", this.localDirectory, docNode.InnerText.Trim((char)32, (char)160));

                            try
                            {
                                c.DownloadFile(docUrl, localPath);
                            }
                            catch
                            {
                            }

                            this.ReadText(false, localPath, ref localdoc);
                        }
                        else
                        {
                            string tag = docUrl.Contains("agenda") ? "agenda" : "minute";
                            localPath = string.Format("{0}\\{1}_{2}", this.localDirectory, tag, docUrl.Split('/').LastOrDefault());
                            string html = string.Empty;
                            try
                            {
                                html = c.DownloadString(docUrl);
                            }
                            catch
                            {
                                continue;
                            }
                            File.WriteAllText(localPath, html);
                            HtmlDocument meetingContentDoc = new HtmlDocument();
                            meetingContentDoc.LoadHtml(html);
                            HtmlNode contentNode = meetingContentDoc.DocumentNode.SelectSingleNode("//*[@class='col_12 last']");
                            string contentBody = HttpUtility.HtmlDecode(contentNode.InnerText);
                            localdoc.DocBodyDic.Add(1, contentBody);
                        }

                        localdoc.DocLocalPath = localPath;
                        docs.Add(localdoc);
                    }
                    else
                    {
                        Console.WriteLine("This file already downloaded...");

                        if (localdoc.DocLocalPath.ToLower().Contains("pdf"))
                        {
                            this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        }
                        else
                        {
                            string html = File.ReadAllText(localdoc.DocLocalPath);
                            HtmlDocument pageContent = new HtmlDocument();
                            pageContent.LoadHtml(html);
                            HtmlNode contentNode = pageContent.DocumentNode.SelectSingleNode("//*[@class='col_12 last']");
                            string contentBody = HttpUtility.HtmlDecode(contentNode.InnerText);

                            if (localdoc.DocBodyDic.Count != 0)
                            {
                                localdoc.DocBodyDic.Clear();
                            }

                            localdoc.DocBodyDic.Add(1, contentBody);
                        }
                    }

                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                    if (qr == null)
                    {
                        qr = new QueryResult();
                        qr.CityId = localdoc.CityId;
                        qr.DocId = localdoc.DocId;
                        qr.MeetingDate = meetingDate;
                        qr.SearchTime = DateTime.Now;

                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localdoc, ref qr);
                    Console.WriteLine("{0} docs added, {1} queries added.", docs.Count, queries.Count);
                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }
        }
    }
}
