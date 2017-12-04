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
    public class PennfieldCharterTownshipMI : City
    {
        private List<string> docUrls = null;

        public PennfieldCharterTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PennfieldCharterTownshipMI",
                CityName = "Pennfield Charter Township",
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

            //this.docUrls = File.ReadAllLines("PennfieldCharterTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = new List<Documents>();
            var queries = new List<QueryResult>();
            WebClient c = new WebClient();
            var url = "http://www.pennfieldtwp.com/LinkClick.aspx?fileticket=37xRMebZ-2c%3d&tabid=6634&portalid=1053&mid=21718";
            DateTime meetingDate = DateTime.MinValue;
            this.ExtractADoc(c, url, "subCategory", "pdf", meetingDate, ref docs, ref queries);
            //to do
        }
    }
}
