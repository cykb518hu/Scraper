using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;

namespace CityCouncilMeetingProject
{
    public class ThetfordTownshipMI : City
    {
        private List<string> docUrls = null;

        public ThetfordTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ThetfordTownshipMI",
                CityName = "Thetford",
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

            this.docUrls = File.ReadAllLines("ThetfordTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                cd.Navigate().GoToUrl(categoryUrl);
                HtmlDocument doc = new HtmlDocument();
                System.Threading.Thread.Sleep(2000);
                doc.LoadHtml(cd.PageSource);
                HtmlNodeCollection fileNodes = category == "City Council" ?
                    doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]") :
                    doc.DocumentNode.SelectNodes("//p[@class='font_7']//a[contains(@href,'.pdf')]");

                Console.WriteLine("{0} docs found...", fileNodes.Count);

                if (fileNodes != null)
                {
                    foreach (HtmlNode fileNode in fileNodes)
                    {
                        string fileUrl = fileNode.Attributes["href"].Value;
                        Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                        if (localdoc == null)
                        {
                            localdoc = new Documents();
                            localdoc.DocId = Guid.NewGuid().ToString();
                            localdoc.CityId = this.cityEntity.CityId;
                            localdoc.Checked = false;
                            localdoc.Important = false;
                            localdoc.DocType = category;
                            localdoc.DocSource = fileUrl;
                            localdoc.DocLocalPath = string.Format("{0}\\{1}.pdf", this.localDirectory, Guid.NewGuid().ToString());
                            try
                            {
                                c.Headers.Add("user-agent", "chrome");
                                c.DownloadFile(fileUrl, localdoc.DocLocalPath);
                            }
                            catch (Exception ex)
                            {

                            }

                            docs.Add(localdoc);
                        }
                        else
                        {
                            Console.WriteLine("This file already downloaded...");
                        }

                        this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                        DateTime meetingDate = DateTime.MinValue;
                        Console.WriteLine("DEBUG:{0}...",categoryUrl);
                        Console.WriteLine("DEBUG:{0}...", localdoc.DocSource);
                        if (localdoc.DocBodyDic.Count != 0)
                        {
                            localdoc.DocType = localdoc.DocType == "City Council" ? 
                                localdoc.DocType : 
                                localdoc.DocBodyDic[1].Contains("Planning") ? 
                                    "Planning" : 
                                    "Zoning";
                            try
                            {
                                if (localdoc.DocType == "City Council")
                                {
                                    meetingDate = DateTime.Parse(dateReg.Match(localdoc.DocBodyDic[1]).ToString());
                                }
                                else
                                {
                                    meetingDate = DateTime.Parse(dateReg.Match(fileNode.ParentNode.ParentNode.InnerText).ToString());
                                }

                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine("MEETING DATE: {0}", meetingDate);
                                Console.ResetColor();

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Early, delete the file...");
                                    File.Delete(localdoc.DocLocalPath);
                                    docs.Remove(localdoc);
                                    continue;
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Failed to parse meeting date...");
                            }
                        }

                        QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                        if (qr == null)
                        {
                            qr = new QueryResult();
                            qr.QueryId = Guid.NewGuid().ToString();
                            qr.DocId = localdoc.DocId;
                            qr.MeetingDate = meetingDate;
                            qr.SearchTime = DateTime.Now;
                            queries.Add(qr);
                        }

                        this.ExtractQueriesFromDoc(localdoc, ref qr);
                        Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                    }

                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }

            cd.Quit();
            cd = null;
        }
    }
}
