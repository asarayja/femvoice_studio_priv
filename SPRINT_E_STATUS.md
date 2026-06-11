# Sprint E — Professional Edition & Research Readiness — Status

**Date:** 2026-06-11
**Method:** 7-agent read-only audit of the existing Sprint E surface (all 13 agent areas) → coordinator synthesis → a minimal, additive, boundary-safe code slice for the two highest-impact verified gaps.
**Environment:** Linux/headless. The WPF app and `dotnet test` CANNOT run here. C# changes were **compile-verified** with `dotnet build -p:EnableWindowsTargeting=true` (both projects, 0 errors). **Build/test were NOT run on Windows and UI/runtime is NOT validated here — the user must run validation on Windows.**

> **Headline:** Sprint E is NOT greenfield — it is **already ~85% implemented and tested** in the repo (stores, engines, assemblers, ViewModels, XAML windows, models, ~22 test files, all DI-registered). The correct action was to audit and fill verified gaps, **not** rebuild. No duplicate engines/analytics/report/coaching systems were created. No frozen RC-0/audio/clinical/SmartCoach/guard boundary was touched.

## Professional Edition Status: **Mostly Implemented**
- **Implemented:** Clinician dashboard (serves Agent 1 + 3), Coach dashboard, 4 report types (Clinical/Coach/Outcome/Timeline) via ReportAssembler, PDF/CSV/JSON export via ExportWriter, ReportVerificationTracker (marks status on real attempts), Manual Override with proven safety precedence, Clinical Notes store, Audit Trail store, Case Review workflow + assembler, all DI-registered and opened from MainWindow.
- **Missing/Partial:** standalone export "packages" selector (Agent 11 names map to sections inside the existing 4 reports, not separate types); a few dashboard localization leaks; ReviewType.Coach/Clinical + ReviewStatus.NeedsFollowUp/Archived are reduced sets.

## Research Edition Status: **Partial**
- **Implemented (core):** ResearchAnonymizer (PII stripping, generated participant IDs), ResearchAggregator (group-only means/shares with k-anonymity MinimumCohortSize=5), ResearchDataset model, ParticipantTokenProvider — all unit-tested (ResearchAnonymizerTests, ResearchAggregatorTests, ResearchNoPiiTests).
- **Missing:** the anonymizer/aggregator have **no production caller** (only PilotReadinessChecker probe + tests); ExportWriter has **no ResearchDataset CSV/PDF branch** (only JSON would serialize); no UI entry; the `Research_Export` label is orphaned; the "completed learning paths" and "adherence patterns" group aggregates do not exist; ResearchAggregator's volume caveat is a baked English string (not localized).

## Outcome Tracking Status: **Mostly Implemented**
- **Implemented:** OutcomeProfile has GoalProgress, RecoveryProgress, ExerciseEffectiveness, LongTermDevelopment with explainable values + the statuses Improving/Stable/Declining/InsufficientData/NotStarted; OutcomeProfileBuilder assembles from existing stored data without changing scoring (OutcomeProfileBuilderTests, OutcomeProfileGuardTests).
- **Missing:** `ComfortProgress` and `Retention` are absent as top-level fields. Comfort is covered indirectly (Recovery ComfortDeclining + VoiceDimension.Comfort). **Retention has no representation and would require NEW scoring logic — that crosses the frozen clinical-scoring boundary, so it was NOT built; scope must be confirmed first.**

## Per-Agent Status
| Agent | Status | Note |
|---|---|---|
| 1 Professional dashboard | Implemented | ClinicianDashboard serves it; opened from MainWindow. UI runtime unverifiable on Linux. |
| 2 Coach dashboard | Partial | Exists/opens; recovery panel uses a `WellRecovered` placeholder instead of RecoveryIntelligenceService; LearningStage labels need nb keys. |
| 3 Clinician dashboard | Partial | Same window as Agent 1; leaks raw `RecoveryProgress.Status`/English/enum `.ToString()` (ClinicianDashboardViewModel ~346/350/418-463) — should reuse existing ReportAssembler localize helpers. |
| 4 Report generation | Implemented | 4 DTOs, all 3 formats, tested; status tracked on real attempts. |
| 5 Outcome tracking | Mostly Implemented | 4/7 brief fields present; ComfortProgress/Retention missing (Retention is out-of-bounds). |
| 6 Research anonymization | Partial | Privacy core complete + tested; no production feed/caller. |
| 7 Manual override | Implemented | Two-stage recovery-floor → gate clamp → Validate; SafetyOverrideInvariantTests prove an override can only HOLD or be MORE conservative. **Do not touch.** |
| 8 Clinical notes | **Implemented (this sprint)** | 4 note types; mutability policy tested; excluded from research export. **Now also emits a ProfessionalNote audit event on every save (gap closed here).** RecoveryNote type still absent. |
| 9 Case review workflow | Partial | Store+Assembler+Window wired; ReviewType missing Coach/Clinical, ReviewStatus missing NeedsFollowUp/Archived (additive, TEXT-safe if added). |
| 10 Research analytics | Partial | Group-only means/shares with k-anonymity; missing learning-path/adherence aggregates; caveat not localized. |
| 11 Professional exports | Mostly Implemented | PDF/CSV/JSON for 4 reports + verification status. No standalone package selector; ResearchDataset/AuditTrail not exported standalone. |
| 12 Audit trail | **Partial → improved (this sprint)** | Store complete + tested. Was a **facade**: only ManualOverride wrote events. **This sprint added the 3 missing event types (ReportGeneration/ResearchExport/ProfessionalNote) and real write sites for ProfessionalNote (note save) and ReportGeneration (report export).** Remaining categories (Recommendation, GoalChange, RecoveryEvent, ReviewAction, ResearchExport) still need write sites. |
| 13 Pilot readiness | Mostly Implemented | PilotReadinessChecker + tests exist (read-only harness, not DI-registered, no in-app entry). |

## What this sprint changed (additive, reuse-only, compile-verified)
| File | Class/Method | Change |
|---|---|---|
| `FemVoiceStudio/Models/AuditEvent.cs` | `AuditEntityType` | Added `ReportGeneration`, `ResearchExport`, `ProfessionalNote` (additive, TEXT-stored, append-only). |
| `FemVoiceStudio/Services/ClinicalNotesStore.cs` | ctor + `SaveNoteAsync` | Optional `AuditTrailStore?` param; on save, appends a best-effort `ProfessionalNote` audit event (never blocks/fails the save). |
| `FemVoiceStudio/App.xaml.cs` | ClinicalNotesStore DI registration | Injects the singleton `AuditTrailStore` (lambda `_`→`sp`). |
| `FemVoiceStudio/ViewModels/ReportExportViewModel.cs` | ctors + `GenerateAsync` + `AppendReportAuditAsync` | Resolves `ClinicalNotesStore`+`AuditTrailStore`; the **clinical report now includes real notes + audit events** for the period (was `Array.Empty`); each generation attempt appends a `ReportGeneration` audit event (success + failure). |
| `FemVoiceStudio.Tests/ClinicalNotesStoreTests.cs` | +2 tests | `SaveNote_WithAuditTrail_EmitsProfessionalNoteAuditEvent`, `SaveNote_WithoutAuditTrail_StillSavesAndDoesNotThrow`. |

## Tests
- **Added (source only, NOT executed here):** the 2 ClinicalNotesStore audit tests above.
- **Updated:** none (all changes additive; existing tests compile unchanged — both projects build with 0 errors).
- **Passed / Failed:** UNKNOWN — `dotnet test` requires the Windows Desktop runtime and was not run on this Linux box.

## Validation scenarios (status here)
| Scenario | Result on Linux | Evidence |
|---|---|---|
| Coach Review | Source present (AuditCompletenessTests, CaseReview*); not executed | audit/case-review stores compile |
| Goal Change | Audit write-site MISSING (GoalChange never written) | only Override + new ProfessionalNote/ReportGeneration write |
| Research Export | NOT wired (no production caller, no CSV/PDF branch) | verified by grep |
| Outcome Tracking | Implemented (4/7 fields); compiles | OutcomeProfileBuilder + tests |
| Manual Override | Implemented + safety-proven; not executed | SafetyOverrideInvariantTests source |
| Professional Reports | Implemented; **now include notes+audit**; compiles | ReportExportViewModel build 0 errors |
| Audit Trail Export | Query/export exists; standalone audit export MISSING | AuditTrailStore.QueryAsync present |
| Dashboard Empty State | Source present; **UI runtime unverifiable on Linux** | ClinicianDashboard null-safe paths |
| Dashboard Normal State | Source present; **UI runtime unverifiable on Linux** | — |
| Norwegian Localization | Reports localized + tested; dashboards have raw-enum leaks | ReportLocalizationTests pass-by-source; ClinicianDashboardViewModel leaks |

## Known limitations
- **Linux/headless:** no WPF runtime, no `dotnet test`. All "Implemented" = source exists + compiles + has test source; UI rendering, binding resolution, and test pass/fail are NOT verified here.
- **Retention metric** would require new scoring → crosses frozen boundary. Not built; confirm scope.
- **Audit completeness is still partial:** ProfessionalNote + ReportGeneration write sites added this sprint; Recommendation / GoalChange / RecoveryEvent / ReviewAction / ResearchExport still need write sites at their action handlers (CaseReviewViewModel, goal edit, recovery events, research export).
- **Research Edition** needs a production data-feed + ResearchDataset export-format branch + UI entry before it is usable.
- **Dashboard localization leaks** (ClinicianDashboardViewModel raw enum/English; ManualOverrideWindow mislabeled result rows; ReportExportWindow dead `ExportCommand` binding; CaseReviewWindow enum list) are XAML/VM fixes that cannot be runtime-validated on Linux.

## Release recommendation: **Needs Additional Work**
The Professional core (dashboards, reports-with-notes, overrides with safety precedence, notes, case review, outcome tracking) is solid and now better wired. Before a Professional Pilot: (1) finish audit write-sites for the remaining categories; (2) wire Research export end-to-end (or descope Research Edition from the pilot); (3) fix the dashboard localization leaks and the dead export button; (4) run `dotnet test` + manual WPF validation on Windows. Research Edition is not pilot-ready as-is.

## Recommended next steps (minimal, reuse-respecting, NOT done here)
1. Add audit write sites: CaseReviewViewModel (ReviewAction), goal edit (GoalChange), research export (ResearchExport) — mirror the ProfessionalNote/ReportGeneration pattern added this sprint.
2. Localize ClinicianDashboardViewModel via existing ReportAssembler.LocalizeStatus/Localize* helpers.
3. Wire ResearchAnonymizer/Aggregator to a real session feed + add a ResearchDataset CSV branch in ExportWriter.
4. Confirm scope for OutcomeProfile ComfortProgress (reuse existing comfort signals) and whether Retention is in scope (needs new scoring — boundary decision).
