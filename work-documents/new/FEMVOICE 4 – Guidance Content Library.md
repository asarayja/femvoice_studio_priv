# FEMVOICE â€“ Guidance Content Library.md

## FormÃ¥l

Dette dokumentet fungerer som den kliniske innholdsbanken for Guidance-systemet.

Alle Ã¸velser skal kunne presentere:

* Clinical Purpose
* Physical Focus
* Common Mistakes
* Safety Information

uten hardkodet UI-logikk.

---

## Struktur

Hver Ã¸velse skal ha:

### Clinical Purpose

Hvorfor Ã¸velsen eksisterer.

### Physical Focus

Hva brukeren skal kjenne etter.

### Common Mistakes

Vanlige feil som reduserer effekt.

### Safety Information

Hvordan unngÃ¥ overbelastning.

---

## Eksempelstruktur

### Resonance Humming

Clinical Purpose:
Utvikle fremre resonans og redusere bakre halsdominans.

Physical Focus:
Vibrasjon foran i ansiktet.

Common Mistakes:
Presse stemmen oppover i stedet for Ã¥ flytte resonans.

Safety Information:
UnngÃ¥ spenning i hals og kjeve.

---

## Lokalisering

Alle tekster lagres som RESX-nÃ¸kler.

Eksempel:

GuidancePurpose_ResonanceHumming

GuidanceFocus_ResonanceHumming

GuidanceMistakes_ResonanceHumming

GuidanceSafety_ResonanceHumming

---

## Fremtid

ProgressionOrchestrator og Health Intelligence kan senere velge alternative Guidance-varianter basert pÃ¥ brukerens utvikling.

## Status: Ferdig

Implementert som RESX-basert guidance content library for aktive ExerciseTargetProfile-profiler. Profilene peker nå på GuidancePurpose_*, GuidanceFocus_*, GuidanceMistakes_* og GuidanceSafety_* nøkler. Norsk base ligger i Strings.resx, og språkspesifikke RESX-filer har engelsk fallback for samme nøkkelsett.

