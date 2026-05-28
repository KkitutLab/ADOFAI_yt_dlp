using MelonLoader;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAI_yt_dlp;

public static class YtDlpManager {
    public static bool HasNode = false;
    public static bool IsLoading { get; private set; }
    public static string CurrentUrl = string.Empty;

    private static string cachedUrl = string.Empty;
    private static AudioClip cachedClip = null!;
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
            MelonLogger.Msg("cache hit");
            return true;
        }

        if(IsLoading) {
            MelonLogger.Msg("loading...");
            return false;
        }

        if(!Uri.TryCreate(CurrentUrl, UriKind.Absolute, out Uri uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)) {
            MelonLogger.Warning("Invalid URL blocked");
            return false;
        }

        StartLoad(CurrentUrl);
        return false;
    }

    private static void StartLoad(string url) {
        if(IsLoading) {
            return;
        }

        IsLoading = true;
        runningUrl = url;

        string safePath = GetTempPath();

        Task.Run(() => {
            string path = RunYtDlp(url, safePath);

            if(string.IsNullOrWhiteSpace(path)) {
                IsLoading = false;
                runningUrl = string.Empty;
                return;
            }

            MelonCoroutines.Start(LoadClip(path, url));
        });
    }

    private static string RunYtDlp(string url, string destinationPath) {
        var psi = new ProcessStartInfo {
            FileName = "yt-dlp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("bestaudio");
        psi.ArgumentList.Add("--extract-audio");
        psi.ArgumentList.Add("--audio-format");
        psi.ArgumentList.Add("wav");
        psi.ArgumentList.Add("--newline");
        psi.ArgumentList.Add("--progress");

        if(HasNode) {
            psi.ArgumentList.Add("--js-runtimes");
            psi.ArgumentList.Add("node");
            psi.ArgumentList.Add("--remote-components");
            psi.ArgumentList.Add("ejs:github");
        }

        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add(destinationPath);

        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(url);

        using var p = Process.Start(psi);
        if(p == null) {
            return null!;
        }

        var stdoutTask = Task.Run(() => {
            while(!p.StandardOutput.EndOfStream) {
                var line = p.StandardOutput.ReadLine();
                if(!string.IsNullOrWhiteSpace(line)) {
                    MelonLogger.Msg(line);
                }
            }
        });

        var stderrTask = Task.Run(() => {
            while(!p.StandardError.EndOfStream) {
                var line = p.StandardError.ReadLine();
                if(!string.IsNullOrWhiteSpace(line)) {
                    MelonLogger.Msg(line);
                }
            }
        });

        p.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);

        return File.Exists(destinationPath) ? destinationPath : null!;
    }

    private static IEnumerator LoadClip(string path, string url) {
        using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV);

        yield return req.SendWebRequest();

        if(req.result != UnityWebRequest.Result.Success) {
            MelonLogger.Warning("clip load failed");
            IsLoading = false;
            runningUrl = string.Empty;
            yield break;
        }

        cachedClip = DownloadHandlerAudioClip.GetContent(req);
        cachedUrl = url;

        TryDelete(path);

        IsLoading = false;
        runningUrl = string.Empty;

        MelonLogger.Msg("ready");
    }

    public static bool ApplyCachedClip(string url) {
        if(cachedClip == null || cachedUrl != url) {
            return false;
        }

        scrConductor.instance.song.clip = cachedClip;
        return true;
    }

    private static string GetTempPath() {
        return Path.Combine(
            Path.GetTempPath(),
            "adofai_yt_" + Guid.NewGuid().ToString("N") + ".wav"
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