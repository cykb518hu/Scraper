﻿//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class WestBloomfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public WestBloomfieldCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WestBloomfieldCharterTownshipMI",
                CityName = "West Bloomfield Charter Township",
                CityUrl = "http://wbtownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WestBloomfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("([a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}|[0-9]{1,2}-[0-9]{1,2}-)[0-9]{4}");

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                foreach (string url in this.docUrls)
                {
                    string category = url.Split('*')[0];
                    string categoryUrl = string.Format(url.Split('*')[1], i);
                    c.Headers.Add("user-agent", "chrome");
                    string json = c.DownloadString(categoryUrl);
                    var dataToken = JsonConvert.DeserializeObject(json) as JToken;
                    var entryTokens = dataToken.SelectTokens("$..data");

                    if (entryTokens != null)
                    {
                        foreach (var entryToken in entryTokens)
                        {
                            string meetingUrl = "https://westbloomfieldtwpmi.documents-on-demand.com" + entryToken.SelectToken("$..href").ToString();
                            string meetingDateText = dateReg.Match(entryToken.ToString()).ToString();

#if debug
                            bool isMatch = dateReg.IsMatch(meetingUrl);
                            if (isMatch)
                            {
                                Console.WriteLine("No problem, continue");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Not match {0}...", meetingUrl);
                                continue;
                            }
#endif
                            
                            DateTime meetingDate = DateTime.MinValue;
                            try
                            {
                                meetingDate = DateTime.Parse(meetingDateText);
                            }
                            catch
                            {
                                meetingDateText = dateReg.Match(meetingUrl).ToString();
                                meetingDate = DateTime.Parse(meetingDateText);
                            }
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            this.ExtractADoc(c, meetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
