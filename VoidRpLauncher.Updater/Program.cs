using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace VoidRpLauncher.Updater;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = UpdaterOptions.Parse(args);

            WaitForLauncherExit(options.ProcessId);

            switch (options.SourceKind)
            {
                case "single-file":
                    ReplaceSingleFileWithRetries(options.SourcePath, options.TargetPath);
                    break;

                case "app-bundle-zip":
                    ReplaceAppBundleFromZipWithRetries(options.SourcePath, options.TargetPath);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported update source kind '{options.SourceKind}'.");
            }

            RestartUpdatedLauncher(options.TargetPath, options.RestartMode);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void WaitForLauncherExit(int processId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    break;
            }
            catch (ArgumentException)
            {
                break;
            }

            Thread.Sleep(300);
        }

        Thread.Sleep(500);
    }

    private static void ReplaceSingleFileWithRetries(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Downloaded update file was not found.", sourcePath);

        var backupPath = targetPath + ".bak";
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                TryDeleteFile(backupPath);

                if (File.Exists(targetPath))
                    File.Move(targetPath, backupPath, overwrite: true);

                File.Copy(sourcePath, targetPath, overwrite: true);

                TryEnsureExecutablePermissions(targetPath);

                TryDeleteFile(sourcePath);
                TryDeleteFile(backupPath);

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;

                try
                {
                    if (!File.Exists(targetPath) && File.Exists(backupPath))
                        File.Move(backupPath, targetPath, overwrite: true);
                }
                catch
                {
                    // Игнорируем промежуточную ошибку восстановления.
                }

                Thread.Sleep(500);
            }
        }

        throw new IOException("Failed to replace launcher after multiple attempts.", lastError);
    }

    private static void ReplaceAppBundleFromZipWithRetries(string zipPath, string targetBundlePath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Downloaded app bundle archive was not found.", zipPath);

        var parentDirectory = Path.GetDirectoryName(targetBundlePath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
            throw new InvalidOperationException("Could not resolve target bundle parent directory.");

        Directory.CreateDirectory(parentDirectory);

        var extractRoot = Path.Combine(
            parentDirectory,
            ".voidrp-app-update-" + Guid.NewGuid().ToString("N"));

        ZipFile.ExtractToDirectory(zipPath, extractRoot);

        var extractedBundlePath = FindExtractedAppBundle(extractRoot);
        if (string.IsNullOrWhiteSpace(extractedBundlePath))
        {
            TryDeleteDirectory(extractRoot);
            throw new InvalidOperationException("Could not find .app bundle inside downloaded archive.");
        }

        var backupPath = targetBundlePath + ".bak";
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                TryDeleteDirectory(backupPath);

                if (Directory.Exists(targetBundlePath))
                    Directory.Move(targetBundlePath, backupPath);

                Directory.Move(extractedBundlePath, targetBundlePath);

                TryDeleteDirectory(extractRoot);
                TryDeleteFile(zipPath);
                TryDeleteDirectory(backupPath);

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;

                try
                {
                    if (!Directory.Exists(targetBundlePath) && Directory.Exists(backupPath))
                        Directory.Move(backupPath, targetBundlePath);
                }
                catch
                {
                    // Игнорируем промежуточную ошибку восстановления.
                }

                Thread.Sleep(500);
            }
        }

        TryDeleteDirectory(extractRoot);
        throw new IOException("Failed to replace .app bundle after multiple attempts.", lastError);
    }

    private static string? FindExtractedAppBundle(string extractRoot)
    {
        var directChild = Directory
            .EnumerateDirectories(extractRoot, "*.app", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(directChild))
            return directChild;

        return Directory
            .EnumerateDirectories(extractRoot, "*.app", SearchOption.AllDirectories)
            .OrderBy(path => path.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar))
            .FirstOrDefault();
    }

    private static void RestartUpdatedLauncher(string targetPath, string restartMode)
    {
        switch (restartMode)
        {
            case "start-file":
                StartFile(targetPath);
                return;

            case "open-app":
                OpenAppBundle(targetPath);
                return;

            default:
                throw new NotSupportedException($"Unsupported restart mode '{restartMode}'.");
        }
    }

    private static void StartFile(string launcherPath)
    {
        if (!File.Exists(launcherPath))
            throw new FileNotFoundException("Updated launcher file was not found.", launcherPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? AppContext.BaseDirectory,
            UseShellExecute = OperatingSystem.IsWindows()
        };

        Process.Start(startInfo);
    }

    private static void OpenAppBundle(string appBundlePath)
    {
        if (!Directory.Exists(appBundlePath))
            throw new DirectoryNotFoundException(
                $"Updated app bundle was not found: {appBundlePath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/open",
            Arguments = Quote(appBundlePath),
            WorkingDirectory = Path.GetDirectoryName(appBundlePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false
        };

        Process.Start(startInfo);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Игнорируем хвосты.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Игнорируем хвосты.
        }
    }

    private static void TryEnsureExecutablePermissions(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return;

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }
        catch
        {
            // Не валим запуск только из-за chmod.
        }
    }

    private sealed class UpdaterOptions
    {
        public int ProcessId { get; init; }
        public string SourcePath { get; init; } = string.Empty;
        public string TargetPath { get; init; } = string.Empty;
        public string SourceKind { get; init; } = string.Empty;
        public string RestartMode { get; init; } = string.Empty;

        public static UpdaterOptions Parse(string[] args)
        {
            var processIdText = GetValue(args, "--pid");
            var sourcePath = GetValue(args, "--source");
            var targetPath = GetValue(args, "--target");
            var sourceKind = GetValue(args, "--source-kind");
            var restartMode = GetValue(args, "--restart");

            if (!int.TryParse(processIdText, out var processId))
                throw new ArgumentException("Missing or invalid --pid argument.");

            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Missing --source argument.");

            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("Missing --target argument.");

            if (string.IsNullOrWhiteSpace(sourceKind))
                throw new ArgumentException("Missing --source-kind argument.");

            if (string.IsNullOrWhiteSpace(restartMode))
                throw new ArgumentException("Missing --restart argument.");

            return new UpdaterOptions
            {
                ProcessId = processId,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                SourceKind = sourceKind,
                RestartMode = restartMode
            };
        }

        private static string GetValue(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return string.Empty;
        }
    }
}