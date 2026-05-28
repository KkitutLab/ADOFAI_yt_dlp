namespace ADOFAI_yt_dlp.Patch;

public static class P_scnGame__ReloadSong {
    public static bool Prefix(scnGame __instance, bool force = false) {
        if(!string.IsNullOrWhiteSpace(YtDlpManager.CurrentUrl) &&
           string.IsNullOrWhiteSpace(__instance.levelData.songFilename)) {

            YtDlpManager.ApplyCachedClip(YtDlpManager.CurrentUrl);
            return false;
        }

        return true;
    }
}