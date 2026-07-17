using System;
using System.Text.Json.Serialization;

namespace FemVoice.Avalonia.History;

/// <summary>
/// One display-only local session record (Avalonia-local history). Holds only harmless, non-clinical fields —
/// when it happened, which surface, how long, and a free note. NO clinical score, NO WPF/DB coupling. Used to show
/// a simple "recent sessions" list; it never feeds the frozen progression/clinical engines.
/// </summary>
public sealed class SessionRecord
{
    /// <summary>UTC ticks of when the session ended (stored numerically for stable JSON round-trip).</summary>
    public long WhenUtcTicks { get; set; }

    /// <summary>Which surface produced it, e.g. "Dashbord" (display label only).</summary>
    public string Source { get; set; } = "";

    /// <summary>Session length in whole seconds (display only).</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Free-text note (display only), e.g. a "kun visning" disclaimer.</summary>
    public string Note { get; set; } = "";

    [JsonIgnore]
    public string WhenText => new DateTime(WhenUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string DurationText => DurationSeconds >= 60
        ? $"{DurationSeconds / 60} min {DurationSeconds % 60} s"
        : $"{DurationSeconds} s";

    [JsonIgnore]
    public string Display => $"{WhenText} · {Source} · {DurationText}";
}
