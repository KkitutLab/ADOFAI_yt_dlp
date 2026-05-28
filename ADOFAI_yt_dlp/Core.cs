using System.Diagnostics;
using ADOFAI_yt_dlp;
using MelonLoader;

[assembly: MelonInfo(typeof(Core), Info.Name, Info.Version, Info.Author, Info.Github)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace ADOFAI_yt_dlp;

public class Core : MelonMod {
    public override void OnInitializeMelon() {
        MelonCoroutines.Start(Initialize());
    }

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