using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Xml;

namespace CityCouncilMeetingProject
{
    public class SouthfieldMICity : City
    {
        private List<string> docUrls = null;

        public SouthfieldMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "SouthfieldMICity",
                CityName = "Southfield",
                CityUrl = "http://www.cityofsouthfield.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SouthfieldMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string url in this.docUrls)
            {
                if (url.Contains("*"))
                {
                    string category = url.Split('*')[0];
                    string apiUrl = url.Split('*')[1];
                    if (category == "Council")
                    {
                        this.ExtractCouncil(apiUrl, category, ref docs, ref queries);
                    }
                    else
                    {
                        this.ExtractCommissionMinutes(apiUrl, ref docs, ref queries);
                    }
                }
                else
                {
                    this.ExtractAgenda(url, ref docs, ref queries);
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }

        private void ExtractCouncil(string url, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            string json = c.DownloadString(url);
            json = string.Format("{0}\"root\":{1}{2}", "{", json, "}");
            XmlDocument docXml = JsonConvert.DeserializeXmlNode(json, "root");
            XmlNodeList docList = docXml.SelectNodes("//root/data");
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{1}[0-9]{1,2},[\\s]+[0-9]{2,4}");

            foreach (XmlNode docNode in docList)
            {
                string title = docNode.SelectSingleNode("./title").InnerText;
                string docUrl = "https://southfieldcitymi.documents-on-demand.com" + docNode.SelectSingleNode("./attr/href").InnerText;
                string meetingText = dateReg.Match(title).ToString();
                DateTime meetingDate = DateTime.Parse(dateReg.Match(title).ToString());
                if (meetingDate < this.dtStartFrom)
                {
                    Console.WriteLine("Too early, skip...");
                    continue;
                }
                Documents localDoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                if (localDoc == null)
                {
                    localDoc = new Documents();
                    localDoc.DocId = Guid.NewGuid().ToString();
                    localDoc.CityId = this.cityEntity.CityId;
                    localDoc.Checked = false;
                    localDoc.DocType = category;
                    localDoc.DocSource = docUrl;
                    string localPath = string.Format("{0}\\{1}_{2}", this.localDirectory, Guid.NewGuid().ToString(), docUrl.Split('/').LastOrDefault());
                    localDoc.DocLocalPath = localPath;

                    try
                    {
                        c.DownloadFile(docUrl, localPath);
                    }
                    catch
                    {
                    }

                    docs.Add(localDoc);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("{0} already downloaded...", docUrl);
                    Console.ResetColor();
                }

                this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
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
                Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
            }
        }

        private void ExtractCommissionMinutes(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{1}[0-9]{1,2},[\\s]+[0-9]{2,4}");
            string category = "Planning Commission";
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            HtmlDocument minuteDoc = web.Load(url);
            HtmlNodeCollection docNodeList = minuteDoc.DocumentNode.SelectNodes("//div[@id='dnn_ctr1988_Display_HtmlHolder']//a[contains(text(),'(pdf)')]");

            if (docNodeList != null && docNodeList.Count > 0)
            {
                foreach (HtmlNode docNode in docNodeList)
                {
                    string docUrl = this.cityEntity.CityUrl + docNode.Attributes["href"].Value;
                    DateTime meetingDate = DateTime.MinValue;
                    bool isDate = DateTime.TryParse(dateReg.Match(docNode.InnerText).ToString(), out meetingDate);

                    if (!isDate)
                    {
                        Regex dateReg1 = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
                        isDate = DateTime.TryParseExact(dateReg1.Match(docUrl).ToString(), "MM-dd-yy", null, System.Globalization.DateTimeStyles.None, out meetingDate);
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("too early, skip...");
                        continue;
                    }

                    Documents localDoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                    if (localDoc == null)
                    {
                        localDoc = new Documents();
                        localDoc.DocId = Guid.NewGuid().ToString();
                        localDoc.CityId = this.cityEntity.CityId;
                        localDoc.DocType = category;
                        localDoc.Checked = false;
                        localDoc.DocSource = docUrl;

                        string localFileName = string.Format("{0}\\{1}_{2}", this.localDirectory, Guid.NewGuid().ToString(), docUrl.Split('/').LastOrDefault().Split('?').FirstOrDefault());
                        localDoc.DocLocalPath = localFileName;

                        try
                        {
                            c.DownloadFile(docUrl, localFileName);
                        }
                        catch
                        {
                        }

                        docs.Add(localDoc);
                    }
                    else
                    {
                        Console.WriteLine("{0} already downloaded", docUrl);
                    }

                    this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                    QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                    if (qr == null)
                    {
                        qr = new QueryResult();
                        qr.DocId = localDoc.DocId;
                        qr.CityId = this.cityEntity.CityId;
                        qr.SearchTime = DateTime.Now;
                        qr.MeetingDate = meetingDate;
                        
                        queries.Add(qr);
                    }

                    this.ExtractQueriesFromDoc(localDoc, ref qr);
                    Console.WriteLine("{0} docs, {1} queries...", docs.Count, queries.Count);
                }
            }
        }

        private void ExtractAgenda(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{1}[0-9]{1,2},[\\s]+[0-9]{2,4}");
            string category = "Planning Commission";
            HtmlWeb web = new HtmlWeb();
            HtmlDocument archiveDoc = web.Load(url);
            HtmlNodeCollection archiveMonthList = archiveDoc.DocumentNode.SelectNodes("//a[@class='archivedisplaymonthlink']");
            HtmlNode currentNode = archiveDoc.DocumentNode.SelectSingleNode("//a[text()='Current']");
            archiveMonthList.Insert(0, currentNode);
            foreach (HtmlNode archiveNode in archiveMonthList)
            {
                Regex digitReg = new Regex("[0-9]{4}");
                int year = archiveNode.InnerText == "Current" ? 2017 : int.Parse(digitReg.Match(archiveNode.InnerText).Value);
                if (year < this.dtStartFrom.Year)
                {
                    Console.WriteLine("Too early, skip...");
                    continue;
                }

                Console.WriteLine("Working on {0}...", archiveNode.InnerText);
                string monthUrl = archiveNode.Attributes["href"].Value;
                HtmlDocument monthDoc = web.Load(monthUrl);
                HtmlNode pageNode = monthDoc.DocumentNode.SelectSingleNode("//a[text()='Current']/parent::td/following-sibling::td");
                int totalPage = pageNode.SelectNodes("./a").Count;
                HtmlNodeCollection meetingNodes = monthDoc.DocumentNode.SelectNodes("//span[@class='newstitle']/a");

                for (int page = 1; page <= totalPage; page++)
                {
                    if (page > 1)
                    {
                        Console.WriteLine("Go to page {0}...", page);
                        monthUrl = monthUrl.Replace("/158/", string.Format("/158/nnpg1480/{0}/", page));
                        monthDoc = web.Load(monthUrl);
                        meetingNodes = monthDoc.DocumentNode.SelectNodes("//span[@class='newstitle']/a");
                    }

                    foreach (HtmlNode meetingNode in meetingNodes)
                    {
                        string meetingUrl = meetingNode.Attributes["href"].Value;
                        string meetingTitle = meetingNode.InnerText;
                        bool goIn = meetingTitle.Contains("Planning Commission") && meetingTitle.ToLower().Contains("cancelled") == false;
                        goIn = goIn || (meetingTitle.ToLower().Contains("city council"));
                        if (goIn)
                        {
                            string meetingAgendaUrl = meetingNode.Attributes["href"].Value;
                            Documents localDoc = docs.FirstOrDefault(t => t.DocSource == meetingAgendaUrl);
                            DateTime meetingDate = DateTime.MinValue;

                            if (localDoc == null)
                            {
                                localDoc = new Documents();
                                localDoc.DocType = category;
                                localDoc.DocId = Guid.NewGuid().ToString();
                                localDoc.CityId = this.cityEntity.CityId;
                                localDoc.DocSource = meetingAgendaUrl;
                                string localFile = string.Format("{0}\\{1}.html",
                                    this.localDirectory,
                                    meetingAgendaUrl.Split('?').FirstOrDefault().Split('/').Reverse().ElementAt(1));
                                localDoc.DocLocalPath = localFile;
                                HtmlDocument agendaDoc = web.Load(meetingAgendaUrl);
                                HtmlNode agendaContentNode = agendaDoc.GetElementbyId("Table1");

                                if (agendaContentNode != null)
                                {
                                    File.WriteAllText(localFile, agendaContentNode.InnerHtml, Encoding.UTF8);
                                }

                                localDoc.DocBodyDic.Add(1, agendaContentNode.InnerText);
                                docs.Add(localDoc);
                            }
                            else
                            {
                                if (localDoc.DocBodyDic.Count == 0)
                                {
                                    string html = File.ReadAllText(localDoc.DocLocalPath);
                                    HtmlDocument htmlDoc = new HtmlDocument();
                                    htmlDoc.LoadHtml(html);
                                    localDoc.DocBodyDic.Add(1, htmlDoc.DocumentNode.InnerText);
                                }
                                Console.WriteLine("this file already downloaded..");
                            }

                            meetingDate = DateTime.Parse(dateReg.Match(localDoc.DocBodyDic[1]).ToString());

                            if (meetingTitle.Contains("City Council") && meetingDate <= DateTime.Now.AddDays(1-DateTime.Now.Day))
                            {
                                continue;
                            }


                            if(meetingDate < this.dtStartFrom)
                            {
                                continue;
                            }

                            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                            if (qr == null)
                            {
                                qr = new QueryResult();
                                qr.CityId = localDoc.CityId;
                                qr.DocId = localDoc.DocId;
                                qr.MeetingDate = meetingDate;
                                qr.SearchTime = DateTime.Now;
                                
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localDoc, ref qr);
                            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                        }
                    }
                }
            }
        }
    }
}
