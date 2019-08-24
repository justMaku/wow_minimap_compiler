
using DBCD.Providers;
using NetVips;
using SereniaBLPLib;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using System.Diagnostics;
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
            var numberOfCores = Environment.ProcessorCount;

            TaskScheduler scheduler = new LimitedConcurrencyLevelTaskScheduler(numberOfCores);
            TaskFactory factory = new TaskFactory(scheduler);

            foreach (dynamic map in maps.Values)
            {
                var task = factory.StartNew(() =>
                {
                    Compile(map);
                });
                 
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
        }

        static DBCD.IDBCDStorage LoadMaps()
        {
            var dbdProvider = new GithubDBDProvider();
            var dbcProvider = new MirrorDBCProvider(BuildConfig);
            var dbcd = new DBCD.DBCD(dbcProvider, dbdProvider);

            var maps = dbcd.Load("Map");

            return maps;
        }

        static void Compile(dynamic map)
        {
            if (File.Exists($"out{Path.DirectorySeparatorChar}{map.ID}.png"))
            {
                return;
            }

            if (map.WdtFileDataID == 0)
            {
                Console.WriteLine($"Unable to compile map {map.MapName_lang} ({map.ID}): No WDT file linked in Map.db2");
                return;
            }

            Console.WriteLine($"Compiling map {map.MapName_lang} ({map.ID})");

            // Grab WDT file from CASC and load the MAID chunks.
            Stream wdtStream = null;
            try
            {
                var casc = new CASC();
                wdtStream = casc.File(BuildConfig, (uint)map.WdtFileDataID);
            } catch
            {
                Console.WriteLine($"Unable to download WDT for map {map.MapName_lang} ({map.ID})");
                return;
            }

            WDT.WDTFileDataId[] minimapChunks = WDT.FileDataIdsFromWDT(wdtStream);

            // Filter out chunks without minimaps.
            minimapChunks = minimapChunks.Where(chunk => chunk.fileDataId != 0).ToArray();

            if (minimapChunks.Length == 0)
            {
                return;
            }

            // Grab the minimum for x and y coordinates.
            var minX = minimapChunks.Select(chunk => chunk.x).Min();
            var minY = minimapChunks.Select(chunk => chunk.y).Min();

            // Prepare the canvas unto which we will be printing
            var canvas = NetVips.Image.Black(1,1);
            
            Console.WriteLine(canvas.Height);

            foreach (var chunk in minimapChunks)
            {
                try
                {
                    var positionX = (int)(chunk.x - minX);
                    var positionY = (int)(chunk.y - minY);

                    Draw(map, chunk, ref canvas, positionX, positionY);
                } catch (Exception ex)
                {
                    Console.WriteLine($"Unable to draw chunk {chunk.x}_{chunk.y} for map {map.MapName_lang} ({map.ID})");
                }
            }

            var finalPath = $"out{Path.DirectorySeparatorChar}{map.ID}.png";
            // We're saving to a temporary path so in case of a crash on another thread we won't leave an half-saved artifact in the out directory.

            var temporaryPath = Path.GetTempFileName();
            Console.WriteLine($"{temporaryPath} => {finalPath}");

            canvas.Pngsave(temporaryPath);
            Console.WriteLine($"Written to: {finalPath}");
            File.Move(temporaryPath, finalPath);

            canvas.Dispose();
        }

        static void Draw(dynamic map, WDT.WDTFileDataId chunk, ref NetVips.Image canvas, int positionX, int positionY)
        {
            Stream chunkStream = null;
            try
            {
                var casc = new CASC();
                chunkStream = casc.File(BuildConfig, chunk.fileDataId);
            }
            catch
            {
                Console.WriteLine($"Failed to download chunk {chunk.x}_{chunk.y} for map {map.MapName_lang} ({map.ID})");
                return;
            }

            var blpStream = new MemoryStream();
            var blpFile = new BlpFile(chunkStream);

            var bitmap = blpFile.GetBitmap(0);
            bitmap.Save(blpStream, System.Drawing.Imaging.ImageFormat.Png);

            var image = NetVips.Image.NewFromBuffer(blpStream.ToArray(), access: Enums.Access.Sequential);
            canvas = canvas.Insert(image, positionX * bitmap.Width, positionY * bitmap.Height, true);
            
            blpStream.Dispose();
            blpFile.Dispose();
            bitmap.Dispose();
            image.Dispose();;
        }
    }
}
