using ADOFAI;
using ADOFAI_yt_dlp;
using ADOFAI_yt_dlp.Patch;
using HarmonyLib;
using MelonLoader;
using System.Diagnostics;

[assembly: MelonInfo(typeof(Core), Info.Name, Info.Version, Info.Author, Info.Github)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace ADOFAI_yt_dlp;

public class Core : MelonMod {
    public override void OnInitializeMelon() => MelonCoroutines.Start(Initialize());

    private System.Collections.IEnumerator Initialize() {
        var ytTask = Task.Run(GetYtDlpVersion);
        var nodeTask = Task.Run(GetNodeVersion);

        while(!ytTask.IsCompleted || !nodeTask.IsCompleted) {
            yield return null;
        }

        string? ytVersion = ytTask.Result;
        string? nodeVersion = nodeTask.Result;

        if(string.IsNullOrWhiteSpace(ytVersion)) {
            MelonLogger.Warning("yt-dlp not found, Disabled");
            yield break;
        }

        MelonLogger.Msg($"yt-dlp {ytVersion}");

        if(!string.IsNullOrWhiteSpace(nodeVersion)) {
            YtDlpManager.HasNode = true;
            MelonLogger.Msg($"node {nodeVersion}");
            MelonLogger.Msg("node args enabled");
        } else {
            YtDlpManager.HasNode = false;
        }

        Patch(HarmonyInstance);
    }

    private void Patch(HarmonyLib.Harmony harmony) {
        harmony.Patch(
            typeof(RDEditorUtils).GetMethod(nameof(RDEditorUtils.CheckModsDependency), [typeof(object[])]),
            prefix: new(typeof(P_RDEditorUtils__CheckModsDependency), nameof(P_RDEditorUtils__CheckModsDependency.Prefix))
        );
        harmony.Patch(
            typeof(LevelData).GetMethod(nameof(LevelData.EncodeToDictionary)),
            postfix: new(typeof(P_ADOFAI__LevelData__EncodeToDictionary), nameof(P_ADOFAI__LevelData__EncodeToDictionary.Postfix))
        );
        harmony.Patch(
            typeof(LevelData).GetMethod(nameof(LevelData.Decode)),
            prefix: new(typeof(P_ADOFAI__LevelData__Decode), nameof(P_ADOFAI__LevelData__Decode.Prefix))
        );
        harmony.Patch(
            typeof(AudioManager).GetMethod(nameof(AudioManager.FindOrLoadAudioClipExternal)),
           prefix: new(typeof(P_AudioManager__FindOrLoadAudioClipExternal), nameof(P_AudioManager__FindOrLoadAudioClipExternal.Prefix))
        );
        harmony.Patch(
            typeof(scnGame).GetMethod(nameof(scnGame.ReloadSong)),
            prefix: new(typeof(P_scnGame__ReloadSong), nameof(P_scnGame__ReloadSong.Prefix))
        );
        harmony.Patch(
            typeof(scnEditor).GetMethod(nameof(scnEditor.PublishToSteamCo)), 
            prefix: new(typeof(P_scnEditor__PublishToSteamCo), nameof(P_scnEditor__PublishToSteamCo.Prefix))
        );
        harmony.Patch(
            typeof(scnEditor).GetMethod(nameof(scnEditor.Play)),
            prefix: new(typeof(P_scnEditor__Play), nameof(P_scnEditor__Play.Prefix))
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

    private static string? GetNodeVersion() {
        try {
            var psi = new ProcessStartInfo {
                FileName = "node",
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

            string output = process.StandardOutput.ReadLine();
            process.WaitForExit();

            if(process.ExitCode != 0) {
                return null;
            }

            return output?.Trim();
        } catch {
            return null;
        }
    }
}