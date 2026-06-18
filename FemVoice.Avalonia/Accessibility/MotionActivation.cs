using FemVoice.Avalonia.Preferences;

namespace FemVoice.Avalonia.Accessibility;

/// <summary>
/// Stage 2C — AVALONIA-OWNED runtime activation of the saved "reduce motion" accessibility preference. It holds
/// a single process-local boolean (<see cref="ReduceMotion"/>) that Avalonia UI motion/animation effects can
/// consult, applies it at startup and live on Save, and raises <see cref="ReduceMotionChanged"/> for live UI
/// updates. It is the single reduce-motion activation point.
///
/// Avalonia-only and side-effect-free beyond this flag: it does NOT touch WPF, Core, the database, audio, clinical
/// code, theme, or language. The Avalonia head currently has no explicit animations/transitions, so the present
/// visual effect is intentionally a NO-OP — the preference is genuinely active and READY to be respected by any
/// future Avalonia motion effect (which should gate itself on <see cref="ReduceMotion"/>). Null-safe everywhere
/// (no Application/UI dependency).
/// </summary>
public static class MotionActivation
{
    private static bool _reduceMotion;

    /// <summary>Current Avalonia-local reduce-motion state. Future motion effects should disable/curtail animation
    /// when this is <c>true</c>.</summary>
    public static bool ReduceMotion
    {
        get => _reduceMotion;
        private set
        {
            if (_reduceMotion == value) return;
            _reduceMotion = value;
            ReduceMotionChanged?.Invoke(value);
        }
    }

    /// <summary>Raised when <see cref="ReduceMotion"/> changes (startup or live Save).</summary>
    public static event System.Action<bool>? ReduceMotionChanged;

    /// <summary>Apply a reduce-motion value live (Avalonia-local; no WPF/Core/DB).</summary>
    public static void Apply(bool reduceMotion) => ReduceMotion = reduceMotion;

    /// <summary>
    /// Startup activation: apply the saved reduce-motion preference if a valid saved file exists; otherwise leave
    /// the safe default (motion enabled / not reduced). Never throws. Returns <c>true</c> if a saved value applied.
    /// </summary>
    public static bool ApplyFromStore(UiPreferencesStore? store = null)
    {
        store ??= new UiPreferencesStore();
        if (store.TryLoad(out var prefs))
        {
            Apply(prefs.ReduceMotion);
            return true;
        }
        Apply(false); // no/invalid saved preference → safe default (not reduced)
        return false;
    }
}
