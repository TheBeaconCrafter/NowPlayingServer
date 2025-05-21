using System.Net;
using System.Text;
using WindowsMediaController;

await MainAsync();

async Task MainAsync()
{
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:52369/");
    listener.Start();
    Console.WriteLine("NowPlayingServer running on http://localhost:52369");

    var mediaManager = new MediaManager();
    await mediaManager.StartAsync();

    byte[]? lastThumbnailBytes = null;

    while (true)
    {
        var context = await listener.GetContextAsync();
        var response = context.Response;
        var path = context.Request.Url?.AbsolutePath;

        if (path == "/now-playing")
        {
            var mediaSession = mediaManager.GetFocusedSession();

            if (mediaSession == null)
            {
                await WriteJson(response, "{\"error\": \"No media session found\"}");
                continue;
            }

            var mediaProperties = await mediaSession.ControlSession.TryGetMediaPropertiesAsync();
            var playbackInfo = mediaSession.ControlSession.GetPlaybackInfo();
            var timeline = mediaSession.ControlSession.GetTimelineProperties();

            // grab thumbnail bytes for /cover.jpg
            if (mediaProperties.Thumbnail != null)
            {
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var memStream = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(memStream);
                lastThumbnailBytes = memStream.ToArray();
            }
            else
            {
                lastThumbnailBytes = null;
            }

            var json = $@"{{
                ""title"": ""{mediaProperties.Title}"",
                ""artist"": ""{mediaProperties.Artist}"",
                ""album"": ""{mediaProperties.AlbumTitle}"",
                ""duration"": {timeline.EndTime.TotalSeconds},
                ""position"": {timeline.Position.TotalSeconds},
                ""state"": ""{playbackInfo.PlaybackStatus}""
            }}";

            await WriteJson(response, json);
        }
        else if (path == "/cover.jpg")
        {
            if (lastThumbnailBytes == null)
            {
                response.StatusCode = 404;
                response.Close();
                continue;
            }

            response.ContentType = "image/jpeg";
            response.ContentLength64 = lastThumbnailBytes.Length;
            await response.OutputStream.WriteAsync(lastThumbnailBytes, 0, lastThumbnailBytes.Length);
            response.Close();
        }
        else
        {
            response.StatusCode = 404;
            response.Close();
        }
    }
}

async Task WriteJson(HttpListenerResponse response, string json)
{
    var buffer = Encoding.UTF8.GetBytes(json);
    response.ContentType = "application/json";
    response.ContentLength64 = buffer.Length;
    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    response.Close();
}