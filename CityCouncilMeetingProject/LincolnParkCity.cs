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
    public class LincolnParkCity : City
    {
        public LincolnParkCity()
        {
            this.cityEntity = new CityInfo();
            cityEntity.CityId = "LincolnparkMICity";
            cityEntity.StateCode = "MI";
            cityEntity.CityName = "Lincolnpark";
            cityEntity.CityUrl = "http://www.lincolnpark.govoffice.com";

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
            Regex dateReg = new Regex("[a-zA-z]+[\\s]{0,2}[0-9]+,[\\s]{0,2}[0-9]+");

            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Dictionary<string, List<string>> meetingCategoryMap = new Dictionary<string, List<string>>();
            this.LoadUrls(ref meetingCategoryMap);

            foreach (string key in meetingCategoryMap.Keys)
            {
                Console.WriteLine("working on {0}...", key);

                foreach (string line in meetingCategoryMap[key])
                {
                    HtmlDocument doc = new HtmlDocument();

                    try
                    {
                        doc = web.Load(line);
                    }
                    catch
                    {
                        string html = this.GetHtml(line, string.Empty);
                        doc.LoadHtml(html);
                    }

                    List<HtmlNode> targetDocList = new List<HtmlNode>();

                    if (line.Contains("bria2"))
                    {
                        HtmlNodeCollection agendaList = doc.DocumentNode.SelectNodes("//a[text()='Agenda']");

                        if (agendaList != null)
                        {
                            targetDocList.AddRange(agendaList.Where(t => t.Attributes.Contains("href")));
                        }

                        HtmlNodeCollection minutesList = doc.DocumentNode.SelectNodes("//a[text()='Minutes']");

                        if (minutesList != null)
                        {
                            targetDocList.AddRange(minutesList.Where(t => t.Attributes.Contains("href")));
                        }
                    }
                    else
                    {
                        HtmlNodeCollection docNodeList = doc.DocumentNode.SelectNodes("//div[@class='attachment']/a");
                        if (docNodeList != null)
                        {
                            targetDocList.AddRange(docNodeList);
                        }

                        HtmlNodeCollection archivedDocNodes = doc.DocumentNode.SelectNodes("//h3[text()='Archived Minutes']/ancestor::aside//a[@href]");
                        if (archivedDocNodes != null)
                        {
                            var targetArchiveList = archivedDocNodes.Where(t =>
                                {
                                    string date = dateReg.Match(t.InnerText).ToString();
                                    return DateTime.Parse(date) > dtStartFrom;
                                }).ToList();

                            targetDocList.AddRange(targetArchiveList);
                        }
                    }

                    ExtractDocsFromNodeList(key, targetDocList, ref docs, ref queries);
                }
            }

            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void ExtractDocsFromNodeList(string category, List<HtmlNode> nodeList, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();

            foreach (HtmlNode docNode in nodeList)
            {
                string url = docNode.Attributes["href"].Value.StartsWith("http") ?
                    docNode.Attributes["href"].Value :
                    "http://www.lincolnpark.govoffice.com" + docNode.Attributes["href"].Value;
                DateTime meetingDate = this.ExtractDate(url);

                if (meetingDate < this.dtStartFrom)
                {
                    Console.WriteLine("Date:{0}, Earlier than {1}...", meetingDate.ToString("yyyy-MM-dd"), this.dtStartFrom.ToString("yyyy-MM-dd"));
                    continue;
                }

                Documents doc = docs.FirstOrDefault(t => t.DocSource.Contains(url));

                if (doc == null)
                {
                    Console.WriteLine("Found new document on {0}...", url);
                    doc = new Documents();
                    doc.CityId = this.cityEntity.CityId;
                    doc.DocId = Guid.NewGuid().ToString();
                    doc.DocType = category;
                    doc.DocSource = url;
                    docs.Add(doc);

                    string localFileName = doc.DocSource.Split('?').FirstOrDefault().Split('/').LastOrDefault();
                    string localPath = string.Format("{0}\\{1}", this.localDirectory, localFileName);
                    localPath = File.Exists(localPath) ?
                        localPath.Replace(Path.GetExtension(localPath), string.Format("_{0}{1}", meetingDate.ToString("yyyy-MM-dd"), Path.GetExtension(localPath))) :
                        localPath;
                    doc.DocLocalPath = localPath;

                    try
                    {
                        c.DownloadFile(doc.DocSource, localPath);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }

                this.ReadText(false,doc.DocLocalPath, ref doc);
                QueryResult qr = queries.FirstOrDefault(t => t.DocId == doc.DocId);

                if (qr == null)
                {
                    qr = new QueryResult();
                    qr.CityId = doc.CityId;
                    qr.DocId = doc.DocId;
                    qr.MeetingDate = meetingDate;
                    qr.SearchTime = DateTime.Now;
                    
                    queries.Add(qr);
                }

                this.ExtractQueriesFromDoc(doc, ref qr);
                Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
            }
        }

        private DateTime ExtractDate(string urlToMatch)
        {
            Regex dtReg1 = new Regex("[0-9]{1,2}_[0-9]{1,2}_[0-9]{4}");
            Regex dtReg2 = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}");
            Regex dtReg3 = new Regex("[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}");
            Regex dtReg4 = new Regex("[0-9]{1,2}_[0-9]{1,2}_[0-9]{2}");
            Regex dtReg5 = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            DateTime dtConverted = DateTime.MinValue;
            string dateText = string.Empty;

            if (dtReg1.IsMatch(urlToMatch))
            {
                dateText = dtReg1.Match(urlToMatch).ToString();
                try
                {
                    dtConverted = DateTime.ParseExact(dateText, "MM_dd_yyyy", null);
                }
                catch
                {
                    dtConverted = DateTime.ParseExact(dateText, "M_d_yyyy", null);
                }
            }
            else if (dtReg2.IsMatch(urlToMatch))
            {
                dateText = dtReg2.Match(urlToMatch).ToString();
                try
                {
                    dtConverted = DateTime.ParseExact(dateText, "MM-dd-yyyy", null);
                }
                catch
                {
                    dtConverted = DateTime.ParseExact(dateText, "M-d-yyyy", null);
                }
            }
            else if (dtReg3.IsMatch(urlToMatch))
            {
                dateText = dtReg3.Match(urlToMatch).ToString();
                dtConverted = DateTime.Parse(dateText);
            }
            else if (dtReg4.IsMatch(urlToMatch))
            {
                dateText = dtReg4.Match(urlToMatch).ToString();
                try
                {
                    dtConverted = DateTime.ParseExact(dateText, "MM_dd_yy", null);
                }
                catch
                {
                    dtConverted = DateTime.ParseExact(dateText, "M_d_yy", null);
                }
            }
            else if (dtReg5.IsMatch(urlToMatch))
            {
                dateText = dtReg5.Match(urlToMatch).ToString();
                try
                {
                    dtConverted = DateTime.ParseExact(dateText, "MM-dd-yy", null);
                }
                catch
                {
                    dtConverted = DateTime.ParseExact(dateText, "M-d-yy", null);
                }
            }

            return dtConverted;
        }

        private void LoadUrls(ref Dictionary<string, List<string>> meetingCategoryMap)
        {
            List<string> lines = File.ReadAllLines("LincolnparkMICity_Urls.txt").ToList();

            foreach (string line in lines)
            {
                string key = line.Split('"')[0];
                string value = line.Split('"')[1];

                if (!meetingCategoryMap.ContainsKey(key))
                {
                    meetingCategoryMap.Add(key, new List<string>());
                }

                meetingCategoryMap[key].Add(value);
                //meetingCategoryMap.Add("Council", new List<string>());
                //meetingCategoryMap.Add("Planning Commission", new List<string>());
                //meetingCategoryMap.Add("Zoning Board of Appeals", new List<string>());
                //meetingCategoryMap["Planning Commission"].Add("http://www.lincolnpark.govoffice.com/index.asp?SEC=37E0ACC1-831F-4EBF-B2A5-4F52589D0DB6&DE=0E42767A-F44D-434E-B560-71694046B0DB&Type=B_BASIC");
                //meetingCategoryMap["Planning Commission"].Add("http://lincolnparkplanning.bria2.net/planning/planning-commission-archive/");
                //meetingCategoryMap["Zoning Board of Appeals"].Add("http://www.lincolnpark.govoffice.com/index.asp?SEC=37E0ACC1-831F-4EBF-B2A5-4F52589D0DB6&DE=5FEFECA7-E574-4BD8-8167-9CA84C51AE70&Type=B_BASIC");
                //meetingCategoryMap["Council"].Add("http://www.lincolnpark.govoffice.com/index.asp?SEC=63AFEF17-5D49-4A71-BA65-75BADF8D8A6B&DE=EA5791D9-CFAD-4584-89B5-1EC4E9DF7960&Type=B_BASIC");
                //meetingCategoryMap["Council"].Add("http://www.lincolnpark.govoffice.com/index.asp?SEC=574A833E-1297-4FB1-88F2-4E34E9D4B0E4&DE=738F684C-02CE-416C-94F8-84C7610C2154&Type=B_BASIC");
                //meetingCategoryMap["Council"].Add("http://www.lincolnpark.govoffice.com/index.asp?SEC=63AFEF17-5D49-4A71-BA65-75BADF8D8A6B&DE=0A03BCAF-9600-4E1C-8AE6-2542EB45F677&Type=B_BASIC");
            }
        }
    }
}
