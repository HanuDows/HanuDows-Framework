using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HanuDowsFramework
{
    public class PostComment
    {
        private int _id, _parent, _postId;
        private string _author, _email, _content;
        private DateTime _commentDate;

        public int CommentID
        {
            get { return _id; }
            set { _id = value; }
        }

        public int PostId
        {
            get { return _postId; }
            set { _postId = value; }
        }

        public int ParentCommentId
        {
            get { return _parent; }
            set { _parent = value; }
        }

        public string Author
        {
            get { return _author; }
            set { _author = value; }
        }

        public string Email
        {
            get { return _email; }
            set { _email = value; }
        }

        public string Content
        {
            get { return _content; }
            set { _content = value; }
        }

        public string CommentDate
        {
            get { return _commentDate.ToString(); }
            set { _commentDate = DateTime.Parse(value); }
        }

        internal DBQuery UpsertQuery()
        {
            //TODO Will implement later.
            return null;
        }
    }
}
