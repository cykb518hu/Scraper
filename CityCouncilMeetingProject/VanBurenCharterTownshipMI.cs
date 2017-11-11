//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class VanBurenCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public VanBurenCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "VanBurenCharterTownshipMI",
                CityName = "Van Buren Charter Township",
                CityUrl = "http://vanburen-mi.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("VanBurenCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            //Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            Regex dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            Regex dateReg1 = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                Console.WriteLine("Category Url: {0}...", categoryUrl);
                HtmlDocument doc = web.Load(categoryUrl);

                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//article//a[contains(text(),'Agenda')]");
                HtmlNodeCollection entryNodes1 = doc.DocumentNode.SelectNodes("//article//a[contains(text(),'Minutes')]");

                foreach (HtmlNode entry in entryNodes)
                {
                    string entryUrl = entry.Attributes["href"].Value;
#if debug
                    bool isMatch = dateReg.IsMatch(entryUrl);
                    if (isMatch)
                    {
                        Console.WriteLine("No problem, continue");

                        if (DateTime.ParseExact(meetingDateText.Trim('-'), "M-d-yy", null) < this.dtStartFrom)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Break!");
                            Console.ResetColor();
                            break;
                        }

                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Not match {0}...", entryUrl);
                        continue;
                    }
#endif

                    DateTime meetingDate = DateTime.MinValue; //DateTime.ParseExact(meetingDateText, "d-M-yy", null);
                    this.ExtractADoc(c, entryUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    var currentDoc = docs.FirstOrDefault(t => t.DocSource == entryUrl);
                    var currentQuery = queries.FirstOrDefault(q => q.DocId == currentDoc.DocId);

                    if(currentQuery.MeetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Break now...");
                        break;
                    }

                    //this.SaveMeetingResultsToSQL(docs, queries);
                }

                foreach (HtmlNode entry in entryNodes1)
                {
                    string entryUrl = entry.Attributes["href"].Value;
#if debug
                    bool isMatch = dateReg.IsMatch(entryUrl);
                    if (isMatch)
                    {
                        Console.WriteLine("No problem, continue");

                        if (DateTime.ParseExact(meetingDateText.Trim('-'), "M-d-yy", null) < this.dtStartFrom)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Break!");
                            Console.ResetColor();
                            break;
                        }

                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Not match {0}...", entryUrl);
                        continue;
                    }
#endif

                    DateTime meetingDate = DateTime.MinValue; //DateTime.ParseExact(meetingDateText, "d-M-yy", null);
                    this.ExtractADoc(c, entryUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    var currentDoc = docs.FirstOrDefault(t => t.DocSource == entryUrl);
                    var currentQuery = queries.FirstOrDefault(q => q.DocId == currentDoc.DocId);

                    if (currentQuery.MeetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Break now...");
                        break;
                    }

                    //this.SaveMeetingResultsToSQL(docs, queries);
                }
            }
        }

        private bool FixMeetingDate(ref List<Documents> docs, ref List<QueryResult> queries)
        {
            string dateMax = DateTime.MaxValue.ToString("yyyy-MM-dd");
            bool breakNow = false;
            Regex dateReg = new Regex("([a-zA-Z]+[\\s]{1,2}[0-9]{1,2}[\\s]{0,1},[\\s]{1,2}[0-9]{4}|[a-zA-Z]+[\\s]{1,2}[0-9]{1,2}\"‘[\\s]{0,1},[\\s]{1,2}[0-9]{4})");
            List<QueryResult> queriesPendingFix = queries.Where(t => t.MeetingDate == DateTime.MinValue).ToList();

            foreach (QueryResult queryPendingFix in queriesPendingFix)
            {
                Documents localdoc = docs.FirstOrDefault(t => t.DocId == queryPendingFix.DocId);

                if (localdoc != null && localdoc.DocBodyDic.Keys.Count > 0)
                {
                    string text = localdoc.DocBodyDic.FirstOrDefault().Value;
                    string meetingDateText = dateReg.Match(text).ToString();
                    Console.WriteLine("DEBUG: TEXT - {0}", text);
                    Console.WriteLine("DEBUG: MATCH - {0}", meetingDateText);

                    if (string.IsNullOrEmpty(meetingDateText))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(localdoc.DocLocalPath);
                        Console.ResetColor();
                        continue;
                    }

                    DateTime meetingDate = DateTime.Parse(meetingDateText.Replace("\"‘", string.Empty));
                    queryPendingFix.MeetingDate = meetingDate;
                    string currentDate = meetingDate.ToString("yyyy-MM-dd");
                    localdoc.DocLocalPath = localdoc.DocLocalPath.Replace(dateMax, currentDate);

                    if (meetingDate < this.dtStartFrom)
                    {
                        breakNow = true;
                    }
                }
            }

            return breakNow;
        }
    }
}
