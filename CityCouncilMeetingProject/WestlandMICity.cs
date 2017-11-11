using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    public class WestlandMICity : City
    {
        private List<string> docUrls = null;

        public WestlandMICity()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "WestlandMICity",
                CityName = "Westland",
                CityUrl = "http://cityofwestland.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WestlandMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            Regex dateReg = new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{4}");
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            for (int i = 1; ; i++)
            {
                string pageUrl = i == 1 ?
                    this.docUrls.FirstOrDefault() :
                    string.Format("{0}/-npage-{1}", this.docUrls.FirstOrDefault(), i);
                HtmlDocument doc = web.Load(pageUrl);
                HtmlNodeCollection meetingNodes = doc.DocumentNode.SelectNodes("//table[@class='front_end_widget listtable']/tbody/tr");
                bool last = false;

                foreach (HtmlNode meetingNode in meetingNodes)
                {
                    string meetingDateText = dateReg.Match(meetingNode.SelectSingleNode("./td[2]/time").InnerText).ToString();
                    DateTime meetingDate = DateTime.Parse(meetingDateText);
                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Too early,skip it.");
                        last = true;
                        break;
                    }

                    string category = "City Council";
                    string meetingTitle = meetingNode.SelectSingleNode("./td[1]").InnerText;

                    HtmlNodeCollection meetingDocNodes = meetingNode.SelectNodes(".//a[contains(@href,'ShowDocument')]");

                    if(meetingDocNodes != null)
                    {
                        foreach(HtmlNode meetingDocNode in meetingDocNodes)
                        {
                            string meetingDocUrl = this.cityEntity.CityUrl + meetingDocNode.Attributes["href"].Value;
                            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == meetingDocUrl);

                            if(localdoc == null)
                            {
                                localdoc = new Documents();
                                localdoc.DocId = Guid.NewGuid().ToString();
                                localdoc.CityId = this.cityEntity.CityId;
                                localdoc.DocSource = meetingDocUrl;
                                localdoc.DocType = category;
                                localdoc.Checked = false;
                                localdoc.Important = false;
                                localdoc.DocLocalPath = string.Format("{0}\\{1}.pdf", this.localDirectory, Guid.NewGuid().ToString());

                                try
                                {
                                    c.DownloadFile(meetingDocUrl, localdoc.DocLocalPath);
                                }
                                catch (Exception ex){ }

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

                if (last)
                {
                    break;
                }
            }
        }
    }
}
