using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class NoviMICity : City
    {
        private List<string> docUrls = null;

        public NoviMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "NoviMICity",
                CityName = "Novi",
                CityUrl = "http://www.cityofnovi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("NoviMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();

            foreach (string url in docUrls)
            {
                for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[@class='tablewithheadingresponsive']/tbody/tr[@class]");

                    if (entryNodes != null)
                    {
                        foreach (HtmlNode entryNode in entryNodes)
                        {
                            HtmlNode meetingDateNode = entryNode.SelectSingleNode("./td");
                            string meetingDateText = meetingDateNode.InnerText.Split('-').FirstOrDefault().Trim((char)32, (char)160);
                            DateTime meetingDate = string.IsNullOrEmpty(meetingDateText) ? DateTime.MinValue : DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            HtmlNodeCollection minuteDocNodes = entryNode.SelectNodes(".//div[@class='pdf']/a[text()='Minutes']");

                            if (minuteDocNodes != null)
                            {
                                foreach (HtmlNode minuteNode in minuteDocNodes)
                                {
                                    string pdfUrl = minuteNode.Attributes["href"].Value;
                                    pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                                    this.ExtractADoc(c, pdfUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }

                            HtmlNodeCollection actionSummaryNodes = entryNode.SelectNodes(".//div[@class='pdf']/a[text()='Action Summary']");

                            if (actionSummaryNodes != null)
                            {
                                foreach (HtmlNode actionSummaryNode in actionSummaryNodes)
                                {
                                    string pdfUrl = actionSummaryNode.Attributes["href"].Value;
                                    pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                                    this.ExtractADoc(c, pdfUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                            }

                            HtmlNodeCollection agendasNodes = entryNode.SelectNodes(".//div[@class='html']/a[text()='Agenda/ Packet']");

                            if (agendasNodes != null)
                            {
                                foreach (HtmlNode agendaNode in agendasNodes)
                                {
                                    string pdfUrl = agendaNode.Attributes["href"].Value;
                                    pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                                    string bodyXPath = "//*[@id='interiorcontenttext']";
                                    this.ExtractADoc(c, pdfUrl, category, "html:" + bodyXPath, meetingDate, ref docs, ref queries);

                                    HtmlDocument agendaDoc = web.Load(pdfUrl);
                                    HtmlNodeCollection docNodeList = agendaDoc.DocumentNode.SelectNodes("//img[@src='/Images/IconsClipArtGraphics/Icon-PDFSmall.aspx']/following-sibling::a");

                                    if (docNodeList != null)
                                    {
                                        foreach(HtmlNode docNode in docNodeList)
                                        {
                                            pdfUrl = docNode.Attributes["href"].Value;
                                            pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                                            this.ExtractADoc(c, pdfUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExtractMinutes(HtmlDocument doc, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            HtmlNodeCollection docNodeCollection = doc.DocumentNode.SelectNodes("//table[@class='tablewithheadingresponsive']/tbody/tr[@class]");

            if (docNodeCollection != null)
            {
                foreach (HtmlNode destinationNode in docNodeCollection)
                {
                    HtmlNode meetingDateNode = destinationNode.SelectSingleNode("./td");
                    string meetingDateText = meetingDateNode.InnerText.Split('-').FirstOrDefault().Trim((char)32, (char)160);
                    DateTime meetingDate = string.IsNullOrEmpty(meetingDateText) ? DateTime.MinValue : DateTime.Parse(meetingDateText);
                    HtmlNodeCollection minuteDocNode = destinationNode.SelectNodes(".//div[@class='pdf']/a[text()='Minutes']");

                    if (minuteDocNode == null || minuteDocNode.Count == 0)
                    {
                        continue;
                    }

                    foreach (HtmlNode docNode in minuteDocNode)
                    {
                        string pdfUrl = docNode.Attributes["href"].Value;
                        pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                        if (localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.DocId = Guid.NewGuid().ToString();
                            localDoc.DocSource = pdfUrl;
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.DocType = category;
                            string localFilePath = string.Format("{0}\\Minutes_{1}_{2}.pdf",
                                this.localDirectory,
                                Path.GetFileNameWithoutExtension(pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault()),
                                Guid.NewGuid().ToString());
                            localDoc.DocLocalPath = localFilePath;
                            localDoc.Checked = false;

                            try
                            {
                                c.DownloadFile(pdfUrl, localFilePath);
                            }
                            catch
                            {
                            }

                            docs.Add(localDoc);
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
                            qr.CityId = localDoc.CityId;
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;
                            qr.DocId = localDoc.DocId;

                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localDoc, ref qr);
                        Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                    }
                }
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void ExtractAgendas(HtmlDocument doc, string url, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            try
            {
                WebClient c = new WebClient();
                HtmlNodeCollection agendasNodes = doc.DocumentNode.SelectNodes("//table[@class='tablewithheadingresponsive']/tbody/tr[@class]");

                if (agendasNodes != null)
                {
                    foreach (HtmlNode agendaNode in agendasNodes)
                    {
                        HtmlNode meetingDateNode = agendaNode.SelectSingleNode("./td");
                        string meetingDateText = meetingDateNode.InnerText.Split('-').FirstOrDefault().Trim((char)32, (char)160);
                        DateTime meetingDate = string.IsNullOrEmpty(meetingDateText) ? DateTime.MinValue : DateTime.Parse(meetingDateText);
                        HtmlNode agendaDocNode = agendaNode.SelectSingleNode(".//div[@class='html']/a");

                        string fileUrl = agendaDocNode == null ? string.Empty : agendaDocNode.Attributes["href"].Value;

                        if (string.IsNullOrEmpty(fileUrl))
                        {
                            continue;
                        }

                        fileUrl = fileUrl.ToLower().StartsWith("http") ? fileUrl : this.cityEntity.CityUrl + fileUrl;
                        Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);
                        HtmlDocument agendaHtmlDoc = new HtmlDocument();

                        if (localDoc == null)
                        {
                            localDoc = new Documents();
                            localDoc.CityId = this.cityEntity.CityId;
                            localDoc.Checked = false;
                            localDoc.DocType = category;
                            localDoc.DocId = Guid.NewGuid().ToString();
                            localDoc.Readable = true;
                            string html = c.DownloadString(fileUrl);
                            agendaHtmlDoc.LoadHtml(html);
                            HtmlNode agendaBodyNode = agendaHtmlDoc.GetElementbyId("interiorcontenttext");
                            string localFile = string.Format("{0}\\{1}_{2}", this.localDirectory, Guid.NewGuid().ToString(), fileUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());
                            File.WriteAllText(localFile, html, Encoding.UTF8);
                            localDoc.DocBodyDic.Add(1, agendaBodyNode.InnerText);
                            docs.Add(localDoc);
                        }
                        else
                        {
                            string html = File.ReadAllText(localDoc.DocLocalPath);
                            agendaHtmlDoc.LoadHtml(html);
                            HtmlNode agendaBodyNode = agendaHtmlDoc.GetElementbyId("interiorcontenttext");
                            if (localDoc.DocBodyDic.Count == 0)
                            {
                                localDoc.DocBodyDic.Add(1, agendaDocNode.InnerText);
                            }
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("This document already downloaded...");
                            Console.ResetColor();
                        }

                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.DocId = localDoc.DocId;
                            qr.CityId = localDoc.CityId;
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;

                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localDoc, ref qr);
                        Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                        this.ExtractMoreAgenda(agendaHtmlDoc, category, meetingDate, ref docs, ref queries);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("DEBUG EXCEPTION:{0}", ex.ToString());
                Console.WriteLine("CURRENT URL: {0}", url);
                Console.ResetColor();
            }
            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void ExtractMoreAgenda(HtmlDocument moreDoc, string category, DateTime meetingDate, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            HtmlNodeCollection docNodeList = moreDoc.DocumentNode.SelectNodes("//img[@src='/Images/IconsClipArtGraphics/Icon-PDFSmall.aspx']/following-sibling::a");

            if (docNodeList != null && docNodeList.Count > 0)
            {
                foreach (HtmlNode docNode in docNodeList)
                {
                    string pdfUrl = docNode.Attributes["href"].Value;
                    pdfUrl = !pdfUrl.StartsWith("http") ? this.cityEntity.CityUrl + pdfUrl : pdfUrl;
                    Documents localDoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                    if (localDoc == null)
                    {
                        localDoc = new Documents();
                        localDoc.DocId = Guid.NewGuid().ToString();
                        localDoc.DocSource = pdfUrl;
                        localDoc.CityId = this.cityEntity.CityId;
                        localDoc.DocType = category;
                        string localFilePath = string.Format("{0}\\Agenda_{1}_{2}.pdf",
                            this.localDirectory,
                            Path.GetFileNameWithoutExtension(pdfUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault()),
                            Guid.NewGuid().ToString());
                        localDoc.DocLocalPath = localFilePath;
                        localDoc.Checked = false;

                        try
                        {
                            c.DownloadFile(pdfUrl, localFilePath);
                        }
                        catch
                        {
                        }

                        docs.Add(localDoc);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("The file already downloaded...");
                        Console.ResetColor();
                    }

                    this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                    if (qr == null)
                    {
                        qr = new QueryResult();
                        qr.DocId = localDoc.DocId;
                        qr.CityId = localDoc.CityId;
                        qr.MeetingDate = meetingDate;
                        qr.SearchTime = DateTime.Now;

                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localDoc, ref qr);
                    Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                }
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }
    }
}
