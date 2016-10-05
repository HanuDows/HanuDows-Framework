using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Networking.PushNotifications;
using Windows.Storage;
using Windows.Web;
using Windows.Web.Http;

namespace HanuDowsFramework
{
    public class HanuDowsApplication
    {
        private static HanuDowsApplication instance;

        private string _blogURL;
        private PostManager postManager;

        public string BlogURL
        {
            get { return _blogURL; }
        }

        public static HanuDowsApplication getInstance()
        {
            if (instance == null)
            {
                instance = new HanuDowsApplication();
            }

            return instance;
        }

        private HanuDowsApplication()
        {
            postManager = PostManager.getInstance();

            ResourceLoader rl = new ResourceLoader();
            _blogURL = rl.GetString("BlogURL");
        }

        public async Task<bool> InitializeApplication()
        {
            // Create DB
            DBHelper.getInstance();

            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values["FirstUse"] == null)
            {
                // Initialize App for first time use
                await InitializeForFirstUse();

                // Set First Use is done
                localSettings.Values["FirstUse"] = "";
            }
            else
            {
                // Initialize App for normal usage
                var success = InitializeForNormalUse();

                // Load Data from DB for Display
                GetAllPosts();
            }

            RegisterForPushNotificationsAsync();

            // Load latest data from Blog
            LoadLatestDataAsync();

            return true;
        }

        private async void LoadLatestDataAsync()
        {
            int count = await PerformSync();
        }

        private async Task<bool> InitializeForFirstUse()
        {
            // Initialize the app for First use

            // Validate Application
            bool validated = await ValidateApplicationUsage();
            if (!validated)
            {
                // Blog is not valid
                return false;
            }

            // Load initial data from file
            await LoadInitialDataFromFile();

            return true;

        }

        private async Task<bool> InitializeForNormalUse()
        {
            // Initialize the app for Normal use

            // Validate Application
            bool validated = await ValidateApplicationUsage();
            if (!validated)
            {
                // Blog is not valid
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateApplicationUsage()
        {
            // Hanu Epoch time.
            DateTime lastValidationTime = new DateTime(2011, 11, 4);
            DateTime now = DateTime.Now;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            if (localSettings.Values["ValidationTime"] != null)
            {
                // This is not the first use. 
                lastValidationTime = DateTime.Parse(localSettings.Values["ValidationTime"].ToString());
            }

            TimeSpan interval = now.Subtract(lastValidationTime);
            if (interval.Days > 7)
            {
                // Validate again

                try
                {
                    using (HttpClient hc = new HttpClient())
                    {
                        Uri address = new Uri("http://apps.ayansh.com/HanuGCM/Validate.php");

                        var values = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("blogurl", _blogURL),
                        };

                        HttpFormUrlEncodedContent postContent = new HttpFormUrlEncodedContent(values);
                        HttpResponseMessage response = await hc.PostAsync(address, postContent).AsTask();
                        string response_text = await response.Content.ReadAsStringAsync();

                        if (response_text.Equals("Success"))
                        {
                            // Set Validation time as now
                            localSettings.Values["ValidationTime"] = now.ToString();
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    WebErrorStatus error = WebError.GetStatus(e.GetBaseException().HResult);
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    System.Diagnostics.Debug.WriteLine(error.ToString());
                    return false;
                }

            }

            return true;
        }

        public async Task<int> PerformSync()
        {
            // Fetch Artifacts.
            int count = await postManager.fetchPostArtifacts();

            // Download Posts
            bool success = await postManager.downloadPosts();

            if (success)
            {
                return count;
            }
            else
            {
                return -1;
            }
            
        }

        private async Task<bool> LoadInitialDataFromFile()
        {
            string default_data_file = @"Assets\DefaultData.xml";
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = await InstallationFolder.GetFileAsync(default_data_file);

            using (Stream default_data = await file.OpenStreamForReadAsync())
            {
                XDocument xdoc = XDocument.Load(default_data);
                postManager.DownloadedPostList = parseXMLToPostList(xdoc);
            }

            bool success = await postManager.savePostsToDB();
            GetAllPosts();
            return success;

        }

        public async Task<bool> UploadNewPost(string title, string content)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var iid = localSettings.Values["InstanceID"];

            string url = _blogURL + "/wp-content/plugins/hanu-droid/CreateNewPost.php";

            title = title.Replace("&", "and");
            content = content.Replace("&", "and");

            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    Uri address = new Uri(url);

                    var values = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("title", title),
                            new KeyValuePair<string, string>("content", content),
                            new KeyValuePair<string, string>("name", ""),
                            new KeyValuePair<string, string>("iid", iid.ToString())
                        };

                    HttpFormUrlEncodedContent postContent = new HttpFormUrlEncodedContent(values);
                    HttpResponseMessage response = await hc.PostAsync(address, postContent).AsTask();
                    string response_text = await response.Content.ReadAsStringAsync();

                    JObject output = JObject.Parse(response_text);
                    int post_id = (int)output.GetValue("post_id");

                    if (post_id > 0)
                    {
                        // Success
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception e)
            {
                return false;
            }

        }

        internal List<Post> parseXMLToPostList(XDocument xdoc)
        {
            List<Post> postList = new List<Post>();

            foreach (XElement postData in xdoc.Root.Elements("PostsInfoRow"))
            {
                Post post = new Post();

                post.PostID = (int)postData.Element("PostData").Attribute("Id");
                post.PubDate = postData.Element("PostData").Attribute("PublishDate").Value;
                post.PostAuthor = postData.Element("PostData").Attribute("Author").Value;
                post.ModDate = postData.Element("PostData").Attribute("ModifiedDate").Value;

                post.PostTitle = postData.Element("PostData").Element("PostTitle").Value;
                post.PostContent = postData.Element("PostContent").Value;

                foreach (XElement postMetaData in postData.Element("PostMetaData").Elements("PostMetaDataRow"))
                {
                    string metaKey = postMetaData.Attribute("MetaKey").Value;
                    string metaValue = postMetaData.Attribute("MetaValue").Value;
                    post.addMetaData(metaKey, metaValue);
                }

                //TODO Comments

                // Category and tags
                foreach (XElement termData in postData.Element("TermsData").Elements("TermsDataRow"))
                {
                    string taxonomy = termData.Attribute("Taxonomy").Value;
                    foreach (XElement termName in termData.Elements("TermName"))
                    {
                        if (taxonomy.Equals("category"))
                        {
                            post.addCategory(termName.Value);
                        }
                        if (taxonomy.Equals("post_tag"))
                        {
                            post.addTag(termName.Value);
                        }
                    }

                }

                postList.Add(post);
            }

            return postList;
        }

        internal async void RegisterForPushNotificationsAsync()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var channel_uri = localSettings.Values["ChannelURI"];
            var reg_status = localSettings.Values["RegistrationStatus"];
            var iid = localSettings.Values["InstanceID"];
            bool save_required = true;
            string platform = "";

#if WINDOWS_PHONE_APP
            platform = "WindowsPhone";
#else
            platform = "Windows";
#endif

            // Get Unique ID
            if (iid == null)
            {
                iid = Windows.System.UserProfile.AdvertisingManager.AdvertisingId;
                if (iid == null || iid.Equals(""))
                {
                    // Generate Random
                    iid = Guid.NewGuid().ToString();
                }

                localSettings.Values["InstanceID"] = iid;
            }

            // Request a Push Notification Channel
            PushNotificationChannel channel = null;

            try
            {
                // Get Channel
                channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();

                // Is it the first time or dejavu?
                if (channel_uri == null || reg_status == null)
                {
                    save_required = true;
                }
                else
                {
                    // OK, so this is Deja-Vu. Is it same as before?
                    if (channel.Uri.Equals(channel_uri) && reg_status.Equals("Success"))
                    {
                        // URI is same. and we have registered it already. so nothing to do.
                        save_required = false;
                    }
                    else
                    {
                        save_required = true;
                    }
                }

                if (!save_required)
                {
                    return;
                }

                // Save it to my server
                using (HttpClient hc = new HttpClient())
                {
                    Uri address = new Uri("http://apps.ayansh.com/HanuGCM/RegisterDevice.php");

                    TimeZoneInfo tz = TimeZoneInfo.Local;
                    Package package = Package.Current;
                    PackageVersion version = package.Id.Version;
                    string app_version = string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

                    var values = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("package", package.Id.Name),
                        new KeyValuePair<string, string>("regid", channel.Uri),
                        new KeyValuePair<string, string>("iid", iid.ToString()),
                        new KeyValuePair<string, string>("tz", tz.StandardName),
                        new KeyValuePair<string, string>("app_version", app_version),
                        new KeyValuePair<string, string>("platform", platform)
                    };

                    HttpFormUrlEncodedContent postContent = new HttpFormUrlEncodedContent(values);
                    HttpResponseMessage response = await hc.PostAsync(address, postContent).AsTask();
                    string response_text = await response.Content.ReadAsStringAsync();

                    if (response_text.Equals("Success"))
                    {
                        // Success
                        localSettings.Values["RegistrationStatus"] = "Success";
                        localSettings.Values["ChannelURI"] = channel.Uri;
                    }
                    else
                    {
                        localSettings.Values["RegistrationStatus"] = "Failed";
                    }

                }

            }

            catch (Exception ex)
            {
                // Could not create a channel. 
                localSettings.Values["ChannelURI"] = "";
                localSettings.Values["RegistrationStatus"] = "Failed";
            }
        }

        public void GetAllPosts()
        {
            // This will clear and add.
            postManager.PostList = DBHelper.getInstance().LoadPostData(null, null);
        }

        public bool DeletePostFromDB(int postID)
        {
            DBQuery query;              // Query Object
            List<DBQuery> queryList = new List<DBQuery>();

            // Delete Post
            query = new DBQuery();
            query.Query = "DELETE FROM Post WHERE Id=?";
            query.addQueryData(postID);
            queryList.Add(query);

            // Delete Post Meta
            query = new DBQuery();
            query.Query = "DELETE FROM PostMeta WHERE PostId=?";
            query.addQueryData(postID);
            queryList.Add(query);

            // Delete Post
            query = new DBQuery();
            query.Query = "DELETE FROM Comments WHERE PostId=?";
            query.addQueryData(postID);
            queryList.Add(query);

            // Delete Post
            query = new DBQuery();
            query.Query = "DELETE FROM Terms WHERE PostId=?";
            query.addQueryData(postID);
            queryList.Add(query);

            return DBHelper.getInstance().executeQueries(queryList);
        }

    }

}
