namespace HanuDowsFramework
{
    public sealed class ObservablePost
    {
        private static ObservablePost instance;
        private PostManager pm;
        private int index;

        private string _postTitle, _postMeta, _postContent;
        private int _postID;

        public static ObservablePost Instance()
        {
            if (instance == null)
            {
                instance = new ObservablePost();
            }

            return instance;
        }

        private ObservablePost()
        {
            pm = PostManager.getInstance();
            index = 0;
            readPost();

        }

        public string PostTitle { get { return _postTitle; } }
        public string PostMeta { get { return _postMeta; } }
        public string PostContent { get { return _postContent; } }
        public int PostID { get { return _postID; } }

        private void readPost()
        {
            Post post = pm.PostList[index];
            _postID = post.PostID;
            _postTitle = post.PostTitle;
            _postMeta = "Published On: " + post.PubDate;
            _postContent = post.ShareableContent;
        }

        public void Reset()
        {
            index = 0;
            if (pm.PostList.Count > 0)
            {
                readPost();
            }
            else
            {
                _postTitle = "Restart the application";
                _postMeta = "";
                _postContent = "Error occured during initialization, please restart the application";
            }

        }

        public bool HasCategory(string category)
        {
            Post post = pm.PostList[index];
            return post.HasCategory(category);
        }
        
        public void NextPost()
        {
            if (index == pm.PostList.Count - 1)
            {
                Reset();
            }
            else
            {
                index++;
                readPost();
            }

            if (pm.PostList.Count - index <= 3)
            {
                HanuDowsApplication.getInstance().ReadPostsFromDB(true);
            }

        }

        public void PreviousPost()
        {
            if (index == 0)
            {
                index = pm.PostList.Count - 1;
            }
            else
            {
                index--;
            }

            readPost();
        }
    }
}
