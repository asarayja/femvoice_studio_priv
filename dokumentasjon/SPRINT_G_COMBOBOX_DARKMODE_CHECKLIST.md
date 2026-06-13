# Sprint G Addendum — ComboBox/DropDown dark-mode readability — Manual Visual Checklist

**Status of the visual fix:** Needs Manual Visual Confirmation (the WPF app cannot be run on the Linux dev box; code is compile-verified and statically guarded by tests, but pixels were not observed here).

Run the app on Windows, switch to **Dark mode** (Settings → theme), and verify each row. Then repeat the same rows in **Light mode**. A row passes only if the **item text stays readable** (no light cyan/blue rectangle hiding the text).

## Where to test (ComboBoxes across the project)
- First-time setup window: Language, Voice style, Training frequency (this window had the local override that caused the bug — verify it specifically).
- Settings: Language, Voice goal focus, Voice goal style, Training frequency.
- Report export: Report type, Format.
- Manual override: Override kind, Profile type, Style goal.
- Case review: Review type.

## Dark mode
- [ ] ComboBox closed — selected text readable on the field.
- [ ] Open dropdown — popup background dark, all item text readable.
- [ ] Hover an item with the mouse — text stays readable (dark hover surface + light text), no light cyan/blue box over the text.
- [ ] Selected item — readable (accent surface + light text).
- [ ] Selected item + hover — readable (distinct accent, still light text).
- [ ] Keyboard arrow navigation — the highlighted item is visibly highlighted AND its text is readable.
- [ ] ComboBox focused via Tab (no mouse) — focus border visible.
- [ ] Disabled ComboBox — visibly dimmed, not broken.
- [ ] Disabled item (if any) — dimmed but legible.

## Light mode (same rows must still pass)
- [ ] Closed / open / hover / selected / selected+hover / keyboard highlight / focus / disabled — all readable.

## Fail condition
If hovering produces a light cyan/blue rectangle where the item text cannot be read, the fix is incomplete — report the exact window + ComboBox.

## Buttons (Sprint G Addendum, part 2)

### Where to test (named problem pages + global)
- **Innstillinger / Settings**: Open microphone calibration, Create backup, Restore backup, Clear database, Close.
- **Manuelle justeringer / Manual adjustments**: the **Apply** button (previously missing theme colors).
- Spot-check other pages' buttons (MainWindow nav/action buttons, Exercise, Reports, Case review) — they now use the global implicit Button style.

### Dark mode button checks
- [ ] Settings buttons: normal / hover / pressed / focus / disabled — text readable, no light system overlay on hover.
- [ ] Manual adjustments **Apply** — themed (accent), readable in all states (was un-themed/light before).
- [ ] Primary (accent) buttons — hover/pressed darker-accent, text stays readable.
- [ ] Secondary buttons — hover/pressed subtle, text readable.
- [ ] Danger/warning buttons (Clear database, Close) — visible hover (error-hover), text readable.
- [ ] Ghost/transparent/icon-only buttons — hover shows a subtle overlay, text/icon never hidden.
- [ ] Focused button (Tab) — focus border visible.
- [ ] Disabled button — dimmed (≈50% opacity) + disabled text, clearly inactive.

### Light mode button checks (regression)
- [ ] Same button rows in light mode — overlay darkens (not lightens), text readable, no dark-mode brush leak.

## What changed (so a reviewer knows where to look)
- `Themes/DarkTheme.xaml` + `LightTheme.xaml`: added `ButtonHoverOverlayBrush`/`ButtonPressedOverlayBrush` and a **global implicit `<Style TargetType="Button">`** (flat themed template; honors inline Background/Foreground; subtle hover/pressed overlay UNDER the content; keyboard-focus border; disabled dim). `DangerButtonStyle` hover now uses `ErrorHoverBrush` (was a no-op Error→Error).
- `Views/SettingsWindow.xaml`: the 5 buttons now use keyed styles (`PrimaryButtonStyle`/`DangerButtonStyle`/`SecondaryButtonStyle`) instead of inline backgrounds on the default/local template.
- `Views/ManualOverrideWindow.xaml`: the **Apply** button now uses `PrimaryButtonStyle` (was bare/un-themed).
- `FemVoiceStudio/Themes/DarkTheme.xaml` and `LightTheme.xaml`: `StandardComboBoxItemStyle` now has explicit, readable triggers for IsMouseOver, IsHighlighted (keyboard), IsSelected, IsSelected+IsMouseOver/IsHighlighted (MultiTrigger), and IsEnabled=False; the ContentPresenter follows the templated Foreground. DarkTheme ComboBox also shows the focus border on IsKeyboardFocusWithin.
- `FemVoiceStudio/Views/FirstTimeSetupWindow.xaml`: removed the local un-templated ComboBox/ComboBoxItem styles that shadowed the themed styles and forced WPF's default light system highlight.
