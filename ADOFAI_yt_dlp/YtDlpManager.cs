using MelonLoader;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace ADOFAI_yt_dlp;

public static class YtDlpManager {
    public static bool HasNode = false;
    public static bool HasDeno = false;
    public static bool HasJsRuntime => HasNode || HasDeno;

    public static string? YtDlpPath = null;
    public static string JsRuntimesArg = string.Empty;
    public static bool NeedRemoteEjs = false;
    public static bool YtDlpSupportsEjs = true;
    public static bool FfmpegAvailable = false;
    public static string? FfmpegPath = null;

    public static bool IsLoading { get; private set; }
    public static string CurrentUrl = string.Empty;

    private static string cachedUrl = string.Empty;
    private static AudioClip cachedClip = null!;
    private static string runningUrl = string.Empty;

    private static scnEditor? pendingEditor;
    private static scnGame? pendingGame;
    private static int pendingGameSeqID;
    private static bool pendingGameRemakeFloors;

    public static bool IsUrlMode(string? songFilename) {
        return !string.IsNullOrWhiteSpace(CurrentUrl) &&
            string.IsNullOrWhiteSpace(songFilename);
    }

    public static bool IsValidHttpUrl(string? url) {
        if(!Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)) {
            return false;
        }

        return uriResult.Scheme == Uri.UriSchemeHttp ||
            uriResult.Scheme == Uri.UriSchemeHttps;
    }

    public static void NotifyLevelDecoded(string? url, string? songFilename) {
        if(!string.IsNullOrWhiteSpace(url)) {
            CurrentUrl = url.Trim();
        } else if(!string.IsNullOrWhiteSpace(songFilename)) {
            CurrentUrl = string.Empty;
            return;
        } else {
            return;
        }

        if(!IsUrlMode(songFilename)) {
            return;
        }

        EnsureLoading(CurrentUrl);
    }

    public static bool TryHandlePlay(scnEditor editor) => TryHandleEditorPlay(editor);

    public static bool TryHandleEditorPlay(scnEditor editor) {
        try {
            string? file = null;
            try {
                file = editor?.levelData?.songFilename;
            } catch { }

            if(!IsUrlMode(file)) {
                return true;
            }

            if(ApplyCachedClip(CurrentUrl)) {
                MelonLogger.Msg("cache hit");
                return true;
            }

            if(IsLoading) {
                MelonLogger.Msg("loading...");
                pendingEditor = editor;
                return false;
            }

            if(!IsValidHttpUrl(CurrentUrl)) {
                MelonLogger.Warning("Invalid URL blocked");
                return false;
            }

            pendingEditor = null;
            EnsureLoading(CurrentUrl);

            if(!IsLoading) {
                MelonLogger.Warning("download could not start, see log above");
            } else {
                pendingEditor = editor;
                MelonLogger.Msg("downloading...");
            }

            return false;
        } catch(Exception ex) {
            MelonLogger.Warning($"TryHandleEditorPlay failed: {ex.Message}");
            return true;
        }
    }

    public static bool TryHandleGamePlay(scnGame game, int seqID, bool remakeFloors) {
        try {
            string? file = null;
            try {
                file = game?.levelData?.songFilename;
            } catch { }

            if(!IsUrlMode(file)) {
                return true;
            }

            if(ApplyCachedClip(CurrentUrl)) {
                MelonLogger.Msg("cache hit");
                return true;
            }

            if(IsLoading) {
                MelonLogger.Msg("loading...");
                pendingGame = game;
                pendingGameSeqID = seqID;
                pendingGameRemakeFloors = remakeFloors;
                return false;
            }

            if(!IsValidHttpUrl(CurrentUrl)) {
                MelonLogger.Warning("Invalid URL blocked");
                return false;
            }

            pendingGame = null;
            EnsureLoading(CurrentUrl);

            if(!IsLoading) {
                MelonLogger.Warning("download could not start, see log above");
            } else {
                pendingGame = game;
                pendingGameSeqID = seqID;
                pendingGameRemakeFloors = remakeFloors;
                MelonLogger.Msg("downloading...");
            }

            return false;
        } catch(Exception ex) {
            MelonLogger.Warning($"TryHandleGamePlay failed: {ex.Message}");
            return true;
        }
    }

    public static void EnsureLoading(string url) {
        try {
            if(string.IsNullOrWhiteSpace(url)) {
                return;
            }

            if(ApplyCachedClip(url)) {
                return;
            }

            if(IsLoading) {
                return;
            }

            if(!IsValidHttpUrl(url)) {
                MelonLogger.Warning("Invalid URL blocked");
                return;
            }

            if(string.IsNullOrWhiteSpace(YtDlpPath)) {
                MelonLogger.Warning("yt-dlp not found, cannot download");
                return;
            }

            StartLoad(url);
        } catch(Exception ex) {
            MelonLogger.Warning($"EnsureLoading failed: {ex.Message}");
        }
    }

    private static void StartLoad(string url) {
        if(IsLoading) {
            return;
        }

        IsLoading = true;
        runningUrl = url;

        string safePath = GetTempPath();

        try {
            MelonCoroutines.Start(LoadRoutine(url, safePath));
        } catch(Exception ex) {
            MelonLogger.Error($"failed to start load coroutine: {ex.Message}");
            IsLoading = false;
            runningUrl = string.Empty;
        }
    }

    private static IEnumerator LoadRoutine(string url, string destinationPath) {
        Task<string?>? dlTask = null;
        try {
            dlTask = Task.Run(() => RunYtDlp(url, destinationPath));
        } catch(Exception ex) {
            MelonLogger.Error($"failed to start yt-dlp task: {ex.Message}");
        }

        if(dlTask == null) {
            IsLoading = false;
            runningUrl = string.Empty;
            FailPending();
            yield break;
        }

        while(!dlTask.IsCompleted) {
            yield return null;
        }

        string? path = null;
        try {
            path = dlTask.Result;
        } catch(Exception ex) {
            MelonLogger.Error($"yt-dlp task failed: {ex.GetBaseException().Message}");
        }

        if(string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            MelonLogger.Error("download failed");
            TryDelete(destinationPath);
            IsLoading = false;
            runningUrl = string.Empty;
            FailPending();
            yield break;
        }

        string uri;
        try {
            uri = new Uri(path).AbsoluteUri;
        } catch {
            uri = "file:///" + path.Replace('\\', '/');
        }

        AudioClip? clip = null;
        using(var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV)) {
            yield return req.SendWebRequest();

            if(req.result != UnityWebRequest.Result.Success) {
                MelonLogger.Warning($"clip load failed: {req.error}");
                IsLoading = false;
                runningUrl = string.Empty;
                TryDelete(path);
                FailPending();
                yield break;
            }

            try {
                clip = DownloadHandlerAudioClip.GetContent(req);
            } catch(Exception ex) {
                MelonLogger.Warning($"clip decode failed: {ex.Message}");
            }
        }

        if(clip == null) {
            MelonLogger.Warning("clip load failed");
            IsLoading = false;
            runningUrl = string.Empty;
            TryDelete(path);
            FailPending();
            yield break;
        }

        try {
            clip.name = "adofai_yt_external";
        } catch { }

        cachedClip = clip;
        cachedUrl = url;

        TryDelete(path);

        IsLoading = false;
        runningUrl = string.Empty;

        MelonLogger.Msg("ready");
        ApplyCachedClip(url);
        RetryPending(url);
    }

    private static string? RunYtDlp(string url, string destinationPath) {
        try {
            string exe = string.IsNullOrWhiteSpace(YtDlpPath) ? "yt-dlp" : YtDlpPath;

            var psi = new ProcessStartInfo {
                FileName = exe,
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

            // ffmpeg may have been found next to the game rather than on PATH, where yt-dlp would not look.
            if(!string.IsNullOrWhiteSpace(FfmpegPath)) {
                psi.ArgumentList.Add("--ffmpeg-location");
                psi.ArgumentList.Add(FfmpegPath!);
            }

            if(YtDlpSupportsEjs) {
                if(!string.IsNullOrWhiteSpace(JsRuntimesArg)) {
                    psi.ArgumentList.Add("--js-runtimes");
                    psi.ArgumentList.Add(JsRuntimesArg);
                }

                if(NeedRemoteEjs) {
                    psi.ArgumentList.Add("--remote-components");
                    psi.ArgumentList.Add("ejs:github");
                }
            }

            psi.ArgumentList.Add("--output");
            psi.ArgumentList.Add(destinationPath);

            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(url);

            using var p = Process.Start(psi);
            if(p == null) {
                MelonLogger.Error("failed to start yt-dlp process");
                return null;
            }

            var stderrTail = new List<string>();
            object stderrLock = new object();

            var stdoutTask = Task.Run(() => {
                try {
                    while(!p.StandardOutput.EndOfStream) {
                        var line = p.StandardOutput.ReadLine();
                        if(string.IsNullOrWhiteSpace(line)) {
                            continue;
                        }

                        if(IsProgressNoise(line)) {
                            continue;
                        }

                        MelonLogger.Msg(line);
                    }
                } catch { }
            });

            var stderrTask = Task.Run(() => {
                try {
                    while(!p.StandardError.EndOfStream) {
                        var line = p.StandardError.ReadLine();
                        if(string.IsNullOrWhiteSpace(line)) {
                            continue;
                        }

                        lock(stderrLock) {
                            stderrTail.Add(line);
                            if(stderrTail.Count > 50) {
                                stderrTail.RemoveAt(0);
                            }
                        }

                        MelonLogger.Warning(line);
                    }
                } catch { }
            });

            p.WaitForExit();
            try {
                Task.WaitAll(stdoutTask, stderrTask);
            } catch { }

            if(p.ExitCode != 0) {
                MelonLogger.Error($"yt-dlp exit={p.ExitCode}");
            }

            return File.Exists(destinationPath) ? destinationPath : null;
        } catch(Exception ex) {
            MelonLogger.Error($"yt-dlp failed: {ex.Message}");
            return null;
        }
    }

    private static bool IsProgressNoise(string line) {
        return line.StartsWith("[download]", StringComparison.Ordinal) &&
            line.Contains('%') &&
            !line.Contains("100%") &&
            !line.Contains("Destination", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ApplyCachedClip(string url) {
        try {
            if(string.IsNullOrWhiteSpace(url)) {
                return false;
            }

            if(cachedClip == null || cachedUrl != url) {
                return false;
            }

            var conductor = scrConductor.instance;
            if(conductor == null || conductor.song == null) {
                return false;
            }

            conductor.song.clip = cachedClip;
            return true;
        } catch {
            return false;
        }
    }

    private static void RetryPending(string url) {
        scnEditor? editor = pendingEditor;
        pendingEditor = null;

        scnGame? game = pendingGame;
        pendingGame = null;
        int seqID = pendingGameSeqID;
        bool remake = pendingGameRemakeFloors;

        try {
            if(editor != null) {
                string? file = null;
                try {
                    file = editor.levelData?.songFilename;
                } catch { }

                if(CurrentUrl == url && IsUrlMode(file)) {
                    MelonLogger.Msg("auto play");
                    editor.Play();
                    return;
                }
            }
        } catch(Exception ex) {
            MelonLogger.Warning($"auto play (editor) failed: {ex.Message}");
        }

        try {
            if(game != null) {
                string? file = null;
                try {
                    file = game.levelData?.songFilename;
                } catch { }

                if(CurrentUrl == url && IsUrlMode(file)) {
                    MelonLogger.Msg("auto play");
                    game.Play(seqID, remake);
                }
            }
        } catch(Exception ex) {
            MelonLogger.Warning($"auto play (game) failed: {ex.Message}");
        }
    }

    private static void FailPending() {
        pendingEditor = null;
        pendingGame = null;
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
