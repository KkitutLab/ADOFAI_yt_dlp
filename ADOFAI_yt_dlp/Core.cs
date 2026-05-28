using ADOFAI_yt_dlp;
using HarmonyLib;
using MelonLoader;
using System.Diagnostics;

[assembly: MelonInfo(typeof(Core), Info.Name, Info.Version, Info.Author, Info.Github)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace ADOFAI_yt_dlp;

public class Core : MelonMod {
    private HarmonyLib.Harmony harmony = new(ADOFAI_yt_dlp.Info.Name);

    public override void OnInitializeMelon() => MelonCoroutines.Start(Initialize());

    private System.Collections.IEnumerator Initialize() {
        var task = Task.Run(GetYtDlpVersion);

        while(!task.IsCompleted) {
            yield return null;
        }

        string? version = task.Result;

        if(string.IsNullOrWhiteSpace(version)) {
            MelonLogger.Warning("yt-dlp not found, Disabled");
            yield break;
        }

        MelonLogger.Msg($"yt-dlp {version}");

        Patch(harmony);
    }

    private void Patch(HarmonyLib.Harmony harmony) {
        harmony.Patch(
            AccessTools.Method(typeof(ADOFAI.LevelData), nameof(ADOFAI.LevelData.EncodeToDictionary)),
            postfix: new HarmonyMethod(typeof(Patch.P_ADOFAI__LevelData__EncodeToDictionary), nameof(ADOFAI_yt_dlp.Patch.P_ADOFAI__LevelData__EncodeToDictionary.Postfix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(ADOFAI.LevelData), nameof(ADOFAI.LevelData.Decode)),
            prefix: new HarmonyMethod(typeof(Patch.P_ADOFAI__LevelData__Decode), nameof(ADOFAI_yt_dlp.Patch.P_ADOFAI__LevelData__Decode.Prefix))
        );

        harmony.Patch(
            AccessTools.Method(typeof(scnEditor), nameof(scnEditor.Play)),
            prefix: new HarmonyMethod(typeof(Patch.P_scnEditor__Play), nameof(ADOFAI_yt_dlp.Patch.P_scnEditor__Play.Prefix))
        );
    }

    private static string? GetYtDlpVersion() {
        try {
            var psi = new ProcessStartInfo {
                FileName = "yt-dlp",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if(process == null) {
                return null;
            }

            process.WaitForExit();
            return process.ExitCode != 0 ? null : process.StandardOutput.ReadLine()?.Trim();
        } catch {
            return null;
        }
    }
}