using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class SouthgateMICity : City
    {
        private List<string> docUrls = null;

        public SouthgateMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SouthgateMICity",
                CityName = "Southgate",
                CityUrl = "http://www.southgatemi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("SouthgateMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(listUrl);
                HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'userfiles')]");

                if (fileNodes != null)
                {
                    foreach(HtmlNode fileNode in fileNodes)
                    {
                        string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("too early, skip...");
                            continue;
                        }

                        string fileUrl = fileNode.Attributes["href"].Value;
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if(localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.Checked = false;
                            localdoc.Important = false;
                            localdoc.DocType = category;
                            localdoc.DocSource = fileUrl;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}.pdf",
                                this.localDirectory,
                                HttpUtility.UrlDecode(fileUrl.Split('/').LastOrDefault().Split('.').FirstOrDefault()),
                                Guid.NewGuid().ToString());

                            try
                            {
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch
                            { }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.WriteLine("This file already downloaded...");
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if(qr == null)
                        {
                            qr = new QueryResult();
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;
                            qr.CityId = localdoc.CityId;
                            qr.DocId = localdoc.DocId;
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
