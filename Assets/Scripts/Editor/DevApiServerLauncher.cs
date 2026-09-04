#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Starts the local Node mock API (DevApiServer) when you press Play in the
/// Editor so Windows and macOS both work the same way. The old .bat files are
/// Windows-only; this process launcher finds Node on either platform.
/// </summary>
[InitializeOnLoad]
public static class DevApiServerLauncher
{
    const int Port = 5002;
    const string AutoStartPref = "FerrisWheel.AutoStartDevApi";
    const string MenuRoot = "Tools/Ferris Wheel/";

    static Process _process;

    static DevApiServerLauncher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += () => StopServer(silent: true);
    }

    static bool AutoStart
    {
        get => EditorPrefs.GetBool(AutoStartPref, true);
        set => EditorPrefs.SetBool(AutoStartPref, value);
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        if (!AutoStart) return;
        if (IsServerUp()) return;

        if (!StartServer())
        {
            EditorUtility.DisplayDialog(
                "Dev API Server",
                "Could not start the local mock API (port 5002).\n\n" +
                "Install Node.js from https://nodejs.org, then press Play again.\n" +
                "The game can still run in offline demo mode if no token is set.",
                "OK");
            return;
        }

        if (!WaitUntilUp(8000))
        {
            Debug.LogWarning("[DevApiServer] Started Node but port 5002 did not open in time.");
        }
    }

    [MenuItem(MenuRoot + "Start Dev API Server", false, 0)]
    public static void MenuStart()
    {
        if (IsServerUp())
        {
            Debug.Log("[DevApiServer] Already running on 127.0.0.1:" + Port);
            return;
        }

        if (!StartServer())
        {
            EditorUtility.DisplayDialog(
                "Dev API Server",
                "Node.js was not found. Install it from https://nodejs.org and retry.",
                "OK");
        }
    }

    [MenuItem(MenuRoot + "Stop Dev API Server", false, 1)]
    public static void MenuStop() => StopServer(silent: false);

    [MenuItem(MenuRoot + "Auto-Start Dev API On Play", false, 20)]
    public static void MenuToggleAutoStart()
    {
        AutoStart = !AutoStart;
        Debug.Log("[DevApiServer] Auto-start on Play: " + (AutoStart ? "ON" : "OFF"));
    }

    [MenuItem(MenuRoot + "Auto-Start Dev API On Play", true)]
    static bool MenuToggleAutoStartValidate()
    {
        Menu.SetChecked(MenuRoot + "Auto-Start Dev API On Play", AutoStart);
        return true;
    }

    static bool StartServer()
    {
        var serverDir = ServerDirectory();
        if (!Directory.Exists(serverDir) || !File.Exists(Path.Combine(serverDir, "server.js")))
        {
            Debug.LogError("[DevApiServer] Missing folder: " + serverDir);
            return false;
        }

        var node = FindExecutable("node");
        if (string.IsNullOrEmpty(node))
        {
            Debug.LogError("[DevApiServer] Node.js not found. Install from https://nodejs.org");
            return false;
        }

        if (!EnsureNpmInstall(serverDir, node)) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = node,
                Arguments = "server.js",
                WorkingDirectory = serverDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            PrependCommonBinDirs(psi);

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Debug.Log("[DevApiServer] " + e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[DevApiServer] " + e.Data);
            };
            _process.Exited += (_, _) =>
            {
                if (_process != null && _process.ExitCode != 0)
                    Debug.LogWarning("[DevApiServer] Process exited with code " + _process.ExitCode);
            };

            if (!_process.Start())
            {
                Debug.LogError("[DevApiServer] Failed to start Node.");
                return false;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            Debug.Log("[DevApiServer] Starting with " + node + " (pid " + _process.Id + ")");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DevApiServer] Start failed: " + ex.Message);
            return false;
        }
    }

    static void StopServer(bool silent)
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            if (!silent) Debug.LogWarning("[DevApiServer] Kill failed: " + ex.Message);
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }

        // Menu Stop also clears a server started from Terminal / .command / .bat.
        // Editor quit only kills the child we spawned, so a manual server stays up.
        if (!silent)
        {
            KillListenerOnPort();
            Debug.Log("[DevApiServer] Stopped.");
        }
    }

    static bool EnsureNpmInstall(string serverDir, string nodePath)
    {
        if (Directory.Exists(Path.Combine(serverDir, "node_modules", "express")))
            return true;

        var npm = FindNpm(nodePath);
        if (string.IsNullOrEmpty(npm))
        {
            Debug.LogError("[DevApiServer] npm not found; cannot install dependencies.");
            return false;
        }

        Debug.Log("[DevApiServer] Running npm install in " + serverDir);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = npm,
                Arguments = "install",
                WorkingDirectory = serverDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            PrependCommonBinDirs(psi);

            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(120000))
            {
                try { p.Kill(); } catch { /* ignore */ }
                Debug.LogError("[DevApiServer] npm install timed out.");
                return false;
            }

            if (p.ExitCode != 0)
            {
                Debug.LogError("[DevApiServer] npm install failed:\n" + p.StandardError.ReadToEnd());
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[DevApiServer] npm install failed: " + ex.Message);
            return false;
        }

        return Directory.Exists(Path.Combine(serverDir, "node_modules", "express"));
    }

    static string ServerDirectory()
    {
        var assets = Application.dataPath;
        return Path.GetFullPath(Path.Combine(assets, "..", "DevApiServer"));
    }

    static bool IsServerUp()
    {
        try
        {
            using var client = new TcpClient();
            var ar = client.BeginConnect("127.0.0.1", Port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(250))) return false;
            client.EndConnect(ar);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    static bool WaitUntilUp(int timeoutMs)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (IsServerUp()) return true;
            System.Threading.Thread.Sleep(150);
        }
        return IsServerUp();
    }

    static string FindExecutable(string name)
    {
        foreach (var candidate in CommonPaths(name))
        {
            if (File.Exists(candidate)) return candidate;
        }

        var fromPath = Which(name);
        if (!string.IsNullOrEmpty(fromPath) && File.Exists(fromPath)) return fromPath;
        return null;
    }

    static string FindNpm(string nodePath)
    {
        var dir = Path.GetDirectoryName(nodePath);
        if (!string.IsNullOrEmpty(dir))
        {
#if UNITY_EDITOR_WIN
            var cmd = Path.Combine(dir, "npm.cmd");
            if (File.Exists(cmd)) return cmd;
#endif
            var npm = Path.Combine(dir, "npm");
            if (File.Exists(npm)) return npm;
        }
        return FindExecutable("npm") ?? FindExecutable("npm.cmd");
    }

    static string[] CommonPaths(string name)
    {
#if UNITY_EDITOR_WIN
        var exe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                  name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            ? name
            : name + ".exe";
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", exe),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", exe),
        };
#else
        return new[]
        {
            "/opt/homebrew/bin/" + name,
            "/usr/local/bin/" + name,
            "/opt/local/bin/" + name,
            "/usr/bin/" + name,
        };
#endif
    }

    static string Which(string name)
    {
        try
        {
#if UNITY_EDITOR_WIN
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
#else
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-lc \"command -v " + name + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
#endif
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            if (p.ExitCode != 0 || string.IsNullOrEmpty(output)) return null;
            var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return first.Length > 0 ? first[0] : null;
        }
        catch
        {
            return null;
        }
    }

    static void PrependCommonBinDirs(ProcessStartInfo psi)
    {
        try
        {
            var path = psi.EnvironmentVariables["PATH"] ?? "";
#if UNITY_EDITOR_WIN
            var extra = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
#else
            const string extra = "/opt/homebrew/bin:/usr/local/bin";
#endif
            psi.EnvironmentVariables["PATH"] = extra + Path.PathSeparator + path;
        }
        catch
        {
            // EnvironmentVariables can throw if UseShellExecute is true; we keep it false.
        }
    }

    static void KillListenerOnPort()
    {
        try
        {
#if UNITY_EDITOR_WIN
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c for /f \"tokens=5\" %a in ('netstat -ano ^| findstr \":" + Port + " \" ^| findstr LISTENING') do taskkill /PID %a /F >nul 2>&1",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(4000);
#else
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-lc \"PIDS=$(lsof -nP -iTCP:" + Port + " -sTCP:LISTEN -t 2>/dev/null); [ -n \\\"$PIDS\\\" ] && kill $PIDS\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(4000);
#endif
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
#endif
