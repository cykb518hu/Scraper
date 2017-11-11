using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Web;
using System.Globalization;

namespace CityCouncilMeetingProject
{
    public class ShutesburyTownMA : City
    {
        private List<string> docUrls = null;

        public ShutesburyTownMA()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ShutesburyTownMA",
                CityName = "Shutesbury",
                CityUrl = "http://www.shutesbury.org/",
                StateCode = "MA"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ShutesburyTownMA_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{2}(0|1)[0-9]{1}[0-3]{1}[0-9]{1}");

            string minuteUrl = this.docUrls[0];
            string agendaUrl = this.docUrls[1];

            this.ExtractMinutes(minuteUrl, dateReg, c, web, ref docs, ref queries);
            this.ExtractAgendas(agendaUrl, dateReg, c, web, ref docs, ref queries);
        }

        private void ExtractAgendas(string agendaUrl, Regex dateReg, WebClient c, HtmlWeb web, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlDocument yearsDoc = web.Load(agendaUrl);
            List<string> years = new List<string>();
            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                years.Add(i.ToString());
            }
            Regex yearReg = new Regex(string.Format("(^({0})$)", string.Join("|", years)));
            var targetLists = yearsDoc.DocumentNode.SelectNodes("//a[@href]").Where(t => yearReg.IsMatch(t.InnerText));

            if (targetLists != null)
            {
                foreach (HtmlNode targetNode in targetLists)
                {
                    string targetUrl = targetNode.Attributes["href"].Value;
                    targetUrl = targetUrl.StartsWith("http") ? targetUrl : this.cityEntity.CityUrl + targetUrl;
                    HtmlDocument targetDoc = web.Load(targetUrl);
                    HtmlNodeCollection entriesNodes = targetDoc.DocumentNode.SelectNodes("//div[@class='content']/div/*");

                    if (entriesNodes != null)
                    {
                        string category = string.Empty;

                        foreach (HtmlNode entryNode in entriesNodes)
                        {
                            if (entryNode.OriginalName.ToLower() == "h4")
                            {
                                string heading = HttpUtility.HtmlDecode(entryNode.InnerText).Trim((char)32, (char)160);
                                if (!string.IsNullOrEmpty(heading))
                                {
                                    category = heading.Split('(').FirstOrDefault().Trim((char)32, (char)160);
                                }
                                continue;
                            }

                            var fileNode = entryNode.SelectSingleNode(".//a[@href]");
                            if(fileNode == null)
                            {
                                continue;
                            }
                            string fileUrl =  fileNode.Attributes["href"].Value;
                            fileUrl = !fileUrl.StartsWith("http") ?
                                this.cityEntity.CityUrl.TrimEnd('/') + '/' + fileUrl.TrimStart('/') :
                                fileUrl;
                            string fileType = entryNode.InnerText.Contains(".pdf") ? "pdf" : "doc";
                            string meetingDateText = dateReg.Match(entryNode.InnerText).ToString();
                            DateTime meetingDate = DateTime.MinValue;
                            DateTime.TryParseExact(meetingDateText, "yyMMdd",null,DateTimeStyles.None, out meetingDate);
                            this.ExtractADoc(c, fileUrl, category, fileType, meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }

        private void ExtractMinutes(string minuteUrl, Regex dateReg, WebClient c, HtmlWeb web, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            string minuteTemplete = "http://www.shutesbury.org/minutes/search?tid=All&min=&max=&items_per_page=50&page={0}";
            for (int i = 0; ; i++)
            {
                Console.WriteLine("Working on page {0}...", i + 1);
                string page = i == 0 ? minuteUrl : string.Format(minuteTemplete, i);
                HtmlDocument doc = web.Load(page);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//table[@class='table table-hover table-striped']/tbody/tr");

                if (entryNodes != null && entryNodes.Count > 0)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        HtmlNode categoryNode = entryNode.SelectSingleNode("./td");
                        string category = categoryNode.InnerText.Split('(').FirstOrDefault().Trim((char)32, (char)160);
                        HtmlNode dateNode = entryNode.SelectSingleNode("./td[2]");
                        //string meetingDateText = dateNode.InnerText;
                        //DateTime meetingDate = DateTime.Parse(meetingDateText);

                        //if (meetingDate < this.dtStartFrom)
                        //{
                        //    Console.WriteLine("Earlier, skip");
                        //    continue;
                        //}

                        HtmlNode docNode = entryNode.SelectSingleNode("./td[3]//a[contains(@title,' in new window')]");
                        string docUrl = docNode.Attributes["href"].Value;
                        docUrl = docUrl.StartsWith("http") ? docUrl : this.cityEntity.CityUrl.TrimEnd('/') + '/' + docUrl.TrimStart('/');
                        string meetingDateText = entryNode.SelectSingleNode("./td[2]").InnerText.Trim('\r', '\n', (char)32, (char)160);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("MEETING DATE: {0}...", meetingDateText);
                        Console.ResetColor();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip..");
                            continue;
                        }

                        string docType = docNode.InnerText.EndsWith("pdf") ? "pdf" : "doc";
                        this.ExtractADoc(c, docUrl, category, docType, meetingDate, ref docs, ref queries);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Reach last page...");
                    Console.ResetColor();
                    break;
                }
            }
        }
    }
}
