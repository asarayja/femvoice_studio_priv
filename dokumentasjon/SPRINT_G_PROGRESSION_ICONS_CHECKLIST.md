# Sprint G Addendum — Progresjon-side icons — Manual Visual Checklist

**Status of the visual fix:** Needs Manual Visual Confirmation (the WPF app cannot run on the Linux dev box; code is compile-verified and statically guarded, but pixels/contrast were not observed here).

Open **Progresjon** in **Dark mode**, verify each row, then repeat in **Light mode**.

## Dark mode
- [ ] Green **Nybegynner** level-badge circle shows a visible icon (white star glyph), not an empty/invisible circle.
- [ ] The badge icon has clear contrast against the coloured circle.
- [ ] Parameter rows (Resonans / Pitch / Intonasjon / Stemmehelse) show their right-side trend arrows — visible, not black-on-dark.
- [ ] **Dagens fokusområde** circle shows a visible (shield) icon, white on the accent circle.
- [ ] **Raskeste forbedring** green circle shows a visible (up) icon, white on green.
- [ ] No icon is black-on-dark.

## Light mode
- [ ] Same icons all visible and readable.
- [ ] Badge / focus / improvement circle icons are readable on their coloured circles (on-accent white).
- [ ] Trend arrows readable (no white-on-light, no disappearing icon).
- [ ] No dark-mode-only icon colour leaked in.

## Regression (must be unchanged)
- [ ] Progress / level values and the score ring number are unchanged.
- [ ] Level name/description text unchanged (only the badge *icon* changed, not the level).
- [ ] **Front page** icons still look correct.
- [ ] **Exercise guide** icons still look correct.

## What changed (for the reviewer)
- `Views/ProgressionDashboard.xaml`: the level-badge, today's-focus and quickest-improvement icons are now themed **Segoe MDL2 glyphs** (star `E735`, shield `EA18`, up `E74A`) with `Foreground="{DynamicResource TextOnAccentBrush}"` (white on the coloured circles) instead of colour emojis (`🟢/🎯/📈`) that ignored Foreground and (for the green level circle) matched the circle colour → invisible. The 4 parameter trend arrows got `FontFamily="Segoe MDL2 Assets"` + `Foreground="{DynamicResource TextSecondaryBrush}"`.
- `Converters/Converters.cs`: `DirectionToArrowConverter` now returns Segoe MDL2 glyphs (up/down/forward/check) instead of colour-emoji arrows, so the bound Foreground applies.
- No change to progress/level/score calculations; the level colour circle (`LevelToColor`) and all values are untouched. `GetLevelEmoji` and its test are unchanged.
