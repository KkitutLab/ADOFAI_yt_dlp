using MelonLoader;
using System.Collections;

namespace ADOFAI_yt_dlp.Patch;

public static class P_ADOFAI__LevelData__EncodeToDictionary {
    public static void Postfix(ref Dictionary<string, object> __result) {
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
    }
}

public static class P_ADOFAI__LevelData__Decode {
    public static void Prefix(Dictionary<string, object> dict) {
        if(!dict.TryGetValue("settings", out var settingsObj)) {
            return;
        }

        if(settingsObj is not Dictionary<string, object> settings) {
            return;
        }

        if(settings.TryGetValue("songURL", out var urlObj) &&
           urlObj is string url &&
           !string.IsNullOrWhiteSpace(url)) {

            YtDlpManager.CurrentUrl = url;
            MelonLogger.Msg(YtDlpManager.CurrentUrl);
        }
    }
}