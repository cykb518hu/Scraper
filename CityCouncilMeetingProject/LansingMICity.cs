using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class LansingMICity : City
    {
        public List<string> docUrls = null;

        public LansingMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LansingMICity",
                CityName = "Lansing",
                CityUrl = "http://www.lansingmi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("LansingMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string docUrl in docUrls)
                {
                    string category = docUrl.Split('*')[0];
                    string apiUrl = string.Format(docUrl.Split('*')[1], i);

                    HtmlDocument listDoc = web.Load(apiUrl);
                    HtmlNodeCollection recordNodes = listDoc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

                    if (recordNodes != null && recordNodes.Count > 0)
                    {
                        foreach (HtmlNode recordNode in recordNodes)
                        {
                            HtmlNode dateNode = recordNode.SelectSingleNode(".//strong");
                            string dateText = dateReg.Match(dateNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(dateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNodeCollection docUrlsNodes = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]");
                            List<string> docFileUrls = docUrlsNodes == null ?
                                null :
                                docUrlsNodes.Select(t => this.cityEntity.CityUrl + t.Attributes["href"].Value)
                                .Where(t => t.ToLower().Contains("previous") == false)
                                .Distinct()
                                .ToList();

                            if (docFileUrls != null)
                            {
                                foreach (string fileUrl in docFileUrls)
                                {
                                    Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                                    if (localdoc == null)
                                    {
                                        localdoc = new Documents();
                                        localdoc.CityId = this.cityEntity.CityId;
                                        localdoc.Checked = false;
                                        localdoc.DocId = Guid.NewGuid().ToString();
                                        localdoc.DocSource = fileUrl;
                                        localdoc.DocType = category;

                                        string localFileName = string.Format("{0}\\{1}_{2}_{3}.pdf",
                                            this.localDirectory,
                                            category,
                                            meetingDate.ToString("yyyy-MM-dd"),
                                            fileUrl.ToLower().Contains("agenda") ? "agenda" : "minutes"
                                            );

                                        try
                                        {
                                            c.DownloadFile(fileUrl, localFileName);
                                        }
                                        catch
                                        {
                                        }

                                        localdoc.DocLocalPath = localFileName;
                                        docs.Add(localdoc);
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine("File already downloaded....");
                                        Console.ResetColor();
                                    }

                                    this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                    if (qr == null)
                                    {
                                        qr = new QueryResult();
                                        qr.CityId = this.cityEntity.CityId;
                                        qr.DocId = localdoc.DocId;
                                        qr.MeetingDate = meetingDate;
                                        qr.SearchTime = DateTime.Now;

                                        queries.Add(qr);
                                    }

                                    this.ExtractQueriesFromDoc(localdoc, ref qr);
                                    Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                                }

                                this.SaveMeetingResultsToSQL(docs, queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
