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
    public class HadleyCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public HadleyCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "HadleyCharterTownshipMI",
                CityName = "Hadley Township MI",
                CityUrl = "http://hadleytownship.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("HadleyCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
           var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
           //var docs = new List<Documents>();
           // var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Dictionary<Regex, string> dateRegFormatDic = new Dictionary<Regex, string>();
            
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{4}"), "M-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{4}"), "MM-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{2}"), "M-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1,2}\\.[0-9]{1,2}\\.[0-9]{2}"), "M.dd.yy");
            foreach (string url in this.docUrls)
            {
                var category = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/wp-content/uploads/')]");
                foreach (var r in list)
                {
                    DateTime meetingDate = DateTime.MinValue;
                    var dateConvert = false;
                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        string format = dateRegFormatDic[dateRegKey];
                        string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
                        if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                            break;
                        }

                    }
                    if (!dateConvert)
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
                    HtmlNode title = null;
                    if (r.ParentNode.Name == "p" && r.ParentNode.ParentNode.Attributes["class"].Value == "leftBox")
                    {
                        title = Closest(r.ParentNode.ParentNode, "h3");
                    }
                    else
                    {
                        title = Closest(r.ParentNode, "h3");
                    }
                    if (title != null)
                    {
                        category = title.InnerText;
                    }
                    if (category.IndexOf("TOWNSHIP BOARD") > -1)
                    {
                        category = "Township Board";
                    }
                    if (category.IndexOf("PLANNING COMMISSION") > -1)
                    {
                        category = "Planning Commission";
                    }
                    this.ExtractADoc(c, r.Attributes["href"].Value, category, ".pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
        }

        public  HtmlNode Closest(HtmlNode node, string search)
        {
            search = search.ToLower();
            while (node.PreviousSibling != null)
            {
                if (node.PreviousSibling.Name.ToLower() == search) return node.PreviousSibling;
                node = node.PreviousSibling;
            }
            return null;
        }
    }
}
