//#define debug
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
    public class WyandotteMICity : City
    {
        private List<string> docUrls = null;

        public WyandotteMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WyandotteMICity",
                CityName = "Wyandotte",
                CityUrl = "http://wyandotte.net",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WyandotteMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[1-9]{1}[0-9]{1}[0-1]{1}[0-9]{1}[0-3]{1}[0-9]{1}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = web.Load(categoryUrl);
                HtmlNodeCollection fileNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");
                List<string> fileUrls = new List<string>();
                Regex fileNameReg = new Regex("(^(a|p)[0-9]{6}$)|(^[0-9]{6}$)");

                for (int i = dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                {
                    string baseUrl = categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), string.Empty);
                    var targetUrls = fileNodes.Where(t => t.Attributes["href"].Value.StartsWith(i.ToString()))
                        .Select(t => baseUrl + t.Attributes["href"].Value)
                        .Where(t => fileNameReg.IsMatch(t.Split('/').LastOrDefault().Split('.').FirstOrDefault()))
                        .ToList();
                    fileUrls.AddRange(targetUrls);
                }

                foreach (string fileUrl in fileUrls)
                {
                    string meetingDateText = dateReg.Match(fileUrl).ToString();
#if debug
                    bool isMatch = dateReg.IsMatch(fileUrl);
                    if (isMatch)
                    {
                        Console.WriteLine("No problem {0}, continue", meetingDateText);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Not match {0}, {1}...", meetingDateText, fileUrl);

                        string fileName = fileUrl.Split('/').LastOrDefault();
                        Regex digitReg = new Regex("[0-9]{2}");
                        int year = 2000 + int.Parse(digitReg.Match(fileName).ToString());
                        string match = fileNodes.FirstOrDefault(t => t.Attributes["href"].Value.EndsWith(fileName)).InnerText;
                        meetingDateText = string.Format("{0}, {1}", match, year);
                        Console.WriteLine(DateTime.Parse(meetingDateText));
                        continue;
                    }
#endif

                    DateTime meetingDate = DateTime.MinValue;
                    try
                    {
                        DateTime.ParseExact(meetingDateText, "yyMMdd", null);
                    }
                    catch
                    {
                        string fileName = fileUrl.Split('/').LastOrDefault();
                        Regex digitReg = new Regex("[0-9]{2}");
                        int year = 2000 + int.Parse(digitReg.Match(fileName).ToString());
                        string match = fileNodes.FirstOrDefault(t => t.Attributes["href"].Value.EndsWith(fileName)).InnerText;
                        meetingDateText = string.Format("{0}, {1}", match, year);
                        meetingDate = DateTime.Parse(meetingDateText);
                    }
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early, skip...");
                        continue;
                    }
                    this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
        }
    }
}
