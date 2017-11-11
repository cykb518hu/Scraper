using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;

namespace CityCouncilMeetingProject
{
    public class HollandCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public HollandCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HollandCharterTownshipMI",
                CityName = "Holland Charter Township",
                CityUrl = "https://www.hct.holland.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadLines("HollandCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            //tsl12 - 3072; tsl11 - 768
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]+[0-9]{1,2},[\\s]+[0-9]{4}");

            foreach (string docUrl in this.docUrls)
            {
                string category = docUrl.Split('*')[0];
                string categoryUrl = docUrl.Split('*')[1] + "?limit=0";
                cd.Navigate().GoToUrl(categoryUrl);
                System.Threading.Thread.Sleep(3000);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(cd.PageSource);
                HtmlNodeCollection meetingNodes = doc.DocumentNode.SelectNodes("//td[@headers='tableOrdering']/a");

                if (meetingNodes != null)
                {
                    foreach (HtmlNode meetingNode in meetingNodes)
                    {
                        string meetingDateText = dateReg.Match(meetingNode.InnerText).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early...");
                            continue;
                        }

                        string fileUrl = this.cityEntity.CityUrl + meetingNode.Attributes["href"].Value + "?format=pdf";
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if (localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.DocType = category;
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.Important = false;
                            localdoc.Checked = false;
                            localdoc.DocSource = fileUrl;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}.pdf",
                                this.localDirectory,
                                fileUrl.Split('?').FirstOrDefault().Split('/').LastOrDefault());

                            try
                            {
                                c.Headers.Add("user-agent", "chrome");
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch (Exception ex)
                            { }

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
                            qr.CityId = this.cityEntity.CityId;
                            qr.DocId = localdoc.DocId;
                            qr.MeetingDate = meetingDate;
                            qr.SearchTime = DateTime.Now;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
                        this.SaveMeetingResultsToSQL(docs, queries);
                    }
                }
            }

            cd.Quit();
            cd = null;
        }
    }
}
