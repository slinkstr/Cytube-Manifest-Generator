using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaToolkit;
using MediaToolkit.Model;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Cytube_Manifest_Generator
{
    class Program
    {
        static void Main(string[] args)
        {
            // valid formats retrieved from https://github.com/calzoneman/sync/blob/3.0/docs/custom-media.md
            List<string> supportedSourceTypes = new List<string>() { "mp4", "webm", "ogg", "aac", "ogg", "mpeg" };
            List<string> supportedTextTypes = new List<string>() { "vtt" };
            List<int> supportedQualityLevels = new List<int>() { 240, 360, 480, 540, 720, 1080, 1440, 2160 };

            IConfiguration config = BuildConfig();
            string baseUrl = config["baseUrl"].Trim('/') + "/";

            if (args.Length < 1)
            {
                Console.WriteLine($"Drag and drop media files or a folder of media files onto {Process.GetCurrentProcess().ProcessName}.exe");
                Exit();
            }

            // get all files inside a folder
            string subfolder = "";
            if (args.Length == 1)
            {
                var attr = File.GetAttributes(args[0]);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    subfolder = args[0].Split('\\').Last() + "/";
                    var fileList = Directory.GetFiles(args[0]);
                    args = fileList;
                }
            }

            var allSupportedTypes = supportedSourceTypes.Concat(supportedTextTypes);
            bool unsupportedType = false;
            List<Source> sources = new List<Source>();
            List<TextTrack> textTracks = new List<TextTrack>();
            for (int i = 0; i < args.Length; i++)
            {
                // unsupported file format
                if (!allSupportedTypes.Any(x => args[i].EndsWith(x)))
                {
                    Console.WriteLine($"Unsupported file type: {args[i]}");
                    unsupportedType = true;
                }
                // text tracks
                else if (supportedTextTypes.Any(x => args[i].EndsWith(x)))
                {
                    string filename = args[i].Split('\\').Last();
                    string extension = filename.Split('.').Last();
                    TextTrack tt = new TextTrack()
                    {
                        url = baseUrl + subfolder + filename,
                        contentType = "text/" + extension,
                        name = "Subtitles " + i,
                        isDefault = !(textTracks.Count > 1)
                    };
                    textTracks.Add(tt);
                }
                // video/audio tracks
                else
                {
                    string filename = args[i].Split('\\').Last();
                    string extension = filename.Split('.').Last();
                    Metadata md = GetVideoInfo(args[i]);
                    int quality = supportedQualityLevels.First();
                    for (int j = 0; j < supportedQualityLevels.Count; j++)
                    {
                        int frameHeight = int.Parse(md.VideoData.FrameSize.Split('x')[1]);
                        if (frameHeight >= supportedQualityLevels[j])
                        {
                            quality = supportedQualityLevels[j];
                        }
                    }

                    int totalBitrate = (md.VideoData.BitRateKbs ?? 0) + md.AudioData.BitRateKbs;

                    Source s = new Source()
                    {
                        url = baseUrl + subfolder + filename,
                        contentType = "video/" + extension,
                        quality = quality,
                        bitrate = totalBitrate,
                        duration = Convert.ToInt32(md.Duration.TotalSeconds)
                    };
                    sources.Add(s);
                }
            }
            if (unsupportedType)
            {
                Console.WriteLine("Valid file types: " + string.Join(", ", allSupportedTypes));
                Exit();
            }
            // only text tracks provided
            if (sources.Count < 1)
            {
                Console.WriteLine("No source files provided.");
                Console.WriteLine("Valid source file types: " + string.Join(", ", supportedSourceTypes));
                Exit();
            }

            string title = sources.First().url.Split('/').Last().Split('.').First();

            JObject json = new JObject(
                new JProperty("title", title),
                new JProperty("duration", sources.First().duration),
                new JProperty("live", false),
                new JProperty("sources",
                    new JArray(
                        from s in sources
                        select new JObject(
                            new JProperty("url", s.url),
                            new JProperty("contentType", s.contentType),
                            new JProperty("quality", s.quality),
                            new JProperty("bitrate", s.bitrate)
                        )
                    )
                ),
                new JProperty("textTracks",
                    new JArray(
                        from t in textTracks
                        select new JObject(
                            new JProperty("url", t.url),
                            new JProperty("contentType", t.contentType),
                            new JProperty("name", t.name),
                            new JProperty("default", t.isDefault)
                        )
                    )
                )
            );

            File.WriteAllText(subfolder + json["title"] + ".json", json.ToString());

            // By default, browsers block requests for WebVTT tracks hosted on different domains than the current page. 
            // In order for text tracks to work cross-origin, the Access-Control-Allow-Origin header needs to be set by the remote server when serving the VTT file.
            if (textTracks.Count > 0)
            {
                File.WriteAllText(subfolder + ".htaccess", "Header set Access-Control-Allow-Origin \"*\"");
            }
        }

        public static void Exit()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        // a method to get Width, Height, Duration in Ticks, and bitrate for a video
        public static Metadata GetVideoInfo(string fileName)
        {
            var inputFile = new MediaFile { Filename = fileName };

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);
            }

            return inputFile.Metadata;
        }

        public class Source
        {
            public string url { get; set; }
            public string contentType { get; set; }
            public int quality { get; set; }
            public int bitrate { get; set; }
            public long duration { get; set; }
        }

        public class TextTrack
        {
            public string url { get; set; }
            public string contentType { get; set; }
            public string name { get; set; }
            public bool isDefault { get; set; }
        }

        // Read the config file
        private static IConfiguration BuildConfig()
        {
            // this whole thing is very questionable.
            // All these give me the temp file the single-file app created.
            // - System.Reflection.Assembly.GetEntryAssembly().Location 
            // - AppDomain.CurrentDomain.BaseDirectory
            // - Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
            // - System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            // - System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            // These give me the path of the media files, not the exe.
            // - System.IO.Directory.GetCurrentDirectory()
            // - Environment.CurrentDirectory
            // This outputs "Unhandled exception. System.ArgumentException: The path must be absolute. (Parameter 'root')"
            // - System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)
            //
            // In the end I had to use System.Diagnostics and Process.GetCurrentProcess() to get the path of the REAL exe,
            // and then some finagling to remove the name of the exe.

            var configlocation = Process.GetCurrentProcess().MainModule.FileName;
            var filename = Process.GetCurrentProcess().ProcessName;

            return new ConfigurationBuilder()
                .SetBasePath(configlocation.Substring(0, configlocation.Length - (filename.Length + 5)))
                .AddJsonFile("config.json")
                .Build();
        }
    }
}
