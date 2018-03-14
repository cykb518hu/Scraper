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
    public class WaylandMICity : City
    {
        private List<string> docUrls = null;

        public WaylandMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "WaylandMICity",
                CityName = "Wayland MI",
                CityUrl = "http://www.lathrupvillage.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("WaylandMICity_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            //var docs = this.LoadDocumentsDoneSQL();
           //var queries = this.LoadQueriesDoneSQL();
             var docs = new List<Documents>();
             var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{4}"), "MM-dd-yyyy");
            foreach (string url in this.docUrls)
            {
                var category = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//option");
                if (list != null)
                {
                    foreach (var r in list)
                    {
                        var dateConvert = false;
                        DateTime meetingDate = DateTime.MinValue;
                        foreach (var dateRegKey in dateRegFormatDic.Keys)
                        {
                            string format = dateRegFormatDic[dateRegKey];
                            string meetingDateText = dateRegKey.Match(r.NextSibling.InnerText).ToString();
                            if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }

                        }
                        if (!dateConvert)
                        {
                            Console.WriteLine(r.NextSibling.InnerText);
                            Console.WriteLine("date format incorrect...");
                            continue;
                        }
                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early...");
                            continue;
                        }
                        category = GetCategory(r);
                        if (category.IndexOf("City Council") > -1)
                        {
                            category = "City Council";
                        }
                        if (category.IndexOf("CC") > -1)
                        {
                            category = "City Council";
                        }
                        if (category.IndexOf("Planning") > -1)
                        {
                            category = "Planning Commission";
                        }
                        //var url=string.Format("{}")
                        //Console.WriteLine(string.Format("datestr:{0},category:{1}", meetingDate.ToString("yyyy-MM-dd"), category));
                        this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);

                    }
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }

        public string GetCategory(HtmlNode node)
        {
            while (node.ParentNode != null)
            {
                if (node.ParentNode.Name.ToLower() == "div"&& node.ParentNode.Attributes["class"]?.Value== "Container-20015-1")
                {
                    return node.ParentNode.FirstChild.NextSibling.InnerText;
                }

                node = node.ParentNode;
            }
            return "";
        }
    }
}
