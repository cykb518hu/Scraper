using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Data;
using System.Web;
using System.Configuration;
using MsWord = Microsoft.Office.Interop.Word;
using HtmlAgilityPack;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    public class DetroitMICity : City
    {
        private string cityCouncilUrl = "http://www.detroitmi.gov/Government/City-Council/City-Council-Sessions";
        private string planUrl = "http://www.detroitmi.gov/Government/Boards/City-Planning-Commission-Meetings";
        private string zbaCalendarUrl = "http://www.detroitmi.gov/Government/Boards/Board-of-Zoning-Appeal-Calendar";
        private List<string> docUrls = new List<string>();
        private List<int> listNoFiles = new List<int>();
        private int largest = 0;

        public DetroitMICity()
        {
            cityEntity = new CityInfo();
            cityEntity.CityId = this.GetType().Name;
            cityEntity.CityName = "Detroit";
            cityEntity.CityUrl = this.cityCouncilUrl;
            cityEntity.StateCode = "MI";
            this.localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, this.GetType().Name);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);

            this.docUrls.AddRange(File.ReadAllLines("DetroitMICity_Urls.txt"));
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            this.listNoFiles = File.Exists(string.Format("{0}_NoFiles.txt", this.GetType().Name)) ?
                File.ReadAllLines(string.Format("{0}_NoFiles.txt", this.GetType().Name)).Select(t => int.Parse(t)).ToList() :
                new List<int>();

            this.ExtractPlanningCommissionFiles(ref docs, ref queries);
            this.DownloadZBAFiles(ref docs, ref queries);
            this.SaveMeetingResultsToSQL(docs, queries);
            
            string earlistUrl = string.Empty;
            string latestUrl = string.Empty;
            this.GetEarliestMeeting(this.cityCouncilUrl, ref earlistUrl, ref latestUrl);
            string councilTemplate = this.docUrls.FirstOrDefault(t => t.StartsWith("City Council"));
            string template = councilTemplate.Split('"').FirstOrDefault().Split('*').LastOrDefault();
            Regex digitReg = new Regex("[0-9]+");
            
            //if (string.IsNullOrWhiteSpace(latestUrl))
            //{
            //    Console.WriteLine("No meeting on page...");
            //    return;
            //}

            int end = int.Parse(digitReg.Matches(latestUrl).Cast<Match>().ToList().LastOrDefault().ToString());
            if(end > this.largest)
            {
                this.largest = end;
            }
            listNoFiles.RemoveAll(t => t > this.largest);
            int start = int.Parse(councilTemplate.Split('"').LastOrDefault());
            Console.WriteLine("Start from {0}...", start);
            Console.WriteLine("End at {0}...", this.largest);

            for (int i = start; i<=this.largest ; i++)
            {
                if (listNoFiles.Contains(i))
                {
                    continue;
                }

                string councilUrl = template.Contains("{0}") ? string.Format(template, i) : template;
                HtmlDocument doc = web.Load(councilUrl);
                HtmlNodeCollection linksNodes = doc.DocumentNode.SelectNodes("//a[@href]");
                List<HtmlNode> fileLinksNodes = linksNodes != null ?
                    linksNodes.Where(t =>
                        t.Attributes["href"].Value.ToLower().Contains(".pdf") ||
                        t.Attributes["href"].Value.ToLower().Contains(".doc") ||
                        t.Attributes["href"].Value.ToLower().Contains("fileticket"))
                    .ToList() : null;

                HtmlNode dateNode = doc.DocumentNode.SelectSingleNode("//*[text()='Start Date/Time:']/parent::div");
                dateNode = dateNode == null ? null : dateNode.NextSibling.NextSibling;
                string date = dateNode == null ? string.Empty : DateTime.Parse(dateNode.InnerText).ToString("yyyy-MM-dd");

                if (fileLinksNodes != null && fileLinksNodes.Count > 0)
                {
                    foreach (HtmlNode fileNode in fileLinksNodes)
                    {
                        string pdfUrl = fileNode.Attributes["href"].Value;
                        pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://www.detroitmi.gov" + fileNode.Attributes["href"].Value;
                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource.Contains(pdfUrl));

                        if (localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.DocId = Guid.NewGuid().ToString();
                            HtmlNode categoryNode = doc.DocumentNode.SelectSingleNode("//*[text()='Category:']/parent::div");
                            categoryNode = categoryNode != null ? categoryNode.NextSibling.NextSibling : categoryNode;

                            if (categoryNode == null)
                            {
                                continue;
                            }

                            localDoc.DocType = categoryNode == null ? string.Empty : categoryNode.InnerText.Trim('\t', '\n', '\r');
                            string localFile = pdfUrl.ToLower().Contains("fileticket") ?
                                pdfUrl.Split('&').FirstOrDefault().Split('=').LastOrDefault().Replace("%", string.Empty) :
                                pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault();
                            string localFileFull = pdfUrl.ToLower().Contains("fileticket") ?
                                string.Format("{0}\\Council_{1}_{2}.pdf", this.localDirectory, localFile, date) :
                                string.Format("{0}\\{1}", localDirectory, localFile);

                            if (File.Exists(localFileFull))
                            {
                                localFileFull = string.Format("{0}\\{1}_{2}.pdf", localDirectory, Path.GetFileNameWithoutExtension(localFile), date);
                            }

                            localDoc.DocLocalPath = localFileFull;
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.DocSource = pdfUrl;

                            try
                            {
                                c.DownloadFile(pdfUrl, localFileFull);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            
                            docs.Add(localDoc);
                        }

                        this.ReadText(true, localDoc.DocLocalPath, ref localDoc);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Now search in doc {0}.", localDoc.DocLocalPath);
                        Console.ResetColor();
                        QueryResult qr = queries.FirstOrDefault(q => q.DocId == localDoc.DocId);

                        if (qr == null)
                        {
                            qr = this.ExtractMeetingInformation(doc, localDoc);
                            queries.Add(qr);
                        }
                        else
                        {
                            this.ExtractQueriesFromDoc(localDoc, ref qr);
                        }

                        Console.WriteLine("{0} queries added, {1} docs added...", queries.Count, docs.Count);
                    }
                }
                else
                {
                    listNoFiles.Add(i);
                    File.WriteAllLines(string.Format("{0}_NoFiles.txt", this.GetType().Name), listNoFiles.Select(t => t.ToString()));
                    Console.WriteLine("No files on {0}...", councilUrl);
                }
            }

            //this.docUrls.Remove(councilTemplate);
            //this.docUrls.Add(string.Format("City Council*{0}\"{1}", template, council_current));
            //File.WriteAllLines("DetroitMICity_Urls.txt", this.docUrls, Encoding.UTF8);
            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void ExtractPlanningCommissionFiles(ref List<Documents> docs, ref List<QueryResult> queries)
        {
            string category = "Planning Commission";
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            HtmlDocument doc = web.Load(this.planUrl);
            HtmlNodeCollection pdfNodeList = doc.DocumentNode.SelectNodes("//a[contains(@href,'fileticket')]");

            if (pdfNodeList != null)
            {
                foreach (HtmlNode fileNode in pdfNodeList)
                {
                    string pdfLink = fileNode.Attributes["href"].Value;
                    pdfLink = !pdfLink.StartsWith("http") ? "http://www.detroitmi.gov" + pdfLink : pdfLink;
                    Documents localDoc = docs.FirstOrDefault(t => t.DocSource.Contains(pdfLink));

                    if (localDoc == null)
                    {
                        localDoc = new Documents();
                        localDoc.DocId = Guid.NewGuid().ToString();
                        localDoc.DocSource = pdfLink;
                        localDoc.CityId = this.cityEntity.CityId;
                        localDoc.DocType = category;
                        string localDocPath = string.Format("{0}\\PC_{1}.pdf", this.localDirectory, fileNode.InnerText.Trim('\n', '\t', (char)32, (char)160));

                        try
                        {
                            c.DownloadFile(pdfLink, localDocPath);
                        }
                        catch (Exception ex)
                        {

                        }

                        localDoc.DocLocalPath = localDocPath;
                        docs.Add(localDoc);
                    }

                    this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                    QueryResult qr = queries.FirstOrDefault(q => q.DocId == localDoc.DocId);

                    if (qr == null)
                    {
                        Regex dateReg = new Regex("[a-zA-z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]+");
                        qr = new QueryResult();

                        qr.DocId = localDoc.DocId;
                        qr.CityId = localDoc.CityId;
                        qr.MeetingDate = DateTime.Parse(dateReg.Match(fileNode.InnerText).ToString());
                        qr.SearchTime = DateTime.Now;
                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localDoc, ref qr);
                    Console.WriteLine("{0} documents added, {1} queries added...", docs.Count, queries.Count);
                }
            }
        }

        public QueryResult ExtractMeetingInformation(HtmlDocument doc, Documents localDoc)
        {
            QueryResult qr = new QueryResult();
            qr.CityId = localDoc.CityId;
            qr.DocId = localDoc.DocId;
            qr.SearchTime = DateTime.Now;
            HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("//div[@id='divEventDetailsTemplate1']/span");
            string title = titleNode == null ? string.Empty : titleNode.InnerText;
            qr.MeetingTitle = title.Trim('\r', '\t', (char)32, (char)160, '\n');

            HtmlNode dateNode = doc.DocumentNode.SelectSingleNode("//*[text()='Start Date/Time:']/parent::div");
            dateNode = dateNode == null ? null : dateNode.NextSibling.NextSibling;
            string date = dateNode == null ? string.Empty : dateNode.InnerText.Trim('\r', '\t', (char)32, (char)160, '\n');
            qr.MeetingDate = DateTime.Parse(date);

            HtmlNode locationNode = doc.DocumentNode.SelectSingleNode("//*[text()='Location:']/parent::div");
            locationNode = locationNode == null ? null : locationNode.NextSibling.NextSibling;
            qr.MeetingLocation = locationNode == null ? string.Empty : locationNode.InnerText.Trim('\r', '\t', (char)32, (char)160, '\n');

            this.ExtractQueriesFromDoc(localDoc, ref qr);

            return qr;
        }

        private void GetEarliestMeeting(string targetUrl, ref string earliestUrl, ref string latestUrl)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(targetUrl);
            HtmlNodeCollection councilMeetingUrlNodes = doc.DocumentNode.SelectNodes("//div[@class='EventDayScroll']//a[contains(@href,'ModuleID')]");

            if (councilMeetingUrlNodes != null)
            {
                earliestUrl = councilMeetingUrlNodes.FirstOrDefault().Attributes["href"].Value;
                latestUrl = councilMeetingUrlNodes.LastOrDefault().Attributes["href"].Value;
            }
        }

        private List<string> GetZBAMeetingsThisMonth()
        {
            List<string> urls = new List<string>();

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(this.zbaCalendarUrl);
            HtmlNodeCollection councilMeetingUrlNodes = doc.DocumentNode.SelectNodes("//div[@class='EventDayScroll']//a[contains(@href,'ModuleID')]");

            if (councilMeetingUrlNodes != null)
            {
                urls.AddRange(councilMeetingUrlNodes.Select(t => t.Attributes["href"].Value));
            }

            return urls;
        }

        public void DownloadZBAFiles(ref List<Documents> docs, ref List<QueryResult> queries)
        {
            //List<string> zbaUrls = this.GetZBAMeetingsThisMonth();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            string zbaUrl = this.docUrls.FirstOrDefault(t => t.StartsWith("Zoning Board of Appeals"));
            string zbaTemplate = zbaUrl.Split('*')[1];
            string earliestUrl = string.Empty;
            string latestUrl = string.Empty;
            this.GetEarliestMeeting(this.zbaCalendarUrl, ref earliestUrl, ref latestUrl);
            Regex digitReg = new Regex("[0-9]+"); 
            int early = int.Parse(digitReg.Matches(earliestUrl).Cast<Match>().LastOrDefault().ToString());
            int end = int.Parse(digitReg.Matches(latestUrl).Cast<Match>().LastOrDefault().ToString());

            if (string.IsNullOrEmpty(latestUrl))
            {
                Console.WriteLine("No more meetings...");
                return;
            }

            for (int i = early; ; i++)
            {
                if (this.listNoFiles.Contains(i))
                {
                    continue;
                }

                string zba = string.Format(zbaTemplate, i);
                HtmlDocument doc = web.Load(zba);

                HtmlNode dateNode = doc.DocumentNode.SelectSingleNode("//*[text()='Start Date/Time:']/parent::div");
                dateNode = dateNode == null ? null : dateNode.NextSibling.NextSibling;

                if (dateNode == null && i > end)
                {
                    Console.WriteLine("The last meeting...");
                    break;
                }

                if (dateNode == null)
                {
                    this.listNoFiles.Add(i);
                    Console.WriteLine("No meetings...");
                    continue;
                }

                DateTime dtMeeting = DateTime.Parse(dateNode.InnerText);

                var docNodesCollection = doc.DocumentNode.SelectNodes("//a[@href]");
                List<HtmlNode> fileLinksNodes = docNodesCollection != null ?
                docNodesCollection.Where(t =>
                    t.Attributes["href"].Value.ToLower().Contains(".pdf") ||
                    t.Attributes["href"].Value.ToLower().Contains(".doc") ||
                    t.Attributes["href"].Value.ToLower().Contains("fileticket"))
                .ToList() : null;

                if (fileLinksNodes != null && fileLinksNodes.Count > 0)
                {
                    foreach (HtmlNode fileNode in fileLinksNodes)
                    {
                        string pdfUrl = fileNode.Attributes["href"].Value;
                        pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : "http://www.detroitmi.gov" + fileNode.Attributes["href"].Value;
                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource.Contains(pdfUrl));

                        if (localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.DocId = Guid.NewGuid().ToString();
                            HtmlNode categoryNode = doc.DocumentNode.SelectSingleNode("//*[text()='Category:']/parent::div");
                            categoryNode = categoryNode != null ? categoryNode.NextSibling.NextSibling : categoryNode;

                            if (categoryNode == null || categoryNode.InnerText.ToLower().Contains("zoning ") == false)
                            {
                                continue;
                            }

                            localDoc.DocType = categoryNode == null ? string.Empty : categoryNode.InnerText.Trim('\t', '\n', '\r');
                            string localFile = pdfUrl.ToLower().Contains("fileticket") ?
                            pdfUrl.Split('&').FirstOrDefault().Split('=').LastOrDefault().Replace("%", string.Empty) :
                            pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault();
                            string localFileFull = pdfUrl.ToLower().Contains("fileticket") ?
                                string.Format("{0}\\Zoning_{1}_{2}.pdf", this.localDirectory, localFile, dtMeeting.ToString("yyyy-MM-dd")) :
                                string.Format("{0}\\{1}", localDirectory, localFile);
                            localDoc.DocLocalPath = localFileFull;
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.DocSource = pdfUrl;

                            try
                            {
                                c.DownloadFile(pdfUrl, localFileFull);
                            }
                            catch (Exception)
                            {
                                continue;
                            }

                            docs.Add(localDoc);
                        }

                        this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Now search in doc {0}.", localDoc.DocLocalPath);
                        Console.ResetColor();
                        QueryResult qr = queries.FirstOrDefault(q => q.DocId == localDoc.DocId);

                        if (qr == null)
                        {
                            qr = this.ExtractMeetingInformation(doc, localDoc);
                            queries.Add(qr);
                        }
                        else
                        {
                            this.ExtractQueriesFromDoc(localDoc, ref qr);
                        }

                        Console.WriteLine("{0} queries added, {1} docs added...", queries.Count, docs.Count);
                    }
                }
                else
                {
                    this.listNoFiles.Add(i);
                    File.WriteAllLines(string.Format("{0}_NoFiles.txt", this.GetType().Name), listNoFiles.Select(t => t.ToString()));
                    Console.WriteLine("No files on {0}...", zba);
                }

                largest = i;
            }
        }
    }
}
