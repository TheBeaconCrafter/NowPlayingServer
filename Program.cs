using System.Net;
using System.Text;
using WindowsMediaController;

await MainAsync();

async Task MainAsync()
{
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:52369/");
    listener.Start();
    Console.WriteLine("Server running on http://localhost:52369");

    var mediaManager = new MediaManager();
    await mediaManager.StartAsync();

    while (true)
    {
        var context = await listener.GetContextAsync();
        var response = context.Response;

        if (context.Request.Url?.AbsolutePath == "/now-playing")
        {
            var mediaSession = mediaManager.GetFocusedSession();

            if (mediaSession == null)
            {
                var errorJson = "{\"error\": \"No media session found\"}";
                var errorBuffer = Encoding.UTF8.GetBytes(errorJson);
                response.ContentType = "application/json";
                response.ContentLength64 = errorBuffer.Length;
                await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
                response.Close();
                continue;
            }

            var mediaProperties = await mediaSession.ControlSession.TryGetMediaPropertiesAsync();
            var playbackInfo = mediaSession.ControlSession.GetPlaybackInfo();
            var timeline = mediaSession.ControlSession.GetTimelineProperties();

            var json = $@"{{
                ""title"": ""{mediaProperties.Title}"",
                ""artist"": ""{mediaProperties.Artist}"",
                ""album"": ""{mediaProperties.AlbumTitle}"",
                ""duration"": {timeline.EndTime.TotalSeconds},
                ""position"": {timeline.Position.TotalSeconds},
                ""state"": ""{playbackInfo.PlaybackStatus}""
            }}";

            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
        else
        {
            response.StatusCode = 404;
            response.Close();
        }
    }
}
