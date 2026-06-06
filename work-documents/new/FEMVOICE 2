# FEMVOICE – VocalHealthSupervisor.md

Status 2026-06-01:

✅ `VocalHealthSupervisor` er implementert som eventdrevet health intelligence service.
✅ `HealthSafetyState` har `Normal`, `Caution`, `Restrict` og `Lock`.
✅ Strain vurderes akutt fra safety lock, hold-kollaps, comfort breaches og micro-stabilitet.
✅ Fatigue vurderes gradvis fra meso-resonansdrift og stabilitetsfall.
✅ Pause, hydration, restrict og lock publiseres som events og går videre gjennom feedback/analytics-flyt.
✅ Recovery krever stabilt vindu før state de-eskaleres; ingen instant reset.
✅ Tester dekker spike-filter, noise-filter, acute strain, slow fatigue, hydration vs pause, restrict/lock, recovery og events.
✅ Ingen åpne punkter i dette dokumentet per 2026-06-01.

## Formål

Health Intelligence Layer for FemVoice.

Beskytter brukeren mot strain, fatigue og overbelastning.

---

## HealthSafetyState

Normal

Caution

Restrict

Lock

---

## Policy-systemer

### StrainDetectionPolicy

Akutt belastning.

Input:

* Mikrotrend
* Hold-kollaps
* Variansøkning

---

### FatigueDetectionPolicy

Gradvis akkumulering.

Input:

* Mesotrend
* Resonansdrift
* Stabilitetsfall

---

### PausePolicy

Uttrykk for:

HealthSafetyState = Caution

---

### RecoveryPolicy

Gradvis tilbakeføring.

Ingen instant reset.

---

## Integrasjon

Health
↓
SmartCoach
↓
Normal Coaching

---

## Arkitekturregler

Ingen polling

Ingen timers

Kun events

Trend-basert

Policy-basert

UX-uavhengig
