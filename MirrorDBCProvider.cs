using System;
using System.IO;
using System.Net.Http;
using System.Web;

namespace DBCD.Providers
{
    public class MirrorDBCProvider : IDBCProvider
    {
        private static Uri BaseURI = new Uri("https://wow.tools/casc/file/");
        private HttpClient client = new HttpClient();
        private string buildConfig;

        public MirrorDBCProvider(string buildConfig)
        {
            this.buildConfig = buildConfig;

            client.BaseAddress = BaseURI;
        }

        public Stream StreamForTableName(string tableName, string build = null)
        {
            var path = $"dbfilesclient/{tableName}.db2";
            var query = $"fname?buildconfig={this.buildConfig}&filename={HttpUtility.UrlEncode(path)}";
            var bytes = client.GetByteArrayAsync(query).Result;
            return new MemoryStream(bytes);
        }
    }
}