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
    public class MiltonCassTownshipMI : City
    {
        private List<string> docUrls = null;

        public MiltonCassTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "MiltonCassTownshipMI",
                CityName = "Milton-Cass Charter Township",
                CityUrl = "http://www.miltontwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("MiltonCassTownshipMI_Urls.txt").ToList();
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
            dateRegFormatDic.Add(new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}"), " ");
            dateRegFormatDic.Add(new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2}"), "Month");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{2}-[0-9]{2}"), "MM-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{2}-[0-9]{1}-[0-9]{2}"), "MM-d-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{2}-[0-9]{2}"), "M-dd-yy");
            dateRegFormatDic.Add(new Regex("[0-9]{1}-[0-9]{1}-[0-9]{2}"), "M-d-yy");
            foreach (string url in this.docUrls)
            {
                var subUrl = url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'/LinkClick.aspx')]");
                foreach (var r in list)
                {
                    var dateConvert = false;
                    DateTime meetingDate = DateTime.MinValue;

                    foreach (var dateRegKey in dateRegFormatDic.Keys)
                    {
                        string format = dateRegFormatDic[dateRegKey];
                        string meetingDateText = dateRegKey.Match(r.InnerText).ToString();
                        if (string.IsNullOrWhiteSpace(format))
                        {
                            if (DateTime.TryParse(meetingDateText, out meetingDate))
                            {
                                dateConvert = true;
                                break;
                            }
                        }
                        if (format=="Month"&& !string.IsNullOrWhiteSpace(meetingDateText))
                        {
                            try
                            {
                                var year = r.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode.PreviousSibling.PreviousSibling.InnerText;
                                var dateStr = meetingDateText + ", " + year;
                                if (DateTime.TryParse(dateStr, out meetingDate))
                                {
                                    dateConvert = true;
                                    break;
                                }
                            }
                            catch
                            {
                                continue;
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
                        Console.WriteLine(r.InnerText);
                        Console.WriteLine("date format incorrect...");
                        continue;
                    }

                    if (meetingDate < this.dtStartFrom)
                    {
                        Console.WriteLine("Early...");
                        continue;
                    }
                   // Console.WriteLine(string.Format("datestr:{0},meeting:{1}", r.InnerText, meetingDate.ToString("yyyy-MM-dd")));
                     this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }
            }
            Console.WriteLine("docs:" + docs.Count + "--- query:" + queries.Count);
            // Console.ReadKey();
        }
    }
}
