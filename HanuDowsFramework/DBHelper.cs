using SQLitePCL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace HanuDowsFramework
{
    class DBHelper
    {
        private static DBHelper instance;

        private string dbPath;

        public static DBHelper getInstance()
        {
            if (instance == null)
            {
                instance = new DBHelper();
            }

            return instance;
        }

        private DBHelper()
        {
            // Get Application ID
            ResourceLoader rl = new ResourceLoader();
            string app_id = rl.GetString("ApplicationID");

            // Made DB Path
            dbPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, app_id);

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            if (localSettings.Values["DBExists"] == null)
            {
                // DB Does not exist. So create
                createDB();
                localSettings.Values["DBExists"] = "X";
            }
            else
            {
                // Nothing to do.
            }
        }

        private void createDB()
        {
            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {

                string createPostsTable = "CREATE TABLE Post (" +
                            "Id INTEGER PRIMARY KEY, " +    // Id
                            "PubDate INTEGER, " +           // PublishDate	Time in MilliSec
                            "ModDate INTEGER, " +           // Modified Date
                            "Author VARCHAR(10), " +        // Author
                            "Title VARCHAR(20), " +         // Title of post
                            "PostContent TEXT" +            // Post Content
                            ")";

                string createPostMetaTable = "CREATE TABLE PostMeta (" +
                            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +      // Primary Key
                            "PostId INTEGER, " +                            // Id of the Post
                            "MetaKey VARCHAR(20), " +                       // Meta Key
                            "MetaValue VARCHAR(20)" +                       // Meta Value
                            ")";

                string createCommentsTable = "CREATE TABLE Comments (" +
                            "CommentId INTEGER PRIMARY KEY, " +     // Comment Id
                            "PostId INTEGER, " +                    // Post Id
                            "Author VARCHAR(10), " +                // Author
                            "AuthorEmail VARCHAR(20), " +           // Author Email
                            "CommentDate VARCHAR(20), " +           // Comment Date
                            "CommentParent INTEGER, " +             // Comment Parent
                            "CommentsContent TEXT, " +              // Comments Content
                            "SyncStatus VARCHAR(1)" +               // Sync Status
                            ")";

                string createTermsTable = "CREATE TABLE Terms (" +
                            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +      // Primary Key
                            "PostId INTEGER, " +                            // Id of the Post
                            "Taxonomy VARCHAR(10), " +                      // Taxonomy
                            "Name VARCHAR(20)" +                            // Name
                            ")";

                string createSyncStatusTable = "CREATE TABLE SyncStatus (" +
                            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +      // Primary Key
                            "Type VARCHAR(20), " +                          // Type: Comment / Rating
                            "SyncId INTEGER" +                              // Comment Id / Rating Id
                            ")";

                // Create View also.
                string createPostTermView = "CREATE VIEW PostTerm AS " +
                                "SELECT a.*, b.* FROM Post As a, Terms as b WHERE a.Id = b.PostId";


                ISQLiteStatement statement;
                statement = dbConn.Prepare(createPostsTable);
                statement.Step();

                statement = dbConn.Prepare(createPostMetaTable);
                statement.Step();

                statement = dbConn.Prepare(createCommentsTable);
                statement.Step();

                statement = dbConn.Prepare(createTermsTable);
                statement.Step();

                statement = dbConn.Prepare(createSyncStatusTable);
                statement.Step();

                statement = dbConn.Prepare(createPostTermView);
                statement.Step();

                //Now create FTS Tables for search.
                string createPostFTSTable = "CREATE VIRTUAL TABLE PostIndex USING fts3 (" +
                                "content=\"Post\", " +
                                "Title, " +
                                "PostContent" +
                                ")";

                string createCommentFTSTable = "CREATE VIRTUAL TABLE CommentsIndex USING fts3 (" +
                                "content=\"Comments\", " +
                                "PostId, " +
                                "CommentsContent" +
                                ")";

                // Creating Triggers
                string post_bu = "CREATE TRIGGER Post_bu BEFORE UPDATE ON Post BEGIN DELETE FROM PostIndex WHERE docid=old.Id; END;";
                string post_bd = "CREATE TRIGGER Post_bd BEFORE DELETE ON Post BEGIN DELETE FROM PostIndex WHERE docid=old.Id; END;";
                string post_au = "CREATE TRIGGER Post_au AFTER UPDATE ON Post BEGIN INSERT INTO PostIndex(docid, Title, PostContent) VALUES(new.Id, new.Title, new.PostContent); END;";
                string post_ai = "CREATE TRIGGER Post_ai AFTER INSERT ON Post BEGIN INSERT INTO PostIndex(docid, Title, PostContent) VALUES(new.Id, new.Title, new.PostContent); END;";

                string comments_bu = "CREATE TRIGGER Comments_bu BEFORE UPDATE ON Comments BEGIN DELETE FROM CommentsIndex WHERE docid=old.CommentId; END;";
                string comments_bd = "CREATE TRIGGER Comments_bd BEFORE DELETE ON Comments BEGIN DELETE FROM CommentsIndex WHERE docid=old.CommentId; END;";
                string comments_au = "CREATE TRIGGER Comments_au AFTER UPDATE ON Comments BEGIN INSERT INTO CommentsIndex(docid, PostId, CommentsContent) VALUES(new.CommentId, new.PostId, new.CommentsContent); END;";
                string comments_ai = "CREATE TRIGGER Comments_ai AFTER INSERT ON Comments BEGIN INSERT INTO CommentsIndex(docid, PostId, CommentsContent) VALUES(new.CommentId, new.PostId, new.CommentsContent); END;";

                statement = dbConn.Prepare(createPostFTSTable);
                statement.Step();

                statement = dbConn.Prepare(createCommentFTSTable);
                statement.Step();

                statement = dbConn.Prepare(post_bu);
                statement.Step();

                statement = dbConn.Prepare(post_bd);
                statement.Step();

                statement = dbConn.Prepare(post_au);
                statement.Step();

                statement = dbConn.Prepare(post_ai);
                statement.Step();

                statement = dbConn.Prepare(comments_bu);
                statement.Step();

                statement = dbConn.Prepare(comments_bd);
                statement.Step();

                statement = dbConn.Prepare(comments_au);
                statement.Step();

                statement = dbConn.Prepare(comments_ai);
                statement.Step();

                statement.Dispose();

            }

        }

        internal List<Post> LoadPostData(int startPoint, int batchSize)
        {
            List<Post> postList = new List<Post>();

            //string sql = "select * from Post ORDER BY Id DESC LIMIT ? OFFSET ?";
            string sql = "select * from Post ORDER BY PubDate DESC LIMIT ? OFFSET ?";

            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {
                ISQLiteStatement statement;

                statement = dbConn.Prepare(sql);
                statement.Bind(1, batchSize);
                statement.Bind(2, startPoint);

                while (statement.Step() == SQLiteResult.ROW)
                {
                    // Post Data
                    Post post = new Post();
                    post.PostID = (int)((long)statement[0]);
                    post.PubDate = statement[1].ToString();
                    post.ModDate = statement[2].ToString();
                    post.PostAuthor = statement[3].ToString();
                    post.PostTitle = statement[4].ToString();
                    post.PostContent = statement[5].ToString();

                    // Post Meta Data
                    sql = "select * from PostMeta where PostId = ?";
                    using (var metaStatement = dbConn.Prepare(sql))
                    {
                        metaStatement.Bind(1, post.PostID);
                        while (metaStatement.Step() == SQLiteResult.ROW)
                        {
                            string metaKey = metaStatement[2].ToString();
                            string metaVal = metaStatement[3].ToString();
                            post.addMetaData(metaKey, metaVal);
                        }
                    }

                    // Post Comments Data
                    sql = "select * from Comments where PostId = ? order by CommentId ASC";
                    using (var commentStatement = dbConn.Prepare(sql))
                    {
                        commentStatement.Bind(1, post.PostID);
                        while (commentStatement.Step() == SQLiteResult.ROW)
                        {
                            PostComment comment = new PostComment();
                            comment.CommentID = (int)((long)commentStatement[0]);
                            comment.PostId = (int)((long)commentStatement[1]);
                            comment.Author = commentStatement[2].ToString();
                            comment.Email = commentStatement[3].ToString();
                            comment.CommentDate = commentStatement[4].ToString();
                            comment.ParentCommentId = (int)((long)commentStatement[5]);
                            comment.Content = commentStatement[6].ToString();
                            post.addPostcomment(comment);
                        }
                    }

                    // Post Terms (Categories and Tags)
                    sql = "select * from Terms where PostId = ?";
                    using (var termStatement = dbConn.Prepare(sql))
                    {
                        termStatement.Bind(1, post.PostID);
                        while (termStatement.Step() == SQLiteResult.ROW)
                        {
                            string term_taxonomy = termStatement[2].ToString();
                            string term_name = termStatement[3].ToString();
                            if (term_taxonomy.Equals("category"))
                            {
                                post.addCategory(term_name);
                            }
                            if (term_taxonomy.Equals("post_tag"))
                            {
                                post.addTag(term_name);
                            }

                        }
                    }

                    postList.Add(post);
                }

                statement.Dispose();
            }

            return postList;
            
        }

        internal List<Post> LoadPostData(string taxonomy, string name)
        {
            List<Post> postList = new List<Post>();
            string sql = "";

            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {
                ISQLiteStatement statement;

                if (taxonomy != null && name != null)
                {
                    if (taxonomy.Equals("author"))
                    {
                        sql = "select * from Post WHERE Author = ?;";
                        statement = dbConn.Prepare(sql);
                        statement.Bind(1, name);
                    }
                    else
                    {
                        sql = "select * from PostTerm WHERE Taxonomy = ? and Name = ?;";
                        statement = dbConn.Prepare(sql);
                        statement.Bind(1, taxonomy);
                        statement.Bind(2, name);
                    }

                }
                else
                {
                    // Select all
                    sql = "select * from Post;";
                    statement = dbConn.Prepare(sql);
                }

                while (statement.Step() == SQLiteResult.ROW)
                {
                    // Post Data
                    Post post = new Post();
                    post.PostID = (int)((long)statement[0]);
                    post.PubDate = statement[1].ToString();
                    post.ModDate = statement[2].ToString();
                    post.PostAuthor = statement[3].ToString();
                    post.PostTitle = statement[4].ToString();
                    post.PostContent = statement[5].ToString();

                    // Post Meta Data
                    sql = "select * from PostMeta where PostId = ?";
                    using (var metaStatement = dbConn.Prepare(sql))
                    {
                        metaStatement.Bind(1, post.PostID);
                        while (metaStatement.Step() == SQLiteResult.ROW)
                        {
                            string metaKey = metaStatement[2].ToString();
                            string metaVal = metaStatement[3].ToString();
                            post.addMetaData(metaKey, metaVal);
                        }
                    }

                    // Post Comments Data
                    sql = "select * from Comments where PostId = ? order by CommentId ASC";
                    using (var commentStatement = dbConn.Prepare(sql))
                    {
                        commentStatement.Bind(1, post.PostID);
                        while (commentStatement.Step() == SQLiteResult.ROW)
                        {
                            PostComment comment = new PostComment();
                            comment.CommentID = (int)((long)commentStatement[0]);
                            comment.PostId = (int)((long)commentStatement[1]);
                            comment.Author = commentStatement[2].ToString();
                            comment.Email = commentStatement[3].ToString();
                            comment.CommentDate = commentStatement[4].ToString();
                            comment.ParentCommentId = (int)((long)commentStatement[5]);
                            comment.Content = commentStatement[6].ToString();
                            post.addPostcomment(comment);
                        }
                    }

                    // Post Terms (Categories and Tags)
                    sql = "select * from Terms where PostId = ?";
                    using (var termStatement = dbConn.Prepare(sql))
                    {
                        termStatement.Bind(1, post.PostID);
                        while (termStatement.Step() == SQLiteResult.ROW)
                        {
                            string term_taxonomy = termStatement[2].ToString();
                            string term_name = termStatement[3].ToString();
                            if (term_taxonomy.Equals("category"))
                            {
                                post.addCategory(term_name);
                            }
                            if (term_taxonomy.Equals("post_tag"))
                            {
                                post.addTag(term_name);
                            }

                        }
                    }

                    postList.Add(post);
                }

                statement.Dispose();
            }

            return postList;
        }

        internal Dictionary<int, PostArtifact> getDBPostArtifacts(string post_ids)
        {
            Dictionary<int, PostArtifact> postArtifacts = new Dictionary<int, PostArtifact>();

            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {
                string sql = @"select p.Id, p.PubDate, p.ModDate, max(c.CommentDate) from Post as P 
                                left outer join Comments as c on p.Id = c.PostId where p.Id IN (?) group by p.Id;";

                using (var statement = dbConn.Prepare(sql))
                {
                    statement.Bind(1, post_ids);

                    while (statement.Step() == SQLiteResult.ROW)
                    {
                        PostArtifact pf = new PostArtifact();
                        pf.PostID = (int)((long)statement[0]);
                        pf.PubDate = DateTime.Parse(statement[1].ToString());
                        pf.ModDate = DateTime.Parse(statement[2].ToString());

                        try
                        {
                            pf.CommentDate = DateTime.Parse(statement[3].ToString());
                        }
                        catch
                        {
                            pf.CommentDate = new DateTime(2011, 11, 4);
                        }

                        postArtifacts.Add(pf.PostID, pf);
                    }
                }

            }

            return postArtifacts;
        }

        internal bool executeQueries(List<DBQuery> queryList)
        {
            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {
                var statement = dbConn.Prepare("BEGIN TRANSACTION");
                statement.Step();

                foreach (DBQuery query in queryList)
                {
                    statement = dbConn.Prepare(query.Query);
                    int i = 1;

                    foreach (Object data in query.QueryData)
                    {
                        statement.Bind(i, data);
                        i++;
                    }

                    statement.Step();
                }

                statement = dbConn.Prepare("COMMIT TRANSACTION");
                statement.Step();
            }

            return true;
        }

        internal bool checkPostExists(int postID)
        {
            var exists = false;

            using (SQLiteConnection dbConn = new SQLiteConnection(dbPath))
            {
                string sql = @"select * from Post where Id = ?;";

                using (var statement = dbConn.Prepare(sql))
                {
                    statement.Bind(1, postID);
                    statement.Step();

                    if (statement.DataCount != 0)
                    {
                        exists = true;
                    }
                    else
                    {
                        exists = false;
                    }
                }
            }

            return exists;
        }
    }
}
