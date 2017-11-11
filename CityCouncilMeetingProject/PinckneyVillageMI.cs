using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class PinckneyVillageMI : City
    {
        private List<string> docUrls = null;

        public PinckneyVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PinckneyVillageMI",
                CityName = "Pinckney",
                CityUrl = "https://www.thetfordtwp.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PinckneyVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

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
                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string fileUrl = "https://pinckneyvillagemi.documents-on-demand.com" + fileToken.SelectToken("$..href").ToString();

                            this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
