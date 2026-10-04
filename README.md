# ADOFAI_yt_dlp

Loads audio from online sources using `yt-dlp`.

Audio is downloaded to a temporary WAV file and loaded into a Unity `AudioClip`.
Temporary files are deleted immediately after loading, and results are cached in memory during runtime.

## Requirements

* [yt-dlp](https://github.com/yt-dlp/yt-dlp) (2025.11.12+ recommended, EJS support)
* [ffmpeg](https://ffmpeg.org/) (required for wav conversion)
* JS runtime for YouTube (at least one):
  * [Deno](https://deno.com/) (>=2.3.0, recommended, enabled by default)
  * [Node.js](https://nodejs.org/) (>=22, enabled via `--js-runtimes node`)

All executables must be discoverable via `PATH`, or placed next to the game / `Mods` folder.

To pin a JS runtime manually, edit `UserData/MelonPreferences.cfg` (created on first run):

```toml
[ADOFAI_yt_dlp]
JsRuntimePath = "/home/user/.deno/bin/deno"
```

`JsRuntimePath` accepts a file or a folder. Whether it is node or deno is detected automatically (by file name, or by `--version` output). Empty = auto-detect.

## Installation

This Mod Uses MelonLoader.  
  
Extract the contents of the downloaded mod zip file and paste all the extracted contents directly into your main game folder.

## Usage

Add `songURL` to level settings:

```json
{
  "songURL": "https://www.youtube.com/watch?v=..."
}
```

## Behavior

* `songURL` missing
  → default ADOFAI behavior

* `songURL` exists + `songFilename` exists
  → default ADOFAI behavior

* `songURL` exists + `songFilename` empty
  → audio is resolved via `yt-dlp` and used as level music

## Notes

* Only one audio clip is cached at a time
* Same URL uses cached audio (no re-download)
* Playback is blocked while loading, then auto-played when ready
* Download starts as soon as the level loads
* No editor integration

## Disclaimer

This project does not include, host, distribute, or provide any audio or media content.

All media retrieval is performed by the end user through externally supplied URLs and third-party services.

The developer does not control, endorse, or guarantee the content accessed through these services.

The user is solely responsible for:

* legality of accessed content
* copyright compliance
* licensing requirements
* adherence to platform terms of service
* any consequences arising from use

The developer shall not be held liable for any damages, claims, or legal issues resulting from the use of this software.
