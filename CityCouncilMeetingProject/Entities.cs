using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace CityCouncilMeetingProject
{
    public class CityInfo
    {
        public string CityId
        {
            get;
            set;
        }

        public string CityName
        {
            get;
            set;
        }

        public string CityUrl
        {
            get;
            set;
        }

        public string StateCode
        {
            get;
            set;
        }
    }

    public class QueryResult
    {
        private DateTime searchTime = DateTime.MinValue;

        public class KeywordEntry
        {
            private List<string> contents = new List<string>();

            public int PageNumber { get; set; }

            public string Keyword { get; set; }

            public List<string> Contents
            {
                get
                {
                    return contents;
                }
                set
                {
                    this.contents = value;
                }
            }

            public Dictionary<string, string> CommentDic
            {
                get;
                set;
            }

            public Dictionary<string, string> GuidDic
            {
                get;
                set;
            }
        }

        private List<KeywordEntry> entries = new List<KeywordEntry>();

        public string QueryId
        {
            get;
            set;
        }

        public string CityId
        {
            get;
            set;
        }

        public DateTime SearchTime
        {
            get
            {
                return this.searchTime;
            }
            set
            {
                this.searchTime = value;
            }
        }

        public DateTime MeetingDate
        {
            get;
            set;
        }

        public string MeetingTitle
        {
            get;
            set;
        }

        public string MeetingLocation
        {
            get;
            set;
        }

        public string DocId
        {
            get;
            set;
        }

        public List<KeywordEntry> Entries
        {
            get
            {
                return this.entries;
            }
            set
            {
                this.entries = value;
            }
        }

        public List<string> ToList1()
        {
            List<string> lines = new List<string>();

            if (this.entries != null && this.entries.Count > 0)
            {
                foreach (KeywordEntry entry in entries)
                {
                    foreach (string line in entry.Contents)
                    {
                        string oneLine = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",",
                            Guid.NewGuid().ToString(), this.CityId, this.DocId, this.MeetingTitle, this.MeetingDate, this.SearchTime, this.MeetingLocation, entry.Keyword, entry.PageNumber, line);
                        oneLine = oneLine.Replace("\r\n", " ");
                        if (!lines.Exists(t => t.Contains(entry.Keyword) && t.Contains(line)))
                        {
                            lines.Add(oneLine);
                        }
                    }
                }
            }

            return lines;
        }

        public override bool Equals(object qrObj)
        {
            QueryResult qr = qrObj as QueryResult;
            if (qr.MeetingDate != this.MeetingDate)
            {
                return false;
            }

            if (qr.entries.Count != this.entries.Count)
            {
                return false;
            }

            if (qr.entries.FirstOrDefault().PageNumber != this.entries.FirstOrDefault().PageNumber)
            {
                return false;
            }

            if (qr.entries.FirstOrDefault().Keyword != this.entries.FirstOrDefault().Keyword)
            {
                return false;
            }

            if (qr.entries.FirstOrDefault().Contents.FirstOrDefault() != this.entries.FirstOrDefault().Contents.FirstOrDefault())
            {
                return false;
            }

            return true;
        }

        public XElement ToXElement()
        {
            XElement qrEle = new XElement("Query");

            XElement docIdEle = new XElement("DocId");
            docIdEle.Value = string.IsNullOrEmpty(this.DocId) ? string.Empty : this.DocId;
            qrEle.Add(docIdEle);
            XElement cityIdEle = new XElement("CityId");
            cityIdEle.Value = string.IsNullOrEmpty(this.CityId ) ? string.Empty : this.CityId;
            qrEle.Add(cityIdEle);
            //XElement queryIdEle = new XElement("QueryId");
            //queryIdEle.Value = string.IsNullOrEmpty(this.QueryId) ? Guid.NewGuid().ToString() : this.QueryId;
            //qrEle.Add(queryIdEle);
            XElement meetingDateEle = new XElement("MeetingDate");
            meetingDateEle.Value = this.MeetingDate.ToString("yyyy-MM-dd");
            qrEle.Add(meetingDateEle);
            XElement searchTimeEle = new XElement("SearchDate");
            searchTimeEle.Value = this.searchTime.ToString("yyyy-MM-dd");
            qrEle.Add(searchTimeEle);
            XElement meetingTitleEle = new XElement("MeetingTitle");
            meetingTitleEle.Value = string.IsNullOrEmpty(this.MeetingTitle) ? string.Empty : this.MeetingTitle;
            qrEle.Add(meetingTitleEle);
            XElement meetingLocationEle = new XElement("MeetingLocation");
            meetingLocationEle.Value = string.IsNullOrEmpty(this.MeetingLocation) ? string.Empty : this.MeetingLocation;
            qrEle.Add(meetingLocationEle);

            XElement entriesEle = new XElement("Entries");
            foreach (KeywordEntry entry in this.entries)
            {
                XElement entryEle = new XElement("Entry");
                XElement pageNoEle = new XElement("PageNumber");
                pageNoEle.Value = entry.PageNumber.ToString();
                entryEle.Add(pageNoEle);
                XElement keywordEle = new XElement("Keyword");
                keywordEle.Value = entry.Keyword;
                entryEle.Add(keywordEle);
                XElement contentElements = new XElement("Contents");
                foreach (string content in entry.Contents)
                {
                    XElement contentEle = new XElement("Content");
                    //contentEle.Value = content;
                    contentEle.Value = Regex.Replace(content, "([\\u0000-\\u0008]+)", string.Empty);
                    contentEle.Value = contentEle.Value.Replace(((char)11).ToString(), string.Empty);
                    contentEle.Value = contentEle.Value.Replace(((char)12).ToString(), string.Empty);
                    contentEle.Value = Regex.Replace(contentEle.Value, "([\\u000e-\\u001f]+)", string.Empty);
                    string comment = entry.CommentDic != null && entry.CommentDic.ContainsKey(content) ? entry.CommentDic[content] : string.Empty;
                    XAttribute commentAttr = new XAttribute("Comment", comment);
                    contentEle.Add(commentAttr);
                    string guid = entry.GuidDic != null && entry.GuidDic.ContainsKey(content) ? entry.GuidDic[content] : Guid.NewGuid().ToString();
                    XAttribute guidAttr = new XAttribute("GUID", guid);
                    contentEle.Add(guidAttr);
                    contentElements.Add(contentEle);
                }
                entryEle.Add(contentElements);
                entriesEle.Add(entryEle);
            }
            qrEle.Add(entriesEle);
            return qrEle;
        }
    }

    public class Documents
    {
        private Dictionary<int, string> docBodyDic = new Dictionary<int, string>();

        public string DocId
        {
            get;
            set;
        }

        public string DocType
        {
            get;
            set;
        }

        public string CityId
        {
            get;
            set;
        }

        public string DocLocalPath
        {
            get;
            set;
        }
        
        public Dictionary<int, string> DocBodyDic
        {
            get
            {
                return this.docBodyDic;
            }
            set
            {
                this.docBodyDic = value;
            }
        }

        public string DocSource
        {
            get;
            set;
        }

        public bool Readable { get; set; }

        public bool Checked { get; set; }

        public bool Important { get; set; }

        public XElement ToXElement()
        {
            XElement docEle = new XElement("Doc");
            XElement cityEle = new XElement("CityId");
            cityEle.Value = string.IsNullOrEmpty(this.CityId) ? string.Empty : this.CityId;
            docEle.Add(cityEle);
            XElement docIdEle = new XElement("DocId");
            docIdEle.Value = string.IsNullOrEmpty(this.DocId) ? string.Empty : this.DocId;
            docEle.Add(docIdEle);
            XElement docTypeEle = new XElement("DocType");
            docTypeEle.Value = string.IsNullOrEmpty(this.DocType) ? string.Empty : this.DocType;
            docEle.Add(docTypeEle);
            XElement docSourceEle = new XElement("DocSource");
            docSourceEle.Value = string.IsNullOrEmpty(this.DocSource) ? string.Empty : this.DocSource;
            docEle.Add(docSourceEle);
            XElement docLocalPathEle = new XElement("DocLocalPath");
            docLocalPathEle.Value = string.IsNullOrEmpty(this.DocLocalPath) ? string.Empty : this.DocLocalPath;
            docEle.Add(docLocalPathEle);
            XElement checkedEle = new XElement("Checked");
            checkedEle.Value = this.Checked.ToString();
            docEle.Add(checkedEle);
            XElement importantEle = new XElement("Important");
            importantEle.Value = this.Important.ToString();
            docEle.Add(importantEle);
            XElement readableEle = new XElement("Readable");
            readableEle.Value = this.Readable.ToString();
            docEle.Add(readableEle);
            return docEle;
        }

        public override string ToString()
        {
            return string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",",
                this.CityId, this.DocId, this.DocLocalPath, this.DocType, this.DocSource, this.Readable, this.Checked ? "Yes" : "No");
        }
    }
}
