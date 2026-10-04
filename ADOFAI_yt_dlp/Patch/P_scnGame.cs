using MelonLoader;

namespace ADOFAI_yt_dlp.Patch;

public static class P_scnGame__ReloadSong {
    public static bool Prefix(scnGame __instance, bool force = false) {
        try {
            string? songFilename = null;
            try {
                songFilename = __instance?.levelData?.songFilename;
            } catch { }

            if(!YtDlpManager.IsUrlMode(songFilename)) {
                return true;
            }

            YtDlpManager.EnsureLoading(YtDlpManager.CurrentUrl);
            YtDlpManager.ApplyCachedClip(YtDlpManager.CurrentUrl);
            return false;
        } catch(Exception ex) {
            MelonLogger.Warning($"scnGame.ReloadSong prefix failed: {ex.Message}");
            return true;
        }
    }
}

public static class P_scnGame__Play {
    public static bool Prefix(scnGame __instance, int seqID, bool remakeFloors, ref bool __result) {
        bool allow;
        try {
            allow = YtDlpManager.TryHandleGamePlay(__instance, seqID, remakeFloors);
        } catch(Exception ex) {
            MelonLogger.Warning($"scnGame.Play prefix failed: {ex.Message}");
            return true;
        }

        if(!allow) {
            __result = false;
        }

        return allow;
    }
}
