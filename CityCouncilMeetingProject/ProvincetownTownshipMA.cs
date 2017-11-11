using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;

namespace CityCouncilMeetingProject
{
    public class ProvincetownTownshipMA : City
    {
        private List<string> docUrls = null;

        public ProvincetownTownshipMA()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ProvincetownTownshipMA",
                CityName = "Provincetown",
                CityUrl = "http://www.provincetown-ma.gov/",
                StateCode = "MA"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ProvincetownTownshipMA_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}[,]{0,1}[\\s]{0,1}[0-9]{4}");
            var categoryList = this.ExtractCategories(this.docUrls[0]);

            if (categoryList != null)
            {
                Console.WriteLine("{0} categories found...", categoryList.Count);
                foreach (string categoryUrl in categoryList)
                {
                    this.ExtractOneCategory(categoryUrl, dateReg, ref docs, ref queries);
                }
            }
        }

        private void ExtractOneCategory(string categoryUrl, Regex dateReg, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            WebClient c = new WebClient();
            StringBuilder yearBuilder = new StringBuilder();
            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(string.Format("{0}|", i));
            }
            Regex yearReg = new Regex(string.Format("{0}", yearBuilder.ToString().TrimEnd('|')));
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(categoryUrl);
            HtmlNode headlineNode = doc.DocumentNode.SelectSingleNode("//div[@class='Headline']");
            string category = headlineNode.InnerText.Replace(" Agendas & Minutes Archive", string.Empty);
            HtmlNodeCollection archiveNodes = doc.DocumentNode.SelectNodes("//a[@href]");

            if (archiveNodes != null)
            {
                List<HtmlNode> targetArchives = archiveNodes.Where(t => yearReg.IsMatch(t.InnerText)).ToList();

                if (targetArchives != null)
                {
                    foreach (HtmlNode targetArchive in targetArchives)
                    {
                        string archiveUrl = targetArchive.Attributes["href"].Value.ToLower().StartsWith("http") ?
                            targetArchive.Attributes["href"].Value :
                            this.cityEntity.CityUrl.TrimEnd('/') + "/" + targetArchive.Attributes["href"].Value.TrimStart('/');
                        HtmlDocument archiveDoc = web.Load(archiveUrl);
                        HtmlNodeCollection archiveMeetingNodes = archiveDoc.DocumentNode.SelectNodes("//table[@summary='Archive Details']//a[@href]");

                        if (archiveMeetingNodes != null)
                        {
                            Console.WriteLine("{0} docs for category {1}...", archiveMeetingNodes.Count, category);
                            foreach (HtmlNode archiveMeetingNode in archiveMeetingNodes)
                            {
                                if (yearReg.IsMatch(archiveMeetingNode.InnerText) == false)
                                {
                                    continue;
                                }

                                string archiveMeetingUrl = archiveMeetingNode.Attributes["href"].Value.ToLower().StartsWith("http") ?
                                    archiveMeetingNode.Attributes["href"].Value :
                                    this.cityEntity.CityUrl.TrimEnd('/') + '/' + archiveMeetingNode.Attributes["href"].Value.TrimStart('/');
                                string meetingDateText = dateReg.Match(archiveMeetingNode.InnerText).ToString();

                                try
                                {
                                    meetingDateText = meetingDateText
                                        .Replace("Apri ", "April ")
                                        .Replace("Juy", "July")
                                        .Replace("Augst", "August");
                                    DateTime meetingDate = DateTime.Parse(meetingDateText);
                                    if (meetingDate < this.dtStartFrom)
                                    {
                                        Console.WriteLine("Too early, skip...");
                                        continue;
                                    }
                                    this.ExtractADoc(c, archiveMeetingUrl, category, "pdf", meetingDate, ref docs, ref queries);
                                }
                                catch
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("DEBUG:{0}...", archiveMeetingNode.InnerText);
                                    Console.ResetColor();
                                    throw new Exception("Failed to parse date...");
                                }

                            }
                        }
                    }
                }
            }
        }

        private List<string> ExtractCategories(string v)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(v);
            HtmlNodeCollection categoriesNodes = doc.DocumentNode.SelectNodes("//div[@id='Section1']/table//td/a[@class='Hyperlink']");
            return categoriesNodes == null ?
                null :
                categoriesNodes.Select(t =>
                    {
                        string u = t.Attributes["href"].Value;
                        u = u.ToLower().StartsWith("http") ? u : this.cityEntity.CityUrl.TrimEnd('/') + "/" + u.TrimStart('/');
                        return u;
                    }).ToList();
        }
    }
}
