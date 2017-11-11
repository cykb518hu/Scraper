using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    class ClintonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public ClintonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ClintonCharterTownshipMI",
                CityName = "Clinton Charter Township",
                CityUrl = "http://clintontownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("ClintonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("(0|1)[0-9]{1}[0-9]{2}[0-9]{2}|(0|1)[0-9]{1}-[0-9]{2}-[0-9]{2}");
            Regex dateReg1 = new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}");
            Regex digitReg = new Regex("[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//td//a[contains(@href,'.pdf')]");

                if (docNodes != null)
                {
                    foreach (HtmlNode docNode in docNodes)
                    {
                        Console.WriteLine("DEBUG:{0}...", categoryUrl);

                        string fileUrl = this.cityEntity.CityUrl + docNode.Attributes["href"].Value;
                        string meetingDateText = string.Empty;
                        DateTime meetingDate = DateTime.MinValue;
                        if (dateReg1.IsMatch(fileUrl))
                        {
                            meetingDateText = dateReg1.Match(fileUrl).ToString();
                            Console.WriteLine("DEBUG: meeting date 1: {0}", meetingDateText);
                            meetingDate = DateTime.Parse(meetingDateText);
                        }
                        else if (dateReg.IsMatch(fileUrl))
                        {
                            meetingDateText = dateReg.Match(fileUrl).ToString();
                            meetingDateText = meetingDateText.Replace("-", string.Empty);
                            Console.WriteLine("DEBUG: meeting date 2: {0}", meetingDateText);
                            meetingDate = DateTime.ParseExact(meetingDateText, "MMddyy", null);
                        }
                        else
                        {
                            string year = digitReg.Match(fileUrl.Split('/').LastOrDefault()).ToString();
                            year = year.Substring(year.Length - 2, 2);
                            year = (2000 + int.Parse(year)).ToString();
                            meetingDateText = string.Format("{0}, {1}", docNode.InnerText, year);
                            Console.WriteLine("DEBUG: meeting date 3: {0}", meetingDateText);
                            meetingDate = DateTime.Parse(meetingDateText); 
                        }
                        
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }

                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if (localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.DocType = category;
                            localdoc.DocSource = fileUrl;
                            localdoc.Important = false;
                            localdoc.Checked = false;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());

                            try
                            {
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch (Exception ex)
                            { }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.WriteLine("This file already downloaded...");
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.SearchTime = DateTime.Now;
                            qr.MeetingDate = meetingDate;
                            qr.DocId = localdoc.DocId;
                            qr.CityId = localdoc.CityId;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} docs saved, {1} queries added...", docs.Count, queries.Count);
                        this.SaveMeetingResultsToSQL(docs, queries);
                    }
                }
            }
        }
    }
}
