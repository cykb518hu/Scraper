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
    public class BreedsvilleVillageMI : City
    {
        private List<string> docUrls = null;

        public BreedsvilleVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "BreedsvilleVillageMI",
                CityName = "Breedsville",
                CityUrl = "http://www.breedsville.org/index.html",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("BreedsvilleVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("[a-zA-Z]+[\\s]{0,2}[0-9]{1,2},[\\s]{0,2}[0-9]{4}");
            StringBuilder yearBuilder = new StringBuilder();

            for (int i = this.dtStartFrom.Year; i <= DateTime.Now.Year; i++)
            {
                yearBuilder.Append(i.ToString());
                yearBuilder.Append('|');
            }

            Regex yearReg = new Regex(string.Format("({0})", yearBuilder.ToString().Trim('|')));

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                HtmlDocument doc = new HtmlDocument();
                string html = this.GetHtml(categoryUrl, string.Empty);
                doc.LoadHtml(html);///web.Load(categoryUrl);
                HtmlNodeCollection docNodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'pdf')]");

                if(docNodes != null)
                {
                    foreach(HtmlNode docNode in docNodes)
                    {
                        if (yearReg.IsMatch(docNode.InnerText) && docNode.InnerText.Contains("Comming in") == false)
                        {
                            string docUrl = this.cityEntity.CityUrl.Replace(this.cityEntity.CityUrl.Split('/').LastOrDefault(),
                                docNode.Attributes["href"].Value);
                            string meetingDateText = dateReg.Match(docNode.InnerText).ToString();
                            //DateTime meetingDate = !string.IsNullOrEmpty(meetingDateText) ?
                            //    DateTime.Parse(meetingDateText) :
                            //    DateTime.MinValue;
                            DateTime meetingDate = DateTime.MinValue;
                            this.ExtractADoc(c, docUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        }
                    }
                }
            }
        }
    }
}
