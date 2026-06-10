# RC-0 placeholder-/malfiler (IKKE ekte runtime-evidence)

Disse fire filene lå tidligere i repo-rota og ble feilaktig tolket som RC-0
runtime-evidence. De har ALDRI inneholdt data fra en ekte økt:

- `RC0_EVIDENCE.json` og `RC0_AUDIO_PIPELINE_DIAGNOSTIC_REPORT.md` er
  håndskrevne placeholdere fra commit `6bf30df` (2026-06-10). Verdiene
  `SessionId: null`, `CaptureStatus: "NOT_RUN"` osv. kan ikke produseres av
  `Rc0EvidenceExporter` (SessionId er non-nullable int; ingen kodevei skriver
  «NOT_RUN»).
- `RC0_VERIFICATION_REPORT.md` og `RC0_VERIFICATION_EVIDENCE.json` ble
  regenerert av testen `Rc0SystemVerificationReportTests` ved hver
  `dotnet test`-kjøring, fra en hardkodet `CreateBlockedBaseline()` med fast
  tidsstempel — alltid med resultat BLOCKED. Testen skriver nå til en
  temp-katalog i stedet.

Ekte RC-0 runtime-evidence skrives av appen (fra commit med RC-0
evidence-pipelinen) til:

- `%LOCALAPPDATA%\FemVoiceStudio\RC0_Evidence\` — startup-sentinel og
  evidence-pakker per økt (primær, ikke OneDrive-omdirigert)
- `%LOCALAPPDATA%\FemVoiceStudio\RC0_Runtime\` — løpende runtime-logg per
  app-start
- `Documents\FemVoiceStudio\RC0_Evidence\` — best-effort synlighetskopi

Se `RC0_EVIDENCE_PIPELINE_ROOT_CAUSE_REPORT.md` i repo-rota for hele
rotårsaksanalysen.
