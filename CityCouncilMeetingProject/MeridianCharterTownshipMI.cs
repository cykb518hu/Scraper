using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class MeridianCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public MeridianCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MeridianCharterTownshipMI",
                CityName = "Meridian Charter Township",
                CityUrl = "http://www.meridian.mi.us/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("MeridianCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            Regex dateReg1 = new Regex("[a-zA-Z]+_[0-9]{1,2}_[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string yearUrl = url.Split('*')[1];
                HtmlDocument yearDoc = web.Load(yearUrl);
                HtmlNodeCollection yearNodes = yearDoc.DocumentNode.SelectNodes("//div[@class='box sectionIntro']//div[@class='body']//a");

                for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
                {
                    if (yearNodes != null)
                    {
                        List<HtmlNode> currentYearNodes = yearNodes.Where(t => t.InnerText.Contains(year.ToString())).ToList();

                        foreach (HtmlNode currentYearNode in currentYearNodes)
                        {
                            string currentYearUrl = currentYearNode.Attributes["href"].Value;
                            HtmlDocument currentYearDoc = web.Load(currentYearUrl.Replace("&amp;", "&"));
                            HtmlNodeCollection docNodes = currentYearDoc.DocumentNode.SelectNodes("//div[@class='attachment']/a");

                            if(docNodes != null)
                            {
                                foreach(HtmlNode docNode in docNodes)
                                {
                                    string docUrl = this.cityEntity.CityUrl + docNode.Attributes["href"].Value;
                                    string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                                    meetingDateText = string.IsNullOrEmpty(meetingDateText) ? dateReg1.Match(docUrl).ToString() : meetingDateText;
                                    Console.WriteLine("Url {0}\r\nUrl 1 {1}\r\nUrl 2 {2}\r\nDateTime {3}",
                                        yearUrl,
                                        currentYearUrl,
                                        docUrl,
                                        meetingDateText);

                                    if (String.IsNullOrEmpty(meetingDateText))
                                    {
                                        continue;
                                    }

                                    DateTime meetingDate = dateReg.IsMatch(docNode.InnerText) ?
                                        DateTime.Parse(meetingDateText) :
                                        DateTime.Parse(meetingDateText.Replace("_", " "));

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
                                        localdoc.DocSource = docUrl;
                                        localdoc.DocType = category;
                                        localdoc.Checked = false;
                                        localdoc.Important = false;
                                        localdoc.DocLocalPath = string.Format("{0}\\{1}",
                                            this.localDirectory,
                                            docUrl.Split('/').LastOrDefault());

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
                                        qr.SearchTime = DateTime.Now;
                                        qr.MeetingDate = meetingDate;
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
    }
}
