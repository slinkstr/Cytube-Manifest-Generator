# CytubeManifestGenerator

Simple utility to automate creating [custom media manifests](https://github.com/calzoneman/sync/blob/3.0/docs/custom-media.md) for use with [cytu.be](https://cytu.be/). This is currently the only way to add soft subtitles to your media.

# Setup

⚠ **ffprobe must be accessible from your PATH or next to the executable.** [Download it here.](https://ffmpeg.org/download.html)

A base URL must be set in `config.json`. Base URL must begin with https or cytube will reject your media.

# Usage

Specify video/audio/text sources by:

* Dragging and dropping the files onto the executable.
* Providing the filepaths to the sources as command line arguments.
* Providing the filepaths to the sources when prompted after running the executable with no args.

If a folder is specified, the folder name will be included in the URLs.
e.g. https://example.com/baseurl/folder_that_I_dragged/videofile.mp4

Alternatively, provide a URL and it will probe the remote content for metadata. (Base URL will be ignored)

### Note for WebVTT sources

> By default, browsers block requests for WebVTT tracks hosted on different domains than the current page.
> In order for text tracks to work cross-origin, the `Access-Control-Allow-Origin` header needs to be set by the remote server when serving the VTT file.
> See [MDN](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Access-Control-Allow-Origin) for more information about setting this header.

Apache users: Enable "CreateHTAccess" in configuration to automatically create an .htaccess file. 

Nginx users: Edit your config to set this header instead. `add_header "Access-Control-Allow-Origin" "https://cytu.be";`

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
