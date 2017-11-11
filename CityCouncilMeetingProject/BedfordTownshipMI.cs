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
    public class BedfordTownshipMI : City
    {
        private List<string> docUrls = null;

        public BedfordTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "BedfordTownshipMI",
                CityName = "Bedford Township",
                CityUrl = "http://www.bedfordmi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BedfordTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{4}");

            HtmlDocument doc = web.Load(this.docUrls.FirstOrDefault());
            HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//*[text()='Township Board']/ancestor::table/tbody/tr[position()>2]");
            
            if(fileNodes != null)
            {
                foreach(HtmlNode fileNode in fileNodes)
                {
                    HtmlNodeCollection tds = fileNode.SelectNodes("./td");
                   
                    for(int i = 0; i < 6; i++)
                    {
                        string category = string.Empty;

                        if (i == 0 || i == 1)
                        {
                            category = "City Council";
                        }
                        else if (i == 2 || i == 3)
                        {
                            category = "Planning Commission";
                        }
                        else if(i == 4 || i == 5)
                        {
                            category = "Zoning Board of Appeals";
                        }

                        HtmlNode currentNode = tds[i];
                        HtmlNodeCollection docNodes = currentNode.SelectNodes("./a");

                        if(docNodes != null)
                        {
                            foreach(HtmlNode docNode in docNodes)
                            {
                                string meetingDocUrl = this.cityEntity.CityUrl + "/" + docNode.Attributes["href"].Value;
                                string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                                
                                if (string.IsNullOrEmpty(meetingDateText))
                                {
                                    Console.WriteLine("No file...");
                                    continue;
                                }

                                DateTime meetingDate = DateTime.Parse(meetingDateText);

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early...");
                                    continue;
                                }
                                
                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == meetingDocUrl);

                                if(localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.Important = false;
                                    localdoc.Checked = false;
                                    localdoc.DocSource = meetingDocUrl;
                                    localdoc.DocType = category;
                                    localdoc.DocLocalPath = string.Format("{0}\\{1}",
                                        this.localDirectory,
                                        meetingDocUrl.Split('/').LastOrDefault());

                                    try
                                    {
                                        c.DownloadFile(meetingDocUrl, localdoc.DocLocalPath);
                                    }
                                    catch (Exception ex) { }

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
    }
}
