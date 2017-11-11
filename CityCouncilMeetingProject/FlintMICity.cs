using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HtmlAgilityPack;
using System.Net;
using System.Configuration;
using System.Web;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    public class FlintMICity : City
    {
        private string councilHomePage = "https://www.cityofflint.com/city-council/";
        // {0}-start date in yyyy-MM-dd format; {1}-end date. 
        private string calendarAPIUrl = "https://clients6.google.com/calendar/v3/calendars/cityofflint.com_n135j0d1m54839d9eqf3pvnc88@group.calendar.google.com/events?calendarId=cityofflint.com_n135j0d1m54839d9eqf3pvnc88%40group.calendar.google.com&singleEvents=true&timeZone=America%2FNew_York&maxAttendees=1&maxResults=250&sanitizeHtml=true&timeMin={0}T00%3A00%3A00-04%3A00&timeMax={1}T00%3A00%3A00-04%3A00&key=AIzaSyBNlYH01_9Hc5S1J9vuFmu2nUqBZJNAXxs";
        private List<string> meetingUrls = new List<string>();
        private string otherUrl = "https://www.cityofflint.com/planning-and-development/planning-and-zoning-2/";

        public FlintMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "FlintMICity",
                CityName = "Flint",
                CityUrl = councilHomePage,
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            meetingUrls.Add("https://www.cityofflint.com/city-council/city-council-committee-agendasminutes/");
            meetingUrls.Add("https://www.cityofflint.com/city-council/city-council-agendasminutes/");
            meetingUrls.Add("https://www.cityofflint.com/zoning-division-meeting-agendas/");
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<Documents> localDocs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            //DownloadFromAnotherUrl(ref queries, ref localDocs);
            Console.WriteLine("Will download pdf files from {0}...", this.dtStartFrom.ToString("yyyy-MM-dd"));

            foreach (string docListUrl in meetingUrls)
            {
                HtmlDocument doc = web.Load(docListUrl);
                var targetResultList = doc.DocumentNode.SelectNodes("//table[contains(@id,'tablepress')]/tbody/tr");

                if (targetResultList == null)
                {
                }

                Console.WriteLine("In total {0} lines...", targetResultList.Count);

                foreach (HtmlNode resultNode in targetResultList)
                {
                    if (resultNode.InnerText.ToUpper().Contains("CANCELLED"))
                    {
                        continue;
                    }

                    List<HtmlNode> pdfFileLinks = null;

                    var pdfFileLinksNodeList = resultNode.SelectNodes(".//a[@href]");
                        
                    pdfFileLinks = pdfFileLinksNodeList == null ? 
                        new List<HtmlNode>() : 
                        pdfFileLinksNodeList.Where(t => t.Attributes["href"].Value.Contains("pdf") ||
                            t.Attributes["href"].Value.Contains("doc") ||
                            t.Attributes["href"].Value.Contains("docx"))
                            .ToList();

                    if (pdfFileLinks != null && pdfFileLinks.Count() > 0)
                    {
                        foreach (HtmlNode pdfLinkNode in pdfFileLinks)
                        {
                            DateTime dtDate = DateTime.MinValue;
                            HtmlNode dateAndCateNode = resultNode.SelectSingleNode("./td");
                            char spliter = dateAndCateNode.InnerText.Contains('\n') ? '\n' : ' ';
                            string date = dateAndCateNode == null ? string.Empty :
                                dateAndCateNode.InnerText.Split(spliter)[0].Split(' ').FirstOrDefault();
                            bool parsed = DateTime.TryParse(date, out dtDate);
                            parsed = parsed ? parsed : DateTime.TryParseExact(date, "M-d-yy", null, System.Globalization.DateTimeStyles.None, out dtDate);

                            if (dtDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Already done or too early!");
                                continue;
                            }

                            string pdfUrl = pdfLinkNode.Attributes["href"].Value;
                            Documents localdoc = localDocs.FirstOrDefault(t => t.DocSource.Contains(pdfUrl));

                            if (localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.CityId = this.cityEntity.CityId;
                                string category = string.Empty;

                                if (meetingUrls.IndexOf(docListUrl) == 2)
                                {
                                    if (pdfUrl.Contains("PC"))
                                    {
                                        category = "Planning Commission";
                                    }
                                    else if (pdfUrl.Contains("HDC"))
                                    {
                                        category = "Historic District Commission";
                                    }
                                    else if (pdfUrl.Contains("ZBA"))
                                    {
                                        category = "Zoning Board of Appeals";
                                    }
                                }
                                else
                                {
                                    category = "council";
                                }

                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.DocType = category;
                                string pdfFileName = pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault();
                                localdoc.DocLocalPath = string.Format("{0}\\{1}", localDirectory, pdfFileName);
                                //localdoc.DocLocalPath = File.Exists(localdoc.DocLocalPath) ?
                                //    localdoc.DocLocalPath.Replace(".pdf", string.Format("_{0}.pdf", date)) :
                                //    localdoc.DocLocalPath;

                                try
                                {
                                    c.DownloadFile(pdfUrl, localdoc.DocLocalPath);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("File {0} from {1} failed to download due to {2}...",
                                        localdoc.DocLocalPath,
                                        pdfUrl,
                                        ex.ToString());
                                    continue;
                                }

                                localdoc.DocSource = pdfUrl;
                                localDocs.Add(localdoc);
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
                                qr.CityId = this.cityEntity.CityId;
                                qr.DocId = localdoc.DocId;
                                qr.MeetingDate = dtDate;
                                qr.SearchTime = DateTime.Now;
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localdoc, ref qr);
                            Console.WriteLine("{0} documents saved...", localDocs.Count);
                            Console.WriteLine("{0} query results saved...", queries.Count);

                        }
                    }
                }

                this.SaveMeetingResultsToSQL(localDocs, queries);
            }
        }
    }
}
