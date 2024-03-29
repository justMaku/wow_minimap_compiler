using System;
using System.IO;
using System.Net.Http;

namespace wow_minimap_compiler
{
    class CASC
    {

        static private string cacheDirectory = "cache";

        private Uri baseURL = new Uri("https://wow.tools/casc/file/");

        public Stream File(string buildConfig, uint fileDataID, string fileName = "output")
        {

            var cached = Cached(buildConfig, fileDataID);

            if (cached != null)
            {
                return cached;
            }

            var query = $"fdid?buildconfig={buildConfig}&filedataid={fileDataID}&filename={fileName}";

            using (var client = new HttpClient())
            {
                client.BaseAddress = this.baseURL;

                try
                {
                    var bytes = client.GetByteArrayAsync(query);
                    var stream = new MemoryStream(bytes.Result);

                    Cache(buildConfig, fileDataID, stream);

                    return stream;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unable to download file: {baseURL}{query}");
                    throw new FileNotFoundException($"Unable to download file {fileDataID} for build {buildConfig}\nReason: {e}");
                }
            }
        }

        public Stream File(string buildConfig, string fileName)
        {
            var cached = Cached(buildConfig, fileName);

            if (cached != null)
            {
                return cached;
            }

            var query = $"fname?buildconfig={buildConfig}&filename={fileName}";

            using (var client = new HttpClient())
            {
                client.BaseAddress = this.baseURL;
                try
                {
                    var bytes = client.GetByteArrayAsync(query);
                    var stream = new MemoryStream(bytes.Result);

                    Cache(buildConfig, fileName, stream);

                    return stream;
                }
                catch (Exception e)
                {
                    throw new FileNotFoundException($"Unable to download file {fileName} for build {buildConfig}\nReason: {e}");
                }
            }
        }

        public Stream Cached(string buildConfig, uint fileDataID)
        {
            return Cached(buildConfig, $"{fileDataID}");
        }

        public Stream Cached(string buildConfig, string fileName)
        {
            var path = $"{cacheDirectory}{Path.DirectorySeparatorChar}{buildConfig}{Path.DirectorySeparatorChar}{fileName}";

            if (System.IO.File.Exists(path) == false)
            {
                return null;
            }

            return System.IO.File.Open(path, FileMode.Open);
        }

        public bool Cache(string buildConfig, uint fileDataID, Stream stream)
        {
            return Cache(buildConfig, $"{fileDataID}", stream);
        }

        public bool Cache(string buildConfig, string fileName, Stream stream)
        {
            if (Directory.Exists(cacheDirectory) == false)
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            if (Directory.Exists($"{cacheDirectory}{Path.DirectorySeparatorChar}{buildConfig}") == false)
            {
                Directory.CreateDirectory($"{cacheDirectory}{Path.DirectorySeparatorChar}{buildConfig}");
            }

            var path = $"{cacheDirectory}{Path.DirectorySeparatorChar}{buildConfig}{Path.DirectorySeparatorChar}{fileName}";

            if (System.IO.File.Exists(path))
            {
                return false;
            }

            var fileStream = System.IO.File.Create(path);

            stream.CopyTo(fileStream);
            stream.Position = 0;
            fileStream.Close();

            return true;
        }
    }
}