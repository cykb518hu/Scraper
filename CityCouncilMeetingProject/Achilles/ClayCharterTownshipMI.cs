using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Globalization;

namespace CityCouncilMeetingProject
{
    public class ClayCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public ClayCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ClayCharterTownshipMI",
                CityName = "Clay Charter Township",
                CityUrl = "http://www.coopertwp.org",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ClayCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
      
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();

            Dictionary<Regex, string> regMap = new Dictionary<Regex, string>();
            //Regex dateReg = new Regex("[A-Za-z]+[\\s]{0,1}[0-9]{1,2},[\\s]{0,1}[0-9]{4}");
            //regMap.Add(dateReg, string.Empty);
            //dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}");
            //regMap.Add(dateReg, "M-d-yyyy");
            //dateReg = new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}");
            //regMap.Add(dateReg, "M-d-yy");
            //dateReg = new Regex("[0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{2}");
            //regMap.Add(dateReg, "M/d/yy");
            //dateReg = new Regex("[0-9]{1,2}\\.[0-9]{1,2}\\.[0-9]{2}");
            //regMap.Add(dateReg, "MM.dd.yy");
            //dateReg = new Regex("(0|1)[0-9]{1}[0-9]{2}[0-9]{4}");
            //regMap.Add(dateReg, "MMddyyyy");
            var dateReg = new Regex("[A-Za-z]+ [0-9]{1,2}");
            regMap.Add(dateReg, "MMMM d, yyyy");
            dateReg = new Regex("[A-Za-z]+ [0-9]{2}");
            regMap.Add(dateReg, "MMMM dd, yyyy");

            foreach (string url in this.docUrls)
            {
                var subUrl= url.Split('*')[1];
                var category = url.Split('*')[0];
                HtmlDocument doc = web.Load(subUrl);
                HtmlNodeCollection list = doc.DocumentNode.SelectNodes("//a[contains(@href,'documents/docs')]");
                foreach (var r in list)
                {
                    var dateStr = r.InnerText.Replace("\r", "").Replace("\n", "").Trim();
                    DateTime meetingDate = DateTime.MinValue;
                    bool dateConvert = false;
                    var year = string.Empty;
                    try
                    {
                        year = r.Attributes["href"].Value;
                        year = year.Substring(year.LastIndexOf("_") + 1).Replace(".pdf", "").Trim();
                        if (year.Length == 2)
                        {
                            year = "20" + year;
                        }
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                    foreach (Regex regKey in regMap.Keys)
                    {
                        if (regKey.IsMatch(dateStr))
                        {
                            dateStr = regKey.Match(dateStr).ToString() + ", " + year;
                            //string meetingDateText = regKey.Match(dateStr).ToString();
                            if (DateTime.TryParseExact(dateStr, regMap[regKey], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out meetingDate))
                            {
                                dateConvert = true;
                            }

                            break;
                        }
                    }
                    if(dateConvert)
                    {
                        Console.WriteLine(category+"--"+ dateStr+"---"+meetingDate.ToString()+"---ok");
                    }
                    else
                    {
                        Console.WriteLine(category + "--" + dateStr + "--- falied");
                    }
                    //if (!dateConvert)
                    //{
                    //    Console.WriteLine("date format incorrect...");
                    //}
                    //if (meetingDate < this.dtStartFrom)
                    //{
                    //    Console.WriteLine("Early...");
                    //    continue;
                    //}
                    //this.ExtractADoc(c, this.cityEntity.CityUrl + r.Attributes["href"].Value, category, "pdf", meetingDate, ref docs, ref queries);
                }

                //if (category.Contains("pzc"))
                //{
                //    subCategory = "Planning Commission";
                //}
                //if (category.Contains("zba"))
                //{
                //    subCategory = "Zoning Board of Appeals";
                //}
                //if (category.Contains("bot"))
                //{
                //    subCategory = "Board of Trustees";
                //}
                //foreach (var r in list)
                //{
                //    DateTime meetingDate = DateTime.MinValue;
                //    try
                //    {
                //        meetingDate = DateTime.ParseExact(r.InnerText.Replace("\r", "").Replace("\n", "").TrimEnd().TrimStart(), "MM-dd-yy", null);
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine("date format incorrect...");
                //        continue;
                //    }
                //    if (meetingDate < this.dtStartFrom)
                //    {
                //        Console.WriteLine("Early...");
                //        continue;
                //    }
                // var str = "http://www.hamptontownship.org/LinkClick.aspx?fileticket=77JEAWMuTn8%3d&tabid=3062&portalid=56&mid=6876";
                //this.ExtractADoc(c, str, "ZOB", "pdf", DateTime.Now, ref docs, ref queries);
                //}
            }
        }
    }
}
