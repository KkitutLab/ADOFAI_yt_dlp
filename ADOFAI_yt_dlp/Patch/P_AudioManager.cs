using MelonLoader;
using System.Collections;

namespace ADOFAI_yt_dlp.Patch;

public static class P_AudioManager__FindOrLoadAudioClipExternal {
    public static bool Prefix(string path, bool mp3Streaming, float length, bool stream, ref IEnumerator __result) {
        try {
            string? songFilename = null;
            try {
                songFilename = ADOBase.customLevel?.levelData?.songFilename;
            } catch { }

            if(!YtDlpManager.IsUrlMode(songFilename)) {
                return true;
            }

            bool exists = false;
            try {
                exists = !string.IsNullOrWhiteSpace(path) && RDFile.Exists(path);
            } catch {
                exists = false;
            }

            if(exists) {
                return true;
            }

            __result = ErrorNotFound();
            return false;
        } catch(Exception ex) {
            MelonLogger.Warning($"FindOrLoadAudioClipExternal prefix failed: {ex.Message}");
            return true;
        }
    }

    private static IEnumerator ErrorNotFound() {
        yield return new RDAudioLoadResult(RDAudioLoadType.ErrorFileNotFound, null);
    }
}
