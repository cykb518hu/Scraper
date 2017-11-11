//#define debug
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Web;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class BrowntownCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public BrowntownCharterTownshipMI()
        {
            this.cityEntity = new CityInfo()
            {
                CityId = "BrowntownCharterTownshipMI",
                CityName = "Browntown Charter Township",
                CityUrl = "http://www.brownstown-mi.org/index.html",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BrowntownCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            List<Documents> docs = this.LoadDocumentsDoneSQL();
            List<QueryResult> queries = this.LoadQueriesDoneSQL();
            HtmlWeb web = new HtmlWeb();
            WebClient c = new WebClient();

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                string baseUrl = categoryUrl.Replace(categoryUrl.Split('/').LastOrDefault(), string.Empty);
                HtmlDocument doc = web.Load(categoryUrl);

                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//div[@class='center_body_text center_scroller']//table//tr/td");

                if (docNodes != null)
                {
                    for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
                    {
                        List<HtmlNode> entries = docNodes.Where(t =>
                        t.SelectSingleNode("./a[@href]") != null &&
                        t.SelectSingleNode("./a[@href]").Attributes["href"].Value.StartsWith(i.ToString()))
                        .ToList();

                        foreach (HtmlNode entryNode in entries)
                        {
                            string meetingDateText = string.Format("{0}, {1}", HttpUtility.HtmlDecode(entryNode.InnerText.Replace("\n", string.Empty).Split('(').FirstOrDefault()), i);
                            HtmlNode entryUrlNode = entryNode.SelectSingleNode("./a");
#if debug
                            try
                            {
                                DateTime.Parse(meetingDateText);
                                Console.WriteLine("No problem...");
                                continue;
                            }
                            catch
                            {
                                Console.WriteLine("Not match: {0} on {1}...", meetingDateText, categoryUrl);
                                continue;
                            }
#endif 

                            DateTime meetingDate = DateTime.Parse(meetingDateText);
                            if (meetingDate < this.dtStartFrom)
                            {
                                Console.WriteLine("Too early, skip...");
                                continue;
                            }
                            string docUrl = string.Format("{0}{1}", baseUrl, entryUrlNode.Attributes["href"].Value);
                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
