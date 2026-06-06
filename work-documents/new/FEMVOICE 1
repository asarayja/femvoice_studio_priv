# FEMVOICE – Guidance System Architecture.md

Status 2026-06-01:

✅ Guidance-systemet er implementert som datadrevet profil → ViewModel → XAML-flyt.
✅ `ExerciseTargetProfile` inneholder guidance-nøkler for clinical purpose, physical focus, common mistakes og safety.
✅ `ExerciseDetailViewModel.RebuildGuidanceItems()` bygger `GuidanceItems` uten per-øvelse UI-logikk.
✅ `ExerciseGuidancePanel` binder til `GuidanceItems` og lokaliserer heading/body via RESX.
✅ Redundant code-behind `ItemsSource`-setting er fjernet; panelet bruker bindingen.
✅ Ingen åpne punkter i dette dokumentet per 2026-06-01.

## Formål

Definerer hele Guidance-systemet i FemVoice Studio.

Guidance er den pedagogiske delen av øvelsesopplevelsen og skal gi klinisk korrekt veiledning før og under trening.

---

## Arkitektur

ExerciseTargetProfile
↓
ExerciseProfileFactory
↓
ExerciseDetailViewModel.RebuildGuidanceItems()
↓
GuidanceItems
↓
ExerciseGuidancePanel

---

## Designprinsipper

* Fullt datadrevet
* Ingen hardkodet tekst
* Full RESX-lokalisering
* MVVM-separasjon
* Ingen kode-behind logikk

---

## Guidance-seksjoner

### Clinical Purpose

Hvorfor øvelsen eksisterer.

### Physical Focus

Hva brukeren skal kjenne etter fysisk.

### Common Mistakes

Vanlige feil som reduserer effekt.

### Safety Information

Hvordan unngå overbelastning.

---

## Lokaliseringsnøkler

Guidance_PanelTitle

Guidance_ClinicalPurpose
Guidance_PhysicalFocus
Guidance_CommonMistakes
Guidance_SafetyInfo

---

## Fremtid

Adaptive guidance basert på:

* ProgressionOrchestrator
* VocalHealthSupervisor
* SessionAnalyticsStore

uten å endre grunnarkitekturen.
