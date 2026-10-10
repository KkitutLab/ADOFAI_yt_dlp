using ADOFAI;
using ADOFAI_yt_dlp;
using ADOFAI_yt_dlp.Patch;
using HarmonyLib;
using MelonLoader;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

[assembly: MelonInfo(typeof(Core), Info.Name, Info.Version, Info.Author, Info.Github)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace ADOFAI_yt_dlp;

public class Core : MelonMod {
    public override void OnInitializeMelon() => MelonCoroutines.Start(Initialize());

    private System.Collections.IEnumerator Initialize() {
        var prefCategory = MelonPreferences.CreateCategory(Info.Name, Info.Name);
        var prefJsRuntimePath = prefCategory.CreateEntry(
            "JsRuntimePath",
            "",
            "JS Runtime Path",
            "Full path to a node or deno executable (file or folder). Empty = auto-detect.");

        string manualJs = string.Empty;
        try {
            manualJs = (prefJsRuntimePath.Value ?? string.Empty).Trim();
        } catch { }

        var detectTask = Task.Run(() => Detect(manualJs));
        while(!detectTask.IsCompleted) {
            yield return null;
        }

        if(detectTask.IsFaulted) {
            MelonLogger.Error($"tool detection failed: {detectTask.Exception?.GetBaseException().Message}");
        }

        Patch(HarmonyInstance);
    }

    private static void Detect(string manualJs) {
        var ytTask = Task.Run(() => FindExe("yt-dlp", "yt-dlp.exe"));
        var nodeTask = Task.Run(() => FindExe("node", "node.exe"));
        var denoTask = Task.Run(() => FindExe("deno", "deno.exe"));
        var ffmpegTask = Task.Run(() => FindExe("ffmpeg", "ffmpeg.exe"));
        Task.WaitAll(ytTask, nodeTask, denoTask, ffmpegTask);

        string? ytPath = ytTask.Result;
        string? autoNodePath = nodeTask.Result;
        string? autoDenoPath = denoTask.Result;
        string? ffmpegPath = ffmpegTask.Result;

        string? effNodePath = autoNodePath;
        string? effDenoPath = autoDenoPath;
        bool forceNodePath = false;
        bool forceDenoPath = false;

        if(!string.IsNullOrWhiteSpace(manualJs)) {
            var (manualNode, manualDeno) = ResolveManualJsRuntime(manualJs);
            if(manualNode == null && manualDeno == null) {
                MelonLogger.Warning($"JsRuntimePath is not usable: {manualJs}, falling back to auto-detect");
            } else {
                if(manualNode != null) {
                    effNodePath = manualNode;
                    forceNodePath = true;
                    MelonLogger.Msg($"manual JS runtime: node [{manualNode}]");
                }
                if(manualDeno != null) {
                    effDenoPath = manualDeno;
                    forceDenoPath = true;
                    MelonLogger.Msg($"manual JS runtime: deno [{manualDeno}]");
                }
            }
        }

        string? nodeVersion = effNodePath != null && effNodePath == autoNodePath
            ? (autoNodePath != null ? TryGetVersion(autoNodePath, "--version") : null)
            : (effNodePath != null ? TryGetVersion(effNodePath, "--version") : null);
        string? denoVersion = effDenoPath != null && effDenoPath == autoDenoPath
            ? (autoDenoPath != null ? TryGetVersion(autoDenoPath, "--version") : null)
            : (effDenoPath != null ? TryGetVersion(effDenoPath, "--version") : null);

        string? ytVersion = ytPath != null ? TryGetVersion(ytPath, "--version", 30000, logFailure: true) : null;
        string? ffmpegVersion = ffmpegPath != null ? TryGetVersion(ffmpegPath, "-version", 10000, logFailure: true) : null;

        if(ytPath == null) {
            MelonLogger.Warning("yt-dlp not found, downloads disabled (levels still load)");
        } else if(string.IsNullOrWhiteSpace(ytVersion)) {
            YtDlpManager.YtDlpPath = ytPath;
            YtDlpManager.YtDlpSupportsEjs = true;
            MelonLogger.Warning($"yt-dlp version unknown, will still try to use it [{ytPath}]");
        } else {
            YtDlpManager.YtDlpPath = ytPath;
            YtDlpManager.YtDlpSupportsEjs = CheckYtDlpEjsSupport(ytVersion);
            MelonLogger.Msg($"yt-dlp {FirstLine(ytVersion)} [{ytPath}]");
            if(!YtDlpManager.YtDlpSupportsEjs) {
                MelonLogger.Warning("yt-dlp is too old for EJS, consider upgrading (2025.11.12+)");
            }
        }

        if(!string.IsNullOrWhiteSpace(nodeVersion) && effNodePath != null) {
            YtDlpManager.HasNode = true;
            MelonLogger.Msg($"node {FirstLine(nodeVersion)} [{effNodePath}]");
            if(!CheckNodeVersion(nodeVersion)) {
                MelonLogger.Warning("node < 22 may not work with yt-dlp EJS");
            }
        } else {
            YtDlpManager.HasNode = false;
        }

        if(!string.IsNullOrWhiteSpace(denoVersion) && effDenoPath != null) {
            YtDlpManager.HasDeno = true;
            MelonLogger.Msg($"deno {FirstLine(denoVersion)} [{effDenoPath}]");
            if(!CheckDenoVersion(denoVersion)) {
                MelonLogger.Warning("deno < 2.3.0 may not work with yt-dlp EJS");
            }
        } else {
            YtDlpManager.HasDeno = false;
        }

        YtDlpManager.JsRuntimesArg = BuildJsRuntimesArg(effNodePath, effDenoPath, forceNodePath, forceDenoPath);
        YtDlpManager.NeedRemoteEjs = YtDlpManager.HasJsRuntime;

        if(!string.IsNullOrWhiteSpace(YtDlpManager.JsRuntimesArg)) {
            MelonLogger.Msg($"js-runtimes: {YtDlpManager.JsRuntimesArg}");
        } else if(YtDlpManager.HasDeno) {
            MelonLogger.Msg("js-runtimes: deno (default)");
        }

        if(!YtDlpManager.HasJsRuntime) {
            MelonLogger.Warning("no JS runtime (node/deno) found, YouTube downloads may fail or miss formats");
        }

        if(!string.IsNullOrWhiteSpace(ffmpegVersion)) {
            YtDlpManager.FfmpegAvailable = true;
            YtDlpManager.FfmpegPath = ffmpegPath;
            MelonLogger.Msg($"ffmpeg found [{ffmpegPath}]");
        } else if(ffmpegPath != null) {
            YtDlpManager.FfmpegAvailable = true;
            YtDlpManager.FfmpegPath = ffmpegPath;
            MelonLogger.Warning($"ffmpeg version unknown, will still try to use it [{ffmpegPath}]");
        } else {
            YtDlpManager.FfmpegAvailable = false;
            MelonLogger.Warning("ffmpeg not found, wav conversion via yt-dlp will fail");
        }
    }

    private void Patch(HarmonyLib.Harmony harmony) {
        TryPatch(harmony,
            () => typeof(RDEditorUtils).GetMethod(
                nameof(RDEditorUtils.CheckModsDependency),
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(object[]) }, null),
            // Must run before other mods' prefixes (e.g. Quartz's RequiredModsGate) that read `mods` and skip the original.
            prefix: new HarmonyMethod(typeof(P_RDEditorUtils__CheckModsDependency), nameof(P_RDEditorUtils__CheckModsDependency.Prefix)) {
                priority = HarmonyLib.Priority.First
            },
            postfix: null,
            name: "RDEditorUtils.CheckModsDependency"
        );
        TryPatch(harmony,
            () => typeof(LevelData).GetMethod(
                nameof(LevelData.EncodeToDictionary),
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null),
            prefix: null,
            postfix: new HarmonyMethod(typeof(P_ADOFAI__LevelData__EncodeToDictionary), nameof(P_ADOFAI__LevelData__EncodeToDictionary.Postfix)),
            name: "LevelData.EncodeToDictionary"
        );
        TryPatch(harmony,
            () => typeof(LevelData).GetMethod(
                nameof(LevelData.Decode),
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Dictionary<string, object>), typeof(LoadResult).MakeByRefType() }, null),
            prefix: new HarmonyMethod(typeof(P_ADOFAI__LevelData__Decode), nameof(P_ADOFAI__LevelData__Decode.Prefix)),
            postfix: null,
            name: "LevelData.Decode"
        );
        TryPatch(harmony,
            () => typeof(AudioManager).GetMethod(
                nameof(AudioManager.FindOrLoadAudioClipExternal),
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string), typeof(bool), typeof(float), typeof(bool) }, null),
            prefix: new HarmonyMethod(typeof(P_AudioManager__FindOrLoadAudioClipExternal), nameof(P_AudioManager__FindOrLoadAudioClipExternal.Prefix)),
            postfix: null,
            name: "AudioManager.FindOrLoadAudioClipExternal"
        );
        TryPatch(harmony,
            () => typeof(scnGame).GetMethod(
                nameof(scnGame.ReloadSong),
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(bool) }, null),
            prefix: new HarmonyMethod(typeof(P_scnGame__ReloadSong), nameof(P_scnGame__ReloadSong.Prefix)),
            postfix: null,
            name: "scnGame.ReloadSong"
        );
        TryPatch(harmony,
            () => typeof(scnGame).GetMethod(
                nameof(scnGame.Play),
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(bool) }, null),
            prefix: new HarmonyMethod(typeof(P_scnGame__Play), nameof(P_scnGame__Play.Prefix)),
            postfix: null,
            name: "scnGame.Play"
        );
        TryPatch(harmony,
            () => typeof(scnEditor).GetMethod(
                nameof(scnEditor.Play),
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null),
            prefix: new HarmonyMethod(typeof(P_scnEditor__Play), nameof(P_scnEditor__Play.Prefix)),
            postfix: null,
            name: "scnEditor.Play"
        );
    }

    private void TryPatch(HarmonyLib.Harmony harmony, Func<MethodInfo?> resolve, HarmonyMethod? prefix, HarmonyMethod? postfix, string name) {
        try {
            MethodInfo? target = resolve();
            if(target == null) {
                MelonLogger.Error($"patch target not found: {name}");
                return;
            }

            harmony.Patch(target, prefix: prefix, postfix: postfix);
            MelonLogger.Msg($"patched: {name}");
        } catch(Exception ex) {
            MelonLogger.Error($"patch failed ({name}): {ex.Message}");
        }
    }

    private static string? FindExe(params string[] names) {
        try {
            var dirs = new List<string>();
            var seen = new HashSet<string>(
                Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

            void AddDir(string? dir) {
                if(string.IsNullOrWhiteSpace(dir)) {
                    return;
                }

                try {
                    string full = Path.GetFullPath(dir);
                    if(seen.Add(full)) {
                        dirs.Add(full);
                    }
                } catch { }
            }

            try {
                string gameDir = MelonLoader.Utils.MelonEnvironment.GameRootDirectory;
                AddDir(gameDir);
                AddDir(Path.Combine(gameDir, "Mods"));
                AddDir(Path.Combine(gameDir, "UserData"));
            } catch { }

            try {
                AddDir(AppDomain.CurrentDomain.BaseDirectory);
            } catch { }

            try {
                AddDir(Directory.GetCurrentDirectory());
            } catch { }

            try {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                AddDir(Path.Combine(home, ".deno", "bin"));
                AddDir(Path.Combine(home, ".local", "bin"));
            } catch { }

            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if(isWindows) {
                try {
                    AddDir(Environment.GetEnvironmentVariable("PROGRAMFILES") != null
                        ? Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES")!, "nodejs")
                        : null);
                    AddDir(Environment.GetEnvironmentVariable("PROGRAMFILES(X86)") != null
                        ? Path.Combine(Environment.GetEnvironmentVariable("PROGRAMFILES(X86)")!, "nodejs")
                        : null);
                } catch { }
            } else {
                AddDir("/usr/local/bin");
                AddDir("/opt/homebrew/bin");
            }

            try {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if(!string.IsNullOrEmpty(pathEnv)) {
                    foreach(string dir in pathEnv.Split(Path.PathSeparator)) {
                        AddDir(dir);
                    }
                }
            } catch { }

            if(isWindows) {
                foreach(var target in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }) {
                    try {
                        string? regPath = Environment.GetEnvironmentVariable("PATH", target);
                        if(!string.IsNullOrEmpty(regPath)) {
                            foreach(string dir in regPath.Split(Path.PathSeparator)) {
                                AddDir(Environment.ExpandEnvironmentVariables(dir));
                            }
                        }
                    } catch { }
                }

                try {
                    string winget = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Microsoft", "WinGet");
                    AddDir(Path.Combine(winget, "Links"));

                    string packages = Path.Combine(winget, "Packages");
                    if(Directory.Exists(packages)) {
                        foreach(string pkg in Directory.GetDirectories(packages)) {
                            AddDir(pkg);
                            foreach(string sub in Directory.GetDirectories(pkg)) {
                                AddDir(Path.Combine(sub, "bin"));
                            }
                        }
                    }
                } catch { }
            }

            var fileNames = new List<string>();
            foreach(string baseName in names) {
                if(isWindows && !baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                    string withExe = baseName + ".exe";
                    if(!fileNames.Contains(withExe)) {
                        fileNames.Add(withExe);
                    }
                }

                if(!fileNames.Contains(baseName)) {
                    fileNames.Add(baseName);
                }
            }

            foreach(string dir in dirs) {
                foreach(string fileName in fileNames) {
                    string full;
                    try {
                        full = Path.Combine(dir, fileName);
                    } catch {
                        continue;
                    }

                    try {
                        if(File.Exists(full)) {
                            return full;
                        }
                    } catch { }
                }
            }

            return null;
        } catch {
            return null;
        }
    }

    private static string? TryGetVersion(string exe, string args, int timeoutMs = 10000, bool logFailure = false) {
        void Fail(string reason) {
            if(logFailure) {
                MelonLogger.Warning($"version check failed [{exe} {args}]: {reason}");
            }
        }

        try {
            var psi = new ProcessStartInfo {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var sw = Stopwatch.StartNew();
            using var process = Process.Start(psi);
            if(process == null) {
                Fail("process did not start");
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if(!process.WaitForExit(timeoutMs)) {
                try {
                    process.Kill();
                } catch { }
                Fail($"timed out after {timeoutMs} ms");
                return null;
            }

            string output = string.Empty;
            string error = string.Empty;
            try {
                if(stdoutTask.Wait(3000)) {
                    output = stdoutTask.Result ?? string.Empty;
                }
            } catch { }
            try {
                if(stderrTask.Wait(3000)) {
                    error = stderrTask.Result ?? string.Empty;
                }
            } catch { }

            if(process.ExitCode != 0) {
                string err = error.Trim();
                Fail($"exit={process.ExitCode} after {sw.ElapsedMilliseconds} ms" +
                    (err.Length > 0 ? $", stderr: {FirstLine(err)}" : string.Empty));
                return null;
            }

            foreach(string raw in output.Split('\n')) {
                string trimmed = raw.Trim();
                if(!string.IsNullOrWhiteSpace(trimmed)) {
                    return trimmed;
                }
            }

            Fail("no output on stdout");
            return null;
        } catch(Exception ex) {
            Fail(ex.Message);
            return null;
        }
    }

    private static string FirstLine(string? version) {
        if(string.IsNullOrWhiteSpace(version)) {
            return "unknown";
        }

        int idx = version.IndexOf('\n');
        return idx < 0 ? version.Trim() : version.Substring(0, idx).Trim();
    }

    private static string BuildJsRuntimesArg(string? nodePath, string? denoPath, bool forceNodePath = false, bool forceDenoPath = false) {
        bool hasNode = !string.IsNullOrWhiteSpace(nodePath);
        bool hasDeno = !string.IsNullOrWhiteSpace(denoPath);

        if(!hasNode && !hasDeno) {
            return string.Empty;
        }

        if(hasNode && !hasDeno) {
            return (forceNodePath || !IsInPath(nodePath!)) ? "node:" + nodePath : "node";
        }

        if(hasDeno && !hasNode) {
            return (forceDenoPath || !IsInPath(denoPath!)) ? "deno:" + denoPath : string.Empty;
        }

        string nodePart = (forceNodePath || !IsInPath(nodePath!)) ? "node:" + nodePath : "node";
        string denoPart = (forceDenoPath || !IsInPath(denoPath!)) ? "deno:" + denoPath : "deno";
        return denoPart + "," + nodePart;
    }

    private static (string? node, string? deno) ResolveManualJsRuntime(string input) {
        try {
            string path = ExpandHome(input.Trim().Trim('"'));
            if(string.IsNullOrWhiteSpace(path)) {
                return (null, null);
            }

            if(File.Exists(path)) {
                string? kind = DetectRuntimeKind(path);
                if(kind == "node") {
                    return (path, null);
                }
                if(kind == "deno") {
                    return (null, path);
                }

                MelonLogger.Warning($"JsRuntimePath kind unknown (need node or deno): {input}");
                return (null, null);
            }

            if(Directory.Exists(path)) {
                string? node = PickExeInDir(path, "node");
                string? deno = PickExeInDir(path, "deno");
                if(node == null && deno == null) {
                    MelonLogger.Warning($"JsRuntimePath folder has no node/deno: {input}");
                }
                return (node, deno);
            }

            return (null, null);
        } catch {
            return (null, null);
        }
    }

    private static string? PickExeInDir(string dir, string baseName) {
        try {
            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            string[] candidates = isWindows
                ? new[] { baseName + ".exe", baseName }
                : new[] { baseName };

            foreach(string fileName in candidates) {
                string full;
                try {
                    full = Path.Combine(dir, fileName);
                } catch {
                    continue;
                }

                try {
                    if(File.Exists(full)) {
                        return full;
                    }
                } catch { }
            }
        } catch { }

        return null;
    }

    private static string? DetectRuntimeKind(string exePath) {
        try {
            string baseName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
            if(baseName == "node" || baseName == "nodejs") {
                return "node";
            }
            if(baseName == "deno") {
                return "deno";
            }

            string? version = TryGetVersion(exePath, "--version");
            if(string.IsNullOrWhiteSpace(version)) {
                return null;
            }

            string v = version.Trim();
            if(v.StartsWith("deno", StringComparison.OrdinalIgnoreCase)) {
                return "deno";
            }
            if(v.StartsWith("v", StringComparison.OrdinalIgnoreCase) || (v.Length > 0 && char.IsDigit(v[0]))) {
                return "node";
            }

            return null;
        } catch {
            return null;
        }
    }

    private static string ExpandHome(string path) {
        try {
            if(path.StartsWith("~/") || path.StartsWith("~\\") || path == "~") {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if(!string.IsNullOrEmpty(home)) {
                    return Path.Combine(home, path.Length > 1 ? path.Substring(2) : string.Empty);
                }
            }
        } catch { }

        return path;
    }

    private static bool IsInPath(string fullPath) {
        try {
            string? dir = Path.GetDirectoryName(fullPath);
            if(string.IsNullOrWhiteSpace(dir)) {
                return false;
            }

            string normDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if(string.IsNullOrEmpty(pathEnv)) {
                return false;
            }

            StringComparison cmp = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach(string entry in pathEnv.Split(Path.PathSeparator)) {
                if(string.IsNullOrWhiteSpace(entry)) {
                    continue;
                }

                try {
                    string normEntry = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if(string.Equals(normDir, normEntry, cmp)) {
                        return true;
                    }
                } catch { }
            }
        } catch { }

        return false;
    }

    private static bool CheckNodeVersion(string version) {
        try {
            string v = version.Trim().TrimStart('v', 'V');
            int dot = v.IndexOf('.');
            string majorStr = dot < 0 ? v : v.Substring(0, dot);
            return int.TryParse(majorStr, out int major) && major >= 22;
        } catch {
            return false;
        }
    }

    private static bool CheckDenoVersion(string version) {
        try {
            string v = version.Trim();
            if(v.StartsWith("deno", StringComparison.OrdinalIgnoreCase)) {
                v = v.Substring(4).Trim();
            }

            string[] parts = v.Split('.');
            if(parts.Length < 2 || !int.TryParse(parts[0], out int major)) {
                return false;
            }

            if(major > 2) {
                return true;
            }

            if(major < 2) {
                return false;
            }

            string minorDigits = new string(parts[1].TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(minorDigits, out int minor) && minor >= 3;
        } catch {
            return false;
        }
    }

    private static bool CheckYtDlpEjsSupport(string version) {
        try {
            string v = version.Trim().TrimStart('v', 'V');
            string[] parts = v.Split('.');
            if(parts.Length < 3) {
                return true;
            }

            if(!int.TryParse(parts[0], out int year) ||
                !int.TryParse(parts[1], out int month) ||
                !int.TryParse(new string(parts[2].TakeWhile(char.IsDigit).ToArray()), out int day)) {
                return true;
            }

            if(year != 2025) {
                return year > 2025;
            }

            if(month != 11) {
                return month > 11;
            }

            return day >= 12;
        } catch {
            return true;
        }
    }
}
