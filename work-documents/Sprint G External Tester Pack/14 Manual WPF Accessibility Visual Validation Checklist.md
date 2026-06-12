# Manual WPF Accessibility & Visual Validation Checklist

Status: klar for Windows-basert manuell validering  
Scope: accessibility, visual robustness og release-readiness  
Ikke scope: audio, pitch, resonance, scoring, SmartCoach, Voice Health, Sprint E/F runtime-logikk

## Viktig status

Denne sjekklisten er laget fra statisk XAML-review og eksisterende Sprint G-regresjon. Full manuell WPF-validering må utføres i Windows UI med tastatur, mus, skjermskalering og begge temaer.

## 1. Keyboard Navigation

Test med kun tastatur:

- [ ] Hovedmeny: Tab går gjennom vanskelighetsvalg, hovednavigasjon, profesjonelle verktøy og Start/Stopp økt i logisk rekkefølge.
- [ ] Øvelsesliste: Tab når søk/filter, øvelseskort og startknapper.
- [ ] Øvelse start/stopp: Start, Stopp og subjektiv rapport kan brukes uten mus.
- [ ] Rapportvindu: rapporttype, format, generer og eksport kan brukes med Tab/Enter/Space.
- [ ] Backup/restore: lag backup, restore, filvelger og confirmation kan brukes med tastatur.
- [ ] Support package export: export action kan startes med tastatur når UI finnes.
- [ ] Settings/onboarding: språk, tema, mikrofonkalibrering, voice goal og accessibility-valg kan brukes med tastatur.
- [ ] Dialoger og confirmation prompts: Enter/Escape fungerer forventet, og fokus starter på trygg knapp der relevant.

## 2. Focus Visibility

- [ ] Tab focus er synlig i light mode.
- [ ] Tab focus er synlig i dark mode.
- [ ] Knapper har tydelig focus state, ikke bare hover state.
- [ ] ComboBox/TextBox/DatePicker har tydelig focus state.
- [ ] Destructive actions, som database reset og restore overwrite, har tydelig confirmation.
- [ ] Confirmation-dialoger viser hvilken handling som utføres før brukeren bekrefter.

## 3. Screen Reader Basics

- [ ] Viktige knapper har tydelig tekst.
- [ ] Icon-only buttons har tooltip og/eller AutomationProperties.Name.
- [ ] Statusmeldinger gir mening uten farge/ikon.
- [ ] Feilmeldinger er trygge og forståelige uten stack trace.
- [ ] Start/Stopp økt kan forstås uten visuell kontekst.
- [ ] Report export status leses som meningsfull tekst.
- [ ] Backup/restore resultater er tydelige uten fargekoder.

## 4. Contrast And Theme

Test både light og dark mode:

- [ ] Vanlig tekst har lesbar kontrast.
- [ ] Sekundær tekst er lesbar.
- [ ] Disabled buttons er tydelige, men ikke for svake.
- [ ] Warning/error/success states er forståelige uten bare farge.
- [ ] Report/export/status labels er lesbare.
- [ ] Voice safety warnings er lesbare og fremhevet nok.
- [ ] Progress bars har tekstlig verdi eller tydelig tilhørende label.

## 5. Font Scaling And Wrapping

Test 100%, 125%, 150% og liten vindusstørrelse der vinduet tillater resize:

- [ ] Norsk lang tekst wrapper riktig.
- [ ] Kritisk safety-tekst klippes ikke.
- [ ] Rapportlabels klippes ikke.
- [ ] Dialoger tåler lengre norsk tekst.
- [ ] Settings privacy/disclaimer tekst klippes ikke.
- [ ] Exercise guidance/status tekst klippes ikke.
- [ ] SmartCoach-kort klipper ikke kritisk anbefaling.
- [ ] Små vinduer viser scroll når innhold ikke får plass.

## 6. Empty And Degraded States

Valider:

- [ ] Ingen session data: dashboard/analysis viser trygg empty state.
- [ ] Ingen analytics data: rapport/analysis krasjer ikke.
- [ ] Ingen reports: rapportvindu håndterer tomt grunnlag.
- [ ] Missing microphone: trygg melding, ingen raw exception.
- [ ] Corrupt settings recovered: trygg warning/defaults.
- [ ] Backup restore failure: trygg melding, eksisterende data bevart.
- [ ] Support package missing files: package lages med tilgjengelige filer og forståelig manifest/status.
- [ ] Research export no data: trygg empty state.

## 7. Report UI

- [ ] Reports open.
- [ ] Reports contain expected data.
- [ ] PDF åpner i vanlig PDF-leser.
- [ ] CSV åpner.
- [ ] JSON parser.
- [ ] Ingen mojibake.
- [ ] Ingen rå enum labels i norsk mode.
- [ ] Timeline labels er lokalisert:
  - [ ] Siste 180 dager
  - [ ] Siste 90 dager
  - [ ] Siste 30 dager
  - [ ] Siste 7 dager
- [ ] Outcome PDF viser ikke `Restitusjonskostn/ad`.
- [ ] Outcome PDF bruker `Rest.kostnad` eller lesbar full label.

## 8. Backup/Restore Safety

- [ ] Restore overwrite confirmation vises.
- [ ] Destructive action er ikke skjult i liten tekst.
- [ ] Restore kan avbrytes.
- [ ] Restore over eksisterende data krever eksplisitt bekreftelse.
- [ ] Restore-feil bruker trygg melding.
- [ ] Ingen rå exception eller stack trace vises.
- [ ] Backupfilnavn inneholder ikke unødvendig PII.

## 9. Support Package Privacy

- [ ] Export action er bruker-triggered.
- [ ] Sensitive free-text er ekskludert som standard.
- [ ] Included/excluded files er forståelige i manifest/status.
- [ ] Settings summary ekskluderer secrets/tokens.
- [ ] Professional notes inkluderes ikke uten eksplisitt valg.
- [ ] Bruker kan finne export path.

## Static XAML Review Findings

Dette er funn fra statisk review. De er ikke klassifisert som runtime blockers uten manuell bekreftelse.

| Area | Finding | Risk | Recommended manual check |
| ---- | ------- | ---- | ------------------------ |
| CalendarWindow | Forrige/neste måned-knapper bruker icon-only MDL2 glyphs. XAML-søk viste ikke AutomationProperties/ToolTip på disse knappene. | Screen reader kan lese utydelig knapp. | Bekreft med skjermleser. Legg til AutomationProperties.Name/ToolTip hvis knappene ikke leses som "Forrige måned" og "Neste måned". |
| SettingsWindow | `ResizeMode="NoResize"` med `Height="500" Width="450"`. | Ved 150% scaling kan lang norsk tekst kreve mye scrolling, og fast vindu kan føles trangt. | Test 125/150% scaling og liten skjerm. Bekreft at all kritisk tekst kan nås via scroll. |
| ReportExportWindow | `ResizeMode="NoResize"` med `Height="380" Width="480"`. | Lengre norsk status/path kan bli trang selv med TextWrapping. | Eksporter til lang path og sjekk at status er lesbar. |
| FirstTimeSetupWindow | Statisk søk fant hardkodede gradientfarger i preview. | Theme-kontrast kan avvike i dark/light mode. | Sjekk onboarding i begge temaer. |
| AnalyzerWindow | Flere hardkodede visual colors i spectrogram/status bars. | Theme/contrast kan være svak i ett tema. | Sjekk dark/light kontrast og om farger alene bærer betydning. |
| AnalysisWindow | Statisk søk fant hardkodede bar colors. | Kan avvike fra sentralt theme og gi kontrastvariasjon. | Sjekk score bars i begge temaer. |
| SmartCoachDashboardView | `TextTrimming="CharacterEllipsis"` og `MaxHeight="60"` på anbefalingstekst. | Lang norsk anbefaling kan klippes. | Test lang norsk SmartCoach-melding ved 125/150% scaling. |
| Multiple views | Begrenset bruk av `AutomationProperties` funnet i statisk søk. | Screen reader-navn kan være avhengig av synlig Content. | Prioriter icon-only og statuskontroller for screen reader test. |

## Manual Validation Result Template

Fyll ut etter Windows-test:

- Tester:
- App version:
- Windows version:
- Display scaling:
- Theme:
- Microphone:
- Keyboard navigation: PASS / FAIL / NOT TESTED
- Focus visibility: PASS / FAIL / NOT TESTED
- Screen reader basics: PASS / FAIL / NOT TESTED
- Contrast/theme: PASS / FAIL / NOT TESTED
- Font scaling/wrapping: PASS / FAIL / NOT TESTED
- Empty/degraded states: PASS / FAIL / NOT TESTED
- Report UI: PASS / FAIL / NOT TESTED
- Backup/restore safety: PASS / FAIL / NOT TESTED
- Support package privacy: PASS / FAIL / NOT TESTED
- Blocking issues:
- Non-blocking issues:
- Screenshots/support package attached:

