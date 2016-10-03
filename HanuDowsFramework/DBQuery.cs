using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HanuDowsFramework
{
    class DBQuery
    {
        private string _query;
        private List<Object> _queryData;

        public DBQuery()
        {
            _queryData = new List<object>();
        }

        public List<Object> QueryData
        {
            get { return _queryData; }
        }

        public string Query
        {
            get { return _query; }
            set { _query = value; }
        }

        public void addQueryData(Object data)
        {
            _queryData.Add(data);
        }
    }
}
