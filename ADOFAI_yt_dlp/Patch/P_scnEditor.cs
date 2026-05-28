using HarmonyLib;
using MelonLoader;

namespace ADOFAI_yt_dlp.Patch;

[HarmonyPatch(typeof(scnEditor), nameof(scnEditor.Play))]
public static class P_scnEditor__Play {
    public static bool Prefix(scnEditor __instance) {
        if(YtDlpLoader.IsLoading) {
            MelonLogger.Warning("yt-dlp still loading, Play Blocked");
            return false;
        }

        string songUrl = __instance.levelData.levelSettings.Get<string>("songURL");

        if(string.IsNullOrWhiteSpace(songUrl) || !string.IsNullOrWhiteSpace(__instance.levelData.songFilename)) {
            return true;
        }

        return YtDlpLoader.ApplyCachedClip(songUrl);
    }
}