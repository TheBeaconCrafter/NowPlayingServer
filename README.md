# NowPlayingServer

A very simple & lightweight HTTP server in C# that provides information about currently playing media on Windows.

## Description

NowPlayingServer uses the Windows Media Controller API to monitor and expose currently playing media information via HTTP endpoints. This server was created to work with BeaconClient.

## Endpoints

- `http://localhost:52369/now-playing` - Returns JSON with information about the currently playing media, including:
  - Title
  - Artist
  - Album
  - Duration (in seconds)
  - Current position (in seconds)
  - Playback state

- `http://localhost:52369/cover.jpg` - Returns the album cover image for the currently playing media (if available)

## Usage

Simply run the application, and it will start listening on port 52369. You can then make HTTP requests to the provided endpoints to get information about currently playing media.

`
NowPlayingServer running on http://localhost:52369
`

## Building
`
dotnet publish -c Release -r win-x64 --self-contained
`

## Requirements

- Windows OS
- .NET 9.0
