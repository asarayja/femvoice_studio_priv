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

## What changed (so a reviewer knows where to look)
- `FemVoiceStudio/Themes/DarkTheme.xaml` and `LightTheme.xaml`: `StandardComboBoxItemStyle` now has explicit, readable triggers for IsMouseOver, IsHighlighted (keyboard), IsSelected, IsSelected+IsMouseOver/IsHighlighted (MultiTrigger), and IsEnabled=False; the ContentPresenter follows the templated Foreground. DarkTheme ComboBox also shows the focus border on IsKeyboardFocusWithin.
- `FemVoiceStudio/Views/FirstTimeSetupWindow.xaml`: removed the local un-templated ComboBox/ComboBoxItem styles that shadowed the themed styles and forced WPF's default light system highlight.
