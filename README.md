# ADOFAI_yt_dlp

Loads audio from online sources using `yt-dlp`.

Audio is downloaded to a temporary WAV file and loaded into a Unity `AudioClip`.
Temporary files are deleted immediately after loading, and results are cached in memory during runtime.

## Requirements

* [yt-dlp](https://github.com/yt-dlp/yt-dlp)
* Optional: [Node.js](https://nodejs.org/) (>=22) (required for some YouTube JS challenge cases)

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
* Playback is blocked while loading
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
