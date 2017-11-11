using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Xml;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace CityCouncilMeetingProject
{
    class MadisonHeightsMICity : City
    {
        private List<string> docUrls = null;

        public MadisonHeightsMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "MadisonHeightsMICity",
                CityName = "Madison Heights",
                CityUrl = "http://www.madison-heights.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MadisonHeightsMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            foreach (string url in this.docUrls)
            {
                if (url.Contains("{0}"))
                {
                    ExtractAgendas(url, c, ref docs, ref queries);
                }
                else
                {
                    continue;
                    int startPage = int.Parse(url.Split('*')[0]);
                    string docUrl = url.Split('*')[1];
                    ExtractMinutes(docUrl, c, startPage, ref docs, ref queries);
                }
            }
        }

        private void ExtractMinutes(string url, WebClient c, int startPage, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            string category = string.Empty;
            if (url.Contains("council"))
            {
                category = "council";
            }
            else if (url.Contains("planning"))
            {
                category = "Planning Commission";
            }
            else if (url.Contains("ZBA"))
            {
                category = "Zoning Board of Appeals";
            }

            string archiveFile = string.Format("{0}\\{1}", this.localDirectory, Path.GetFileName(url));

            try
            {
                if (File.Exists(archiveFile))
                {
                    File.Delete(archiveFile);
                }

                c.DownloadFile(url, archiveFile);
            }
            catch (Exception ex)
            {
            }

            var filesDic = this.ExtractContent(archiveFile, startPage);
            Documents localDoc = null;
            foreach (string key in filesDic.Keys)
            {
                localDoc = new Documents();
                localDoc.DocId = Guid.NewGuid().ToString();
                localDoc.CityId = this.cityEntity.CityId;
                localDoc.DocLocalPath = archiveFile;
                localDoc.DocBodyDic = filesDic[key];
                localDoc.DocSource = url;
                localDoc.Checked = false;
                localDoc.Readable = true;
                //docs.Add(localDoc);

                DateTime meetingDate = DateTime.MinValue;
                if (key.Contains("-"))
                {
                    int[] items = key.Split('-').Select(t => int.Parse(t)).ToArray();
                    meetingDate = new DateTime(items[2] + 2000, items[0], items[1]);
                }
                else
                {
                    meetingDate = DateTime.Parse(key);
                }

                QueryResult qr = new QueryResult();
                qr.CityId = localDoc.CityId;
                qr.DocId = localDoc.DocId;
                qr.MeetingDate = meetingDate;
                qr.SearchTime = DateTime.Now;
                
                //queries.Add(qr);
                this.ExtractQueriesFromDoc(localDoc, ref qr);
                QueryResult qr1 = queries.FirstOrDefault(t => t.Equals(qr));

                if (qr.Entries.Count > 0)
                {
                    if (!queries.Exists(t => t.Equals(qr)))
                    {
                        queries.Add(qr);
                        docs.Add(localDoc);
                    }
                }

                queries.RemoveAll(t => t.ToList1().Count == 0);
                Console.WriteLine("{0} docs, {1} queries...", docs.Count, queries.Count);
            }

            if(!docs.Exists(t=>t.DocLocalPath == localDoc.DocLocalPath))
            {
                docs.Add(localDoc);
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private Dictionary<string, Dictionary<int, string>> ExtractContent(string pdfFile, int startPage)
        {
            Dictionary<string, Dictionary<int, string>> filesDic = new Dictionary<string, Dictionary<int, string>>();
            Dictionary<int, string> pagesDic = new Dictionary<int, string>();
            PdfReader r = new PdfReader(pdfFile);
            int pages = r.NumberOfPages;
            var dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            var dateReg1 = new Regex("[a-zA-Z]+[\\s]*[0-9]{1,2},[\\s]*[0-9]{2,4}");

            for (int i = startPage; i <= pages; i++)
            {
                string text = PdfTextExtractor.GetTextFromPage(r, i);

                if (!string.IsNullOrEmpty(text))
                {
                    pagesDic.Add(i, text);
                }
            }

            string date = string.Empty;

            foreach (int page in pagesDic.Keys)
            {
                string pageBody = pagesDic[page];
                date = dateReg.IsMatch(pageBody) ?
                    dateReg.Matches(pageBody).Cast<Match>().LastOrDefault().ToString() :
                    string.Empty;
                if (string.IsNullOrEmpty(date) || int.Parse(date.Split('-')[0]) > 12)
                {
                    date = dateReg1.IsMatch(pageBody) ?
                        dateReg1.Match(pageBody).ToString() :
                        date;
                }

                if (!filesDic.ContainsKey(date))
                {
                    filesDic.Add(date, new Dictionary<int, string>());
                }

                filesDic[date].Add(page, pagesDic[page]);
            }

            return filesDic;
        }

        private void ExtractAgendas(string url, WebClient c, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            List<string> dates = new List<string>();
            DateTime month = this.dtStartFrom;

            while (month <= DateTime.Now)
            {
                dates.Add(month.ToString("yyyy-MM"));
                month = month.AddMonths(1);
            }

            foreach (string date in dates)
            {
                string apiUrl = string.Format(url, date);
                string xml = c.DownloadString(apiUrl);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                var events = doc.DocumentElement.SelectNodes("//event[@id]");

                if (events != null && events.Count > 0)
                {
                    foreach (XmlNode eventNode in events)
                    {
                        string eventName = eventNode.SelectSingleNode("./name").InnerText.ToLower();
                        string category = string.Empty;

                        if (eventName.Contains("city council"))
                        {
                            category = "City Council";
                        }
                        else if (eventName.Contains("planning commission"))
                        {
                            category = "Planning Commission";
                        }
                        else if (eventName.Contains("zoning"))
                        {
                            category = "Zoning Board of Appeals";
                        }

                        if (string.IsNullOrEmpty(category) == false)
                        {
                            string dateText = eventNode.SelectSingleNode("./date_begin").InnerText;
                            DateTime meetingDate = DateTime.Parse(dateText);

                            string detail = eventNode.SelectSingleNode("./detail").InnerText;

                            if (!string.IsNullOrEmpty(detail))
                            {
                                HtmlNode detailNode = HtmlNode.CreateNode(detail);
                                detailNode = detailNode.Name.ToLower() == "a" ?
                                   detailNode :
                                   detailNode.SelectSingleNode(".//a[@href]");
                                string pdfUrl = detailNode == null ? string.Empty : detailNode.Attributes["href"].Value;
                                pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : this.cityEntity.CityUrl + pdfUrl.Trim('.');
                                Documents localDoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                                if (localDoc == null)
                                {
                                    localDoc = new Documents();
                                    localDoc.DocId = Guid.NewGuid().ToString();
                                    localDoc.CityId = this.cityEntity.CityId;
                                    localDoc.Checked = false;
                                    localDoc.DocType = category;
                                    localDoc.DocSource = pdfUrl;
                                    docs.Add(localDoc);
                                    string localDocFile = string.Format("{0}\\{1}", this.localDirectory, pdfUrl.Split('/').LastOrDefault().Split('?').FirstOrDefault());
                                    localDoc.DocLocalPath = localDocFile;

                                    try
                                    {
                                        c.DownloadFile(pdfUrl, localDocFile);
                                    }
                                    catch
                                    {
                                    }
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("This document already downloaded...");
                                    Console.ResetColor();
                                }

                                this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                                if (qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.DocId = localDoc.DocId;
                                    qr.CityId = this.cityEntity.CityId;
                                    qr.MeetingDate = meetingDate;
                                    qr.SearchTime = DateTime.Now;
                                    
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localDoc, ref qr);
                                Console.WriteLine("{0} docs, {1} queries...", docs.Count, queries.Count);
                            }
                        }
                    }

                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }
        }
    }
}
