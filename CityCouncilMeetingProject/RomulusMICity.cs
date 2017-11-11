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
    /// <summary>
    /// http://romulusgov.com/government/city_council/city_council_agendas.php
    /// http://romulusgov.com/government/city_council/city_council_minutes.php
    /// http://romulusgov.com/government/boards_and_commissions/agendas.php
    /// http://romulusgov.com/government/boards_and_commissions/agendasplanning.php
    /// </summary>
    public class RomulusMICity : City
    {
        private List<string> docUrls = null;

        public RomulusMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "RomulusMICity",
                CityName = "Romulus",
                CityUrl = "http://romulusgov.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RomulusMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();

            foreach (string url in this.docUrls)
            {
                HtmlDocument listDoc = web.Load(url);
                HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//div[@id='file_name']/a");

                if (docNodeList != null)
                {
                    Console.WriteLine("{0} files...", docNodeList.Count);

                    foreach (HtmlNode docNode in docNodeList)
                    {
                        DateTime meetingDate = DateTime.MinValue;

                        try
                        {
                            meetingDate = DateTime.Parse(HttpUtility.HtmlDecode(docNode.InnerText.Replace("Special Meeting", string.Empty)));
                        }
                        catch
                        {
                            string[] meetingTagArray = docNode.InnerText.Split('.').FirstOrDefault().Split(' ');
                            try
                            {
                                meetingDate = DateTime.Parse(string.Join(" ", meetingTagArray.Take(3)));
                            }
                            catch
                            {

                            }
                        }


                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("{0} earlier than {1}. Skip...", meetingDate, dtStartFrom);
                            continue;
                        }

                        string pdfUrl = docNode.Attributes["href"].Value;
                        pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://romulusgov.com/" + pdfUrl;
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                        if (localdoc == null)
                        {
                            string category = string.Empty;

                            if (url.Contains("city_council"))
                            {
                                category = "Council";
                            }
                            else if (url.Contains("planning"))
                            {
                                category = "Planning Commission";
                            }
                            else
                            {
                                category = "Board of Appeal";
                            }

                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.DocType = category;
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocSource = pdfUrl;

                            string localPath = string.Format("{0}\\{1}", this.localDirectory, pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
                            localPath = HttpUtility.UrlDecode(localPath);
                            localdoc.DocLocalPath = localPath;

                            try
                            {
                                c.DownloadFile(pdfUrl, localPath);
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
