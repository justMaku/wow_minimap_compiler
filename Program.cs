
using DBCD.Providers;
using Konsole;
using NetVips;
using SereniaBLPLib;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;

namespace wow_minimap_compiler
{

    static class Program
    {
        static string BuildConfig = "1a02465e2f1f1e30bba258ca49d2b60b";

        static void Main()
        {

            var maps = LoadMaps();

            var tasks = new List<Task>();
            var bars = new ConcurrentBag<ProgressBar>();

            var numberOfCores = Environment.ProcessorCount;

            TaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(numberOfCores);
            TaskFactory factory = new TaskFactory(scheduler);

            foreach (dynamic map in maps.Values)
            {
                var task = factory.StartNew(() =>
                {
                    var progressBar = new ProgressBar(100);
                    bars.Add(progressBar);
                    progressBar.Refresh(0, map.MapName_lang);

                    Compile(map, progressBar);
                });

                tasks.Add(task);
            }
        }

        static DBCD.IDBCDStorage LoadMaps()
        {
            var dbdProvider = new GithubDBDProvider();
            var dbcProvider = new MirrorDBCProvider(BuildConfig);
            var dbcd = new DBCD.DBCD(dbcProvider, dbdProvider);

            var maps = dbcd.Load("Map");

            return maps;
        }

        static void Compile(int WdtFileDataID)
        {
            var casc = new CASC();
            try
            {
                // Grab WDT file from CASC and load the MAID chunks.
                var wdtStream = casc.File(BuildConfig, (uint)WdtFileDataID);
                WDT.WDTFileDataId[] minimapChunks = WDT.FileDataIdsFromWDT(wdtStream);

                // Filter out chunks without minimaps.
                minimapChunks = minimapChunks.Where(chunk => chunk.fileDataId != 0).ToArray();

                // Grab the minimum for x and y coordinates.
                var minX = minimapChunks.Select(chunk => chunk.x).Min();
                var minY = minimapChunks.Select(chunk => chunk.y).Min();

                // Prepare the canvas unto which we will be printing
                var bmp = new Bitmap(1, 1);
                bmp.SetPixel(0, 0, Color.Transparent);
                var canvasStream = new MemoryStream();
                bmp.Save(canvasStream, System.Drawing.Imaging.ImageFormat.Png);
                var canvas = NetVips.Image.NewFromBuffer(canvasStream.ToArray());

                var currentTile = 1;
                foreach (var chunk in minimapChunks)
                {
                    var chunkStream = casc.File(BuildConfig, chunk.fileDataId);
                    using (var blpStream = new MemoryStream())
                    {
                        var blpFile = new BlpFile(chunkStream);
                        var bitmap = blpFile.GetBitmap(0);
                        var blpRes = bitmap.Width;
                        bitmap.Save(blpStream, System.Drawing.Imaging.ImageFormat.Png);
                        var image = NetVips.Image.NewFromBuffer(blpStream.ToArray());
                        canvas = canvas.Insert(image, (int)(chunk.x - minX) * blpRes, (int)(chunk.y - minY) * blpRes, true);
                        blpFile.Dispose();
                        bitmap.Dispose();
                        image.Dispose();
                    }


                    chunkStream.Dispose();
                    bmp.Dispose();
                    canvasStream.Dispose();

                    currentTile += 1;
                }

                canvas.WriteToFile($"out/wdt_{WdtFileDataID}.png");
                canvas.Dispose();
            }
            catch
            {
                return;
            }
        }
        static void Compile(dynamic map, ProgressBar progressBar)
        {
            if (File.Exists($"out/{map.ID}.png"))
            {
                progressBar.Refresh(progressBar.Max, map.MapName_lang);
                return;
            }

            var casc = new CASC();
            try
            {
                // Grab WDT file from CASC and load the MAID chunks.
                var wdtStream = casc.File(BuildConfig, (uint)map.WdtFileDataID);
                WDT.WDTFileDataId[] minimapChunks = WDT.FileDataIdsFromWDT(wdtStream);

                // Filter out chunks without minimaps.
                minimapChunks = minimapChunks.Where(chunk => chunk.fileDataId != 0).ToArray();

                progressBar.Max = minimapChunks.Length;
                progressBar.Refresh(0, map.MapName_lang);

                // Grab the minimum for x and y coordinates.
                var minX = minimapChunks.Select(chunk => chunk.x).Min();
                var minY = minimapChunks.Select(chunk => chunk.y).Min();

                // Prepare the canvas unto which we will be printing
                var bmp = new Bitmap(1, 1);
                bmp.SetPixel(0, 0, Color.Transparent);
                var canvasStream = new MemoryStream();
                bmp.Save(canvasStream, System.Drawing.Imaging.ImageFormat.Png);
                var canvas = NetVips.Image.NewFromBuffer(canvasStream.ToArray());

                var currentTile = 1;
                foreach (var chunk in minimapChunks)
                {
                    // progressBar.Next($"{chunk.fileDataId}");
                    progressBar.Next(map.MapName_lang);

                    var chunkStream = casc.File(BuildConfig, chunk.fileDataId);
                    using (var blpStream = new MemoryStream())
                    {
                        var blpFile = new BlpFile(chunkStream);
                        var bitmap = blpFile.GetBitmap(0);
                        var blpRes = bitmap.Width;
                        bitmap.Save(blpStream, System.Drawing.Imaging.ImageFormat.Png);
                        var image = NetVips.Image.NewFromBuffer(blpStream.ToArray());
                        canvas = canvas.Insert(image, (int)(chunk.x - minX) * blpRes, (int)(chunk.y - minY) * blpRes, true);
                        blpFile.Dispose();
                        bitmap.Dispose();
                        image.Dispose();
                    }


                    chunkStream.Dispose();
                    bmp.Dispose();
                    canvasStream.Dispose();

                    currentTile += 1;
                }

                canvas.WriteToFile($"out/{map.ID}.png");
                canvas.Dispose();
            }
            catch
            {
                progressBar.Refresh(progressBar.Max, map.MapName_lang);
                return;
            }
        }
    }
}
