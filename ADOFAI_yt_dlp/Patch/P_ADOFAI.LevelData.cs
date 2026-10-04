using ADOFAI;
using MelonLoader;
using System.Collections;

namespace ADOFAI_yt_dlp.Patch;

public static class P_ADOFAI__LevelData__EncodeToDictionary {
    public static void Postfix(LevelData __instance, ref Dictionary<string, object> __result) {
        try {
            if(string.IsNullOrWhiteSpace(YtDlpManager.CurrentUrl)) {
                return;
            }

            string? songFilename = null;
            try {
                songFilename = __instance?.songFilename;
            } catch { }

            if(!string.IsNullOrWhiteSpace(songFilename)) {
                return;
            }

            if(!__result.TryGetValue("settings", out var settingsObj)) {
                return;
            }

            if(settingsObj is not Dictionary<string, object> settings) {
                return;
            }

            var mods = new List<object>();

            if(settings.TryGetValue("requiredMods", out var modsObj)) {
                switch(modsObj) {
                    case IList list:
                        for(int i = 0; i < list.Count; i++) {
                            if(list[i] is string modName) {
                                if(modName == Info.Name || string.Equals(modName, "YouTubeStream", StringComparison.OrdinalIgnoreCase)) {
                                    continue;
                                }
                            }

                            mods.Add(list[i]);
                        }
                        break;
                }
            }

            mods.Add(Info.Name);

            settings["requiredMods"] = mods.ToArray();
            settings["songURL"] = YtDlpManager.CurrentUrl;
        } catch(Exception ex) {
            MelonLogger.Warning($"EncodeToDictionary postfix failed: {ex.Message}");
        }
    }
}

public static class P_ADOFAI__LevelData__Decode {
    public static void Prefix(Dictionary<string, object> dict) {
        try {
            if(dict == null || !dict.TryGetValue("settings", out var settingsObj)) {
                YtDlpManager.NotifyLevelDecoded(string.Empty, null);
                return;
            }

            if(settingsObj is not Dictionary<string, object> settings) {
                YtDlpManager.NotifyLevelDecoded(string.Empty, null);
                return;
            }

            string url = string.Empty;
            if(settings.TryGetValue("songURL", out var urlObj) &&
                urlObj is string urlStr &&
                !string.IsNullOrWhiteSpace(urlStr)) {
                url = urlStr.Trim();
            }

            string? songFilename = null;
            if(settings.TryGetValue("songFilename", out var fileObj) &&
                fileObj is string fileStr) {
                songFilename = fileStr;
            }

            YtDlpManager.NotifyLevelDecoded(url, songFilename);

            if(!string.IsNullOrWhiteSpace(url)) {
                MelonLogger.Msg(url);
            }
        } catch(Exception ex) {
            MelonLogger.Warning($"LevelData.Decode prefix failed: {ex.Message}");
        }
    }
}
