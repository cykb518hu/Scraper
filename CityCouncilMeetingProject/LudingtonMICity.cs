using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class LudingtonMICity : City
    {
        private List<String> docUrls = null;

        public LudingtonMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LudingtonMICity",
                CityName = "Ludington",
                CityUrl = "http://www.ludington.mi.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("LudingtonMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            Regex dateReg1 = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            WebClient c = new WebClient();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    string json = c.DownloadString(categoryUrl);
                    JToken docToken = JsonConvert.DeserializeObject(json) as JToken;
                    var fileTokens = docToken.SelectTokens("$..data");

                    if (fileTokens != null)
                    {
                        foreach (var fileToken in fileTokens)
                        {
                            string meetingDateText = dateReg.Match(fileToken.ToString()).ToString();
                            DateTime meetingDate = DateTime.MinValue;
                            if (!string.IsNullOrEmpty(meetingDateText))
                            {
                                meetingDate = DateTime.Parse(meetingDateText);
                            }
                            else
                            {
                                meetingDateText = dateReg1.Match(fileToken.ToString()).ToString();

                                if (string.IsNullOrEmpty(meetingDateText))
                                {
                                    Console.WriteLine("Skip...");
                                    continue;
                                }

                                meetingDate = DateTime.ParseExact(meetingDateText, "MM-dd-yy", null);
                            }
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string fileUrl = "https://ludingtoncitymi.documents-on-demand.com" + fileToken.SelectToken("$..href").ToString();
                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
