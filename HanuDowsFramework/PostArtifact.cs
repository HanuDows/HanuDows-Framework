using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HanuDowsFramework
{
    class PostArtifact
    {
        private int _postID;
        private DateTime _pubDate, _modDate, _commentDate;

        public int PostID
        {
            get { return _postID; }
            set { _postID = value; }
        }

        public DateTime PubDate
        {
            get { return _pubDate; }
            set { _pubDate = value; }
        }

        public DateTime ModDate
        {
            get { return _modDate; }
            set { _modDate = value; }
        }

        public DateTime CommentDate
        {
            get { return _commentDate; }
            set { _commentDate = value; }
        }
    }
}
