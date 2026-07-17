# Avalonia — Truthful audio copy (post real-mic activation) — Slice Report

Date: 2026-07-17 · Branch: `avalonia-truthful-audio-copy-slice` (off `main` @ `a4c544d`) · Host: Linux (.NET 10 `10.0.110`, real mic).

## Goal

After Stage 3D routed the real microphone into the dashboard, several static UI strings still claimed "synthetic
audio / no microphone", which is now false when a real mic is active. Make the audio-related copy **truthful** and
hide the synthetic test-tone selector when a real mic drives the dashboard. Small correctness/UX slice; verified
with an offscreen screenshot.

## What changed (files)

- **`FemVoice.Avalonia/ViewModels/MainDashboardViewModel.cs`** — new `IsSyntheticBackend` (`_capture is
  SyntheticAudioCaptureService`) to drive the selector's visibility.
- **`FemVoice.Avalonia/Views/DashboardView.axaml`**:
  - Subtitle reworded from "…fra **syntetisk lyd på Linux**." → "…fra **ekte mikrofon når en er tilgjengelig, ellers
    syntetisk testlyd**."
  - The synthetic-mode row is now `IsVisible="{Binding IsSyntheticBackend}"` (hidden with a real mic) and relabeled
    "Syntetisk lyd (Linux):" → "Syntetisk testlyd:".
- **`FemVoice.Avalonia/Views/ShellView.axaml`** — info-sidebar detail fallback reworded "**Syntetisk lyd · ingen
  mikrofon** · ingen lagring · ingen klinisk endring." → "**Ekte mikrofon når tilgjengelig** · ingen lagring · ingen
  klinisk endring." (The still-true "ingen lagring · ingen klinisk endring" disclaimers are kept.)

No overlay translations or smokes referenced these strings, so the reword is safe. The status strip already showed
the truthful `MicStatusText` ("Mikrofon: enheter funnet: N").

## Verification

- Offscreen dashboard screenshot confirms: truthful subtitle, "Ekte mikrofon når tilgjengelig" sidebar, and the
  synthetic selector **hidden** (real mic active on this box).
- Gate: build 0 err, **41/41 smokes**, portable **1570/1580** (baseline; Core untouched).

## Not changed

No clinical/DSP/scoring/Core/WPF change. The app-wide "kun visning" header/status text is kept (most pages are still
display-only scaffolds and the dashboard doesn't persist). When no real mic is present the synthetic selector
reappears and the copy still reads truthfully ("…ellers syntetisk testlyd").
