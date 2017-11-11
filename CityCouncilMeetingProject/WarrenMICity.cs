using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    /// <summary>
    /// http://www.cityofwarren.org/index.php/council-agenda
    /// http://www.cityofwarren.org/index.php/government/zoning-board-of-appeals
    /// </summary>
    class WarrenMICity : City
    {
        private Dictionary<string, string> meetingUrlMap = new Dictionary<string, string>();

        public WarrenMICity()
        {
            this.cityEntity = new CityInfo();
            cityEntity.CityId = "WarrenMICity";
            cityEntity.StateCode = "MI";
            cityEntity.CityName = "Warren";
            cityEntity.CityUrl = "http://www.cityofwarren.org/index.php/council-agenda";

            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);

            meetingUrlMap.Add("Council", "http://www.cityofwarren.org/index.php/council-agenda");
            meetingUrlMap.Add("Zoning board", "http://www.cityofwarren.org/index.php/government/zoning-board-of-appeals");
            meetingUrlMap.Add("Planning Commission", "http://www.cityofwarren.org/index.php/planning-commission/114-planning-commission/189-planning-commission-agendas");
        }

        public void DownloadCouncilPdfFiles()
        {
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            Console.WriteLine("Will download pdf files from {0}...", this.dtStartFrom.ToString("yyyy-MM-dd"));

            foreach (string key in meetingUrlMap.Keys)
            {
                Console.WriteLine("Working on {0} files...", key);
                string meetingUrl = meetingUrlMap[key];
                HtmlDocument doc = web.Load(meetingUrl);
                HtmlNodeCollection docNodeList = doc.DocumentNode.SelectNodes("//a[text()='Agenda']/ancestor::p");

                if (docNodeList != null)
                {
                    foreach (HtmlNode docNode in docNodeList)
                    {
                        Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]+");
                        string dateText = dateReg.IsMatch(docNode.InnerText) ?
                            dateReg.Match(docNode.InnerText).ToString() :
                            string.Empty;

                        if (DateTime.Parse(dateText) < this.dtStartFrom)
                        {
                            Console.WriteLine("Earlier than {0}, skip...", dtStartFrom);
                            continue;
                        }

                        var targetLinkNodes = docNode.SelectNodes(".//a[@href]").Where(t =>
                            t.InnerText == "Agenda" || t.InnerText == "Minutes" || t.InnerText == "Approved Minutes");

                        if (targetLinkNodes != null && targetLinkNodes.Count() > 0)
                        {
                            foreach (HtmlNode docLinkNode in targetLinkNodes)
                            {
                                string docUrl = docLinkNode.Attributes["href"].Value;
                                docUrl = docUrl.StartsWith("http") ? docUrl : string.Format("http://www.cityofwarren.org{0}", docUrl);

                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource.Contains(docUrl));
                                if (localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.CityId = cityEntity.CityId;
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.DocType = key;
                                    string localDocPath = string.Format("{0}\\{1}", localDirectory, docUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
                                    localdoc.DocSource = docUrl;
                                    localdoc.DocLocalPath = localDocPath;

                                    try
                                    {
                                        c.DownloadFile(docUrl, localDocPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("File {0} failed to download due to {1}...", docUrl, ex.ToString());
                                        continue;
                                    }

                                    docs.Add(localdoc);
                                }

                                this.ReadText(false,localdoc.DocLocalPath, ref localdoc);
                                #region extract to another method ReadText()
                                //string content = string.Empty;
                                //bool readable = this.ReadPdf(localdoc.DocLocalPath, out content);

                                //if (readable == false || (readable = true && string.IsNullOrEmpty(content)))
                                //{
                                //    Console.ForegroundColor = ConsoleColor.Yellow;
                                //    Console.WriteLine("File {0} cannot read, OCR!", localdoc.DocLocalPath);
                                //    Console.ResetColor();
                                //    Dictionary<int, string> docBodyDic = new Dictionary<int, string>();
                                //    this.OCRPdf(localdoc.DocLocalPath, ref docBodyDic);
                                //    StringBuilder contentBuilder = new StringBuilder();

                                //    foreach (int page in docBodyDic.Keys)
                                //    {
                                //        contentBuilder.AppendFormat("{0} ", docBodyDic[page].ToString());
                                //    }

                                //    content = contentBuilder.ToString();
                                //}

                                //localdoc.DocBody = content;
                                //localdoc.Readable = readable;
                                #endregion
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                if (qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.QueryId = Guid.NewGuid().ToString();
                                    qr.DocId = localdoc.DocId;
                                    qr.SearchTime = DateTime.Now;
                                    qr.MeetingDate = DateTime.Parse(dateText);
                                    qr.CityId = this.cityEntity.CityId;
                                    
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localdoc, ref qr);

                                Console.WriteLine("{0} query results saved...", queries.Count);
                                Console.WriteLine("{0} docs saved...", docs.Count);
                            }
                        }
                    }
                }
            }

            queries.RemoveAll(t => t.Entries.Count == 0);
            this.SaveMeetingResultsToSQL(docs, queries);
        }
    }
}
