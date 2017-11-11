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
    public class ChesaningVillageMI : City
    {
        private List<string> docUrls = null;

        public ChesaningVillageMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "ChesaningVillageMI",
                CityName = "Chesaning",
                CityUrl = "http://www.villageofchesaning.org/",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("ChesaningVillageMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            HtmlWeb web = new HtmlWeb();
            Regex dateReg = new Regex("(([0-9]{1,2}\\/[0-9]{1,2}\\/[0-9]{4})|((0|1)[0-9]{1}[0-9]{2}[0-9]{4}))");

            foreach (string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                string html = this.GetHtml(categoryUrl, string.Empty);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);
                HtmlNode councilPacketNode = doc.DocumentNode.SelectSingleNode("//*[text()='Council Packet']");
                HtmlNodeCollection fileNodes = null;

                if (councilPacketNode != null)
                {
                    var ancestorsPacket = councilPacketNode.Ancestors();
                    councilPacketNode = ancestorsPacket.FirstOrDefault(t => t.OriginalName == "table");
                    fileNodes = councilPacketNode.SelectNodes(".//div[@id='file_name']//a[contains(@href,'.pdf')]");
                }
                else
                {
                    fileNodes = doc.DocumentNode.SelectNodes("//div[@id='RZdocument_center']//a[contains(@href,'.pdf')]");
                }

                if (fileNodes != null)
                {
                    var fileNodesTarget = fileNodes.Where(t => t.SelectSingleNode("./img") == null);
                    foreach (HtmlNode fileNode in fileNodesTarget)
                    {
                        string fileUrl = fileNode.Attributes["href"].Value;
                        fileUrl = !fileUrl.StartsWith("http") ? this.cityEntity.CityUrl + fileUrl : fileUrl;
                        string meetingDateText = dateReg.Match(fileNode.InnerText).ToString();

                        Console.WriteLine("DEBUG: {0}", fileUrl);
                        Console.WriteLine("DEBUG: {0}", fileNode.OuterHtml);
                        Console.WriteLine("DEBUG: meeting date - {0}...", meetingDateText);

                        DateTime meetingDate = meetingDateText.Length == 8 ?
                            DateTime.ParseExact(meetingDateText, "MMddyyyy", null) :
                            DateTime.Parse(meetingDateText);

                        if (meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Early, skip...");
                            continue;
                        }

                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                    }
                }
            }
        }
    }
}
