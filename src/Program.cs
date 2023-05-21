using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

internal class Program
{
    public static ProgramConfig config;

    private static void Main(string[] args)
    {
        try
        {
            ProgramConfig.CreateConfigIfMissing();
            config = ProgramConfig.LoadConfig();

            if (!config.BaseURL.StartsWith("https://"))
            {
                Console.WriteLine("Base URL does not begin with \"https://\".");
                Console.WriteLine("Cytube will reject your media - aborting.");
                ExitPrompt();
            }

            if (args.Length < 1)
            {
                Console.WriteLine("Enter file path or URL for source(s), enter blank to stop:");
                while (true)
                {
                    string? src = Console.ReadLine();
                    if(string.IsNullOrWhiteSpace(src))
                    {
                        break;
                    }
                    args = args.Append(src).ToArray();
                    Console.WriteLine("More?");
                }
                if(args.Length < 1)
                {
                    Console.WriteLine($"Usage: {Process.GetCurrentProcess().ProcessName}.exe media_file.mp4 text_track.vtt");
                    Console.WriteLine($"       Alternatively drag and drop media files onto the executable.");
                    ExitPrompt();
                }
            }

            string folderPrefix = "";
            if (args.Length == 1 && Directory.Exists(args[0]))
            {
                var directoryInfo = new DirectoryInfo(args[0]);
                folderPrefix = directoryInfo.Name + "/";
                var fileList = Directory.GetFiles(directoryInfo.FullName);
                args = fileList;
            }

            Console.WriteLine("Processing files...");
            Manifest manifest = ProcessFiles(args, folderPrefix);
            if (manifest.Sources.Count == 0)
            {
                Console.WriteLine("Unable to create manifest: No primary source found.");
                ExitPrompt();
            }

            string outputFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (!manifest.Sources.First().IsWebResource())
            {
                outputFolder = Path.GetDirectoryName(manifest.Sources.First().Filepath);
            }

            string jsonText = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            string jsonFilePath = Path.Combine(outputFolder, manifest.Sources.First().FilenameNoExtension() + ".json");
            File.WriteAllText(jsonFilePath, jsonText);
            Console.WriteLine("Wrote manifest to " + jsonFilePath);

            if (config.CreateHTAccess && manifest.TextTracks.Count > 0)
            {
                File.WriteAllText(Path.Combine(outputFolder, ".htaccess"), "Header set Access-Control-Allow-Origin \"*\"");
                Console.WriteLine("Wrote htaccess to " + outputFolder);
            }
        }
        catch (Exception exc)
        {
            Console.WriteLine(exc.ToString());
            Console.WriteLine();
            ExitPrompt();
        }
    }

    private static Manifest ProcessFiles(string[] files, string folderPrefix)
    {
        Manifest manifest = new Manifest();

        bool firstSource = true;
        foreach (string file in files)
        {
            if(VideoSource.IsFileValid(file))
            {
                VideoSource vid = new VideoSource(file, folderPrefix);
                manifest.Sources.Add(vid);
                if(firstSource)
                {
                    manifest.Title = vid.Title();
                    manifest.Duration = vid.Duration;
                    firstSource = false;
                }
            }
            else if (AudioSource.IsFileValid(file))
            {
                manifest.AudioTracks.Add(new AudioSource(file, folderPrefix));
            }
            else if (TextSource.IsFileValid(file))
            {
                manifest.TextTracks.Add(new TextSource(file, folderPrefix));
            }
            else
            {
                Console.WriteLine("File was not valid: " + file);
            }
        }

        return manifest;
    }

    private static void ExitPrompt()
    {
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        Environment.Exit(0);
    }
}

public class Manifest
{
    [JsonProperty(PropertyName = "title")]
    public string Title { get; set; }
    [JsonProperty(PropertyName = "duration")]
    public int Duration { get; set; }
    [JsonProperty(PropertyName = "live")]
    public bool Live { get; set; } = false;
    [JsonProperty(PropertyName = "sources")]
    public List<VideoSource> Sources { get; set; } = new List<VideoSource>();
    [JsonProperty(PropertyName = "audioTracks")]
    public List<AudioSource> AudioTracks { get; set; } = new List<AudioSource>();
    [JsonProperty(PropertyName = "textTracks")]
    public List<TextSource> TextTracks { get; set; } = new List<TextSource>();
}

public class Source
{
    [JsonIgnore]
    public string Filepath { get; set; }
    [JsonProperty(PropertyName = "url")]
    public string Url { get; set; }
    [JsonProperty(PropertyName = "contentType")]
    public string ContentType { get; set; }

    public string Filename()
    {
        return Path.GetFileName(Filepath);
    }

    public string FilenameNoExtension()
    {
        int lastPeriodIndex = Filename().LastIndexOf(".");
        if (lastPeriodIndex == -1)
        {
            throw new Exception("No file extension found for " + Filepath);
        }

        return Filename()[..lastPeriodIndex];
    }

    public string Extension()
    {
        int lastPeriodIndex = Filename().LastIndexOf(".");
        if (lastPeriodIndex == -1)
        {
            throw new Exception("No file extension found for " + Filepath);
        }

        return Filename()[lastPeriodIndex..];
    }

    public string Title()
    {
        return FilenameNoExtension().Replace("_", " ");
    }

    public bool IsWebResource()
    {
        if (!Uri.TryCreate(Filepath, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            throw new Exception("Cytube will reject insecure resources - use HTTPS. File path: " + Filepath);
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

public class VideoSource : Source
{
    [JsonProperty(PropertyName = "quality")]
    public int Quality { get; set; }
    [JsonProperty(PropertyName = "bitrate")]
    public int Bitrate { get; set; }
    [JsonIgnore]
    public int Duration { get; set; } // not required for the source but a useful property to have

    public static Dictionary<string, string> MIMEMap = new()
    {
        { ".mp4", "video/mp4" },
        { ".webm", "video/webm" },
        { ".ogv", "video/ogg" },
    };

    public VideoSource(string path, string folderPrefix)
    {
        Filepath = path;
        if (IsWebResource())
        {
            Url = path;
        }
        else
        {
            Url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        }
        ContentType = MIMEMap[Extension()];

        var ffprobeProps = ffprobe.GetFileProperties(path);
        Quality = GetClosestQuality(ffprobeProps.Quality);
        Bitrate = ffprobeProps.Bitrate / 1000;
        Duration = ffprobeProps.Duration;
    }

    public static bool IsFileValid(string path)
    {
        foreach(KeyValuePair<string, string> entry in MIMEMap)
        {
            if(path.EndsWith(entry.Key.ToString()))
            {
                return true;
            }
        }
        return false;
    }

    public static int GetClosestQuality(int quality)
    {
        List<int> supportedQualities = new List<int>()
        {
            240,
            360,
            480,
            540,
            720,
            1080,
            1440,
            2160,
        };

        int closest = supportedQualities.Aggregate((x, y) => Math.Abs(x - quality) < Math.Abs(y - quality) ? x : y);
        return closest;
    }
}

public class AudioSource : Source
{
    [JsonProperty(PropertyName = "label")]
    public string Label { get; set; }
    [JsonProperty(PropertyName = "language")]
    public string Language { get; set; }

    public static Dictionary<string, string> MIMEMap = new()
    {
        { ".aac",  "audio/aac" },
        { ".mp3",  "audio/mpeg" },
        { ".mpga", "audio/mpeg" },
        { ".ogg",  "audio/ogg" },
        { ".oga",  "audio/ogg" },
    };

    public AudioSource(string path, string folderPrefix)
    {
        Filepath = path;
        if (IsWebResource())
        {
            Url = path;
        }
        else
        {
            Url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        }
        ContentType = MIMEMap[Extension()];
        Label = Title();
        Language = "EN";
    }

    public static bool IsFileValid(string path)
    {
        foreach (KeyValuePair<string, string> entry in MIMEMap)
        {
            if (path.EndsWith(entry.Key.ToString()))
            {
                return true;
            }
        }
        return false;
    }
}

public class TextSource : Source
{
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }
    [JsonProperty(PropertyName = "default")]
    public bool Default { get; set; }

    public static Dictionary<string, string> MIMEMap = new()
    {
        { ".vtt",  "text/vtt" },
    };

    public TextSource(string path, string folderPrefix)
    {
        Filepath = path;
        if (IsWebResource())
        {
            Url = path;
        }
        else
        {
            Url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        }
        ContentType = MIMEMap[Extension()];
        Name = Title();
    }

    public static bool IsFileValid(string path)
    {
        foreach (KeyValuePair<string, string> entry in MIMEMap)
        {
            if (path.EndsWith(entry.Key.ToString()))
            {
                return true;
            }
        }
        return false;
    }

    public bool ShouldSerializedefault()
    {
        return Default;
    }
}

public static class ffprobe
{
    public static string RunCommand(string args)
    {
        string output = "";
        using (var ffprobe = Process.Start(new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true
        }))
        {
            output = ffprobe.StandardOutput.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(output)) { throw new Exception("ffprobe output empty."); }
        return output;
    }

    //public static int Duration(string path)
    //{
    //    string output = RunCommand("-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"");
    //    if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe duration output."); }
    //    return (int)duration;
    //}

    //public static int Bitrate(string path)
    //{
    //    string output = RunCommand("-v error -select_streams v:0 -show_entries stream=bit_rate -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"");
    //    if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe bitrate output."); }
    //    return (int)duration;
    //}

    //public static int Quality(string path)
    //{
    //    string output = RunCommand("-v error -select_streams v:0 -show_entries stream=height -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"");
    //    if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe quality output."); }
    //    return (int)duration;
    //}

    public static FfprobeProperties GetFileProperties(string path)
    {
        string output = RunCommand("-v error -select_streams v:0 -show_entries stream=height -show_entries stream=duration -show_entries stream=bit_rate -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"");
        string[] outputSplit = output.Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new FfprobeProperties()
        {
            Quality = int.Parse(outputSplit[0]),
            Duration = (int)float.Parse(outputSplit[1]),
            Bitrate = int.Parse(outputSplit[2]),
        };
    }

    public class FfprobeProperties
    {
        public int Quality;
        public int Duration;
        public int Bitrate;
    }
}

public class ProgramConfig
{
    public string BaseURL { get; set; } = "https://example.com/cytube";
    public bool CreateHTAccess { get; set; } = false;
    public static string ConfigFile = AppDomain.CurrentDomain.BaseDirectory + "/config.json";

    public static void CreateConfigIfMissing()
    {
        if(File.Exists(ConfigFile))
        {
            return;
        }

        using (var stream = File.Create(ConfigFile))
        {
            string cfgString = JsonConvert.SerializeObject(new ProgramConfig(), Formatting.Indented);
            var cfgBytes = Encoding.UTF8.GetBytes(cfgString);
            var cfgBytesLen = Encoding.UTF8.GetByteCount(cfgString);
            stream.Write(cfgBytes, 0, cfgBytesLen);
        }
    }

    public static ProgramConfig LoadConfig()
    {
        var configText = string.Join("\n", File.ReadAllText(ConfigFile));
        ProgramConfig config = JsonConvert.DeserializeObject<ProgramConfig>(configText);
        if (config == null)
        {
            throw new Exception($"Error deserializing config.json.");
        }

        config.BaseURL = config.BaseURL.TrimEnd('/');

        return config;
    }
}