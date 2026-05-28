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

        if(settings.TryGetValue("requiredMods", out var modsObj)) {
            switch(modsObj) {
                case List<object> list:
                    if(!list.Contains(Info.Name)) {
                        list.Add(Info.Name);
                    }
                    break;

                case object[] array:
                    var newList = new List<object>(array);

                    if(!newList.Contains(Info.Name)) {
                        newList.Add(Info.Name);
                    }

                    settings["requiredMods"] = newList;
                    break;
            }
        } else {
            settings["requiredMods"] = new List<object> {
                Info.Name
            };
        }

        if(settings.TryGetValue("songURL", out var songUrlObj)
           && songUrlObj is string songUrl
           && !string.IsNullOrWhiteSpace(songUrl)) {

            settings["songFilename"] = "";
        }
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

        if(!settings.TryGetValue("requiredMods", out var modsObj) || modsObj is not IList list) {
            return;
        }

        for(int i = 0; i < list.Count; i++) {
            if(list[i] is string and "YoutubeStream") {
                list[i] = Info.Name; // fuck u
            }
        }
    }
}