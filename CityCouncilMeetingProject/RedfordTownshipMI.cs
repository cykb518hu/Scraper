using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class RedfordTownshipMI : City
    {
        private List<string> docUrls = null;

        public RedfordTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RedfordTownshipMI",
                CityName = "Redford Township",
                CityUrl = "http://www.redfordtwp.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("RedfordTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            for(int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach(string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    string json = c.DownloadString(categoryUrl);
                    JToken dataToken = JsonConvert.DeserializeObject(json) as JToken;

                    if(dataToken != null)
                    {
                        var docTokens = dataToken.SelectTokens("$..data");

                        if(docTokens!= null)
                        {
                            foreach(JToken docToken in docTokens)
                            {
                                string docUrl = "https://redfordtwpmi.documents-on-demand.com" + docToken.SelectToken("$..href").ToString();
                                string meetingDateText = dateReg.Match(docToken.SelectToken("$..title").ToString()).ToString();
                                DateTime meetingDate = DateTime.Parse(meetingDateText);
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }
                                this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
