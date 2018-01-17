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
    public class SpartaVillageMI : City
    {
        private List<string> docUrls = null;

        public SpartaVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "SpartaVillageMI",
                CityName = "Sparta",
                CityUrl = "https://spartami.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("SpartaVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            // var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}"), "str");
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}"), "number");
            string[] formats = { "MM/dd/yy", "MM/d/yy", "M/dd/yy", "M/d/yy", "MM/dd/yyyy", "MM/d/yyyy", "M/dd/yyyy", "M/d/yyyy" };

            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];

                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'documents/')]");
                foreach (var r in list)
                {

                    var meetingDateStr = r.InnerText;
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;

                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        var meetingDateText = dateRegKey.Match(meetingDateStr).ToString();
                        if (!string.IsNullOrWhiteSpace(meetingDateText))
                        {
                            string format = dateRegFormatDic[dateRegKey];
                            if (format == "str")
                            {
                                if (DateTime.TryParse(meetingDateText, out meetingDate))
                                {
                                    dateConvert = true;
                                    break;
                                }
                            }
                            else
                            {
                               
                                if (DateTime.TryParseExact(meetingDateText, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                                {
                                    dateConvert = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!dateConvert)
                    {
                        Console.WriteLine(meetingDateStr);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                    var type = r.Attributes["href"].Value.IndexOf(".pdf") > 0 ? "pdf" : "docx";
                   // Console.WriteLine(string.Format("meeting date:{0},category:{1}", meetingDate.ToString("yyyy-MM-dd"), category));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, type, meetingDate, ref docs, ref queries);
                }
            }
        }

    }

}
