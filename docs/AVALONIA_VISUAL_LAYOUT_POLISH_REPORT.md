# Avalonia Visual Layout Polish — Slice Report

Date: 2026-06-17 · Branch: `avalonia-visual-layout-polish-slice` (off `main` @ `31aa0c9`).

> **Visual/layout only — behavior-neutral.** No new behavior, no persistence, no runtime platform implementation,
> no deferred-feature enablement, no clinical/domain or WPF change. A modest consistency pass, not a redesign.

## Motivation
The accepted Settings screenshot was structurally good but narrow/left-aligned with large unused space on the
right (wide desktop windows). The reading-oriented scaffold pages shared the same `MaxWidth≈820` +
`HorizontalAlignment="Left"` pattern. This pass makes those pages use desktop width in a balanced, consistent way.

## Layout changes (XAML only)
- **Shared pattern:** content columns changed from `HorizontalAlignment="Left"` (hugging the left) → **`Center`**
  with a wider `MaxWidth` and a consistent `Margin="24"`, so on a wide window the content is centered/balanced and
  on a narrow window it still fills.
- **Settings** (`SettingsView.axaml`): centered, `MaxWidth=1000`; section cards now flow in a **responsive
  `WrapPanel`** (each card `Width=480`) — **2 columns on a wide window, 1 when narrow** — directly reducing the
  empty right side. Disabled controls + deferred labels/chips unchanged.
- **SmartCoach / Progression / Analysis / Reports / Diagnostics** scaffolds: centered, `MaxWidth=960`. Cards/values
  unchanged (still synthetic "—", disabled actions).
- **Exercise Guide** (`ExerciseGuideView.axaml`): centered, `MaxWidth=1100` — rows stay readable on a wide window;
  filter/search row + card list + row-click + no-target-Hz all unchanged.
- **Dashboard** and **Exercise runtime**: left as-is (already use width via their grid layouts; chart untouched).
- **Shell / sidebar / right info panel / status bar**: unchanged.

## Behavior
No bindings, commands, services, or data paths changed — these are pure container/alignment/width/`ItemsPanel`
edits. Every previously-disabled/deferred control stays disabled/deferred; SmartCoach/Progression values stay "—";
Settings persists nothing; Exercise Guide filtering is unchanged; the Dashboard chart model is untouched.

## Smoke
New `--visual-layout-polish-smoke` (27th): **source-inspection** that Settings uses a `WrapPanel` + centered column,
the scaffolds + Exercise Guide are centered, and the Guide kept filter/search with no target-Hz (skipped → pass
from the published DLL, where the source tree isn't shipped); plus **VM-level** guarantees that always run —
Settings inert (`AllControlsDeferred`, not IDisposable), SmartCoach/Progression deferred + disabled + synthetic
"—" + no service deps, Exercise Guide filter/search intact (search narrows to 0 on a no-match), Dashboard chart
model present, shell nav 9/6 intact. Behavioral layout (actual pixel widths) remains a **manual** visual check.

## Manual verification checklist
Settings uses width better (centered, up to 2 columns); SmartCoach/Progression/Analysis/Reports/Diagnostics
centered + intact; Exercise Guide filter/search + list intact; Dashboard chart intact; dark baseline + sidebar +
right info panel intact; no deferred feature appears enabled.

## Guardrails (verified)
`Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` + `FemVoice.Audio.Abstractions`;
leak guard clean; no persistence/DB/analytics; no settings/theme/language/audio/privacy behaviour; no
SmartCoach/Progression engine behaviour; no clinical/domain or WPF change; no runtime platform implementation.

> The repository is private/proprietary; no open-source license assumed.
