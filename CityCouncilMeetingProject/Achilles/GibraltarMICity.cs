using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace CityCouncilMeetingProject
{
    public class GibraltarMICity : City
    {
        private List<string> docUrls = null;

        public GibraltarMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "GibraltarMICity",
                CityName = "Gibraltar MI",
                CityUrl = "https://gibraltarcitymi.documents-on-demand.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("GibraltarMICity_Urls.txt").ToList();
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
                        foreach(var b in buttons)
                        {
                            var foderId = b.Attributes["data-folderid"].Value;
                            var fileList = GetFilesList(foderId);
                            foreach (var f in fileList)
                            {
                                string meetingDateText = dateReg.Match(f).ToString();
                                DateTime meetingDate;
                                if (!DateTime.TryParse(meetingDateText, out meetingDate))
                                {
                                    Console.WriteLine(f);
                                    Console.WriteLine("date format incorrect...");
                                    continue;
                                }
                                if (meetingDate < this.dtStartFrom)
                                {
                                    Console.WriteLine("Early...");
                                    continue;
                                }
                                this.ExtractADoc(c, this.cityEntity.CityUrl + f, category, "pdf", meetingDate, ref docs, ref queries);
                            }
                        }
                        
                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);

        }

        public List<string> GetFilesList(string folderId)
        {
            var result = new List<string>();
            var url = string.Format("https://gibraltarcitymi.documents-on-demand.com/Mod/JsonDisplaySubFolders?FolderId={0}", folderId);
            var data = Getdata<RootObject>(url);
            if(data!=null&&data.Data.Children.Any())
            {
                foreach(var r in data.Data.Children)
                {
                    if(Convert.ToInt32(r.Data.Title)>2015)
                    {
                        var fileUrl = string.Format("https://gibraltarcitymi.documents-on-demand.com/MOD/JsonDisplayFolderDocuments/?FolderId={0}&Key={1}", folderId, r.Data.Title);
                        var fileList = Getdata<List<FileRootObject>>(fileUrl);
                        foreach(var f in fileList)
                        {
                            result.Add(f.Data.Attr.Href);
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


    public class RootObject
    {
        [JsonProperty("Success")]
        public bool Success { get; set; }
        [JsonProperty("Message")]
        public string Message { get; set; }
        [JsonProperty("data")]
        public Data Data { get; set; }
    }
    public class Data
    {
        [JsonProperty("data")]
        public Data1 DataDetail { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
        [JsonProperty("attr")]
        public Attr Attr { get; set; }
        [JsonProperty("icon")]
        public string Icon { get; set; }
        [JsonProperty("processData")]
        public bool ProcessData { get; set; }
        [JsonProperty("children")]
        public List<Child> Children { get; set; }
    }
    public class Data1
    {
        [JsonProperty("title")]
        public string Title { get; set; }
    }
    public class Attr
    {
        [JsonProperty("rel")]
        public string Rel { get; set; }
    }
    public class Child
    {
        [JsonProperty("data")]
        public Data2 Data { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
        [JsonProperty("attr")]
        public Attr1 Attr { get; set; }
        [JsonProperty("icon")]
        public string Icon { get; set; }
    }
    public class Data2
    {
        [JsonProperty("title")]
        public int Title { get; set; }
    }
    public class Attr1
    {
        [JsonProperty("rel")]
        public string Rel { get; set; }
        [JsonProperty("data-key")]
        public int DataKey { get; set; }
    }
    public class FileRootObject
    {
        [JsonProperty("data")]
        public FileData Data { get; set; }

    }
    public class FileData
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("attr")]
        public FileAttr Attr { get; set; }
    }
    public class FileAttr
    {
        [JsonProperty("href")]
        public string Href { get; set; }
        [JsonProperty("data-id")]
        public string DataId { get; set; }
    }
}
