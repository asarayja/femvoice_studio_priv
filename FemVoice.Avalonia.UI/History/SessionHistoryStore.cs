using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FemVoice.Avalonia.History;

/// <summary>
/// Avalonia-LOCAL, display-only session history: a small JSON list at
/// <c>&lt;ApplicationData&gt;/FemVoiceAvalonia/session-history.json</c> (distinct from the WPF app + its SQLite
/// database). It records only harmless non-clinical fields and is used purely to show a "recent sessions" list.
/// It NEVER touches the WPF `DatabaseService`, never feeds the frozen progression/clinical engines, and never
/// throws (missing/empty/corrupt → empty; write failures are swallowed). Mirrors the Stage-1 UiPreferencesStore
/// pattern.
/// </summary>
public sealed class SessionHistoryStore
{
    public const int MaxRecords = 200;

    private readonly string _path;

    public SessionHistoryStore(string? path = null)
        => _path = path ?? DefaultPath;

    /// <summary>Default Avalonia-local path (distinct from the WPF/DB storage).</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FemVoiceAvalonia", "session-history.json");

    public string FilePath => _path;

    /// <summary>All records in stored (oldest→newest) order. Graceful: missing/empty/corrupt → empty; never throws.</summary>
    public IReadOnlyList<SessionRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<SessionRecord>();
            string json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SessionRecord>();
            return JsonSerializer.Deserialize<List<SessionRecord>>(json) ?? new List<SessionRecord>();
        }
        catch { return Array.Empty<SessionRecord>(); }
    }

    /// <summary>The most recent <paramref name="count"/> records, newest first.</summary>
    public IReadOnlyList<SessionRecord> Recent(int count)
        => Load().Reverse().Take(Math.Max(0, count)).ToList();

    public int Count => Load().Count;

    /// <summary>Append a record (capped at <see cref="MaxRecords"/>). Never throws; write failures are swallowed.</summary>
    public void Append(SessionRecord record)
    {
        try
        {
            var list = new List<SessionRecord>(Load()) { record };
            if (list.Count > MaxRecords) list.RemoveRange(0, list.Count - MaxRecords);
            string? dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(list));
        }
        catch { /* display-only history: never surface a write error to the app */ }
    }

    /// <summary>Delete the local history file (never throws).</summary>
    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
    }
}
