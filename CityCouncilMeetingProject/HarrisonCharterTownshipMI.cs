using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class HarrisonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public HarrisonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HarrisonCharterTownshipMI",
                CityName = "Harrison Charter Township",
                CityUrl = "http://www.harrison-township.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("HarrisonCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("(0|1)[0-9]{7}");
            Regex dateReg1 = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[0-9]{4}");

            foreach(string url in this.docUrls)
            {
                Console.WriteLine("Working on {0}...", url);

                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                string dataJson = c.DownloadString(categoryUrl);
                JToken dataToken = JsonConvert.DeserializeObject(dataJson) as JToken;

                if(dataToken != null)
                {
                    var docTokens = dataToken.SelectTokens("$..href");

                    if(docTokens != null)
                    {
                        foreach(JToken docToken in docTokens)
                        {
                            string docUrl = "https://harrisontwpmi.documents-on-demand.com" + docToken.ToString();
                            DateTime meetingDate = DateTime.MinValue;

                            if (dateReg.IsMatch(docUrl))
                            {
                                string meetingDateText = dateReg.Match(docUrl).ToString();
                                meetingDate = DateTime.ParseExact(meetingDateText, "MMddyyyy", null);

                            }
                            else if (dateReg1.IsMatch(docUrl))
                            {
                                string meetingDateText = dateReg1.Match(docUrl).ToString();
                                meetingDate = DateTime.Parse(meetingDateText);
                            }
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
                                localdoc.DocType = category;
                                localdoc.DocSource = docUrl;
                                localdoc.Checked = false;
                                localdoc.Important = false;
                                localdoc.DocLocalPath = string.Format("{0}\\{1}", this.localDirectory, docUrl.Split('/').LastOrDefault());

                                try
                                {
                                    c.DownloadFile(docUrl, localdoc.DocLocalPath);
                                }
                                catch (Exception ex)
                                {
                                }

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
                                qr.MeetingDate = meetingDate;
                                qr.SearchTime = DateTime.Now;
                                qr.CityId = this.cityEntity.CityId;
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
}
