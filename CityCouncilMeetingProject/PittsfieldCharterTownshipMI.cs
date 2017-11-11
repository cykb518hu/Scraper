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
    public class PittsfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PittsfieldCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "PittsfieldCharterTownshipMI",
                CityName = "Pittsfield Charter Township",
                CityUrl = "http://www.twp-pittsfield.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PittsfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();
            string currentDate = DateTime.Now.ToString("MM-dd-yyyy").Replace("-", "%2F");
            string targetUrl = string.Format(this.docUrls[0], this.dtStartFrom.Year, currentDate);
            HtmlDocument doc = web.Load(targetUrl);
            string[] categories = { "Board of Trustees", "Planning Commission", "Zoning Board of Appeals" };

            foreach (string category in categories)
            {
                var dataNodes = doc.DocumentNode.SelectSingleNode("//table[@summary='Search Results']/tr/td/p").ChildNodes
                    .Where(t => t.NodeType == HtmlNodeType.Element).ToList();

                foreach (HtmlNode dataNode in dataNodes)
                {
                    if (dataNode.InnerText.Contains(category))
                    {
                        HtmlNodeCollection entryNodes = dataNodes[dataNodes.IndexOf(dataNode) + 1].SelectNodes(".//a[@href]");

                        if (entryNodes != null)
                        {
                            foreach(HtmlNode entryNode in entryNodes)
                            {
                                string entryUrl = this.cityEntity.CityUrl + "/" + entryNode.Attributes["href"].Value;
                                string meetingDateText = entryNode.InnerText.Split(' ').FirstOrDefault().Trim((char)32, (char)160, '\r', '\n', '\t');
                                DateTime meetingDate = DateTime.Parse(meetingDateText);
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }
                                string category1 = category == "Board of Trustees" ? "City Council" : category;
                                string fileType = entryNode.ParentNode.OuterHtml.Contains("iconpdf") ? "pdf" : "doc";
                                this.ExtractADoc(c, entryUrl, category1, fileType, meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
