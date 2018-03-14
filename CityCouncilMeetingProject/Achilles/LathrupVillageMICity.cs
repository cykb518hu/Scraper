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
    public class LathrupVillageMICity : City
    {
        private List<string> docUrls = null;

        public LathrupVillageMICity()
        {
            cityEntity = new CityInfo()
            {
                CityId = "LathrupVillageMICity",
                CityName = "Lathrup Village City",
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

            this.docUrls = File.ReadAllLines("LathrupVillageMICity_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}"), "");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+. [0-9]{2} [\\s]{0,2}[0-9]{4}"), "MMM dd yyyy");
            dateRegFormatDic.Add(new Regex("[a-zA-Z]+ [\\s]{0,2}[0-9]{4}"), "MMMM yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{4}"), "MM-dd-yyyy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{1}-[0-9]{2}"), "MM-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{2}"), "M-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{1}-[0-9]{2}"), "M-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}/[0-9]{2}/[0-9]{2}"), "M/dd/yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}/[0-9]{1}/[0-9]{2}"), "M/d/yy");
            dateRegFormatDic.Add(new Regex("[0-9]{8}"), "MMddyyyy");
           

            foreach (string url in this.docUrls)
            {
                var category = "";
                HtmlDocument doc = web.Load(url);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'.pdf')]");
                foreach (var r in list)
                {
                    if (r.FirstChild.Name == "img")
                    {
                        continue;
                    }
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;
                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        string format = dateRegFormatDic[dateRegKey];
                        string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
                        if (string.IsNullOrEmpty(format))
                        {
                            if (DateTime.TryParse(meetingDateText, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }
                        }
                        if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                        {
                            dateConvert = true;
                            break;
                        }

                    }
                    if (!dateConvert)
                    {
                        foreach (var dateRegKey in dateRegFormatDic.Keys)
                        {

                            string format = dateRegFormatDic[dateRegKey];
                            string meetingDateText = dateRegKey.Match(r.ParentNode.NextSibling.NextSibling.InnerText).ToString();
                            if (string.IsNullOrEmpty(format))
                            {
                                if (DateTime.TryParse(meetingDateText, out meetingDate))
                                {
                                    dateConvert = true;
                                    break;
                                }
                            }

                            if (DateTime.TryParseExact(meetingDateText, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }

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
                    HtmlNode categoryNode = Closest(r);
                    category = categoryNode.InnerText.Replace("\r", "").Replace("\n", "").TrimEnd().TrimStart();

                    if (category.IndexOf("City Council") > -1)
                    {
                        category = "City Council";
                    }
                    if (category.IndexOf("Planning") > -1)
                    {
                        category = "Planning Commission";
                    }
                    if (category.IndexOf("Downtown") > -1)
                    {
                        category = "Downtown Development";
                    }
                    if (category.IndexOf("ZBA") > -1)
                    {
                        category = "Zoning Board of Appeals";
                    }
                   // Console.WriteLine(string.Format("datestr:{0},category:{1}", meetingDate.ToString("yyyy-MM-dd"), category));
                    this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }

        public HtmlNode Closest(HtmlNode node)
        {
            node = node.ParentNode.ParentNode;
            while (node.PreviousSibling != null)
            {
                if (node.PreviousSibling.Name.ToLower() == "div")
                {
                    if (node.PreviousSibling.Attributes["id"] != null && node.PreviousSibling.Attributes["id"].Value.IndexOf("sec") > -1)
                    {
                        return node.PreviousSibling;
                    }
                }

                node = node.PreviousSibling;


            }
            return null;
        }

    }

}
