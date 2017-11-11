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
    public class MarshallMICity : City
    {
        private List<string> docUrls = null;

        public MarshallMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MarshallMICity",
                CityName = "Marshall",
                CityUrl = "http://cityofmarshall.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("MarshallMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg1 = new Regex("[0-9]{2}.[0-9]{2}.[0-9]{2}");
            Regex dateReg2 = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");
            Regex dateReg3 = new Regex("[0-9]{4}[\\s]+[a-zA-Z]+[\\s]+[0-9]{1,2}");
            Dictionary<string, string> categoryKeywordMap = new Dictionary<string, string>();
            categoryKeywordMap.Add(" PC ", "Planning Commission");
            categoryKeywordMap.Add("Planning", "Planning Commission");
            categoryKeywordMap.Add("ZBA", "Zoning Board of Appeals");
            categoryKeywordMap.Add("Zoning Board", "Zoning Board of Appeals");
            categoryKeywordMap.Add("Council", "City Council");

            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNodeCollection docNodeCollection = doc.DocumentNode.SelectNodes("//dt[@class='pdf']/a[@href]");

            if (docNodeCollection != null)
            {
                Dictionary<string, List<HtmlNode>> docNodeMap = new Dictionary<string, List<HtmlNode>>();

                foreach (string key in categoryKeywordMap.Keys)
                {
                    List<HtmlNode> nodes = docNodeCollection.Where(t => t.InnerText.Contains(key)).ToList();

                    if (!docNodeMap.ContainsKey(categoryKeywordMap[key]))
                    {
                        docNodeMap.Add(categoryKeywordMap[key], nodes);
                    }
                    else
                    {
                        docNodeMap[categoryKeywordMap[key]].AddRange(nodes.ToList());
                    }
                }

                foreach (string key in docNodeMap.Keys)
                {
                    foreach (HtmlNode fileNode in docNodeMap[key])
                    {
                        string fileUrl = this.cityEntity.CityUrl + fileNode.Attributes["href"].Value;
                        DateTime meetingDate = DateTime.MinValue;

                        if (dateReg1.IsMatch(fileNode.InnerText))
                        {
                            meetingDate = DateTime.ParseExact(dateReg1.Match(fileNode.InnerText).ToString(), "MM.dd.yy", null);
                        }
                        else if (dateReg2.IsMatch(fileNode.InnerText))
                        {
                            meetingDate = DateTime.Parse(dateReg2.Match(fileNode.InnerText).ToString());
                        }
                        else if (dateReg3.IsMatch(fileNode.InnerText))
                        {
                            meetingDate = DateTime.Parse(dateReg3.Match(fileNode.InnerText).ToString());
                        }

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("too early...");
                            continue;
                        }

                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if(localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.DocType = key;
                            localdoc.DocSource = fileUrl;
                            localdoc.Important = false;
                            localdoc.Checked = false;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());

                            try
                            {
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch (Exception ex) { }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.WriteLine("This file already downloaded....");
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if(qr == null)
                        {
                            qr = new QueryResult();
                            qr.CityId = this.cityEntity.CityId;
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
    }
}
