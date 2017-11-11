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
    public class YpsilantiMICity : City
    {
        public YpsilantiMICity()
        {
            this.cityEntity = new CityInfo();
            cityEntity.CityId = "YpsilantiMICity";
            cityEntity.StateCode = "MI";
            cityEntity.CityName = "Ypsilanti";
            cityEntity.CityUrl = "http://cityofypsilanti.com/AgendaCenter";

            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            List<Documents> docs = this.LoadDocumentsDoneSQL();

            Dictionary<string, int> categoryDic = new Dictionary<string, int>();
            categoryDic.Add("Council", 10);
            categoryDic.Add("Planning Commission", 12);
            categoryDic.Add("Zoning Board of Appeals", 13);
            string urlTemplate = "http://cityofypsilanti.com/AgendaCenter/UpdateCategoryList?year={0}&catID={1}&startDate=&endDate=&term=&prevVersionScreen=false";
            int startYear = this.dtStartFrom.Year;
            int endYear = DateTime.Now.Year;

            for (int i = startYear; i <= endYear; i++)
            {
                Console.WriteLine("Working on year {0}...", startYear);

                foreach (string key in categoryDic.Keys)
                {
                    Console.WriteLine("Working on category {0}...", key);
                    string listUrl = string.Format(urlTemplate, i, categoryDic[key]);
                    HtmlDocument listDoc = web.Load(listUrl);

                    HtmlNodeCollection docNodeList = listDoc.DocumentNode.SelectNodes("//tr[@class='catAgendaRow']");

                    if (docNodeList != null && docNodeList.Count > 0)
                    {
                        foreach (HtmlNode docNode in docNodeList)
                        {
                            ExtractFilesFromNode(docNode, key, ref docs, ref queries);
                        }
                    }
                }
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void ExtractFilesFromNode(HtmlNode node, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlNode minuteNode = node.SelectSingleNode(".//td[@class='minutes']/a");
            HtmlNode dateNode = node.SelectSingleNode("./td/h4/a[@href]");
            
            string minuteUrl = minuteNode == null ? string.Empty : "http://cityofypsilanti.com" + minuteNode.Attributes["href"].Value;
            string date = dateNode == null ? string.Empty : dateNode.InnerText;
            date = date.Contains("Amended")
                ? date.Split(new string[] { "Amended" }, StringSplitOptions.None).LastOrDefault().Trim('\r', '\n', '\t', (char)32, (char)160)
                : date.Trim('\r', '\n', '\t', (char)32, (char)160);
            string agendaUrl = dateNode == null ? string.Empty : "http://cityofypsilanti.com" + dateNode.Attributes["href"].Value;

            if (!string.IsNullOrEmpty(minuteUrl))
            {
                this.ExtractOneResult(minuteUrl, string.Empty,date, category, ref docs, ref queries);
                Console.WriteLine("{0} documents added, {1} query result added...", docs.Count, queries.Count);
            }

            if(string.IsNullOrEmpty(agendaUrl) == false && agendaUrl.Contains("html=true") == false)
            {
                this.ExtractOneResult(agendaUrl,string.Empty, date, category, ref docs, ref queries);
                Console.WriteLine("{0} documents added, {1} query result added...", docs.Count, queries.Count);
            }
            else
            {
                this.ExtractAgendaFiles(agendaUrl, date, category, ref docs, ref queries);
            }
        }

        private void ExtractOneResult(string fileUrl, string fileName, string date, string category, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            string localFileName = fileName;

            if (string.IsNullOrEmpty(localFileName))
            {
                localFileName = fileUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault();
                localFileName = localFileName.ToLower().Contains("pdf") ? localFileName : localFileName + ".pdf";
            }

            string localPath = string.Format("{0}\\{1}", this.localDirectory, localFileName);
            Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

            if (localDoc == null)
            {
                Console.WriteLine("Found new document - {0}...", fileUrl);
                localDoc = new Documents();
                localDoc.CityId = this.cityEntity.CityId;
                localDoc.DocId = Guid.NewGuid().ToString();
                localDoc.DocLocalPath = localPath;
                localDoc.DocSource = fileUrl;
                localDoc.DocType = category;
                docs.Add(localDoc);

                try
                {
                    c.DownloadFile(fileUrl, localPath);
                }
                catch (Exception ex)
                {

                }
            }

            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

            if (qr == null)
            {
                qr = new QueryResult();
                qr.CityId = this.cityEntity.CityId;
                qr.DocId = localDoc.DocId;
                qr.SearchTime = DateTime.Now;
                qr.MeetingDate = DateTime.Parse(date);
                
                queries.Add(qr);
            }

            this.ReadText(false, localPath, ref localDoc);
            this.ExtractQueriesFromDoc(localDoc, ref qr);
        }
        
        public void ExtractAgendaFiles(string agendaUrl, string date, string docType, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(agendaUrl);

            HtmlNodeCollection documentNodeList = doc.DocumentNode.SelectNodes("//div[@id='divItems']//a[@href]");

            if(documentNodeList != null && documentNodeList.Count > 0)
            {
                foreach(HtmlNode documentNode in documentNodeList)
                {
                    string fileName = documentNode.InnerText;
                    string docUrl = "http://cityofypsilanti.com" + documentNode.Attributes["href"].Value;
                    this.ExtractOneResult(docUrl, fileName, date, docType, ref docs, ref queries);

                    Console.WriteLine("{0} documents added, {1} query result added...", docs.Count, queries.Count);
                }
            }
        }
    }
}
