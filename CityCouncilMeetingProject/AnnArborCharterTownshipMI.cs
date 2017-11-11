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
    public class AnnArborCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public AnnArborCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "AnnArborCharterTownshipMI",
                CityName = "AnnArbor Charter Township",
                CityUrl = "http://aatwp.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("AnnArborCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            string firstLevelTemplate = "https://annarbortwpmi.documents-on-demand.com/Mod/JsonDisplaySubFolders?FolderId={0}";
            // {0} - folder id; {1} - year; 
            string secondLevelTemplate = "https://annarbortwpmi.documents-on-demand.com/MOD/JsonDisplayFolderDocuments/?FolderId={0}&Key={1}";
            HtmlDocument doc = web.Load(this.docUrls[0]);
            HtmlNode containerNode = doc.GetElementbyId("MOD-FileRoot");
            HtmlNodeCollection level2Nodes = containerNode.SelectNodes(".//button[@class='folder-button level-2']");

            if(level2Nodes != null)
            {
                foreach(HtmlNode level2Node in level2Nodes)
                {
                    string folderId = level2Node.Attributes["data-folderid"].Value;
                    string level1Url = string.Format(firstLevelTemplate, folderId);
                    string level1Json = c.DownloadString(level1Url);
                    var level1Token = Newtonsoft.Json.JsonConvert.DeserializeObject(level1Json) as Newtonsoft.Json.Linq.JToken;
                    var categoryToken = level1Token.SelectToken("$.data.data.title");
                    string category = categoryToken.ToString().Trim('\\').Split('\\').FirstOrDefault();
                    Console.WriteLine("Working on category {0}...", category);
                    for (int year = this.dtStartFrom.Year; year <= DateTime.Now.Year; year++)
                    {
                        Console.WriteLine("Working on year {0}...", year);
                        c.Headers.Add("user-agent", "chrome");
                        string level2Url = string.Format(secondLevelTemplate, folderId, year);
                        string json = c.DownloadString(level2Url);
                        var dataToken = Newtonsoft.Json.JsonConvert.DeserializeObject(json) as Newtonsoft.Json.Linq.JToken;
                        var entryTokens = dataToken.SelectTokens("$..data");

                        if(entryTokens != null)
                        {
                            foreach(var entryToken in entryTokens)
                            {
                                string meetingDateText = dateReg.Match(entryToken.ToString()).ToString();
                                DateTime meetingDate = DateTime.Parse(meetingDateText);

                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Too early, skip...");
                                    continue;
                                }

                                string fileUrl = entryToken.SelectToken("$..href").ToString();
                                fileUrl = fileUrl.StartsWith("http") ? this.docUrls[0].TrimEnd('/') + fileUrl : fileUrl;
                                this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                    }
                }
            }
        }
    }
}
