using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

internal class Program
{
    public static ProgramConfig config;

    private static void Main(string[] args)
    {
        ProgramConfig.CreateConfigIfMissing();
        config = ProgramConfig.LoadConfig();

        if(!config.BaseURL.StartsWith("https://"))
        {
            Console.WriteLine("Base URL does not begin with \"https://\".");
            Console.WriteLine("Cytube will reject your media - aborting.");
            ExitPrompt();
        }

        if (args.Length < 1)
        {
            Console.WriteLine($"Usage: {Process.GetCurrentProcess().ProcessName}.exe media_file.mp4 text_track.vtt");
            Console.WriteLine($"       Alternatively drag and drop media files onto the executable.");
            ExitPrompt();
        }

        string folderPrefix = "";
        if (args.Length == 1 && Directory.Exists(args[0]))
        {
            var directoryInfo = new DirectoryInfo(args[0]);
            folderPrefix = directoryInfo.Name + "/";
            var fileList = Directory.GetFiles(directoryInfo.FullName);
            args = fileList;
        }

        Manifest manifest = ProcessFiles(args, folderPrefix);
        if(manifest.sources.Count == 0)
        {
            Console.WriteLine("Unable to create manifest: No primary source found.");
            ExitPrompt();
        }

        string outputFolder = Path.GetDirectoryName(manifest.sources.First().filepath);
        string jsonText = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        File.WriteAllText(outputFolder + "/" + manifest.sources.First().FilenameNoExtension() + ".json", jsonText);

        if(config.CreateHTAccess && manifest.textTracks.Count > 0)
        {
            File.WriteAllText(outputFolder + "/.htaccess", "Header set Access-Control-Allow-Origin \"*\"");
            Console.WriteLine("Wrote htaccess to " + outputFolder);
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
                manifest.sources.Add(vid);
                if(firstSource)
                {
                    manifest.title = vid.Title();
                    manifest.duration = vid.duration;
                    firstSource = false;
                }
            }
            else if (AudioSource.IsFileValid(file))
            {
                manifest.audioTracks.Add(new AudioSource(file, folderPrefix));
            }
            else if (TextSource.IsFileValid(file))
            {
                manifest.textTracks.Add(new TextSource(file, folderPrefix));
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
    public string title { get; set; }
    public int duration { get; set; }
    public bool live { get; set; } = false;
    public List<VideoSource> sources { get; set; } = new List<VideoSource>();
    public List<AudioSource> audioTracks { get; set; } = new List<AudioSource>();
    public List<TextSource> textTracks { get; set; } = new List<TextSource>();
}

public class Source
{
    [JsonIgnore]
    public string filepath { get; set; }
    public string url { get; set; }
    public string contentType { get; set; }

    public string Filename()
    {
        return Path.GetFileName(filepath);
    }

    public string FilenameNoExtension()
    {
        int lastPeriodIndex = Filename().LastIndexOf(".");
        if (lastPeriodIndex == -1)
        {
            throw new Exception("No file extension found for " + filepath);
        }

        return Filename()[..lastPeriodIndex];
    }

    public string Extension()
    {
        int lastPeriodIndex = Filename().LastIndexOf(".");
        if (lastPeriodIndex == -1)
        {
            throw new Exception("No file extension found for " + filepath);
        }

        return Filename()[lastPeriodIndex..];
    }

    public string Title()
    {
        return FilenameNoExtension().Replace("_", " ");
    }
}

public class VideoSource : Source
{
    public int quality { get; set; }
    public int bitrate { get; set; }
    [JsonIgnore]
    public int duration { get; set; } // not required for the source but a useful property to have

    public static Dictionary<string, string> MIMEMap = new()
    {
        { ".mp4", "video/mp4" },
        { ".webm", "video/webm" },
        { ".ogv", "video/ogg" },
    };

    public VideoSource(string path, string folderPrefix)
    {
        filepath = path;
        url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        contentType = MIMEMap[Extension()];
        quality = GetClosestQuality(ffprobe.Quality(filepath));
        bitrate = ffprobe.Bitrate(filepath) / 1000;
        duration = ffprobe.Duration(filepath);
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
    public string label { get; set; }
    public string language { get; set; }

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
        filepath = path;
        url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        contentType = MIMEMap[Extension()];
        label = Title();
        language = "EN";
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
    public string name { get; set; }
    public bool @default { get; set; }

    public static Dictionary<string, string> MIMEMap = new()
    {
        { ".vtt",  "text/vtt" },
    };

    public TextSource(string path, string folderPrefix)
    {
        filepath = path;
        url = Program.config.BaseURL + "/" + folderPrefix + Path.GetFileName(path);
        contentType = MIMEMap[Extension()];
        name = Title();
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
        return @default;
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

    public static int Duration(string path)
    {
        string output = RunCommand("-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1 " + path);
        if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe duration output."); }
        return (int)duration;
    }

    public static int Bitrate(string path)
    {
        string output = RunCommand("-v error -select_streams v:0 -show_entries stream=bit_rate -of default=noprint_wrappers=1:nokey=1 " + path);
        if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe bitrate output."); }
        return (int)duration;
    }

    public static int Quality(string path)
    {
        string output = RunCommand("-v error -select_streams v:0 -show_entries stream=height -of default=noprint_wrappers=1:nokey=1 " + path);
        if (!float.TryParse(output, out var duration)) { throw new Exception("Unable to parse ffprobe quality output."); }
        return (int)duration;
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