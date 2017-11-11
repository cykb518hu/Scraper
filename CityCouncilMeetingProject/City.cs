using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Drawing;
using System.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Configuration;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using MsWord = Microsoft.Office.Interop.Word;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web;

namespace CityCouncilMeetingProject
{
    public class City
    {
        protected string localDirectory = "default";
        protected List<string> searchterms = null;
        protected List<string> searchTermsDependency = null;
        protected DateTime dtStartFrom = DateTime.MinValue;
        protected int distance = 0;
        protected CityInfo cityEntity = new CityInfo();
        protected string homePage = string.Empty;
        protected CookieContainer container = new CookieContainer();
        protected string localQueryFile = string.Empty;
        protected string localDocFile = string.Empty;
        protected string dataFolder = string.Empty;

        public City()
        {
            if (!Directory.Exists(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            string searchTermsConfig = ConfigurationManager.AppSettings["searchTerms"];
            searchterms = searchTermsConfig.Split(';').Select(t => t.Trim('*')).ToList();
            string searchTermsDependencyConfig = ConfigurationManager.AppSettings["dependencySearchTerms"];
            searchTermsDependency = searchTermsDependencyConfig.Split(';').ToList();

            string searchTermFile = ConfigurationManager.AppSettings["termFile"];
            if (string.IsNullOrEmpty(searchTermFile) == false && File.Exists(searchTermFile))
            {
                string json = File.ReadAllText(searchTermFile);
                JToken keywordsToken = JsonConvert.DeserializeObject(json) as JToken;
                var keywordTokens = keywordsToken.SelectTokens("$..KeyWord");

                if (keywordTokens != null)
                {
                    foreach (JToken keywordToken in keywordTokens)
                    {
                        string aword = keywordToken.ToString();
                        if (!searchterms.Contains(aword) && searchTermsDependency.Contains(aword) == false)
                        {
                            searchterms.Add(aword);
                        }
                    }
                }
            }
            
            distance = int.Parse(ConfigurationManager.AppSettings["distance"]);
            dtStartFrom = DateTime.Parse(ConfigurationManager.AppSettings["startFrom"]);
            this.dataFolder = ConfigurationManager.AppSettings["outputPath"];

            if (!Directory.Exists(this.dataFolder))
            {
                Directory.CreateDirectory(this.dataFolder);
            }
        }

        protected virtual void ExtractADoc(WebClient c, string docUrl, string category, string fileType, DateTime meetingDate, ref List<Documents> docs, ref List<QueryResult> queries)
        {
            this.DealWithFileName(ref category);
            Documents localdoc = docs.FirstOrDefault(t => t.DocSource == docUrl);
            string xpath = fileType.Split(':').LastOrDefault();
            fileType = fileType.Split(':').FirstOrDefault();

            if (localdoc == null)
            {
                localdoc = new Documents();
                localdoc.DocSource = docUrl;
                localdoc.CityId = this.cityEntity.CityId;
                localdoc.DocId = Guid.NewGuid().ToString();
                localdoc.DocType = category;
                localdoc.Important = false;
                localdoc.Checked = false;
                localdoc.DocLocalPath = string.Format("{0}\\{1}_{2}_{3}.{4}",
                    this.localDirectory,
                    category,
                    meetingDate.ToString("yyyy-MM-dd"),
                    Guid.NewGuid().ToString(),
                    fileType);

                try
                {
                    c.Headers.Add("user-agent", "chrome");
                    c.DownloadFile(docUrl, localdoc.DocLocalPath);
                }
                catch (Exception ex)
                {
                    if (ex.ToString().Contains("404"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("NOT FOUND......");
                        Console.ResetColor();
                    }

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("Failed to download file {0}...", docUrl);
                    Console.WriteLine("ERROR: {0}", ex.ToString());
                    Console.ResetColor();
                    return;
                }

                docs.Add(localdoc);
            }
            else
            {
                Console.WriteLine("This file already downloaded...");
            }

            if (fileType != "html")
            {
                this.ReadText(false, localdoc.DocLocalPath, ref localdoc);
            }
            else
            {
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(File.ReadAllText(localdoc.DocLocalPath));
                localdoc.DocBodyDic.Add(1, doc.DocumentNode.SelectSingleNode(xpath).InnerText);
            }

            QueryResult qr = queries.FirstOrDefault(t => t.DocId == localdoc.DocId);

            if (qr == null)
            {
                qr = new QueryResult();
                qr.QueryId = Guid.NewGuid().ToString();
                qr.CityId = this.cityEntity.CityId;
                qr.DocId = localdoc.DocId;
                qr.MeetingDate = meetingDate;
                qr.SearchTime = DateTime.Now;
                queries.Add(qr);
            }

            if (qr.MeetingDate == DateTime.MinValue)
            {
                List<Regex> dateRegList = new List<Regex>();
                dateRegList.Add(new Regex("[a-zA-Z]{3,}[\\s]{1}[0-9]{1,2},[\\s]{1}[0-9]{4}"));
                dateRegList.Add(new Regex("[a-zA-Z]{3,}[\\s]{1}[0-9]{1,2}[\\s]{1}[0-9]{4}"));
                dateRegList.Add(new Regex("[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}"));
                dateRegList.Add(new Regex("[a-zA-Z]+[\\s]{1,2}[0-9]{1,2}(st|ST|nd|ND|rd|RD|th|TH)[,]{0,1}[\\s]{0,2}[0-9]{4}"));

                foreach (Regex dateReg in dateRegList)
                {
                    Console.WriteLine("Match meeting date again...");

                    if (localdoc.DocBodyDic.Count > 0)
                    {
                        string meetingDateText = localdoc.DocBodyDic.FirstOrDefault().Value;
                        Console.WriteLine("DEBUG:{0}", meetingDateText);
                        if (dateReg.IsMatch(meetingDateText))
                        {
                            if(dateRegList.IndexOf(dateReg) == dateRegList.Count - 1)
                            {
                                meetingDateText = meetingDateText.ToLower().Replace("th", string.Empty).Replace("rd", string.Empty).Replace("nd", string.Empty).Replace("st", string.Empty);
                            }

                            Console.WriteLine("Match meeting date succeefully.");
                            meetingDateText = dateReg.Match(meetingDateText).ToString().Replace("Sept ", "Sep ");
                            Console.WriteLine("DEBUG:{0}", meetingDateText);
                            meetingDate = DateTime.Parse(meetingDateText.ToLower());
                            qr.MeetingDate = meetingDate;
                            break;
                        }
                    }
                }
            }

            this.ExtractQueriesFromDoc(localdoc, ref qr);
            Console.WriteLine("{0} docs added, {1} queries added...", docs.Count, queries.Count);
            this.SaveMeetingResultsToSQL(docs, queries);
        }

        private void DealWithFileName(ref string text)
        {
            foreach(char c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace('c'.ToString(), string.Empty);
            }
        }

        public List<Documents> LoadDocumentsDoneSQL()
        {
            List<Documents> docs = new List<Documents>();
            string connectionString = ConfigurationManager.ConnectionStrings["local"].ConnectionString;
            SqlConnection localConnection = new SqlConnection(connectionString);
            SqlCommand selectCommand = new SqlCommand(@"SELECT [DOC_GUID]
                    ,[CITY_NM]
                    ,[DOC_TYPE]
                    ,[DOC_SOURCE]
                    ,[DOC_PATH]
                    ,[CHECKED]
                    ,[IMPORTANT]
                    ,[READABLE]
                FROM[MunicipalityPublicMeetingDB].[dbo].[DOCUMENT]
                WHERE CITY_NM='" + this.cityEntity.CityId + "'", localConnection);
            localConnection.Open();
            SqlDataReader docsReader = selectCommand.ExecuteReader();

            while (docsReader.Read())
            {
                Documents localDoc = new Documents();
                localDoc.DocId = docsReader.GetString(0);
                localDoc.CityId = docsReader.GetString(1);
                localDoc.DocType = docsReader.GetString(2);
                localDoc.DocSource = docsReader.GetString(3);
                localDoc.DocLocalPath = docsReader.GetString(4);
                localDoc.Checked = bool.Parse(docsReader.GetString(5));
                localDoc.Important = bool.Parse(docsReader.GetString(6));
                localDoc.Readable = bool.Parse(docsReader.GetString(7));
                docs.Add(localDoc);
            }

            return docs;
        }

        public List<QueryResult> LoadQueriesDoneSQL()
        {
            List<QueryResult> queries = new List<QueryResult>();

            string connectionString = ConfigurationManager.ConnectionStrings["local"].ConnectionString;
            SqlConnection localConnection = new SqlConnection(connectionString);
            SqlDataAdapter queryAdapter = new SqlDataAdapter(string.Format(@"SELECT q.[QUERY_GUID]
                    ,q.[DOC_GUID]
                    ,[MEETING_DATE]
                    ,[SEARCH_DATE]
                    ,[MEETING_TITLE]
                    ,[MEETING_LOCATION]
                FROM[MunicipalityPublicMeetingDB].[dbo].[QUERY] q
                JOIN DOCUMENT d on q.DOC_GUID = d.DOC_GUID
                WHERE d.CITY_NM='{0}';
                SELECT[ENTRY_GUID]
                    ,[QUERY_GUID]
                    ,[PAGE_NUMBER]
                    ,[KEYWORD]
                    ,[COMMENT]
                    ,[CONTENT]
                FROM[MunicipalityPublicMeetingDB].[dbo].[QUERY_ENTRY]
                WHERE QUERY_GUID in (SELECT [QUERY_GUID]
                FROM [MunicipalityPublicMeetingDB].[dbo].[QUERY] q JOIN DOCUMENT d on 
                q.DOC_GUID = d.DOC_GUID
                WHERE d.CITY_NM='{0}')", this.cityEntity.CityId), localConnection);
            DataSet queriesDataSet = new DataSet();
            queryAdapter.Fill(queriesDataSet);

            foreach (DataRow queryRow in queriesDataSet.Tables["Table"].Rows)
            {
                QueryResult qr = new QueryResult();

                string queryId = queryRow["QUERY_GUID"].ToString();
                queryId = string.IsNullOrEmpty(queryId) ? Guid.NewGuid().ToString() : queryId;
                var entryRows = queriesDataSet.Tables["Table1"].Select(string.Format("QUERY_GUID='{0}'", queryId));

                if (entryRows != null)
                {
                    foreach (DataRow entryRow in entryRows)
                    {
                        qr.QueryId = queryId;
                        qr.MeetingDate = DateTime.Parse(queryRow["MEETING_DATE"].ToString());
                        qr.SearchTime = DateTime.Parse(queryRow["SEARCH_DATE"].ToString());
                        qr.DocId = queryRow["DOC_GUID"].ToString();
                        QueryResult.KeywordEntry ke = qr.Entries.FirstOrDefault(t => t.Keyword == entryRow["KEYWORD"].ToString()
                            && t.PageNumber == int.Parse(entryRow["PAGE_NUMBER"].ToString()));

                        if (ke == null)
                        {
                            ke = new QueryResult.KeywordEntry();
                            ke.Keyword = entryRow["KEYWORD"].ToString();
                            ke.PageNumber = int.Parse(entryRow["PAGE_NUMBER"].ToString());
                            ke.CommentDic = new Dictionary<string, string>();
                            ke.GuidDic = new Dictionary<string, string>();
                            qr.Entries.Add(ke);
                        }

                        string content = entryRow["CONTENT"].ToString();
                        string comment = entryRow["COMMENT"].ToString();
                        string entryId = entryRow["ENTRY_GUID"].ToString();

                        if (ke.CommentDic.ContainsKey(content) == false)
                        {
                            ke.CommentDic.Add(content, entryId);
                            ke.GuidDic.Add(content, entryId);
                        }
                    }
                }

                queries.Add(qr);
            }

            return queries;
        }

        protected void ExtractQueriesFromDoc(Documents doc, ref QueryResult qr)
        {
            foreach (int page in doc.DocBodyDic.Keys)
            {
                string text = doc.DocBodyDic[page];

                foreach (string searchterm in searchterms)
                {
                    QueryResult.KeywordEntry entry = qr.Entries.FirstOrDefault(t => t.Keyword == searchterm && t.PageNumber == page);

                    if (entry == null)
                    {
                        entry = new QueryResult.KeywordEntry();
                        entry.Keyword = searchterm;
                        entry.PageNumber = page;
                        entry.GuidDic = new Dictionary<string, string>();
                        entry.CommentDic = new Dictionary<string, string>();
                        qr.Entries.Add(entry);
                    }

                    if (text.ToLower().Contains(searchterm.ToLower()))
                    {
                        string[] bodyWords = text.Split(' ');
                        string[] targetWords = Array.FindAll(bodyWords, t => t.ToLower().StartsWith(searchterm.ToLower()));
                        List<string> entryLines = entry.Contents;

                        int indexOfCurrent = 0;
                        for (int j = 0; j < targetWords.Length; j++)
                        {
                            indexOfCurrent = Array.IndexOf(bodyWords, targetWords[j], indexOfCurrent + 1);
                            List<string> words = new List<string>();
                            int rangeStart = indexOfCurrent - 11;
                            int rangeEnd = indexOfCurrent + 11;
                            rangeStart = rangeStart < 0 ? 0 : rangeStart;
                            rangeEnd = rangeEnd >= bodyWords.Length ? bodyWords.Length - 1 : rangeEnd;

                            for (int i = rangeStart; i <= rangeEnd; i++)
                            {
                                words.Add(bodyWords[i]);
                            }

                            string line = string.Join(" ", words.Select(t => t.Replace("\r", string.Empty).Replace("\n", " ")));

                            if (words.Count < 20)
                            {
                                Console.WriteLine("Please check!");
                            }

                            if (!entryLines.Exists(t => t.Contains(line)))
                            {
                                entryLines.Add(line);
                            }
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("search term {0} appears {1} times in document {2}...", searchterm, targetWords.Length, doc.DocLocalPath);
                        Console.ResetColor();
                        entry.Contents = entryLines;

                        foreach (string content in entry.Contents)
                        {
                            if (!entry.GuidDic.ContainsKey(content))
                            {
                                entry.GuidDic.Add(content, Guid.NewGuid().ToString());
                            }

                            if (!entry.CommentDic.ContainsKey(content))
                            {
                                entry.CommentDic.Add(content, string.Empty);
                            }
                        }
                    }

                    if (entry.Contents.Count == 0)
                    {
                        qr.Entries.Remove(entry);
                    }
                }

                foreach (string searchTermD in searchTermsDependency)
                {
                    QueryResult.KeywordEntry entry = qr.Entries.FirstOrDefault(t => t.Keyword == searchTermD && t.PageNumber == page);
                    if (entry == null)
                    {
                        entry = new QueryResult.KeywordEntry();
                        entry.Keyword = searchTermD;
                        entry.PageNumber = page;
                        qr.Entries.Add(entry);
                    }

                    Console.WriteLine("Search {0}...", searchTermD);
                    if (text.ToLower().Contains(searchTermD.ToLower()))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Dependency search term {0} found...", searchTermD);
                        Console.ResetColor();
                        string[] bodyWords = text.Split(' ');
                        string[] targetWords = Array.FindAll(bodyWords, t => t.ToLower().StartsWith(searchTermD.ToLower()));

                        if (targetWords.Length != 0)
                        {
                            List<string> entryLines = entry.Contents;
                            int indexOfCurrent = 0;

                            for (int j = 0; j < targetWords.Length; j++)
                            {
                                indexOfCurrent = Array.IndexOf(bodyWords, targetWords[j], indexOfCurrent);
                                List<string> words = new List<string>();
                                int rangeStart = indexOfCurrent - 11;
                                int rangeEnd = indexOfCurrent + 11;
                                rangeStart = rangeStart < 0 ? 0 : rangeStart;
                                rangeEnd = rangeEnd >= bodyWords.Length ? bodyWords.Length - 1 : rangeEnd;

                                for (int i = rangeStart; i <= rangeEnd; i++)
                                {
                                    words.Add(bodyWords[i]);
                                }

                                string line = string.Join(" ", words.Select(t => t.Replace("\r", string.Empty).Replace("\n", " ")));
                                List<string> termsDepends = "marijuana;marihuana;cannabis;Dispensary;Dispensaries;provisioning;Cultivat".Split(';').ToList();
                                if (termsDepends.Exists(t => line.ToLower().Contains(t.ToLower())) && entryLines.Exists(t => t.Contains(line) == false))
                                {
                                    entryLines.Add(line);
                                }
                            }

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Search term {0} appears {1} times...", searchTermD, entryLines.Count);
                            Console.ResetColor();
                            entry.Contents = entryLines;

                            foreach (string content in entry.Contents)
                            {
                                if (!entry.GuidDic.ContainsKey(content))
                                {
                                    entry.GuidDic.Add(content, Guid.NewGuid().ToString());
                                }

                                if (!entry.CommentDic.ContainsKey(content))
                                {
                                    entry.CommentDic.Add(content, string.Empty);
                                }
                            }
                        }
                    }

                    if (entry.Contents.Count == 0)
                    {
                        qr.Entries.Remove(entry);
                    }
                }
            }
        }

        internal protected void SaveMeetingResultsToSQL(List<Documents> docs, List<QueryResult> queries)
        {
            List<string> docIDList = new List<string>();
            List<string> queryIDList = new List<string>();
            List<string> entryIDList = new List<string>();
            string idQueryText = @"select distinct DOC_GUID from dbo.DOCUMENT;
                select distinct DOC_GUID from dbo.QUERY;
                select distinct ENTRY_GUID from dbo.QUERY_ENTRY;";
            SqlConnection localConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["local"].ConnectionString);
            SqlCommand readCommand = localConnection.CreateCommand();
            readCommand.CommandText = idQueryText;
            readCommand.CommandType = CommandType.Text;
            localConnection.Open();
            var idReader = readCommand.ExecuteReader();
            while (idReader.Read())
            {
                docIDList.Add(idReader.GetString(0));
            }
            idReader.NextResult();
            while (idReader.Read())
            {
                queryIDList.Add(idReader.GetString(0));
            }
            idReader.NextResult();
            while (idReader.Read())
            {
                entryIDList.Add(idReader.GetString(0));
            }
            localConnection.Close();
            localConnection.Open();
            var newDocs = docs.Where(t => docIDList.Contains(t.DocId) == false).ToList();
            Console.WriteLine("In total {0} new docs added...", newDocs.Count);
            foreach (Documents localDoc in newDocs)
            {
                StringBuilder docInsertBuilder = new StringBuilder();
                docInsertBuilder.Append("INSERT INTO dbo.DOCUMENT(DOC_GUID,CITY_NM,DOC_TYPE,DOC_SOURCE,DOC_PATH,CHECKED,IMPORTANT,READABLE) values(");
                docInsertBuilder.AppendFormat("'{0}',", localDoc.DocId);
                docInsertBuilder.AppendFormat("'{0}',", localDoc.CityId);
                docInsertBuilder.AppendFormat("'{0}',", localDoc.DocType.Replace("'", "''"));
                docInsertBuilder.AppendFormat("'{0}',", localDoc.DocSource.Replace("'", "''"));
                docInsertBuilder.AppendFormat("'{0}',", localDoc.DocLocalPath.Replace("'", "''"));
                docInsertBuilder.AppendFormat("'{0}',", localDoc.Checked.ToString());
                docInsertBuilder.AppendFormat("'{0}',", localDoc.Important.ToString());
                docInsertBuilder.AppendFormat("'{0}')", localDoc.Readable.ToString());
                string sql = docInsertBuilder.ToString();
                SqlCommand insertCommand = localConnection.CreateCommand();
                insertCommand.CommandText = sql;
                Console.WriteLine("\r\nSQL:{0}\r\n", sql);
                insertCommand.ExecuteNonQuery();
            }

            localConnection.Close();
            localConnection.Open();
            var newQueries = queries.Where(t => queryIDList.Contains(t.DocId) == false).ToList();
            newQueries.RemoveAll(t => t.Entries.Count == 0);
            Console.WriteLine("In total {0} new queries added...", newQueries.Count);
            foreach (QueryResult qr in newQueries)
            {
                //if (qr.MeetingDate == DateTime.MinValue)
                //{
                //    Documents targetDoc = docs.FirstOrDefault(t => t.DocId == qr.DocId);

                //    if (targetDoc != null)
                //    {
                //        string targetPath = targetDoc.DocLocalPath;
                //        int index = newQueries.IndexOf(qr);
                //        qr.MeetingDate = DateTime.Parse("April 12, 2016");
                //    }
                //}
                qr.QueryId = string.IsNullOrEmpty(qr.QueryId) ? Guid.NewGuid().ToString() : qr.QueryId;
                StringBuilder queryInsertBuilder = new StringBuilder();
                queryInsertBuilder.Append("INSERT INTO dbo.QUERY(QUERY_GUID,DOC_GUID,MEETING_DATE,SEARCH_DATE,MEETING_TITLE,MEETING_LOCATION) values(");
                queryInsertBuilder.AppendFormat("'{0}',", qr.QueryId);
                queryInsertBuilder.AppendFormat("'{0}',", qr.DocId);
                queryInsertBuilder.AppendFormat("'{0}',", qr.MeetingDate);
                queryInsertBuilder.AppendFormat("'{0}',", qr.SearchTime);
                queryInsertBuilder.AppendFormat("'{0}',", qr.MeetingTitle);
                queryInsertBuilder.AppendFormat("'{0}')", qr.MeetingLocation);
                string sql = queryInsertBuilder.ToString();
                SqlCommand queryInsertCommand = localConnection.CreateCommand();
                queryInsertCommand.CommandText = sql;
                Console.WriteLine("\r\nSQL:{0}\r\n", sql);
                queryInsertCommand.ExecuteNonQuery();
            }
            localConnection.Close();
            localConnection.Open();

            foreach (QueryResult qr in queries)
            {
                var entries = qr.Entries;
                foreach (var entry in entries)
                {
                    foreach (string key in entry.GuidDic.Keys)
                    {
                        if (!entryIDList.Contains(entry.GuidDic[key]))
                        {
                            StringBuilder entryBuilder = new StringBuilder();
                            entryBuilder.Append("INSERT INTO dbo.QUERY_ENTRY(ENTRY_GUID,QUERY_GUID,PAGE_NUMBER,KEYWORD,COMMENT,CONTENT) values(");
                            entryBuilder.AppendFormat("'{0}',", entry.GuidDic[key]);
                            entryBuilder.AppendFormat("'{0}',", qr.QueryId);
                            entryBuilder.AppendFormat("'{0}',", entry.PageNumber);
                            entryBuilder.AppendFormat("'{0}',", entry.Keyword);
                            entryBuilder.AppendFormat("'{0}',", entry.CommentDic[key]);
                            entryBuilder.AppendFormat("'{0}')", key.Replace("'", "''"));
                            string sql = entryBuilder.ToString();
                            SqlCommand entryInsertCommand = localConnection.CreateCommand();
                            entryInsertCommand.CommandText = sql;
                            Console.WriteLine("\r\nSQL:{0}\r\n", sql);
                            entryInsertCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            localConnection.Close();
        }

        public bool ReadPdf(string pdfFile, ref Documents doc, ref int pages)
        {
            bool success = false;

            try
            {
                if (pdfFile.ToLower().Contains("pdf"))
                {
                    StringBuilder textBuilder = new StringBuilder();
                    PdfReader r = new PdfReader(pdfFile);
                    pages = r.NumberOfPages;

                    for (int i = 1; i <= pages; i++)
                    {
                        PdfReaderContentParser parser = new PdfReaderContentParser(r);
                        ITextExtractionStrategy st = parser.ProcessContent<SimpleTextExtractionStrategy>(i, new SimpleTextExtractionStrategy());
                        string text = st.GetResultantText().Trim('\r', '\n', '\t', (char)32, (char)160);

                        if (!string.IsNullOrEmpty(text))
                        {
                            doc.DocBodyDic.Add(i, text);
                        }
                        else
                        {
                            text = PdfTextExtractor.GetTextFromPage(r, i).Trim('\r', '\n', '\t', (char)32, (char)160);

                            if (!string.IsNullOrEmpty(text))
                            {
                                doc.DocBodyDic.Add(i, text);
                            }
                        }
                    }

                    r.Close();
                    success = true;
                }
                else if (pdfFile.ToLower().Contains("doc"))
                {
                    MsWord.Application newApp = null;
                    MsWord.Document msdoc = null;

                    try
                    {
                        int retry = 2;
                        while (retry > 0)
                        {
                            try
                            {
                                //newApp = (MsWord.Application)Marshal.GetActiveObject("Word.Application");
                                newApp = newApp == null ? new MsWord.Application() : newApp;
                                System.Threading.Thread.Sleep(1000);
                                //msdoc = newApp.ActiveDocument;
                                msdoc = newApp.Documents.Open(pdfFile);
                                System.Threading.Thread.Sleep(1000);
                                object nothing = Missing.Value;
                                MsWord.WdStatistic stat = MsWord.WdStatistic.wdStatisticPages;
                                int num = msdoc.ComputeStatistics(stat, ref nothing);

                                for (int i = 1; i <= num; i++)
                                {
                                    if (doc.DocBodyDic.ContainsKey(i))
                                    {
                                        continue;
                                    }

                                    object objWhat = MsWord.WdGoToItem.wdGoToPage;
                                    object objWhich = MsWord.WdGoToDirection.wdGoToAbsolute;

                                    object objPage = (object)i;
                                    MsWord.Range range1 = msdoc.GoTo(ref objWhat, ref objWhich, ref objPage, ref nothing);
                                    MsWord.Range range2 = range1.GoToNext(MsWord.WdGoToItem.wdGoToPage);

                                    object objStart = range1.Start;
                                    object objEnd = range2.Start;
                                    if (range1.Start == range2.Start)
                                    {
                                        objEnd = msdoc.Characters.Count;
                                    }

                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("DEBUG: Path: {0}, {1}-{2}........", pdfFile, objStart, objEnd);
                                    Console.ResetColor();

                                    if ((int)objStart <= (int)objEnd)
                                    {
                                        string innerText = msdoc.Range(ref objStart, ref objEnd).Text;
                                        doc.DocBodyDic.Add(i, innerText);
                                    }
                                }

                                success = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Retry to read word {0}, Exception: {1}..", pdfFile, ex.ToString());
                                Console.ResetColor();
                                System.Threading.Thread.Sleep(1000);
                                retry--;
                            }
                            finally
                            {
                                if (newApp != null)
                                {
                                    newApp.NormalTemplate.Saved = true;

                                    if (msdoc != null)
                                    {
                                        msdoc.Close(false);
                                    }

                                    newApp.Quit();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return success;
        }

        public void ReadText(bool rotate, string localFile, ref Documents doc)
        {
            int pages = 0;
            doc.Readable = this.ReadPdf(localFile, ref doc, ref pages);
            double percent = ((double)doc.DocBodyDic.Count) / ((double)pages);

            if (doc.DocBodyDic.Count == 0)
            //if (doc.DocBodyDic.Count == 0 || percent < 0.1)
            {
                doc.Readable = false;
                string ocrFolder = string.Format("{0}\\{1}", this.localDirectory, Path.GetFileNameWithoutExtension(localFile));

                if (Directory.Exists(ocrFolder))
                {
                    Console.WriteLine("This file {0} already OCRed!", localFile);
                    List<string> pageFiles = Directory.GetFiles(ocrFolder, "*.txt")
                        .OrderBy(t => int.Parse(Path.GetFileNameWithoutExtension(t).Split('_').LastOrDefault()))
                        .ToList();

                    foreach (string pageFile in pageFiles)
                    {
                        int page = int.Parse(Path.GetFileNameWithoutExtension(pageFile).Split('_').LastOrDefault());
                        string text = File.ReadAllText(pageFile).Trim('\r', '\n', '\t', (char)32, (char)160);
                        if (!doc.DocBodyDic.ContainsKey(page) && string.IsNullOrEmpty(text) == false)
                        {
                            doc.DocBodyDic.Add(page, text);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Page {0} could read...", page);
                            Console.ResetColor();
                        }
                    }
                }
                else
                {
                    try
                    {
                        this.OCRPdf(rotate, localFile, ref doc);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0} failed to OCR!", localFile);
                    }
                }
            }
            else
            {
                doc.Readable = true;
            }
        }

        public void OCRPdf(bool rotate, string docPath, ref Documents doc)
        {
            PdfReader pdfReader = new PdfReader(docPath);
            int totalPage = pdfReader.NumberOfPages;
            Console.WriteLine("Pdf file {0} contains {1} pages...", docPath, totalPage);
            List<int> pageNos = new List<int>();
            for (int i = 1; i <= totalPage; i++)
            {
                if (!doc.DocBodyDic.ContainsKey(i))
                {
                    pageNos.Add(i);
                }
            }

            foreach (int pageNumber in pageNos)
            {
                try
                {
                    Console.WriteLine("Working on page {0}...", pageNumber);
                    PdfReader pdf = new PdfReader(docPath);
                    PdfDictionary pg = pdf.GetPageN(pageNumber);
                    PdfDictionary res = (PdfDictionary)PdfReader.GetPdfObject(pg.Get(PdfName.RESOURCES));
                    PdfDictionary xobj = (PdfDictionary)PdfReader.GetPdfObject(res.Get(PdfName.XOBJECT));
                    foreach (PdfName name in xobj.Keys)
                    {
                        PdfObject obj = xobj.Get(name);

                        if (obj.IsIndirect())
                        {
                            PdfDictionary tg = (PdfDictionary)PdfReader.GetPdfObject(obj);
                            string width = tg.Get(PdfName.WIDTH).ToString();
                            float widthValue = float.Parse(width);
                            string height = tg.Get(PdfName.HEIGHT).ToString();
                            float heightValue = -1;
                            bool isDigit = float.TryParse(height, out heightValue);
                            heightValue = isDigit ? heightValue : widthValue;

                            if (heightValue < 100 || widthValue < 100)
                            {
                                continue;
                            }

                            ImageRenderInfo imgRI = ImageRenderInfo.CreateForXObject(new Matrix(float.Parse(width), heightValue), (PRIndirectReference)obj, tg);
                            PdfImageObject image = imgRI.GetImage();
                            string imageFileName = string.Empty;

                            using (Image dotnetImg = image.GetDrawingImage())
                            {
                                if (dotnetImg != null)
                                {
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        dotnetImg.Save(ms, ImageFormat.Jpeg);
                                    }
                                }

                                string ocrFolder = string.Format("{0}\\{1}", this.localDirectory, Path.GetFileNameWithoutExtension(docPath));

                                if (!Directory.Exists(ocrFolder))
                                {
                                    Directory.CreateDirectory(ocrFolder);
                                }

                                imageFileName = string.Format("{0}\\{1}\\Page_{2}.jpg", localDirectory, Path.GetFileNameWithoutExtension(docPath), pageNumber);
                                dotnetImg.Save(imageFileName);
                            }

                            //string text = RunOCRCommand(imageFileName);
                            string text = RetryText(imageFileName);

                            if ((!doc.DocBodyDic.ContainsKey(pageNumber)) && (!string.IsNullOrEmpty(text)))
                            {
                                doc.DocBodyDic.Add(pageNumber, text);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Page {0} could read...", pageNumber);
                                Console.ResetColor();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
            pdfReader.Close();
        }

        protected string RetryText(string imagePath)
        {
            RotateFlipType[] rotates = { RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate270FlipNone };
            string text = RunOCRCommand(imagePath);

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            foreach (RotateFlipType rotate in rotates)
            {
                Regex tagReg = new Regex("([a-z]+[A-Z]+[a-z]+)|([0-9]+[a-z]+[0-9]+[A-Z]+)|[a-zA-Z]+_[A-Za-z]*");
                var matches = tagReg.Matches(text);
                List<string> randomWords = new List<string>();
                randomWords.AddRange(new string[] {
                    "city",
                    "township",
                    "village",
                    "planning",
                    "zoning",
                    "council",
                    "commission",
                    "minute",
                    "agenda",
                    "meeting"
                });

                bool rotateOrNo = ((string.IsNullOrEmpty(text) || matches.Count > 3) && !randomWords.Exists(t => text.ToLower().Contains(t)));
                if (rotateOrNo)
                {
                    Console.WriteLine("Try {0}...", rotate);
                    Image image1 = null;
                    using (FileStream imageStream = new FileStream(imagePath, FileMode.Open))
                    using (MemoryStream msStream = new MemoryStream())
                    {
                        imageStream.CopyTo(msStream);
                        image1 = Image.FromStream(msStream);
                    }
                    image1.RotateFlip(rotate);
                    image1.Save(imagePath);
                    text = RunOCRCommand(imagePath);
                }
                else
                {
                    break;
                }
            }

            return text;
        }

        private string RunOCRCommand(string imageFile)
        {
            string text = string.Empty;
            Process ocrProcess = new Process();
            ocrProcess.StartInfo.FileName = "tesseract";
            ocrProcess.StartInfo.Arguments = string.Format(" \"{0}\" \"{1}\" -l eng",
                imageFile, imageFile.Replace(Path.GetExtension(imageFile), ""));
            ocrProcess.StartInfo.UseShellExecute = false;
            ocrProcess.StartInfo.CreateNoWindow = true;
            ocrProcess.EnableRaisingEvents = true;
            ocrProcess.Exited += (o, e) =>
                {
                    Console.WriteLine("File {0} processed...", imageFile);
                    string dataFile = imageFile.Replace(Path.GetExtension(imageFile), ".txt");
                    text = File.Exists(dataFile) ? File.ReadAllText(dataFile) : string.Empty;
                };
            ocrProcess.Start();
            ocrProcess.WaitForExit();

            return text;
        }

        protected string GetHtml(string url, string cookie)
        {
            Uri cityhomeUrl = new Uri(this.cityEntity.CityUrl);
            HttpWebRequest pageRequest = (HttpWebRequest)WebRequest.Create(url);
            pageRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.86 Safari/537.36";
            pageRequest.KeepAlive = true;
            pageRequest.AllowAutoRedirect = true;
            pageRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
            pageRequest.Referer = url;
            pageRequest.Host = cityhomeUrl.Host;
            pageRequest.ProtocolVersion = HttpVersion.Version11;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            pageRequest.CookieContainer = this.container;

            if (!string.IsNullOrEmpty(cookie))
            {
                string[] cookies = cookie.Split(new string[] { "; " }, StringSplitOptions.None);
                foreach (string cookieItem in cookies)
                {
                    System.Net.Cookie ck = new System.Net.Cookie();
                    string domain = cityhomeUrl.Host;
                    string path = "/";
                    ck.Path = path;
                    ck.Domain = domain;
                    ck.Name = cookieItem.Split('=').FirstOrDefault().Trim(' ');
                    ck.Value = HttpUtility.UrlEncode(cookieItem.Split('=').LastOrDefault().Trim(' '));
                    this.container.Add(ck);
                }
            }

            HttpWebResponse pageResponse = (HttpWebResponse)pageRequest.GetResponse();
            string html = null;
            //StringBuilder htmlBuilder = new StringBuilder();

            using (Stream pageRs = pageResponse.GetResponseStream())
            using (StreamReader htmlReader = new StreamReader(pageRs))
            {
                html = htmlReader.ReadToEnd();
                //while(htmlReader.Peek() > -1)
                //{
                //    htmlBuilder.Append(htmlReader.ReadLine());
                //}
            }

            return html;
        }
        
        public void DetectStartDate()
        {
            if (Directory.Exists(localDirectory))
            {
                List<string> files = Directory.GetFiles(localDirectory).ToList();

                if(files.Count > 0)
                {
                    this.dtStartFrom = DateTime.Now.AddMonths(-2);
                }
            }
        }
    }
}
