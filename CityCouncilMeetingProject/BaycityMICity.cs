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
    public class BaycityMICity : City 
    {
        private List<string> docUrls = null;

        public BaycityMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BaycityMICity",
                CityName = "Bay City",
                CityUrl = "http://www.baycitymi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("BaycityMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument listDoc = web.Load(categoryUrl);
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
                                .Where(t => !t.Attributes["href"].Value.Contains("html"))
                                .FirstOrDefault(t => t.Attributes["href"].Value
                                .ToLower().Contains("/agenda/")) :
                                dateNode.ParentNode;
                            string agendaUrl = agendaNode.Attributes["href"].Value;
                            agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                            HtmlNode minuteNode = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                                .FirstOrDefault(t => t.Attributes["href"].Value.ToLower().Contains("minutes"));
                            string minuteUrl = minuteNode == null ? string.Empty : minuteNode.Attributes["href"].Value;
                            List<string> fileUrls = new List<string>();
                            fileUrls.Add(agendaUrl);
                            if (!string.IsNullOrEmpty(minuteUrl))
                            {
                                minuteUrl = minuteUrl.StartsWith("http") ? minuteUrl : this.cityEntity.CityUrl + minuteUrl;
                                fileUrls.Add(minuteUrl);
                            }

                            foreach (string fileUrl in fileUrls)
                            {
                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);
                                string tag = fileUrl.ToLower().Contains("minute") ? "minute" : "agenda";

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
                                        tag);
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
    }
}
