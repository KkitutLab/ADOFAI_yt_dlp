using MelonLoader;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAI_yt_dlp;

public static class YtDlpManager {
    public static bool IsLoading { get; private set; }

    public static string CurrentUrl = string.Empty;

    private static string cachedUrl = string.Empty;
    private static AudioClip cachedClip = null!;
    private static string tempPath = string.Empty;

    private static string runningUrl = string.Empty;

    public static bool TryHandlePlay(scnEditor editor) {
        string file = editor.levelData.songFilename;

        if(string.IsNullOrWhiteSpace(CurrentUrl)) {
            return true;
        }

        if(!string.IsNullOrWhiteSpace(file)) {
            return true;
        }

        if(ApplyCachedClip(CurrentUrl)) {
            MelonLogger.Msg("cache ok");
            return true;
        }

        if(IsLoading) {
            MelonLogger.Msg("loading...");
            return false;
        }

        StartLoad(CurrentUrl);
        return false;
    }

    private static void StartLoad(string url) {
        if(IsLoading) {
            return;
        }

        if(runningUrl == url) {
            return;
        }

        runningUrl = url;
        IsLoading = true;

        Task.Run(() => RunYtDlp(url));
    }

    private static void RunYtDlp(string url) {
        tempPath = GetTempPath(url);

        var psi = new ProcessStartInfo {
            FileName = "yt-dlp",
            Arguments =
                $"--no-playlist -f bestaudio " +
                $"--extract-audio --audio-format wav " +
                $"--newline --progress " +
                $"--output \"{tempPath}\" \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        if(process == null) {
            IsLoading = false;
            runningUrl = string.Empty;
            return;
        }

        while(!process.HasExited) {
            string line = process.StandardError.ReadLine();

            if(!string.IsNullOrWhiteSpace(line)) {
                MelonLogger.Msg(line);
            }
        }

        process.WaitForExit();

        MelonCoroutines.Start(LoadClipCoroutine(url, tempPath));

        IsLoading = false;
        runningUrl = string.Empty;
    }

    private static IEnumerator LoadClipCoroutine(string url, string path) {
        float timeout = 10f;
        float t = 0f;

        while(!File.Exists(path) && t < timeout) {
            t += Time.deltaTime;
            yield return null;
        }

        if(!File.Exists(path)) {
            MelonLogger.Error("file not found");
            yield break;
        }

        using var req =
            UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV);

        yield return req.SendWebRequest();

        if(req.result != UnityWebRequest.Result.Success) {
            MelonLogger.Error("clip load failed");
            yield break;
        }

        cachedClip = DownloadHandlerAudioClip.GetContent(req);
        cachedUrl = url;

        TryDelete(path);

        MelonLogger.Msg("ready");
    }

    public static bool ApplyCachedClip(string url) {
        if(cachedClip == null || cachedUrl != url) {
            return false;
        }

        scrConductor.instance.song.clip = cachedClip;
        return true;
    }

    private static string GetTempPath(string url) {
        return Path.Combine(
            Path.GetTempPath(),
            "adofai_yt_" + url.GetHashCode() + ".wav"
        );
    }

    private static void TryDelete(string path) {
        try {
            if(File.Exists(path)) {
                File.Delete(path);
            }
        } catch { }
    }
}