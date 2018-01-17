using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Net.Http;
using Newtonsoft.Json;

namespace CityCouncilMeetingProject
{
    public class VergennesCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public VergennesCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "VergennesCharterTownshipMI",
                CityName = "Vergennes Charter Township",
                CityUrl = "https://vergennestwpmi.documents-on-demand.com/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("VergennesCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
            //var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection entryNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'level-1')]");
                if (entryNodes != null)
                {
                    foreach (HtmlNode entryNode in entryNodes)
                    {
                        var category = entryNode.SelectSingleNode("h3").InnerText;
                        category = category.Replace("\r", "").Replace("\n", "").Trim();
                        var buttons = entryNode.SelectNodes(".//button");
                        foreach (var b in buttons)
                        {
                            var foderId = b.Attributes["data-folderid"].Value;
                            var fileList = GetFilesList(foderId);
                            foreach (var f in fileList)
                            {
                                string meetingDateText = dateReg.Match(f.Name).ToString();
                                DateTime meetingDate;
                                if (!DateTime.TryParse(meetingDateText, out meetingDate))
                                {
                                    Console.WriteLine(f.Name);
                                    Console.WriteLine("date format incorrect...");
                                    continue;
                                }
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Early...");
                                    continue;
                                }
                                this.ExtractADoc(c, this.cityEntity.CityUrl + f.Url, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }

                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }

        public List<FileNameAndUrl> GetFilesList(string folderId)
        {
            var result = new List<FileNameAndUrl>();
            var url = string.Format("https://vergennestwpmi.documents-on-demand.com/Mod/JsonDisplaySubFolders?FolderId={0}", folderId);
            var data = Getdata<RootObject>(url);
            if (data != null && data.Data.Children.Any())
            {
                foreach (var r in data.Data.Children)
                {
                    if (Convert.ToInt32(r.Data.Title) > 2015)
                    {
                        var fileUrl = string.Format("https://vergennestwpmi.documents-on-demand.com/MOD/JsonDisplayFolderDocuments/?FolderId={0}&Key={1}", folderId, r.Data.Title);
                        var fileList = Getdata<List<FileRootObject>>(fileUrl);
                        if (fileList.Any())
                        {
                            foreach (var f in fileList)
                            {
                                var item = new FileNameAndUrl();
                                item.Name = f.Data.Title;
                                item.Url = f.Data.Attr.Href;
                                result.Add(item);
                            }
                        }
                    }
                }
            }
            return result;

        }

        public static T Getdata<T>(string url)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var stringTask = client.GetStringAsync(url).Result;
                    return JsonConvert.DeserializeObject<T>(stringTask);
                }
            }
            catch (Exception ex)
            {

            }
            return default(T);
        }
    }
}
