using MelonLoader;

namespace ADOFAI_yt_dlp.Patch;

public static class P_AudioManager__FindOrLoadAudioClipExternal {
    public static bool Prefix(string path) {
        if(!string.IsNullOrWhiteSpace(YtDlpManager.CurrentUrl) &&
           string.IsNullOrWhiteSpace(ADOBase.customLevel.levelData.songFilename)) {

            return false;
        }

        return true;
    }
}