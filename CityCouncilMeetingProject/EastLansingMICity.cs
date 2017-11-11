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
    public class EastLansingMICity : City
    {
        private List<string> docUrls = null;

        public EastLansingMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "EastLansingMICity",
                CityName = "East Lansing",
                CityUrl = "https://www.cityofeastlansing.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("EastLansingMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach(string url in this.docUrls)
            {
                if (url.Contains("AgendaCenter"))
                {
                    ExtractZoningBoardOfAppeals(url, ref docs, ref queries);
                }
                else
                {
                    ExtractOthers(url, ref docs, ref queries);
                }
            }
        }

        public void ExtractZoningBoardOfAppeals(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            HtmlDocument listDoc = null;
            try
            {
                string cookie = "ASP.NET_SessionId=xcaakihobtkitu5scj0uhsat; CP_IsMobile=false";
                listDoc = new HtmlDocument();
                string html = this.GetHtml(url, cookie);
                listDoc.LoadHtml(html);
                //listDoc = web.Load(url);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("DEBUG:\r\n");
                Console.WriteLine("URL:{0}...", url);
                Console.WriteLine("EXCEPTION:{0}...", ex.ToString());
                Console.ResetColor();
            }
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
                        List<string> fileUrls = new List<string>();
                        var agendaNodes = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                            .Where(t => !t.Attributes["href"].Value.ToLower().Contains("/agenda/"));
                        if(agendaNodes != null)
                        {
                            foreach(HtmlNode agendaNode in agendaNodes)
                            {
                                string agendaUrl = agendaNode.Attributes["href"].Value;
                                agendaUrl = agendaUrl.StartsWith("http") ? agendaUrl : this.cityEntity.CityUrl + agendaUrl;
                                fileUrls.Add(agendaUrl);
                            }
                        }

                        HtmlNode minuteNode = recordNode.SelectNodes(".//a[contains(@href,'ViewFile')]")
                            .FirstOrDefault(t => t.Attributes["href"].Value.ToLower().Contains("minutes"));
                        string minuteUrl = minuteNode == null ? string.Empty : minuteNode.Attributes["href"].Value;
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
                                localdoc.DocType = "Zoning Board of Appeals";
                                string localFileName = string.Format("{0}\\{1}_{2}_{3}.pdf",
                                    this.localDirectory,
                                    "Zoning Board of Appeals",
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

        public void ExtractOthers(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            HtmlNode cityCouncilPanelNode = doc.GetElementbyId("tabCityCouncil");
            HtmlNode cityCouncilWorkStationPanelNode = doc.GetElementbyId("tabCityCouncilWorkSession");
            HtmlNode planningCommissionPanelNode = doc.GetElementbyId("tabPlanningCommission");
            this.ExtractFiles(cityCouncilPanelNode, "City Council", ref docs, ref queries);
            this.ExtractFiles(cityCouncilWorkStationPanelNode, "City Council", ref docs, ref queries);
            this.ExtractFiles(planningCommissionPanelNode, "Planning Commission", ref docs, ref queries);
        }

        public void ExtractFiles(HtmlNode cityCouncilNode, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            List<HtmlNode> recordsNodes = new List<HtmlNode>();
            HtmlNodeCollection oddNodes = cityCouncilNode.SelectNodes(".//tr[@class='odd']");
            HtmlNodeCollection evenNodes = cityCouncilNode.SelectNodes(".//tr[@class='even']");
            StringBuilder yearBuilder = new StringBuilder();
            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(i.ToString());
                yearBuilder.Append("|");
            }
            string years = yearBuilder.ToString().Trim('|').Replace("20", string.Empty);
            Regex dateReg = new Regex("[0-9]{2}/[0-9]{2}/(" + years + ")");

            if(oddNodes != null)
            {
                recordsNodes.AddRange(oddNodes);
            }

            if(evenNodes != null)
            {
                recordsNodes.AddRange(evenNodes);
            }

            if(recordsNodes.Count > 0)
            {
                recordsNodes = recordsNodes.Where(t => dateReg.IsMatch(t.InnerText)).ToList();

                foreach(HtmlNode recordNode in recordsNodes)
                {
                    HtmlNode dateNode = recordNode.SelectSingleNode("./td[2]");
                    string dateText = dateReg.Match(dateNode.InnerText).ToString();
                    DateTime meetingDate = DateTime.ParseExact(dateText, "MM/dd/yy", null);
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip...");
                        continue;
                    }
                    HtmlNode agendaNode = recordNode.SelectSingleNode(".//a[text()='Agenda']");
                    HtmlNode minuteNode = recordNode.SelectSingleNode(".//a[text()='Minutes']");
                    Dictionary<string, string> fileUrlDic = new Dictionary<string, string>();

                    if(agendaNode != null)
                    {
                        fileUrlDic.Add("agenda", agendaNode.Attributes["href"].Value);
                    }

                    if(minuteNode != null)
                    {
                        fileUrlDic.Add("minute", minuteNode.Attributes["href"].Value);
                    }

                    foreach(string key in fileUrlDic.Keys)
                    {
                        string docFileUrl = fileUrlDic[key];
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docFileUrl);

                        if(localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocSource = docFileUrl;
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.Important = false;
                            localdoc.Checked = false;
                            localdoc.DocType = category;

                            if (docFileUrl.Contains("Minute"))
                            {
                                localdoc.DocLocalPath = string.Format("{0}\\{1}_Minute_{2}_{3}.pdf",
                                    localDirectory,
                                    category,
                                    meetingDate.ToString("yyyy-MM-dd"),
                                    Guid.NewGuid().ToString());
                                try
                                {
                                    c.DownloadFile(docFileUrl, localdoc.DocLocalPath);
                                }
                                catch
                                {
                                }

                                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                            }
                            else
                            {
                                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.html", localDirectory, category, Guid.NewGuid().ToString());
                                try
                                {
                                    string html = c.DownloadString(docFileUrl);
                                    File.WriteAllText(localdoc.DocLocalPath, html, Encoding.UTF8);
                                    HtmlDocument agendaDoc = new HtmlDocument();
                                    agendaDoc.LoadHtml(html);
                                    localdoc.DocBodyDic.Add(1, agendaDoc.DocumentNode.InnerText);
                                }
                                catch
                                {

                                }
                            }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("This file already downloaded...");
                            Console.ResetColor();

                            if (localdoc.DocLocalPath.ToLower().Contains("pdf"))
                            {
                                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                            }
                            else
                            {
                                string html = File.ReadAllText(localdoc.DocLocalPath);
                                HtmlDocument pageContent = new HtmlDocument();
                                pageContent.LoadHtml(html);
                                localdoc.DocBodyDic.Add(1, pageContent.DocumentNode.InnerText);
                            }
                        }

                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);
                        if(qr == null)
                        {
                            qr = new QueryResult();
                            qr.CityId = this.cityEntity.CityId;
                            qr.DocId = localdoc.DocId;
                            qr.MeetingDate = meetingDate;
                            qr.SearchTime = DateTime.Now;
                            
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                        this.SaveMeetingResultsToSQL(docs, queries);
                    }
                }
            }
        }
    }
}
