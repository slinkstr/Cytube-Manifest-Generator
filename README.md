# CytubeManifestGenerator

Drag and drop media files onto the app to generate a manifest .json automatically.

Intended to be used to add soft subs to a video.

ffprobe must be accessible from your PATH or next to the exe. [Download it here.](https://ffmpeg.org/download.html)

# Usage

Enter base URL in `config.json`.
Drag and drop your media files onto `CytubeManifestGenerator.exe` (or pass them as arguments at command line).
The manifest is created next to the first source.

If you drag and drop a folder with media files inside, the folder name will be included in the URLs.
e.g. https://example.com/baseurl/folder_that_I_dragged/videofile.mp4

# Note

> By default, browsers block requests for WebVTT tracks hosted on different domains than the current page.
> In order for text tracks to work cross-origin, the `Access-Control-Allow-Origin` header needs to be set by the remote server when serving the VTT file.
> See [MDN](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Access-Control-Allow-Origin) for more information about setting this header.

Apache users: Enable "CreateHTAccess" to automatically create an .htaccess file. If you're using Nginx you will need to edit your config to set this header instead.

# Accepted Files

#### Video:
* .mp4
* .webm
* .ogv

#### Audio:
* .aac
* .mp3
* .mpga
* .ogg
* .oga

#### Text:
* .vtt