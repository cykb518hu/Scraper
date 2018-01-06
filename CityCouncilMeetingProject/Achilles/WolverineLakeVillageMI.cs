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
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CityCouncilMeetingProject
{
    public class WolverineLakeVillageMI : City
    {
        private List<string> docUrls = null;

        public WolverineLakeVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WolverineLakeVillageMI",
                CityName = "WolverineLake",
                CityUrl = "https://www.wolverinelake.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WolverineLakeVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                if (category == "Council Meeting")
                {
                    HtmlDocument doc = web.Load(categoryUrl);
                    HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");

                    foreach (var r in list)
                    {
                        string meetingDateText = dateReg.Match(r.InnerText).ToString();
                        DateTime meetingDate;
                        if (!DateTime.TryParse(meetingDateText, out meetingDate))
                        {
                            Console.WriteLine(r.InnerText);
                            Console.WriteLine("date format incorrect...");
                            continue;
                        }
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early...");
                            continue;
                        }
                        // Console.WriteLine(string.Format("url:{0},category:{1}", this.cityEntity.CityUrl + r.Attributes["href"].Value, category));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
                else
                {
                    var data = Getdata(category, categoryUrl);

                    foreach(var r in data.d.results)
                    {
                        string meetingDateText = dateReg.Match(r.FileRef).ToString();
                        DateTime meetingDate;
                        if (!DateTime.TryParse(meetingDateText, out meetingDate))
                        {
                            Console.WriteLine(r.FileRef);
                            Console.WriteLine("date format incorrect...");
                            continue;
                        }
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early...");
                            continue;
                        }
                        // Console.WriteLine(string.Format("url:{0},category:{1}", this.cityEntity.CityUrl + r.Attributes["href"].Value, category));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.FileRef, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }

            }
        }

        public WoRootObject Getdata(string category,string url)
        {
            try
            {
                WebClient client = new WebClient();

                // Add a user agent header in case the 
                // requested URI contains a query.

                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                client.Headers.Add("Content-Type", "application/json;odata=verbose;charset=utf-8");
                client.Headers.Add("Accept", "application/json;odata=verbose");

                Stream data = client.OpenRead(url);
                StreamReader reader = new StreamReader(data);
                string s = reader.ReadToEnd();
                data.Close();
                reader.Close();
                return JsonConvert.DeserializeObject<WoRootObject>(s);
            }
            catch (Exception ex)
            {
                Console.WriteLine("no data exception" + ex.ToString());
            }
            return default(WoRootObject);
        }
    }
    public class WoResult
    {
        public string FileLeafRef { get; set; }
        public string Title { get; set; }
        public string Board { get; set; }
        public string FileRef { get; set; }
    }

    public class WoD
    {
        public List<WoResult> results { get; set; }
    }

    public class WoRootObject
    {
        public WoD d { get; set; }
    }
}
