using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class TroyMICity : City
    {
        private List<string> docUrls = null;

        public TroyMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "TroyMICity",
                CityName = "Troy",
                CityUrl = "http://www.troymi.gov",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("TroyMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{2,4}");
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();

            foreach(string url in docUrls)
            {
                string category = string.Empty;

                if (url.Contains("planningcommission"))
                {
                    category = "Planning Commission";
                }
                else if (url.Contains("zoningboardofappeals(zba)"))
                {
                    category = "Zoning Board of Appeals";
                }
                else if (url.Contains("CityCouncil"))
                {
                    category = "City Council";
                }

                HtmlDocument listDoc = web.Load(url);
                HtmlNode sectionNode = listDoc.DocumentNode.SelectSingleNode("//div[@class='DisplayedSection']");

                if(sectionNode != null)
                {
                    var childs = sectionNode.ChildNodes;
                    List<HtmlNode> targetChilds = new List<HtmlNode>();
                    if(childs != null && childs.Count > 0)
                    {
                        foreach(HtmlNode child in childs)
                        {
                            if(child.Name.ToLower() == "h3" || child.Name.ToLower() == "ul")
                            {
                                try
                                {
                                    targetChilds.Add(child);
                                }
                                catch (Exception ex)
                                {

                                }
                            }
                        }
                        //targetChilds = childs.Where(t => t.Name != "#text").ToList();
                    }

                    for(int i = 0; i < targetChilds.Count; i = i + 2)
                    {
                        string meetingText = dateReg.Match(targetChilds[i].InnerText).ToString();
                        DateTime meetingDate = DateTime.MinValue;
                        bool isDate = DateTime.TryParse(meetingText, out meetingDate);

                        if(isDate == false)
                        {
                            Console.WriteLine("Current URL {0}...", url);
                            Console.WriteLine("DATE TEXT: {0}...", targetChilds[i].InnerText);
                            continue;
                        }

                        HtmlNodeCollection docNodes = targetChilds[i + 1].SelectNodes(".//a[contains(@href,'.pdf')]");

                        if(docNodes != null && docNodes.Count > 0)
                        {
                            foreach(HtmlNode docNode in docNodes)
                            {
                                string fileUrl = docNode.Attributes["href"].Value;
                                var fileUrlEles = fileUrl.Split('?').FirstOrDefault().Split('/').Reverse().ToList();
                                Documents localDoc = docs.FirstOrDefault(t => t.DocSource == fileUrl);

                                if(localDoc == null)
                                {
                                    localDoc = new Documents();
                                    localDoc.DocType = category;
                                    localDoc.DocId = Guid.NewGuid().ToString();
                                    localDoc.CityId = this.cityEntity.CityId;
                                    localDoc.DocSource = fileUrl;
                                    localDoc.Checked = false;

                                    string localFilePath = string.Format("{0}\\{1}_{2}_{3}_{4}",
                                        this.localDirectory,
                                        category,
                                        meetingDate.ToString("yyyy-MM-dd"),
                                        fileUrlEles[1],
                                        fileUrlEles[0]);

                                    try
                                    {
                                        c.DownloadFile(fileUrl, localFilePath);
                                    }
                                    catch
                                    {
                                    }

                                    localDoc.DocLocalPath = localFilePath;
                                    docs.Add(localDoc);
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("File aready downloaded...");
                                    Console.ResetColor();
                                }

                                this.ReadText(false, localDoc.DocLocalPath, ref localDoc);
                                QueryResult qr = queries.FirstOrDefault(t => t.DocId == localDoc.DocId);

                                if(qr == null)
                                {
                                    qr = new QueryResult();
                                    qr.DocId = localDoc.DocId;
                                    qr.SearchTime = DateTime.Now;
                                    qr.MeetingDate = meetingDate;
                                    qr.CityId = localDoc.CityId;
                                    queries.Add(qr);
                                }

                                this.ExtractQueriesFromDoc(localDoc, ref qr);
                                Console.WriteLine("{0} docs saved, {1} queries saved...", docs.Count, queries.Count);
                            }
                        }
                    }

                    this.SaveMeetingResultsToSQL(docs, queries);
                }
            }
        }
    }
}
