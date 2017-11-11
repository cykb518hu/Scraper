using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    class RosevilleMICity : City
    {
        private List<string> docUrls = null;

        public RosevilleMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "RosevilleMICity",
                CityName = "Roseville",
                CityUrl = "http://www.ci.roseville.mi.us",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RosevilleMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];

                HtmlDocument categoryDoc = web.Load(categoryUrl);
                HtmlNodeCollection listNodes = categoryDoc.DocumentNode.SelectNodes("//div[@id='dnn_ctr1872_HtmlModule_lblContent']/table/tbody/tr");
                listNodes = listNodes == null ?
                    categoryDoc.DocumentNode.SelectNodes("//div[@id='dnn_ctr1880_HtmlModule_lblContent']/table/tbody/tr") : listNodes;
                listNodes = listNodes == null ?
                    categoryDoc.DocumentNode.SelectNodes("//div[@id='dnn_ctr1892_HtmlModule_lblContent']/table/tbody/tr") : listNodes;
                if (listNodes != null)
                {
                    foreach (HtmlNode listNode in listNodes)
                    {
                        HtmlNode dateNode = listNode.SelectSingleNode("./td[2]");
                        string dateText = dateNode.InnerText.Replace("Febraury", "February");

                        if (listNode.InnerText.Contains((this.dtStartFrom.Year - 1).ToString()))
                        {
                            break;
                        }

                        DateTime meetingDate = DateTime.Parse(dateText);
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Too early, skip...");
                            continue;
                        }
                        HtmlNodeCollection fileNodes = listNode.SelectNodes(".//a[contains(@href,'fileticket')]");

                        if (fileNodes != null)
                        {
                            foreach (HtmlNode fileNode in fileNodes)
                            {
                                string fileUrl = fileNode.Attributes["href"].Value.Replace("&amp;", "&");
                                fileUrl = fileUrl.StartsWith("http") ? fileUrl : this.cityEntity.CityUrl + fileUrl;

                                Documents localdoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                                if (localdoc == null)
                                {
                                    localdoc = new Documents();
                                    localdoc.DocId = Guid.NewGuid().ToString();
                                    localdoc.CityId = this.cityEntity.CityId;
                                    localdoc.DocType = category;
                                    localdoc.DocSource = fileUrl;
                                    localdoc.Important = false;
                                    localdoc.Checked = false;

                                    string localFile = string.Format("{0}\\{1}_{2}_{3}.pdf",
                                        this.localDirectory,
                                        category,
                                        fileNode.InnerText.Trim('\r', '\n', '\t', (char)32, (char)160),
                                        Guid.NewGuid().ToString());
                                    localdoc.DocLocalPath = localFile;

                                    try
                                    {
                                        c.Headers.Add("user-agent", "chrome");
                                        c.DownloadFile(fileUrl, localFile);
                                    }
                                    catch (Exception ex)
                                    { }

                                    docs.Add(localdoc);
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("This file already downloaded...");
                                    Console.ResetColor();
                                }

                                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

                                if (qr == null)
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
