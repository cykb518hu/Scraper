using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class PontiacMICity :City
    {
        private List<string> docUrls = null;

        public PontiacMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "PontiacMICity",
                CityName = "Pontiac",
                CityUrl = "http://www.pontiac.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PontiacMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            
            foreach(string url in this.docUrls)
            {
                string category = url.ToLower().Contains("council") ? "City Council" : string.Empty;

                if(category == "City Council")
                {
                    ExtractCouncil(url, ref docs, ref queries);
                }
                else
                {
                    this.ExtractOthers(url, ref docs, ref queries);
                }

                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }

        public void ExtractCouncil(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]+");
            HtmlDocument doc = web.Load(url);
            HtmlNodeCollection pdfFileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");
            List<HtmlNode> targetFilesNodes = null;
            List<int> years = new List<int>();
            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                years.Add(i);
            }
            
            if(pdfFileNodes!= null)
            {
                targetFilesNodes = pdfFileNodes.Where(t => years.Exists(y => t.InnerText.Contains(y.ToString()))).ToList();
            }

            foreach(HtmlNode pdfFileNode in targetFilesNodes)
            {
                string dateText = dateReg.Match(pdfFileNode.InnerText).ToString();
                DateTime meetingDate = string.IsNullOrEmpty(dateText) ?
                    DateTime.MinValue :
                    DateTime.Parse(dateText);
                string pdfUrl = pdfFileNode.Attributes["href"].Value;
                pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : this.cityEntity.CityUrl.Trim('/') + "/" + pdfUrl.TrimStart('/');
                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);
                
                if(localdoc == null)
                {
                    localdoc = new Documents();
                    localdoc.Important = false;
                    localdoc.Checked = false;
                    localdoc.CityId = this.cityEntity.CityId;
                    localdoc.DocId = Guid.NewGuid().ToString();
                    localdoc.DocSource = pdfUrl;
                    localdoc.DocType = "City Council";
                    localdoc.DocLocalPath = string.Format("{0}\\City Council_{1}",
                        this.localDirectory,
                        pdfUrl.Split('?').LastOrDefault().Split('/').LastOrDefault());

                    try
                    {
                        c.DownloadFile(pdfUrl, localdoc.DocLocalPath);
                    }
                    catch { }

                    docs.Add(localdoc);
                }
                else
                {
                    Console.WriteLine("This file already downloaded...");
                }

                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);

                if(meetingDate == DateTime.MinValue)
                {
                    if(localdoc.DocBodyDic.Count >0)
                    {
                        if (dateReg.IsMatch(localdoc.DocBodyDic[1]))
                        {
                            meetingDate = DateTime.Parse(dateReg.Match(localdoc.DocBodyDic[1]).ToString());
                        }
                    }
                }

                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                if(qr == null)
                {
                    qr = new QueryResult();
                    qr.CityId = localdoc.CityId;
                    qr.DocId = localdoc.DocId;
                    qr.SearchTime = DateTime.Now;
                    qr.MeetingDate = meetingDate;
                    
                    queries.Add(qr);
                }

                this.ExtractQueriesFromDoc(localdoc, ref qr);
                Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }

        public void ExtractOthers(string url, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]+");
            HtmlDocument doc = web.Load(url);
            HtmlNodeCollection pdfFileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");
            List<HtmlNode> targetFilesNodes = null;
            List<int> years = new List<int>();
            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                years.Add(i);
            }

            string[] tagsToSearch = { "zoning", "planning", "appeals" };

            if (pdfFileNodes != null)
            {
                targetFilesNodes = pdfFileNodes.Where(t => years.Exists(y => t.InnerText.Contains(y.ToString()))).ToList();
                foreach(string tag in tagsToSearch)
                {
                    targetFilesNodes = targetFilesNodes.Where(t => t.Attributes["href"].Value.Contains("tag")).ToList();
                }
            }

            foreach(HtmlNode pdfNode in targetFilesNodes)
            {
                DateTime meetingDate = DateTime.MinValue;

                string pdfUrl = pdfNode.Attributes["href"].Value;
                pdfUrl = pdfUrl.StartsWith("http") ? pdfUrl : this.cityEntity.CityUrl.TrimEnd('/') + "/" + pdfUrl.TrimStart('/');
                string category = string.Empty;

                if (pdfUrl.Contains("planning"))
                {
                    category = "Planning Commission";
                }
                else if (pdfUrl.Contains("zoning"))
                {
                    category = "Zoning Board";
                }
                else if (pdfUrl.Contains("appeals"))
                {
                    category = "Board of Appeals";
                }

                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == pdfUrl);

                if (localdoc == null)
                {
                    localdoc = new Documents();
                    localdoc.Important = false;
                    localdoc.Checked = false;
                    localdoc.CityId = this.cityEntity.CityId;
                    localdoc.DocId = Guid.NewGuid().ToString();
                    localdoc.DocSource = pdfUrl;
                    localdoc.DocType = category;
                    localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}",
                        this.localDirectory,
                        category,
                        pdfUrl.Split('?').LastOrDefault().Split('/').LastOrDefault());

                    try
                    {
                        c.DownloadFile(pdfUrl, localdoc.DocLocalPath);
                    }
                    catch { }

                    docs.Add(localdoc);
                }
                else
                {
                    Console.WriteLine("This file already downloaded...");
                }

                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);

                if (meetingDate == DateTime.MinValue)
                {
                    if (localdoc.DocBodyDic.Count > 0)
                    {
                        if (dateReg.IsMatch(localdoc.DocBodyDic[1]))
                        {
                            meetingDate = DateTime.Parse(dateReg.Match(localdoc.DocBodyDic[1]).ToString());
                        }
                    }
                }

                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                if (qr == null)
                {
                    qr = new QueryResult();
                    qr.CityId = localdoc.CityId;
                    qr.DocId = localdoc.DocId;
                    qr.SearchTime = DateTime.Now;
                    qr.MeetingDate = meetingDate;
                    
                    queries.Add(qr);
                }

                this.ExtractQueriesFromDoc(localdoc, ref qr);
                Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                this.SaveMeetingResultsToSQL(docs, queries);
            }
        }
    }
}
