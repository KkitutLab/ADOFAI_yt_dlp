using MelonLoader;
using System.Collections;

namespace ADOFAI_yt_dlp.Patch;

public static class P_scnEditor__Play {
    public static bool Prefix(scnEditor __instance) => YtDlpManager.TryHandlePlay(__instance);
}

public static class P_scnEditor__PublishToSteamCo {
    public static bool Prefix(ref IEnumerator __result) {
        if(!string.IsNullOrWhiteSpace(YtDlpManager.CurrentUrl)) {
            __result = EmptyCoroutine();
            MelonLogger.Msg("u blocked cuz have songURL");
            return false;
        }
        return true;
    }

    private static IEnumerator EmptyCoroutine() {
        yield break;
    }
}