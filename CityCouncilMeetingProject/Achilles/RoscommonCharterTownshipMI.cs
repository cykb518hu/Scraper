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
    public class RoscommonCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public RoscommonCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "RoscommonCharterTownshipMI",
                CityName = "Roscommon Charter Township",
                CityUrl = "http://www.roscommontownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("RoscommonCharterTownshipMI_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2}"), "MMMM dd, yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]"), "MMMM, yyyy");
            foreach (string url in this.docUrls)
            {
                HtmlDocument doc = web.Load(url);
                var category = "";
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/uploads/')]");
                foreach (var r in list)
                {
                    var yearNode = Closest(r, "u");
                    string year = "";
                    if (yearNode != null)
                    {
                        year = yearNode.InnerText.Replace("/r", "").Replace("/n", "").Trim().Split(' ')[1];
                    }
                    if (string.IsNullOrWhiteSpace(year))
                    {
                        continue;
                    }
                    var meetingDateStr = r.InnerText;
                    meetingDateStr = meetingDateStr.Replace("&#8203;", "").Replace("&nbsp;","");
                    meetingDateStr = meetingDateStr + ", " + year;
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;
                    if (DateTime.TryParse(meetingDateStr, out meetingDate))
                    {
                        dateConvert = true;
                    }
                    //    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    //{
                    //    string format = dateRegFormatDic[dateRegKey];
                    //    if (DateTime.TryParseExact(meetingDateStr, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                    //    {
                    //        dateConvert = true;
                    //        break;
                    //    }

                    //}
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
                    var type = ParentClosest(r);
                    if (type != null)
                    {
                        category = type.InnerText;
                    }
                    if(string.IsNullOrWhiteSpace(category))
                    {
                        category = "Board Meeting";
                    }
                    if(category.IndexOf("SPECIAL MEETING")>-1)
                    {
                        category = "Special Meeting";
                    }
                    if (category.IndexOf("PLANNING COMMISSION") > -1)
                    {
                        category = "Planning Commission";
                    }
                    var fileType = r.Attributes["href"].Value.Substring(r.Attributes["href"].Value.LastIndexOf(".") + 1);
                     this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, fileType, meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
           // Console.ReadKey();
        }

        public HtmlNode Closest(HtmlNode node, string search)
        {
            search = search.ToLower();
            while (node.PreviousSibling != null)
            {
                if (node.PreviousSibling.Name.ToLower() == search) return node.PreviousSibling;
                node = node.PreviousSibling;
            }
            return null;
        }
        public HtmlNode ParentClosest(HtmlNode node)
        {
            HtmlNode tempNode = null;
            while (node.ParentNode != null)
            {
                if (node.Name == "div" && node.Attributes["class"].Value == "wsite-multicol")
                {
                    tempNode = node;
                    break;
                }
                node = node.ParentNode;
            }
            return Closest(tempNode.ParentNode, "h2");
        }
    }
}
