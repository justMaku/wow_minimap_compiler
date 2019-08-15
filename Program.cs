using System;
using System.Drawing;
using System.IO;
using System.Linq;

using SereniaBLPLib;
using NetVips;

using DBCD.Providers;

namespace wow_minimap_compiler
{

    static class Program
    {
        static string BuildConfig = "1a02465e2f1f1e30bba258ca49d2b60b";

        static void Main()
        {

            var dbdProvider = new GithubDBDProvider();
            var dbcProvider = new MirrorDBCProvider(BuildConfig);
            var dbcd = new DBCD.DBCD(dbcProvider, dbdProvider);

            var maps = dbcd.Load("Map");

            if (maps.AvailableColumns.Contains("WdtFileDataID")) // We're dealing with a >8.2 minimaps
            {
                foreach (dynamic map in maps.Values)
                {
                    try
                    {
                        Compile(map);
                    }
                    catch (StackOverflowException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        static void Compile(dynamic map)
        {
            Console.WriteLine($"Parsing map {map.MapName_lang} ({map.ID}) with WDT FileDataID: {map.WdtFileDataID}");
            var casc = new CASC();
            try
            {
                Console.WriteLine($"Downloading WDT for {map.MapName_lang} [FileDataID: {map.WdtFileDataID}]");

                // Grab WDT file from CASC and load the MAID chunks.
                var wdtStream = casc.File(BuildConfig, (uint)map.WdtFileDataID);
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
                    Console.WriteLine($"Progress: {currentTile}/{minimapChunks.Count()}");
                    var chunkStream = casc.File(BuildConfig, chunk.fileDataId);
                    Console.WriteLine(1);
                    using (var blpStream = new MemoryStream())
                    {
                        Console.WriteLine(1.5);

                        var blpFile = new BlpFile(chunkStream);
                        Console.WriteLine(1.7);

                        var bitmap = blpFile.GetBitmap(0);
                        Console.WriteLine(1.9);

                        Console.WriteLine(2);
                        var blpRes = bitmap.Width;
                        Console.WriteLine(3);
                        bitmap.Save(blpStream, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine(4);
                        var image = NetVips.Image.NewFromBuffer(blpStream.ToArray());
                        Console.WriteLine(5);

                        canvas = canvas.Insert(image, (int)(chunk.x - minX) * blpRes, (int)(chunk.y - minY) * blpRes, true);
                        Console.WriteLine(6);

                        blpFile.Dispose();
                        bitmap.Dispose();
                        image.Dispose();
                        Console.WriteLine(7);
                    }


                    chunkStream.Dispose();
                    bmp.Dispose();
                    canvasStream.Dispose();

                    currentTile += 1;
                    Console.WriteLine(8);
                }

                canvas.WriteToFile($"out/{map.ID}.png");
                canvas.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to download WDT for map {map.MapName_lang} due to {e}");
                return;
            }
        }
    }
}
