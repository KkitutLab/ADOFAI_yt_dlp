# ADOFAI_yt_dlp

Loads audio directly from online sources through `yt-dlp`.

No audio files are written to disk.
Audio is resolved and streamed entirely in memory.

## Requirements

* `yt-dlp`

## Usage

Add the following property to level settings:

```json
{
  "songURL": "https://www.youtube.com/watch?v=..."
}
```

Behavior:

* `songURL` missing
  → default ADOFAI behavior

* `songURL` exists + `songFilename` exists
  → default ADOFAI behavior

* `songURL` exists + `songFilename` empty
  → audio loaded through `yt-dlp`

## Notes

* Only one audio clip is cached at a time.
* Cached clip is reused when the same URL is loaded again.
* Playback is blocked while audio is loading.
* No editor UI integration.
* Logging is handled through MelonLoader console output only.

## Disclaimer

This software is provided "as is", without warranty of any kind, express or implied, including but not limited to merchantability, fitness for a particular purpose, and noninfringement.

This project does not provide, distribute, host, mirror, archive, upload, or include any copyrighted audio or media content.

All media access is initiated exclusively by the end user through externally supplied URLs and third-party services.

The end user is solely responsible for:

* legality of accessed content
* copyright compliance
* licensing permissions
* regional law compliance
* redistribution
* streaming or public playback
* any resulting legal consequences

The developer does not endorse or encourage copyright infringement or unauthorized distribution of protected content.

Under no circumstances shall the developer be held liable for:

* copyright violations
* DMCA claims
* unauthorized streaming
* misuse of third-party services
* data loss
* service bans
* legal disputes
* indirect or direct damages arising from use of this software

By using this project, the user accepts full responsibility for all actions performed with it.
