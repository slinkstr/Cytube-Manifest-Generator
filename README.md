# Cytube-Manifest-Generator
Creates a custom media manifest for CyTube

# How to Use
Enter your base URL in **`config.json`**.  
Drag and drop your media files onto **`Cytube Manifest Generator.exe`**  
The .json will be created in the same folder as the video track.

If you drag and drop a folder with media files inside, the folder name will be included in the URLs.  
e.g. https://example.com/baseurl/folder_that_I_dragged/videofile.mp4

Otheriwse, only the base URL is used, e.g. https://example.com/baseurl/videofile.mp4

# Supported file formats
mp4, webm, ogg, aac, ogg, mpeg, vtt

# Notes
> By default, browsers block requests for WebVTT tracks hosted on different domains than the current page. In order for text tracks to work cross-origin, the `Access-Control-Allow-Origin` header needs to be set by the remote server when serving the VTT file.

A .htaccess file is created to set this header for Apache users if you use a .vtt track. If you're using Nginx you will need to edit your config to set this header instead.