using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class MidlandMICity : City
    {
        private List<string> docUrls = null;

        public MidlandMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "MidlandMICity",
                CityName = "Midland",
                CityUrl = "http://cityofmidlandmi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MidlandMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string docUrl in docUrls)
            {
                string apiUrl = docUrl.Split('*')[1];
                string category = docUrl.Split('*')[0];

                HtmlDocument listDoc = web.Load(apiUrl);
                HtmlNodeCollection recordNodes = listDoc.DocumentNode.SelectNodes("//table/tbody/tr[@class='catAgendaRow']");

                if (recordNodes != null && recordNodes.Count > 0)
                {
                    foreach (HtmlNode recordNode in recordNodes)
                    {
                        try
                        {
                            HtmlNode dateNode = recordNode.SelectSingleNode(".//strong");
                            string dateText = dateReg.Match(dateNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.Parse(dateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNode agendaNode = dateNode == null ?
                                recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                                .Where(t => t.Attributes["href"].Value.Contains("html"))
                                .FirstOrDefault(t => t.Attributes["href"].Value
                                .ToLower().Contains("agenda")) :
                                dateNode.ParentNode;
                            string agendaUrl = agendaNode.Attributes["href"].Value;
                            agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                            this.ExtractAgendas(meetingDate, agendaUrl, category, ref docs, ref queries);
                            HtmlNode minuteNode = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                                .FirstOrDefault(t => t.Attributes["href"].Value.ToLower().Contains("minutes"));

                            if (minuteNode != null)
                            {
                                string minuteUrl = minuteNode.Attributes["href"].Value;
                                minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == minuteUrl);

                                if (localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.Checked = false;
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.DocSource = minuteUrl;
                                    localdoc.DocType = category;
                                    string localFileName = string.Format("{0}\\{1}_{2}_{3}.pdf",
                                        this.localDirectory,
                                        category,
                                        meetingDate.ToString("yyyy-MM-dd"),
                                        "minute");
                                    try
                                    {
                                        c.DownloadFile(minuteUrl, localFileName);
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
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("DEBUG EXCEPTION:{0}", ex.ToString());
                            Console.WriteLine("DATA: {0}", recordNode.InnerHtml);
                        }
                    }
                    
                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }
        }

        private void ExtractAgendas(DateTime meetingDate, string agendaHtmlUrl, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            List<string> agendaFileUrlList = new List<string>();
            WebClient c = new WebClient();

            if (agendaHtmlUrl.Contains("html"))
            {
                HtmlWeb web = new HtmlWeb();
                HtmlDocument agendaDoc = web.Load(agendaHtmlUrl);

                HtmlNodeCollection agendaFileNodeCollection = agendaDoc.DocumentNode.SelectNodes("//a[contains(@href,'ViewFile')]");

                if (agendaFileNodeCollection != null && agendaFileNodeCollection.Count > 0)
                {
                    foreach (HtmlNode agendaNode in agendaFileNodeCollection)
                    {
                        string aFileUrl = agendaNode.Attributes["href"].Value;
                        aFileUrl = aFileUrl.StartsWith("http") ? aFileUrl : this.cityEntity.CityUrl + aFileUrl;
                        agendaFileUrlList.Add(string.Format("{0}*{1}", agendaNode.InnerText.Replace(".pdf", string.Empty).Trim('\t', '\r', '\n', (char)32, (char)160), aFileUrl));
                    }
                }
            }
            else
            {
                agendaFileUrlList.Add("*" + agendaHtmlUrl);
            }

            foreach (string agendaFileUrl in agendaFileUrlList)
            {
                string fileNameTag = agendaFileUrl.Split('*')[0];
                fileNameTag = string.IsNullOrEmpty(fileNameTag) ? "agenda" : fileNameTag;
                string fileUrl = agendaFileUrl.Split('*')[1];
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
                        fileNameTag);
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
                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
