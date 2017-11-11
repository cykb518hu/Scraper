using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Net;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class LansingCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public LansingCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LansingCharterTownshipMI",
                CityName = "Lansing Charter Township",
                CityUrl = "http://www.lansingtownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("LansingCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                string json = c.DownloadString(categoryUrl);
                JToken dataToken = JsonConvert.DeserializeObject(json) as JToken;

                if(dataToken != null)
                {
                    var docTokens = dataToken.SelectTokens("$..data");

                    if(docTokens != null)
                    {
                        foreach(JToken docToken in docTokens)
                        {
                            string title = docToken.SelectToken("$..title").ToString();
                            string docUrl = "https://lansingtwpmi.documents-on-demand.com" + docToken.SelectToken("$..href").ToString();
                            string meetingDateText = dateReg.Match(title).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);

                            if(localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.Checked = false;
                                localdoc.Important = false;
                                localdoc.DocType = category;
                                localdoc.DocSource = docUrl;
                                localdoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, docUrl.Split('/').LastOrDefault());

                                try
                                {
                                    c.DownloadFile(docUrl, localdoc.DocLocalPath);
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
                                qr.CityId = localdoc.CityId;
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
}
