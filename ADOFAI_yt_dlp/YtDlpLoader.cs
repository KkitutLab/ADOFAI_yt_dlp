using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAI_yt_dlp;

public static class YtDlpLoader {
    public static bool IsLoading { get; private set; }

    private static string cachedUrl = null!;
    private static AudioClip cachedClip = null!;

    public static IEnumerator Preload(scnGame game, string url) {
        if(IsLoading) {
            yield break;
        }

        if(cachedUrl == url && cachedClip) {
            MelonLogger.Msg("Using cached clip");
            yield break;
        }

        IsLoading = true;

        MelonLogger.Msg($"yt-dlp loading: {url}");

        string? audioUrl = null;

        yield return RunYtDlp(url, x => audioUrl = x);

        if(string.IsNullOrWhiteSpace(audioUrl)) {
            MelonLogger.Error("Failed to resolve audio url");
            IsLoading = false;
            yield break;
        }

        using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.MPEG);

        yield return req.SendWebRequest();

        if(req.result != UnityWebRequest.Result.Success) {
            MelonLogger.Error(req.error);
            IsLoading = false;
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);

        if(!clip) {
            MelonLogger.Error("Clip load failed");
            IsLoading = false;
            yield break;
        }

        cachedUrl = url;
        cachedClip = clip;

        MelonLogger.Msg("yt-dlp loaded");

        IsLoading = false;
    }

    public static bool ApplyCachedClip(string url) {
        if(cachedUrl != url || !cachedClip) {
            return false;
        }

        scrConductor.instance.song.clip = cachedClip;

        MelonLogger.Msg("Applied cached clip");

        return true;
    }

    private static IEnumerator RunYtDlp(string url, System.Action<string?> onDone) {
        var psi = new ProcessStartInfo {
            FileName = "yt-dlp",
            Arguments = $"-f bestaudio -g \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(psi);

        if(process == null) {
            yield break;
        }

        while(!process.HasExited) {
            yield return null;
        }

        string? result = process.StandardOutput.ReadLine()?.Trim();

        if(string.IsNullOrWhiteSpace(result)) {
            string err = process.StandardError.ReadToEnd();

            if(!string.IsNullOrWhiteSpace(err)) {
                MelonLogger.Error(err);
            }
        }

        onDone?.Invoke(result);
    }
}