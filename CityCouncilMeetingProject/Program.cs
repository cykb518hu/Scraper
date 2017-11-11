using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Reflection;

namespace CityCouncilMeetingProject
{
    class Program
    {
        /// <summary>
        /// public enum SecurityProtocolType
        /// {
        ///     Ssl3 = 48,
        ///     Tls = 192,
        ///     Tls11 = 768,
        ///     Tls12 = 3072,
        /// }
        /// </summary>
        /// <param name="args"></param>
        static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                return RunCityReflection(args[0]);
            }

            return 0;
        }

        public static int RunCityReflection(string cityName)
        {
            //bool searchGoogle = bool.Parse(ConfigurationManager.AppSettings["searchGoogle"]);
            bool clean = bool.Parse(ConfigurationManager.AppSettings["clean"]);
            bool import = bool.Parse(ConfigurationManager.AppSettings["import"]);
            try
            {
                Type cityType = Type.GetType("CityCouncilMeetingProject." + cityName);
                var currentCity = Activator.CreateInstance(cityType);

                if (clean)
                {
                    MethodInfo cleanMethod = currentCity.GetType().GetMethod("CleanUselessRecords");

                    if (cleanMethod != null)
                    {
                        cleanMethod.Invoke(currentCity, null);
                        return 0;
                    }
                }

                if (import)
                {
                    MethodInfo importMethod = currentCity.GetType().GetMethod("ImportData");

                    if (importMethod != null)
                    {
                        importMethod.Invoke(currentCity, null);
                        return 0;
                    }
                }

                MethodInfo detectMethod = currentCity.GetType().GetMethod("DetectStartDate");

                if(detectMethod != null)
                {
                    detectMethod.Invoke(currentCity, null);
                }

                MethodInfo downloadMethod = currentCity.GetType().GetMethod("DownloadCouncilPdfFiles");

                if (downloadMethod != null)
                {
                    downloadMethod.Invoke(currentCity, null);
                }

                //if (searchGoogle)
                //{
                //    MethodInfo searchGoogleMethod = currentCity.GetType().GetMethod("SearchOnGoogle1");

                //    if (searchGoogleMethod != null)
                //    {
                //        searchGoogleMethod.Invoke(currentCity, null);
                //    }
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine("Encountered error: {0}.", ex.ToString());
                return 1;
            }

            return 0;
        }

        static List<string> FindCitiesUseCivicMI()
        {
            string stateUrl = "http://www.michigan.gov/som/0,4669,7-192-29701_31713_31714-97070--,00.html";
            List<string> data = new List<string>();

            HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(stateUrl);
            var cityList = doc.DocumentNode.SelectNodes("//table[@id='govtList']/tbody/tr[position()>2]");

            foreach (var cityNode in cityList)
            {
                var nameNode = cityNode.SelectSingleNode("./td[1]");
                string cityName = nameNode.InnerText;
                var citySiteNode = cityNode.SelectSingleNode("./td[last()]/a");
                string citySite = citySiteNode == null ? string.Empty :
                    citySiteNode.Attributes.Contains("href") ?
                    citySiteNode.Attributes["href"].Value :
                    citySiteNode.InnerText;

                if (!string.IsNullOrEmpty(citySite))
                {
                    if (citySite.Contains("michigantownships"))
                    {
                        var detailDoc = web.Load(citySite);
                        var realSiteNode = detailDoc.DocumentNode.SelectSingleNode("//div[contains(text(),'Website:')]/a");
                        string realSite = realSiteNode == null ? string.Empty : realSiteNode.Attributes["href"].Value;

                        if (!string.IsNullOrEmpty(realSite))
                        {
                            data.Add(string.Format("\"{0}\",\"{1}\",", cityName, realSite));
                        }
                    }
                    else
                    {
                        data.Add(string.Format("\"{0}\",\"{1}\",", cityName, citySite));
                    }
                }

                Console.WriteLine("{0} cities added...", data.Count);
            }

            File.WriteAllLines("MICities.txt", data, Encoding.UTF8);
            List<string> civicList = new List<string>();

            foreach (string line in data)
            {
                string website = line.Trim('"', ',').Split(',').LastOrDefault().Trim('"');
                //string newSite = "http://www.lansingmi.gov/AgendaCenter";
                string newSite = website.TrimEnd('/') + "/agendacenter";
                try
                {
                    doc = web.Load(newSite);
                    if (doc.DocumentNode.SelectSingleNode("//h1[text()='Agenda Center']") != null)
                    {
                        civicList.Add(line);
                    }
                    Console.WriteLine("{0} found use civic", civicList.Count);
                }
                catch
                {
                }
                Console.WriteLine("{0} visited...", data.IndexOf(line));
            }

            File.WriteAllLines("CivicListMI.txt", civicList, Encoding.UTF8);

            return data;
        }

        static List<string> FindCitiesUseCivicMA()
        {
            List<string> data = new List<string>();
            string url = "https://www.mma.org/print/16150";
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(url);
            var linksNodes = doc.DocumentNode.SelectNodes("//div[@class='linkRow']");

            if(linksNodes != null)
            {
                foreach(var linkNode in linksNodes)
                {
                    var nameNode = linkNode.SelectSingleNode("./div[@class='comName']");
                    string cityName = nameNode.InnerText;
                    var websiteNode = linkNode.SelectSingleNode(".//a[@href]");

                    if(websiteNode == null)
                    {
                        continue;
                    }

                    string website = websiteNode.Attributes["href"].Value;
                    string record = string.Format("\"{0}\",\"{1}\",", cityName, website);
                    data.Add(record);
                }
            }

            File.WriteAllLines("MACities.txt", data, Encoding.UTF8);
            List<string> civicList = new List<string>();

            foreach (string line in data)
            {
                string website = line.Trim('"', ',').Split(',').LastOrDefault().Trim('"');
                //string newSite = "http://www.lansingmi.gov/AgendaCenter";
                string newSite = website.TrimEnd('/') + "/agendacenter";
                try
                {
                    doc = web.Load(newSite);
                    if (doc.DocumentNode.SelectSingleNode("//h1[text()='Agenda Center']") != null)
                    {
                        civicList.Add(line);
                    }
                    Console.WriteLine("{0} found use civic", civicList.Count);
                }
                catch
                {
                }
                Console.WriteLine("{0} visited...", data.IndexOf(line));
            }

            File.WriteAllLines("CivicListMA.txt", civicList, Encoding.UTF8);

            return data;
        }

        static void GenerateCityJSON()
        {
            List<string> cities = File.ReadAllLines("city.txt").ToList();
            List<dynamic> cityObjects = new List<dynamic>();

            foreach(string line in cities)
            {
                var cityObj = new
                {
                    City = line.Split('\t')[0],
                    Date = line.Split('\t').LastOrDefault()
                };
                cityObjects.Add(cityObj);
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(cityObjects);
            File.WriteAllText("City.json", json, Encoding.UTF8);
        }

        public static void InsertDeployedCity()
        {
            var token = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText("city.json"));
            var cityArr = token as Newtonsoft.Json.Linq.JArray;
            List<string> sqlList = new List<string>();
            foreach(var cityToken in cityArr)
            {
                var cityName = cityToken.SelectToken("$.City").ToString();
                var deployDate = cityToken.SelectToken("$.Date").ToString();
                string sql = "INSERT INTO CITY(CITY_NM,DEPLOYE_DATE) VALUES('" + cityName + "','" + deployDate + "');";
                sqlList.Add(sql);
            }

            System.Data.SqlClient.SqlConnection localConnection = new System.Data.SqlClient.SqlConnection(ConfigurationManager.ConnectionStrings["local"].ConnectionString);
            var command = localConnection.CreateCommand();
            command.CommandText = string.Join("\r\n", sqlList);
            localConnection.Open();
            command.ExecuteNonQuery();
            localConnection.Close();
        }
    }
}
