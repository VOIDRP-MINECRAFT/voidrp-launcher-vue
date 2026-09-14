using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Remembers recent game crashes (per launcher install) so repeated failures escalate
/// the advice, and so the pre-launch check can remind about an unresolved crash.
/// A clean game exit clears the history.
/// </summary>
public sealed class CrashHistoryService
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);
    private const string UnrecognizedKey = "<unrecognized>";

    private readonly string _filePath;
    private readonly object _lock = new();

    public CrashHistoryService(LauncherPathsService pathsService)
        => _filePath = Path.Combine(pathsService.StateDirectory, "crash-history.json");

    /// <summary>Records a crash and returns how many crashes with the same key happened within the window.</summary>
    public int Record(string? ruleKey, string title)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var entries = Load().Where(e => now - e.At < Window).ToList();
            var key = string.IsNullOrWhiteSpace(ruleKey) ? UnrecognizedKey : ruleKey;
            entries.Add(new Entry { Key = key, Title = title, At = now });
            Save(entries.TakeLast(20).ToList());
            return entries.Count(e => e.Key == key);
        }
    }

    /// <summary>The most recent crash within the window, if the game has not exited cleanly since.</summary>
    public Entry? Latest()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            return Load().Where(e => now - e.At < Window).OrderByDescending(e => e.At).FirstOrDefault();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { }
        }
    }

    private List<Entry> Load()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_filePath)) ?? new List<Entry>()
                : new List<Entry>();
        }
        catch
        {
            return new List<Entry>();
        }
    }

    private void Save(List<Entry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(entries));
        }
        catch { }
    }

    public sealed class Entry
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset At { get; set; }
    }
}
