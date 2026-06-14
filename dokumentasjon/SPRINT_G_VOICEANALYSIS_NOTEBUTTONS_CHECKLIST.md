# Sprint G Addendum — Voice Analysis note-selector — Manual Visual Checklist

**Status of the visual fix:** Needs Manual Visual Confirmation (the WPF app cannot run on the Linux dev box; code is compile-verified and statically guarded, but pixels/contrast were not observed here).

Open **Voice Analysis** (the Analyzer window), section **"Select target frequency with notes"** (Octave 2/3/4 × C…B). Verify in **Dark mode**, then repeat in **Light mode**. A row passes only if the note text stays readable in every state and the selected target note is clearly distinct.

## Dark mode
- [ ] All Octave 2 note buttons: text readable (no red/pink-with-white low contrast).
- [ ] All Octave 3 note buttons: text readable.
- [ ] All Octave 4 note buttons: text readable.
- [ ] Hover a note — visibly brighter surface, text still readable.
- [ ] Click/select a note — it fills with the app accent (purple) and stays clearly distinct from the others.
- [ ] Hover the selected note — distinct accent, text still readable.
- [ ] Selecting another note deselects the previous one (single selection).
- [ ] Keyboard focus (Tab / arrows) — focus border visible; arrow keys move selection.
- [ ] "Octave N" labels are readable (not black-on-dark).
- [ ] Disabled state (if the selector is ever disabled) — dimmed but legible.

## Light mode (regression)
- [ ] Same rows readable; selected note fills with the light-theme accent (blue); no dark-mode brush leak.

## Behavior regression (must be unchanged)
- [ ] Selecting a note still sets the target frequency (the "NNN Hz" readout updates to the note's frequency).
- [ ] The target line on the analyzer moves to the selected note as before.
- [ ] Note→frequency values are unchanged (e.g. A4 ≈ 440 Hz); pitch analysis behaves as before.

## What changed (for the reviewer)
- `Themes/DarkTheme.xaml` + `LightTheme.xaml`: new shared `NoteRadioButtonStyle` (pill RadioButton; theme brushes only; states: normal / hover / pressed / checked(selected) / checked+hover / keyboard-focus / disabled). Accent fill for the selected note.
- `Views/AnalyzerWindow.xaml.cs`: note buttons are now themed `RadioButton`s (GroupName single-select, IsChecked = current target) using `NoteRadioButtonStyle`; the "Octave N" label is themed; the per-frequency red/pink `GetFrequencyColor` palette was removed. `NoteToFrequency` and the selection→target-frequency behavior are unchanged.
