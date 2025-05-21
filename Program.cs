using System.Net;
using System.Text;
using WindowsMediaController;

try
{
    await MainAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

async Task MainAsync()
{
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:52369/");
    listener.Start();
    Console.WriteLine("NowPlayingServer running on http://localhost:52369");

    var mediaManager = new MediaManager();
    try
    {
        await mediaManager.StartAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting MediaManager: {ex.Message}");
        throw;
    }

    byte[]? lastThumbnailBytes = null;

    while (true)
    {
        HttpListenerContext? context = null;
        try
        {
            context = await listener.GetContextAsync();
            var response = context.Response;
            var path = context.Request.Url?.AbsolutePath;

            if (path == "/now-playing")
            {
                try
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
                        try
                        {
                            using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                            using var memStream = new MemoryStream();
                            await stream.AsStreamForRead().CopyToAsync(memStream);
                            lastThumbnailBytes = memStream.ToArray();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing thumbnail: {ex.Message}");
                            lastThumbnailBytes = null;
                        }
                    }
                    else
                    {
                        lastThumbnailBytes = null;
                    }

                    var json = $@"{{
                        ""title"": ""{EscapeJsonString(mediaProperties.Title)}"",
                        ""artist"": ""{EscapeJsonString(mediaProperties.Artist)}"",
                        ""album"": ""{EscapeJsonString(mediaProperties.AlbumTitle)}"",
                        ""duration"": {timeline.EndTime.TotalSeconds},
                        ""position"": {timeline.Position.TotalSeconds},
                        ""state"": ""{playbackInfo.PlaybackStatus}""
                    }}";

                    await WriteJson(response, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing media session: {ex.Message}");
                    await WriteJson(response, $"{{\"error\": \"Error processing media data: {EscapeJsonString(ex.Message)}\"}}");
                }
            }
            else if (path == "/cover.jpg")
            {
                if (lastThumbnailBytes == null)
                {
                    response.StatusCode = 404;
                    response.Close();
                    continue;
                }

                try
                {
                    response.ContentType = "image/jpeg";
                    response.ContentLength64 = lastThumbnailBytes.Length;
                    await response.OutputStream.WriteAsync(lastThumbnailBytes, 0, lastThumbnailBytes.Length);
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending thumbnail: {ex.Message}");
                    try { response.Close(); } catch { }
                }
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request processing error: {ex.Message}");
            try
            {
                if (context != null)
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
            catch { }
        }
    }
}

async Task WriteJson(HttpListenerResponse response, string json)
{
    try
    {
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error writing response: {ex.Message}");
    }
    finally
    {
        try { response.Close(); } catch { }
    }
}

string EscapeJsonString(string str)
{
    if (string.IsNullOrEmpty(str))
        return string.Empty;
        
    return str
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");
}