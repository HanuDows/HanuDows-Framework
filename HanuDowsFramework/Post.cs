using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Web.Http;

namespace HanuDowsFramework
{
    public sealed class Post
    {
        private int _id;
        private string _author, _title, _content;
        private DateTime _pubDate, _modDate;
        private Dictionary<string, string> _metaData;
        internal List<string> _categories, _tags;
        private List<PostComment> _postComments;

        public int PostID
        {
            get { return _id; }
            set { _id = value; }
        }

        public string PostAuthor
        {
            get { return _author; }
            set { _author = value; }
        }

        public string PostTitle
        {
            get { return _title; }
            set { _title = value; }
        }

        public string PostContent
        {
            get { return _content; }
            set { _content = value; }
        }

        public string ShareableContent
        {
            get
            {
                return getSharableContent();
            }
        }

        public string PubDate
        {
            get { return _pubDate.ToString(CultureInfo.CurrentCulture.DateTimeFormat); }
            set { _pubDate = DateTime.Parse(value); }
        }

        public string ModDate
        {
            get { return _modDate.ToString(); }
            set { _modDate = DateTime.Parse(value); }
        }

        public Post()
        {
            _metaData = new Dictionary<string, string>();
            _categories = new List<string>();
            _tags = new List<string>();
            _postComments = new List<PostComment>();
            _title = _content = "DUMMY";
            _pubDate = new DateTime();
        }

        public void addMetaData(string metaKey, string metaValue)
        {
            _metaData.Add(metaKey, metaValue);
        }

        public void addCategory(string category)
        {
            _categories.Add(category);
        }

        public void addTag(string tag)
        {
            _tags.Add(tag);
        }

        public void addPostcomment(PostComment comment)
        {
            _postComments.Add(comment);
        }

        internal static int CompareByPubDate(Post x, Post y)
        {
            return y._pubDate.CompareTo(x._pubDate);
        }

        internal async Task<bool> saveToDB()
        {
            /* First find out if there are any images in post.
             * If there are, then save them to local folder.
             * Then save the post to DB
             */

            int start = 0, end = 0, s = 0, e = 0, index = 0, newStart = 0;
            int counter = 0;
            string subStr = "", replaceStr;
            string fileName, file_extension;

            do
            {

                start = _content.IndexOf("<a", newStart);
                end = _content.IndexOf("</a>", newStart);

                if (start >= 0 && end > 0)
                {
                    subStr = _content.Substring(start, end-start);
                }

                if (start >= 0 && end > 0 && subStr.Contains("<img"))
                {
                    // We found something.
                    replaceStr = subStr = _content.Substring(start, end-start);
                    s = subStr.IndexOf("src=\"") + 5;
                    e = subStr.IndexOf('"', s);
                    subStr = subStr.Substring(s, e-s);    // This is Image URL.

                    index = subStr.LastIndexOf(".");
                    file_extension = subStr.Substring(index, 4);
                    fileName = _id + "-" + counter + file_extension;

                    // Download Image.
                    string file = await downloadImage(subStr, fileName);

                    // If download is success. Replace the File name
                    string imgSrc = "<img class=\"alignnone\" src=\"file:" + file + "\">";

                    _content = _content.Replace(replaceStr, imgSrc);
                    counter++;

                }

                newStart = end + 3;

            } while (start > 0 && end > 0);


            // Now start saving to DB
            String sql;                 // The query itself
            DBQuery query;              // Query Object
            List<DBQuery> queryList = new List<DBQuery>();

            // Check if this post exists in DB
            DBHelper db = DBHelper.getInstance();
            if (db.checkPostExists(_id))
            {
                // Update Post table
                sql = @"UPDATE Post SET Title = ?, PubDate = ?, ModDate = ?, PostContent = ? WHERE Id = ?";

                query = new DBQuery();
                query.Query = sql;

                query.addQueryData(_title);
                query.addQueryData(_pubDate);
                query.addQueryData(_modDate);
                query.addQueryData(_content);
                query.addQueryData(_id);

                queryList.Add(query);

                // Update Meta Data
                foreach (KeyValuePair<string, string> metadata in _metaData)
                {
                    sql = @"UPDATE PostMeta SET MetaValue=? WHERE Id=? AND MetaKey=?;";

                    query = new DBQuery();
                    query.Query = sql;

                    query.addQueryData(metadata.Value);
                    query.addQueryData(_id);
                    query.addQueryData(metadata.Key);

                    queryList.Add(query);
                }

                // Update Comments
                foreach (PostComment comment in _postComments)
                {
                    queryList.Add(comment.UpsertQuery());
                }

                // Delete categories and tags. And insert again
                sql = @"DELETE FROM Terms WHERE PostId=?";

                query = new DBQuery();
                query.Query = sql;
                query.addQueryData(_id);
                queryList.Add(query);

                // Add queries for categories
                addQueriesForCategories(queryList);

                // Add queries for tags.
                addQueriesForTags(queryList);
            }
            else
            {
                // Insert new post
                sql = @"INSERT INTO Post (Id, PubDate, ModDate, Author, Title, PostContent) 
                        VALUES (?,?,?,?,?,?);";

                query = new DBQuery();
                query.Query = sql;

                query.addQueryData(_id);
                query.addQueryData(_pubDate);
                query.addQueryData(_modDate);
                query.addQueryData(_author);
                query.addQueryData(_title);
                query.addQueryData(_content);

                queryList.Add(query);

                // Update Meta Data
                foreach (KeyValuePair<string, string> metadata in _metaData)
                {
                    sql = @"INSERT INTO PostMeta (PostId, MetaKey, MetaValue) VALUES (?,?,?);";

                    query = new DBQuery();
                    query.Query = sql;

                    query.addQueryData(_id);
                    query.addQueryData(metadata.Key);
                    query.addQueryData(metadata.Value);

                    queryList.Add(query);
                }

                // New Comments
                foreach (PostComment comment in _postComments)
                {
                    queryList.Add(comment.UpsertQuery());
                }

                // Add queries for categories
                addQueriesForCategories(queryList);

                // Add queries for tags.
                addQueriesForTags(queryList);

            }

            // Now Execute Queries
            return DBHelper.getInstance().executeQueries(queryList);
        }

        private void addQueriesForTags(List<DBQuery> queryList)
        {
            foreach (string tag in _tags)
            {
                string sql = @"INSERT INTO Terms (PostId, Taxonomy, Name) VALUES (?,?,?);";

                DBQuery query = new DBQuery();
                query.Query = sql;
                query.addQueryData(_id);
                query.addQueryData("post_tag");
                query.addQueryData(tag);
                queryList.Add(query);
            }
        }

        private void addQueriesForCategories(List<DBQuery> queryList)
        {
            foreach (string category in _categories)
            {
                string sql = @"INSERT INTO Terms (PostId, Taxonomy, Name) VALUES (?,?,?);";

                DBQuery query = new DBQuery();
                query.Query = sql;
                query.addQueryData(_id);
                query.addQueryData("category");
                query.addQueryData(category);
                queryList.Add(query);
            }
        }

        private async Task<string> downloadImage(string imageURL, string fileName)
        {
            using (HttpClient hc = new HttpClient())
            {
                StorageFolder storageFolder = ApplicationData.Current.LocalFolder;

                // Create Images Folder
                StorageFolder imageFolder = await storageFolder.CreateFolderAsync("images", CreationCollisionOption.OpenIfExists);

                // Create Folder for Post - This is helpful while deleting the post.
                StorageFolder postFolder = await imageFolder.CreateFolderAsync(_id.ToString(), CreationCollisionOption.OpenIfExists);

                StorageFile sampleFile = await postFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                Uri uri = new Uri(imageURL);

                var buffer = await hc.GetBufferAsync(uri);
                await FileIO.WriteBufferAsync(sampleFile, buffer);
                return sampleFile.Name;
            }
        }

        public bool HasCategory(string category)
        {
            bool has_category = false;

            foreach (string cat in _categories)
            {
                if (category.Equals(cat))
                {
                    has_category = true;
                    break;
                }
            }

            return has_category;
        }

        public string getHTMLCode()
        {
            string user_count;
            string html = "<html>" +

                // HTML HEAD
                "<head>" +

                // Meta
                "<meta content=text/html; />" +

                // Java Script

                // CSS
                "<style>" +
                "h3 {color:blue;font-family:arial,helvetica,sans-serif;}" +
                "#pub_date {color:black;font-family:verdana,geneva,sans-serif;font-size:14px;}" +
                "#content {color:black;font-family:arial,helvetica,sans-serif; font-size:18px;}" +
                ".taxonomy {color:black;font-family:arial,helvetica,sans-serif; font-size:14px;}" +
                "#comments {color:black;font-family:arial,helvetica,sans-serif; font-size:16px;}" +
                "#ratings {color:black; font-family:verdana,geneva,sans-serif; font-size:14px;}" +
                "#footer {color:black; font-family:verdana,geneva,sans-serif; font-size:14px;}" +
                "</style>" +

                "</head>" +

                // HTML Body
                "<body>" +

                // Heading
                "<h3>" + PostTitle + "</h3>" +

                // Pub Date
                "<div id=\"pub_date\">" + PubDate + "</div>" +
                "<hr />" +

                // Content
                "<div id=\"content\">" + PostContent + "</div>" +
                "<hr />" +

                // Author
                "<div class=\"taxonomy\">" +
                "by <a href=\"javascript:loadPosts('author','" + PostAuthor + "')\">" + PostAuthor + "</a>" +
                "</div>";

            // Ratings
            //*
            if (_metaData.TryGetValue("ratings_users", out user_count))
            {
                string avj_rating;
                _metaData.TryGetValue("ratings_average", out avj_rating);

                // We have some ratings !
                html = html + "<div id=\"ratings\">" + "<br>Rating: " + decimal.Parse(avj_rating).ToString("N2") + " / 5 (by " + user_count + " users)";
                html = html + "</div>";
            }
            //*/

            // Footer
            html = html + "<br /><hr />" + "<div id=\"footer\">"
                    + "Powered by <a href=\"http://hanu-droid.varunverma.org\">Hanu-Droid framework</a>"
                    + "</div>" +

                    "</body>" +
                    "</html>";

            return html;

        }

        private string getSharableContent()
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(_content);

            StringWriter sw = new StringWriter();
            ConvertTo(doc.DocumentNode, sw);
            sw.Flush();

            return sw.ToString();
        }

        private void ConvertTo(HtmlNode node, TextWriter outText)
        {
            string html;
            switch (node.NodeType)
            {
                case HtmlNodeType.Comment:
                    // don't output comments
                    break;

                case HtmlNodeType.Document:
                    ConvertContentTo(node, outText);
                    break;

                case HtmlNodeType.Text:
                    // script and style must not be output
                    string parentName = node.ParentNode.Name;
                    if ((parentName == "script") || (parentName == "style"))
                        break;

                    // get text
                    html = ((HtmlTextNode)node).Text;

                    // is it in fact a special closing node output as text?
                    if (HtmlNode.IsOverlappedClosingElement(html))
                        break;

                    // check the text is meaningful and not a bunch of whitespaces
                    if (html.Trim().Length > 0)
                    {
                        outText.Write(HtmlEntity.DeEntitize(html));
                    }
                    break;

                case HtmlNodeType.Element:
                    switch (node.Name)
                    {
                        case "p":
                            // treat paragraphs as crlf
                            outText.Write("\r\n");
                            break;

                        case "br":
                            // treat paragraphs as crlf
                            outText.Write("\r");
                            break;
                    }

                    if (node.HasChildNodes)
                    {
                        ConvertContentTo(node, outText);
                    }
                    break;
            }
        }

        private void ConvertContentTo(HtmlNode node, TextWriter outText)
        {
            foreach (HtmlNode subnode in node.ChildNodes)
            {
                ConvertTo(subnode, outText);
            }
        }

    }

}
