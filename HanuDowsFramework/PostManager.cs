using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Web;
using Windows.Web.Http;

namespace HanuDowsFramework
{
    public class PostManager
    {

        private static PostManager instance;

        private List<Post> _postList;
        private List<Post> _downloadedPostList;
        private List<PostArtifact> _postArtifacts;

        public List<Post> DownloadedPostList
        {
            get { return _downloadedPostList; }
            set { _downloadedPostList.AddRange(value); }
        }

        public List<Post> PostList
        {
            get { return _postList; }
            set
            {
                _postList.AddRange(value);
                _postList.Sort(Post.CompareByPubDate);
            }
        }

        public static PostManager getInstance()
        {
            if (instance == null)
            {
                instance = new PostManager();
            }

            return instance;
        }

        private PostManager()
        {
            _postList = new List<Post>();
            _downloadedPostList = new List<Post>();
            _postArtifacts = new List<PostArtifact>();
        }

        public void addToDownloadedList(Post post)
        {
            _downloadedPostList.Add(post);
        }

        internal async Task<bool> savePostsToDB()
        {
            bool allGood = true;
            foreach (Post post in _downloadedPostList.ToList())
            {
                bool success = await post.saveToDB();
                if (success)
                {
                    // Good. Remove from Download list and add to Display List
                    _downloadedPostList.Remove(post);
                }
                else
                {
                    allGood = false;
                }
            }

            return allGood;
        }

        internal async Task<int> fetchPostArtifacts()
        {
            // Hanu Epoch time.
            DateTime lastSyncTime = new DateTime(2011, 11, 4);
            DateTime now = DateTime.Now;
            string post_id_list = "";

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            if (localSettings.Values["LastSyncTime"] != null)
            {
                // This is not the first use. 
                lastSyncTime = DateTime.Parse(localSettings.Values["LastSyncTime"].ToString());
            }

            var iid = localSettings.Values["InstanceID"];

            TimeSpan interval = now.Subtract(lastSyncTime);

            if (interval.Minutes < 5)
            {
                return 0;
            }

            // It has been more than 5 min since we checked, so we can check again.
            var syncData = new JObject();

            if (localSettings.Values["SyncCategory"] != null)
            {
                syncData.Add("category", localSettings.Values["SyncCategory"].ToString());
            }

            if (localSettings.Values["SyncTags"] != null)
            {
                syncData.Add("tag", localSettings.Values["SyncTags"].ToString());
            }

            string sync_params = JsonConvert.SerializeObject(syncData);

            using (HttpClient hc = new HttpClient())
            {
                String url = HanuDowsApplication.getInstance().BlogURL + "/wp-content/plugins/hanu-droid/PostArtifacts.php";
                Uri address = new Uri(url);

                var values = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("modified_time", lastSyncTime.ToString("yyyy-MM-dd HH:mm:ss")),
                        new KeyValuePair<string, string>("iid", iid.ToString()),
                        new KeyValuePair<string, string>("sync_params", sync_params)
                    };

                HttpFormUrlEncodedContent postContent = new HttpFormUrlEncodedContent(values);
                HttpResponseMessage response = await hc.PostAsync(address, postContent).AsTask();
                string response_text = await response.Content.ReadAsStringAsync();

                XDocument xdoc = XDocument.Parse(response_text);
                foreach (XElement post_artifact in xdoc.Root.Elements("PostArtifcatData"))
                {
                    PostArtifact pf = new PostArtifact();
                    pf.PostID = (int)post_artifact.Attribute("Id");
                    pf.PubDate = DateTime.Parse(post_artifact.Attribute("PublishDate").Value);
                    pf.ModDate = DateTime.Parse(post_artifact.Attribute("ModifiedDate").Value);

                    // Sometimes comment date may not be available.
                    try
                    {
                        pf.CommentDate = DateTime.Parse(post_artifact.Attribute("CommentDate").Value);
                    }
                    catch
                    {
                        pf.CommentDate = new DateTime(2011, 11, 4);
                    }

                    _postArtifacts.Add(pf);
                    post_id_list += pf.PostID + ",";
                }

            }

            // Get DB Artifacts now
            post_id_list = post_id_list.TrimEnd(',');
            Dictionary<int, PostArtifact> dbPostArtifacts = DBHelper.getInstance().getDBPostArtifacts(post_id_list); ;

            // Filter Artifacts now
            PostArtifact dbArtifact;
            foreach (PostArtifact pf in _postArtifacts.ToList())
            {
                dbPostArtifacts.TryGetValue(pf.PostID, out dbArtifact);
                if (dbArtifact != null)
                {
                    // See if DB artifact is older or not
                    if (pf.PubDate.CompareTo(dbArtifact.PubDate) >= 0 ||
                        pf.ModDate.CompareTo(dbArtifact.ModDate) >= 0 ||
                        pf.CommentDate.CompareTo(dbArtifact.CommentDate) >= 0)
                    {
                        // Keep this
                    }
                    else
                    {
                        // Remove this
                        _postArtifacts.Remove(pf);
                    }
                }
            }

            return _postArtifacts.Count;

        }

        internal async Task<bool> downloadPosts()
        {
            if (_postArtifacts.Count > 0)
            {
                String url = HanuDowsApplication.getInstance().BlogURL + "/wp-content/plugins/hanu-droid/FetchPosts.php";

                int batchSize = 5;
                int size = _postArtifacts.Count;
                int loops = size / batchSize;
                int x = 0;

                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                bool allGood = true;

                // Now time
                DateTime now = DateTime.UtcNow;

                while (x <= loops)
                {
                    IEnumerable<PostArtifact> pfList = _postArtifacts.Skip(x * batchSize).Take(batchSize);

                    if (pfList.Count() <= 0)
                    {
                        x++;
                        continue;
                    }

                    // Get Post IDs
                    string post_ids = "";
                    string response_text = "";

                    foreach (PostArtifact pf in pfList)
                    {
                        post_ids += pf.PostID + ",";
                    }
                    post_ids = post_ids.TrimEnd(',');

                    try
                    {
                        // Download
                        using (HttpClient hc = new HttpClient())
                        {
                            Uri address = new Uri(url);

                            var values = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("post_id", post_ids)
                            };

                            HttpFormUrlEncodedContent postContent = new HttpFormUrlEncodedContent(values);
                            HttpResponseMessage response = await hc.PostAsync(address, postContent).AsTask();

                            var buffer = await response.Content.ReadAsBufferAsync();
                            var byteArray = buffer.ToArray();
                            response_text = Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);

                            XDocument xdoc = XDocument.Parse(response_text);
                            DownloadedPostList = HanuDowsApplication.getInstance().parseXMLToPostList(xdoc);

                        }

                        bool success = await savePostsToDB();

                        if (!success)
                        {
                            allGood = false;
                        }

                    }
                    catch (Exception ex)
                    {
                        WebErrorStatus error = WebError.GetStatus(ex.GetBaseException().HResult);
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(error.ToString());
                    }

                    x++;

                }

                if (allGood)
                {
                    // Set Last sync time
                    localSettings.Values["LastSyncTime"] = now.ToString();
                }
                else
                {
                    // Set last sync time to Hanu Epoch
                    localSettings.Values["LastSyncTime"] = (new DateTime(2011, 11, 4)).ToString();
                }

            }

            return true;
        }

    }
}
