using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using MediaToolkit;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Reflection;

namespace Cytube_Manifest_Generator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check for no args (no files)
            if (args.Length <= 0)
            {
                Console.WriteLine("Drag and drop files onto this exe to use.");
                Console.ReadKey();
                Environment.Exit(404);
            }

            // Set up dictionary to store values from the foreach below
            var dict = new Dictionary<string, FileInfo>();
            string[] supportedVideo = { ".mp4", ".m4a", ".webm", ".ogg", ".aac" };
            string[] supportedText = { ".vtt" };

            // Iterate through every file provided and halt execution if one of them is invalid
            foreach (var file in args)
            {
                // Add to dictionary if it's found to be valid
                if (supportedVideo.Any(x => file.EndsWith(x)) || supportedText.Any(x => file.EndsWith(x)))
                {
                    dict[file] = new FileInfo(file);
                }
                // user provided invalid file
                else
                {
                    string response = $"Invalid content type: {file}\nSupported formats: ";
                    foreach (string format in supportedVideo)
                    {
                        response += format + " ";
                    }
                    foreach (string format in supportedText)
                    {
                        response += format + " ";
                    }

                    Console.WriteLine(response + "\nPress any key to exit");
                    Console.ReadKey();
                    Environment.Exit(404);
                }
            }

            // Movie object to hold all the values
            Movie movie = new Movie();

            // Getting exe's current location and reading baseUrl.txt to set the baseUrl string
            string path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            var directory = System.IO.Path.GetDirectoryName(path).Substring(6);

            // Reading file contents and removing any trailing slashes, to ensure there's only 1 slash
            IConfiguration config = BuildConfig();
            string baseUrl = config["baseUrl"].Trim('/') + "/";

            // Iterate through all files with valid extension (text and video)
            foreach (var info in dict)
            {
                if (supportedVideo.Any(x => info.Key.EndsWith(x)))
                {
                    // Get a tuple with the width (1), height (2), length in ticks (3), and bitrate (4)
                    var videoInfo = GetVideoInfo(info.Key);

                    // Set title, duration, and it's never live
                    movie.title = info.Value.Name;
                    movie.duration = (long)TimeSpan.FromTicks(videoInfo.Item3).TotalSeconds;
                    movie.live = false;

                    // Setting url to the file
                    movie.sources.url = $"{baseUrl}{info.Value.Name}";

                    // Check the first three formats and insert "video/", check the last 2 and insert "audio/"
                    if (supportedVideo.Take(3).Any(x => info.Value.Extension == x))
                    {
                        movie.sources.contentType = "video/" + info.Value.Extension.Substring(1, info.Value.Extension.Length - 1);
                    }
                    else if (supportedVideo.TakeLast(2).Any(x => info.Value.Extension == x))
                    {
                        movie.sources.contentType = "audio/" + info.Value.Extension.Substring(1, info.Value.Extension.Length);
                    }

                    // All valid qualities from the cytube custom media page
                    int[] validSizes = new int[] { 240, 360, 480, 540, 720, 1080, 1440, 2160 };

                    movie.sources.quality = 240;

                    // Setting quality based on just the width of the video, rounded up
                    for (int i = 0; i < validSizes.Length; i++)
                    {
                        // Work up validSizes, rounding up to the nearest size
                        if (validSizes[i] >= videoInfo.Item1)
                        {
                            movie.sources.quality = validSizes[i];
                            break;
                        }
                    }

                    // Setting bitrate
                    movie.sources.bitrate = videoInfo.Item4;
                }

                // Handling subtitle tracks
                else if (supportedText.Any(x => info.Key.EndsWith(x)))
                {
                    movie.textTracks.url = $"{baseUrl}{info.Value.Name}";
                    movie.textTracks.contentType = "text/vtt";
                    movie.textTracks.name = "English Subtitles";
                }

                // I genuinely have no idea how someone could ever end up here
                else
                {
                    Console.WriteLine("if you see this message copy and paste output to the developer");
                }
            }
            // Print out everything in the console window
            Console.WriteLine(movie.ToString());
            Console.WriteLine();

            // Serialize the movie object to JSON
            string json = JsonConvert.SerializeObject(movie, Formatting.Indented);

            // Get the directory of any source (really) to put the JSON in
            var firstSource = dict.First();
            var firstDirectory = firstSource.Value.DirectoryName;
            var jsonDestination = firstDirectory + "\\" + firstSource.Value.Name.Substring(0, firstSource.Value.Name.Length - firstSource.Value.Extension.Length) + ".json";


            // Create the file
            FileStream fs = new FileStream(jsonDestination, FileMode.Create);

            // Convert json text to bytes to write it into this file
            byte[] bdata = Encoding.Default.GetBytes(json);
            fs.Write(bdata, 0, bdata.Length);
            fs.Close();

            // Final confirmation
            Console.WriteLine("File has been created in " + firstDirectory);
            Console.ReadKey();
        }

        // Objects to serialize into a json later on
        public class Movie
        {
            public string title { get; set; }
            public long duration { get; set; }
            public bool live { get; set; }
            
            // Named confusingly, text sources have a separate place
            public VideoSource sources { get; set; }
            public TextSource textTracks { get; set; }

            public Movie()
            {
                title = "";
                duration = 0;
                live = false;
                sources = new VideoSource();
                textTracks = new TextSource();
            }

            public override string ToString()
            {
                string returnvalue = $"Title: {title}\n" +
                    $"Duration: {duration}\n" +
                    $"Live: {live}\n" +
                    $"Video\n" +
                    $"    URL: {sources.url}\n" +
                    $"    ContentType: {sources.contentType}\n" +
                    $"    Quality: {sources.quality}\n" +
                    $"    Bitrate: {sources.bitrate}\n" +
                    $"Subtitles\n" +
                    $"    URL: {textTracks.url}\n" +
                    $"    Name: {textTracks.name}\n" +
                    $"    ContentType: {textTracks.contentType}";

                return returnvalue;
            }
        }

        public class VideoSource
        {
            public string url { get; set; }
            public string contentType { get; set; }
            public int quality { get; set; }
            public int bitrate { get; set; }
        }

        public class TextSource
        {
            public string url { get; set; }
            public string contentType { get; set; }
            public string name { get; set; }
        }

        // a method to get Width, Height, Duration in Ticks, and bitrate for a video
        public static Tuple<int, int, long, int> GetVideoInfo(string fileName)
        {
            var inputFile = new MediaToolkit.Model.MediaFile { Filename = fileName };

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);
            }

            // FrameSize is returned as '1280x768' string.
            var size = inputFile.Metadata.VideoData.FrameSize.Split(new[] { 'x' }).Select(o => int.Parse(o)).ToArray();
            int bitrate = inputFile.Metadata.VideoData.BitRateKbs ?? int.MaxValue;

            return new Tuple<int, int, long, int>(size[0], size[1], inputFile.Metadata.Duration.Ticks, bitrate);
        }

        // Read the config file
        private static IConfiguration BuildConfig()
        {
            // this whole thing is very questionable.
            // - System.Reflection.Assembly.GetEntryAssembly().Location 
            // - AppDomain.CurrentDomain.BaseDirectory
            // - Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
            // - System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            // - System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            // All would give me the temp file (that doesn't exist) the single-file app created.
            // - System.IO.Directory.GetCurrentDirectory()
            // - Environment.CurrentDirectory
            // Would give me the path of the media files, not the exe.
            // - System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)
            // outputs "Unhandled exception. System.ArgumentException: The path must be absolute. (Parameter 'root')"
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
