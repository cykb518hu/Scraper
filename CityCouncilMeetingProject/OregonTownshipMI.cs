using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class OregonTownshipMI : City
    {
        private List<string> docUrls = null;

        public OregonTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "OregonTownshipMI",
                CityName = "Oregon Township",
                CityUrl = "http://oregontownship.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("OregonTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            WebClient c = new WebClient();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    string json = c.DownloadString(categoryUrl);
                    JToken docToken = JsonConvert.DeserializeObject(json) as JToken;
                    var fileTokens = docToken.SelectTokens("$..data..href");

                    if (fileTokens != null)
                    {
                        foreach (var fileToken in fileTokens)
                        {
                            string meetingDateText = dateReg.Match(fileToken.ToString()).ToString();
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string fileUrl = "https://oregontwpmi.documents-on-demand.com" + fileToken.ToString();

                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
