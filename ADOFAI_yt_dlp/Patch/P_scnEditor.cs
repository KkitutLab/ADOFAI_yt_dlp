namespace ADOFAI_yt_dlp.Patch;

public static class P_scnEditor__Play {
    public static bool Prefix(scnEditor __instance) => YtDlpManager.TryHandlePlay(__instance);
}