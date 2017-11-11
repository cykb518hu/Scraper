using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace CityCouncilMeetingProject
{
    public class PortageMICity : City
    {
        private List<string> docUrls = null;

        public PortageMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PortageMICity",
                CityName = "PortageMI",
                CityUrl = "http://www.portagemi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("PortageMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];

                if (category == "City Council")
                {
                    this.ExtractCouncil(url, ref docs, ref queries);
                }
                else
                {
                    this.ExtractOthers(listUrl, category, ref docs, ref queries);
                }
            }
        }

        private void ExtractCouncil(string listUrl, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            string category = listUrl.Split('*')[0];
            string url = listUrl.Split('*')[1];
            int startIndex = int.Parse(listUrl.Split('*')[2]);

            for(int i = startIndex; ; i++)
            {
                List<string> targetUrls = new List<string>();
                targetUrls.Add(string.Format(url, i));
                targetUrls.Add(string.Format(url, i) + "&p=1");
                targetUrls.Add(string.Format(url, i) + "&m=1");
                Dictionary<string, bool> notAddedDic = new Dictionary<string, bool>();

                foreach (string targetUrl in targetUrls)
                {
                    HtmlDocument doc = web.Load(targetUrl);
                    if(doc.DocumentNode.InnerText.Contains("content has not been published for this meeting"))
                    {
                        notAddedDic.Add(targetUrl, true);
                        continue;
                    }
                    
                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == targetUrl);

                    if (localdoc == null)
                    {
                        localdoc = new Documents();
                        localdoc.DocId = Guid.NewGuid().ToString();
                        localdoc.CityId = this.cityEntity.CityId;
                        localdoc.Important = false;
                        localdoc.Checked = false;
                        localdoc.DocType = "City Council";
                        localdoc.DocSource = targetUrl;

                        if (targetUrl.Contains("p=1"))
                        {
                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf",
                                this.localDirectory,
                                "Agenda Packet",
                                Guid.NewGuid().ToString());
                        }
                        else if (targetUrl.Contains("m=1"))
                        {
                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf",
                                this.localDirectory,
                                "Minutes",
                                Guid.NewGuid().ToString());
                        }
                        else
                        {
                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf",
                                this.localDirectory,
                                "Agenda",
                                Guid.NewGuid().ToString());
                        }

                        try
                        {
                            c.Headers.Add("user-agent", "chrome");
                            c.DownloadFile(targetUrl, localdoc.DocLocalPath);
                        }
                        catch (Exception ex)
                        { }

                        docs.Add(localdoc);
                    }
                    else
                    {
                        Console.WriteLine("This file already downloaded...");
                    }

                    this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                    DateTime meetingDate = DateTime.MinValue;

                    if(localdoc.DocBodyDic.Count != 0)
                    {
                        string meetingDateText = dateReg.IsMatch(localdoc.DocBodyDic[1]) ?
                            dateReg.Match(localdoc.DocBodyDic[1]).ToString() :
                            string.Empty;

                        if (!string.IsNullOrEmpty(meetingDateText))
                        {
                            meetingDate = DateTime.Parse(meetingDateText);
                        }
                    }

                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                    if(qr == null)
                    {
                        qr = new QueryResult();
                        qr.SearchTime = DateTime.Now;
                        qr.CityId = localdoc.CityId;
                        qr.DocId = localdoc.DocId;
                        qr.MeetingDate = meetingDate;
                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localdoc, ref qr);
                    Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                    this.SaveMeetingResultsToSQL(docs, queries);
                }

                if(notAddedDic.Count > 0 && notAddedDic.All(t => t.Value == true))
                {
                    Console.WriteLine("Reach last page...");
                    break;
                }
            }
        }

        private void ExtractOthers(string listUrl, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex yearReg = new Regex("[0-9]{4}");
            HtmlDocument listDoc = web.Load(listUrl);
            HtmlNodeCollection fileNodeList = listDoc.DocumentNode.SelectNodes("//table[@style='width: 100%;']//tr");
            string currentYear = string.Empty;

            if (fileNodeList != null)
            {
                foreach (HtmlNode fileNode in fileNodeList)
                {
                    string text = HttpUtility.HtmlDecode(fileNode.InnerText);
                    text = text.Trim('\r', '\n', '\t', (char)32, (char)160);

                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (yearReg.IsMatch(text))
                    {
                        currentYear = text;
                        continue;
                    }

                    HtmlNode dateNode = fileNode.SelectSingleNode("./td");
                    string dateText = HttpUtility.HtmlDecode(dateNode.InnerText).Trim('\r', '\n', '\t', (char)32, (char)160);
                    string meetingDateText = string.Format("{0}, {1}", dateText, currentYear);
                    DateTime meetingDate = DateTime.Parse(meetingDateText);

                    HtmlNodeCollection fileLinkNodes = fileNode.SelectNodes(".//a[contains(@href,'.pdf')]");

                    if (fileLinkNodes != null)
                    {
                        foreach (HtmlNode fileLinkNode in fileLinkNodes)
                        {
                            string fileUrl = this.cityEntity.CityUrl + fileLinkNode.Attributes["href"].Value;
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.DocType = category;
                                localdoc.DocSource = fileUrl;
                                localdoc.Important = false;
                                localdoc.Checked = false;
                                string localPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());
                                localdoc.DocLocalPath = localPath;

                                try
                                {
                                    c.DownloadFile(fileUrl, localPath);
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
                            this.SaveMeetingResultsToSQL(docs, queries);
                            Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                        }
                    }
                }
            }
        }
    }
}
