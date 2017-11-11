using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace CityCouncilMeetingProject
{
    public class PinconningTownshipMI : City
    {
        private List<string> docUrls = null;

        public PinconningTownshipMI()
        {
            cityEntity = new CityInfo()
            {
                CityId = "PinconningTownshipMI",
                CityName = "Pinconning",
                CityUrl = "http://pinconningtownship.com",
                StateCode = "MI"
            };

            this.localQueryFile = string.Format("{0}_Queries.csv", this.cityEntity.CityName);
            this.localDocFile = string.Format("{0}_Docs.csv", this.cityEntity.CityName);
            localDirectory = string.Format("{0}\\{1}", Environment.CurrentDirectory, cityEntity.CityId);

            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            this.docUrls = File.ReadAllLines("PinconningTownshipMI_Urls.txt").ToList();
        }

        public void DownloadCouncilPdfFiles()
        {
            var docs = this.LoadDocumentsDoneSQL();
            var queries = this.LoadQueriesDoneSQL();
            WebClient c = new WebClient();
            ChromeDriver cd = new ChromeDriver();

            foreach(string url in this.docUrls)
            {
                string category = url.Split('*')[0];
                string categoryUrl = url.Split('*')[1];
                cd.Navigate().GoToUrl(categoryUrl);
                System.Threading.Thread.Sleep(3000);

                var selectList = cd.FindElementsByXPath("//select");
                var goList = cd.FindElementsByXPath("//a[text()='Go']");
                string currentWindowHandle = cd.CurrentWindowHandle;

                for(int i = 0; i < selectList.Count; i++)
                {
                    SelectElement fileListSelectEle = new SelectElement(selectList[i]);
                    Dictionary<string, string> optionValues = fileListSelectEle.Options.ToDictionary(t => t.GetAttribute("value"), t => t.Text);
                    var dateReg = new System.Text.RegularExpressions.Regex("[a-zA-Z]+[\\s]{1}[0-9]{1,2},[\\s]{1}[0-9]{4}");
                    bool reachlast = false;

                    for(int j = 0; j < optionValues.Count; j++)
                    {
                        string value = optionValues.ElementAt(j).Key;
                        string text = optionValues.ElementAt(j).Value;
                        string meetingDateText = dateReg.Match(text).ToString();
                        DateTime meetingDate = DateTime.Parse(meetingDateText);

                        if(meetingDate < this.dtStartFrom)
                        {
                            Console.WriteLine("Skip....");
                            reachlast = true;
                            break;
                        }

                        fileListSelectEle.SelectByValue(value);
                        goList[i].Click();
                        System.Threading.Thread.Sleep(2000);
                        string fileUrl = string.Empty;

                        if(cd.WindowHandles.Count > 1)
                        {
                            string nextWindowHandle = cd.WindowHandles.FirstOrDefault(t => t != currentWindowHandle);
                            var nextWindow = cd.SwitchTo().Window(nextWindowHandle);
                            fileUrl = nextWindow.Url;
                            nextWindow.Close();
                            cd.SwitchTo().Window(currentWindowHandle);
                            Console.WriteLine("Sleep 3 seconds...");
                            System.Threading.Thread.Sleep(3000);
                        }
                        else
                        {
                            fileUrl = cd.Url;
                        }
                        
                        this.ExtractADoc(c, fileUrl, category, "pdf", meetingDate, ref docs, ref queries);
                        Console.WriteLine("DEBUG INFO BEFORE RETURN TO HOME PAGE");
                        cd.Navigate().GoToUrl(categoryUrl);
                        System.Threading.Thread.Sleep(3000);
                        Console.WriteLine("DEBUG INFO AFTER RETURN TO HOME PAGE");
                        selectList = cd.FindElementsByXPath("//select");
                        goList = cd.FindElementsByXPath("//a[text()='Go']");
                        fileListSelectEle = new SelectElement(selectList[i]);
                    }

                    if (reachlast)
                    {
                        Console.WriteLine("Reach last....");
                        break;
                    }
                }
            }

            cd.Quit();
            cd = null;
        }
    }
}
