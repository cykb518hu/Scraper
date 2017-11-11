using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    /// <summary>
    /// 
    /// </summary>
    public class AlgonacMICity : City
    {
        private List<string> docUrls = null;

        public AlgonacMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "AlgonacMICity",
                CityName = "Algonac",
                CityUrl = "http://www.algonac-mi.gov/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("AlgonacMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{1,2}\\.[0-9]{1,2}\\.[0-9]{2}");
            WebClient c = new WebClient();
            foreach (string url in this.docUrls)
            {
                string date = string.Empty;
                string category = string.Empty;

                if (url.Contains("*"))
                {
                    date = url.Split('*')[0];
                    category = "Council";
                }
                else
                {
                    if (url.Contains("council"))
                    {
                        category = "Council";
                    }
                    else
                    {
                        category = "Planning Commission";
                    }
                }

                List<HtmlNode> docNodeList = null;
                if (string.IsNullOrEmpty(date))
                {
                    HtmlDocument listDoc = web.Load(url);
                    docNodeList = listDoc.DocumentNode.SelectNodes("//ul[@class='linklist ']/li/a").ToList();
                }
                else
                {
                    docNodeList = new List<HtmlNode>();
                    docNodeList.Add(HtmlNode.CreateNode(string.Format("<a href='{0}'>{1}</a>", url.Split('*')[1], date)));
                }

                if (docNodeList != null)
                {
                    Console.WriteLine("{0} files...", docNodeList.Count);

                    foreach (HtmlNode docNode in docNodeList)
                    {
                        string dateText = string.Join(" ", docNode.InnerText.Trim('\t', '\r', '\n', (char)32, (char)160).Split(' ').Take(3));
                        DateTime meetingDate = DateTime.MinValue;
                        try
                        {
                            meetingDate = string.IsNullOrEmpty(date) ? DateTime.Parse(dateText) : DateTime.Parse(date);
                        }
                        catch
                        {
                            dateText = dateReg.Match(dateText).ToString();
                            meetingDate = DateTime.ParseExact(dateText, "M.d.yy", null);
                        }

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("{0} earlier than {1}. Skip...", meetingDate, dtStartFrom);
                            continue;
                        }

                        string pdfUrl = docNode.Attributes["href"].Value;
                        pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://www.algonac-mi.gov" + pdfUrl;
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                        if (localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.DocType = category;
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocSource = pdfUrl;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}_{3}.pdf", this.localDirectory, category, meetingDate.ToString("yyyy-MM-dd"), Guid.NewGuid().ToString());

                            try
                            {
                                c.DownloadFile(pdfUrl, localdoc.DocLocalPath);
                            }
                            catch
                            {
                            }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("This document already downloaded...");
                            Console.ResetColor();
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.DocId = localdoc.DocId;
                            qr.CityId = localdoc.CityId;
                            qr.MeetingDate = meetingDate;
                            
                            qr.SearchTime = DateTime.Now;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} documents saved...", docs.Count);
                        Console.WriteLine("{0} query results saved...", queries.Count);
                    }
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
