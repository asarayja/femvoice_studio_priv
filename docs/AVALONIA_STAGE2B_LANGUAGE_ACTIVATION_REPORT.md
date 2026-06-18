# Avalonia Stage 2B — Runtime Language Activation — Slice Report

Date: 2026-06-17 · Branch: `avalonia-stage2b-language-activation-slice` (off `main` @ `aa35246`).

> **Stage 2B only — STARTUP-ONLY language activation (truthful).** Activates the saved **language** preference,
> **Avalonia-only**, applied at **app startup** within the existing Avalonia-owned localization/scaffold mechanism.
> Saving persists + applies the language to the Avalonia-local resolver, but **already-rendered text refreshes on
> restart, not live** (the UI copy says so — see "Manual-test fix" below). It does NOT add native translations for
> the backlog, does NOT activate reduce-motion, and keeps Stage-2A theme activation (live) intact. No WPF
> `LocalizationService`/`SetLanguage`/`LocExtension`/`LocConverter`, no DB/`UserSettings`/SQLite/`IDatabaseService`,
> no WPF `ThemeManager`, no Core/WPF/clinical/audio behaviour change, **no global thread-culture change**.

## Manual-test fix (PR #32 review)
Manual testing reported "changing language doesn't work." **Root cause:** Avalonia VMs resolve `Localized.Get(...)`
once at construction and bind to get-only properties with no change-notification, so changing the language on Save
did **not** refresh already-rendered views (issue **D**); meanwhile the Save status text overpromised by saying
language was "aktivert" live (issue **E**). Startup activation itself works (VMs built after `ApplyFromStore`
resolve Core-backed keys in the saved language). Of the 146 Avalonia `Localized.Get` keys, **31 are Core-resource-
backed** (these follow the language — including most Settings-page headers) and **115 are Avalonia-only scaffold
keys** (these stay Norwegian by design — no native parity). A true live refresh would require per-VM `INotify`
plumbing across ~all VMs (broad/risky), so per the smallest-safe-fix rule this slice is **startup-only with
truthful copy**: the Save status now says "Tema brukes med en gang. Språk brukes ved omstart (kun oversatt tekst
følger språket; resten vises på norsk)." and no longer claims live language switching.

## How language activation works (Avalonia-LOCAL)
The Avalonia resolver `Localized` now owns an **Avalonia-LOCAL `CurrentCulture`** and an **Avalonia-owned
`ResourceManager`** over the same shared string resources (`FemVoiceStudio.Resources.Strings` in the Core
assembly). `Localized.Get(key, fallback)` resolves: (1) the Avalonia scaffold overlay; (2) the shared resources
for `Localized.CurrentCulture` via the Avalonia-owned `ResourceManager.GetString(key, culture)`; (3) the provided
fallback. This **never** calls Core `SetLanguage`, **never** changes the global thread culture, and **never**
mutates the shared Core `LocalizationService` state — so WPF (separate process) and the portable tests are
untouched. `CurrentCulture` defaults to the Core service culture, so startup resolution is identical to before.

## What was added / changed
- **`Localization/LanguageActivation.cs`** (new) — the single language-activation point. `Apply(code)` sets
  `Localized.CurrentCulture` (unknown/invalid → `nb-NO`). `ApplyFromStore(store)` applies the saved language **only
  if a valid saved preference exists** (the model already normalizes unknown/unsupported languages to `nb-NO`),
  else leaves the current default culture.
- **`Localization/Localized.cs`** — rewritten to resolve via the Avalonia-owned `ResourceManager` for the
  Avalonia-local `CurrentCulture` (see above). Backward-compatible default; richer doc of the boundary.
- **`App.axaml.cs`** — `OnFrameworkInitializationCompleted` now calls `LanguageActivation.ApplyFromStore()`
  (alongside the Stage-2A `ThemeActivation.ApplyFromStore()`) **before** the window is created.
- **`ViewModels/UiPreferencesViewModel.cs`** — `Save` persists, then live-applies theme (Stage 2A) **and** the
  Avalonia-local language (Stage 2B). Reduce-motion remains persisted-only. Status text updated.

## Fallback / no-parity behaviour
- A selected culture without a translated value falls back through its parent chain to the **Norwegian neutral**
  resource (the resources' neutral fallback). Avalonia-only scaffold keys (absent from the shared resources) keep
  their **Norwegian fallback** — so **no native parity is claimed** for the 105+ backlog keys.
- Core-resource-backed Avalonia text (e.g. `Settings_Title`) follows the selected language: nb=`Innstillinger`,
  en=`Settings`, sv-SE=`Inställningar`.

## Startup-only; NO live refresh (truthful)
**Language is applied at app STARTUP only.** `Save` **persists** the language but deliberately does **not** switch
the running resolver — so there is no inconsistent half-live state and **no live-refresh claim**. The saved
language takes effect on the **next launch/restart** (`App.OnFrameworkInitializationCompleted` → `ApplyFromStore`).
In-session live refresh is **deferred** (a full live re-render of already-rendered views would need broad
VM/`INotify` refactoring — out of scope). The Settings copy and Save status state this truthfully ("Tema brukes med
en gang. Språk brukes ved omstart …") and never claim live language switching. Theme activation (Stage 2A) remains
**live** on Save and at startup.

## Coverage / fallback (sparse — native parity deferred)
Only **31 of 146** Avalonia `Localized.Get` keys are Core-resource-backed (these follow the saved language — e.g.
the Settings page title and the Theme/Language section labels). The other **115** are Avalonia-only **scaffold**
keys that exist only in Norwegian, and several language resx files (notably **sv-SE**) are **sparse** and fall back
through their parent chain to the **Norwegian neutral** resource. Consequently the high-visibility UI (nav rail,
Dashboard, status, most labels) stays Norwegian for every language today. **No full language parity is claimed.**
Populating the high-visibility scaffold strings + sparse resx is the **next slice** (native translation), explicitly
**deferred** here per the slice decision.

## Smoke coverage
- **`--settings-language-activation-smoke`** (33rd; pure resolver/culture logic, no Avalonia platform → also runs
  from the published DLL): saved sv-SE/en-US/nb-NO apply (Core-backed `Settings_Title`/`Common_Save` resolve in the
  selected language); Avalonia-only scaffold key falls back; **startup read** via `ApplyFromStore`;
  **missing/corrupt** file → no apply (sentinel preserved); **unknown** language (`zz-ZZ`) → normalized to `nb-NO`;
  **`threadCultureUntouched=True`** (global thread culture never changed — Avalonia-local only); **and (manual-fix
  additions)** `capturedUnchanged=True` (a string resolved before a language change does not retroactively change —
  models "live refresh needs restart"); **`saveDoesNotSwitchLive=True`** (Save persists the language but does NOT
  switch the running resolver — enforces startup-only / no live refresh); and **`truthfulStatus=True`** (the Save
  status says "omstart"/restart and does NOT claim live language activation — this assertion fails on the old
  overpromising copy, catching the manual issue).
- **Updated `--settings-persistence-readiness-smoke`**: now also scans `LanguageActivation.cs` + `Localized.cs`;
  Avalonia-local language activation (`Localized.CurrentCulture`) and theme activation are allowed, while GLOBAL
  thread-culture change / Core culture mutation / Core `SetLanguage` and reduce-motion activation remain forbidden.
- `--settings-theme-activation-smoke`, `--theme-loc-smoke`, `--localization-text-polish-smoke`,
  `--avalonia-localization-coverage-smoke`, `--settings-smoke`, `--settings-visual-parity-smoke` remain green.

## Guardrails (verified)
Build 0/0; **33/33 smokes**; `Tmds.DBus.Protocol` 0.21.3; `FemVoice.Avalonia` references only `FemVoice.Core` +
`FemVoice.Audio.Abstractions`; leak guard clean (no `IDatabaseService`/WPF `ThemeManager`/`LocalizationService`/
`LocExtension`/`LocConverter`/`MicrophoneCalibration`/engine refs); **Core `Strings.*.resx` untouched (git-clean)**;
portable **1570/1580** (10 known localization-data baseline failures; unchanged); packaging 19/19 published + `.deb`
+ macOS. Diff scope: only `FemVoice.Avalonia/` + `docs/`.

## Explicitly NOT done (boundaries respected)
No native translation of the backlog; no reduce-motion activation; no WPF `LocalizationService`/`SetLanguage`/
`LocExtension`/`LocConverter`; no WPF settings files; no DB/`UserSettings`/SQLite/`IDatabaseService`; no WPF
`ThemeManager`; no global thread-culture change; no audio/mic, backup/restore, privacy export/delete, reports/export,
clinical/domain, SmartCoach/Progression, Core, or WPF change. Stage-2A theme activation remains intact.

> The repository is private/proprietary; no open-source license assumed.
