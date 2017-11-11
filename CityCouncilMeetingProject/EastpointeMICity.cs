using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class EastpointeMICity : City
    {
        private List<string> docUrls = null;

        public EastpointeMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "EastpointeMICity",
                CityName = "Eastpointe",
                CityUrl = "http://www.cityofeastpointe.net/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("EastpointeMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string listUrl = url.Split('*')[1];
                Console.WriteLine("Working on {0}...", listUrl);

                string json = c.DownloadString(listUrl);
                var jsonDoc = JsonConvert.DeserializeObject(json) as JToken;

                if (jsonDoc != null)
                {
                    var fileUrlsNodes = jsonDoc.SelectTokens("$..href");

                    if (fileUrlsNodes != null)
                    {
                        foreach (var fileUrlNode in fileUrlsNodes)
                        {
                            string fileUrl = "https://eastpointecitymi.documents-on-demand.com" + fileUrlNode.ToString();
                            string meetingDateText = dateReg.Match(fileUrl).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                            if(localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocSource = fileUrl;
                                localdoc.Important = false;
                                localdoc.Checked = false;
                                localdoc.DocType = category;
                                string localPath = string.Format("{0}\\{1}", this.localDirectory, fileUrl.Split('/').LastOrDefault());
                                localdoc.DocLocalPath = localPath;

                                try
                                {
                                    c.DownloadFile(fileUrl, localPath);
                                }
                                catch { }

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
                                qr.DocId = localdoc.DocId;
                                qr.CityId = localdoc.CityId;
                                qr.MeetingDate = meetingDate;
                                qr.SearchTime = DateTime.Now;
                                queries.Add(qr);
                            }

                            this.ExtractQueriesFromDoc(localdoc, ref qr);
                            Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                            this.SaveMeetingResultsToSQL(docs, queries);
                        }
                    }
                }
            }
        }
    }
}
