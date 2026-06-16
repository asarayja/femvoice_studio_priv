using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Regresjonstester for de tre bekreftede runtime-validation-funnene i Sprint A.1/A.2
    /// bølge 3:
    ///   1) Per-kanal anti-flom i FeedbackConsistencyGuard — distinkte forside-paneler
    ///      sulter ikke hverandre ut (CoachExplanation/SessionSummary-utsulting).
    ///   2) Trådsikker MainScreenFeedbackDebouncer — ingen Dictionary-race mellom
    ///      UI-tråd og audio-trådpool-tråd.
    ///   3) (Stil-resonans-kompoundering dekkes av en egen test som verifiserer at
    ///      den persisterte override-en forblir stil-nøytral — se nederst.)
    /// </summary>
    public class Bolge3RegressionTests
    {
        private static DateTime _t = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static FeedbackConsistencyGuard GuardWithControllableClock(Func<DateTime> clock)
            => new FeedbackConsistencyGuard(
                clock: clock,
                minimumInterval: TimeSpan.FromSeconds(2),
                escalationThreshold: 99);

        private static FeedbackCandidate MainScreen(
            FeedbackPriority priority, string reason, string channel)
            => new("text", reason, priority, MessageSeverity.Info, "MainScreen", reason, channel);

        // ── Fix B: per-kanal anti-flom ────────────────────────────────────────────

        [Fact]
        public void Guard_DistinctChannels_DoNotStarveEachOther_WithinRateWindow()
        {
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            // Pitch-sone-coaching godkjennes (kanal RealtimeFeedback).
            var pitch = guard.Submit(
                MainScreen(FeedbackPriority.TechniqueCorrection, "MAINSCREEN_PITCH_ZONE", "MainScreen:RealtimeFeedback"));
            Assert.Equal(FeedbackDecisionKind.Approved, pitch.Kind);

            // Coach-forklaring i SAMME tick, ANNEN kanal — skal IKKE undertrykkes av
            // den globale anti-flommen (det var dette som sultet ut panelet før fiksen).
            var coach = guard.Submit(
                MainScreen(FeedbackPriority.TechniqueCorrection, "MAINSCREEN_COACH_TECHNIQUE", "MainScreen:CoachExplanation"));
            Assert.Equal(FeedbackDecisionKind.Approved, coach.Kind);
        }

        [Fact]
        public void Guard_SameChannel_StillAntiFloods_WithinRateWindow()
        {
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            // To kandidater i SAMME kanal (delt panel) skal fortsatt anti-flomme
            // hverandre — den andre (lavere/lik prioritet, < HealthWarning) undertrykkes.
            var first = guard.Submit(
                MainScreen(FeedbackPriority.ProgressionUpdate, "MAINSCREEN_PROGRESSION", "MainScreen:StatusText"));
            Assert.Equal(FeedbackDecisionKind.Approved, first.Kind);

            var second = guard.Submit(
                MainScreen(FeedbackPriority.PerformancePraise, "MAINSCREEN_SESSION_PRAISE_X", "MainScreen:StatusText"));
            Assert.Equal(FeedbackDecisionKind.Suppressed, second.Kind);
        }

        [Fact]
        public void Guard_DefaultChannel_PreservesSharedCoachPanelAntiFlood()
        {
            // Tom kanal ("") = den delte coach-panel-overflaten (EDVM/SmartCoach/…).
            // Oppførselen skal være uendret: andre sub-HealthWarning-melding undertrykkes.
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            var a = guard.Submit(new FeedbackCandidate(
                "t", "REASON_A", FeedbackPriority.TechniqueCorrection, MessageSeverity.Info, "EDVM", "CONFLICT_A"));
            Assert.Equal(FeedbackDecisionKind.Approved, a.Kind);

            var b = guard.Submit(new FeedbackCandidate(
                "t", "REASON_B", FeedbackPriority.TechniqueCorrection, MessageSeverity.Info, "EDVM", "CONFLICT_B"));
            Assert.Equal(FeedbackDecisionKind.Suppressed, b.Kind);
        }

        // ── F1 (safety): the health band must bypass the per-channel time gate ─────

        [Fact]
        public void Guard_HealthBand_SkipsPerChannelGate_AfterLowerCoachingMessage()
        {
            // F1-regresjon: en tidligere lavere-prioritert coaching-melding på den DELTE
            // tomme kanalen ("") fikk før tidsundertrykke en påfølgende helse-melding via
            // per-kanal-tidsgaten. Etter fiksen MÅ hele helse-båndet (>= HydrationSuggestion
            // = 40) hoppe over den gaten. Distinkte reason codes brukes slik at den
            // per-reason rate-limiten ikke maskerer testen.
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            // Lower-priority coaching first on the shared empty channel — warms the gate.
            var technique = guard.Submit(new FeedbackCandidate(
                "t", "F1_TECHNIQUE", FeedbackPriority.TechniqueCorrection,
                MessageSeverity.Info, "MainScreen", "F1_TECHNIQUE", Channel: ""));
            Assert.Equal(FeedbackDecisionKind.Approved, technique.Kind);

            // All three health-band messages within the same tick (window) MUST pass —
            // the gate must NOT time-suppress them behind the earlier coaching message.
            var strain = guard.Submit(new FeedbackCandidate(
                "t", "F1_STRAIN", FeedbackPriority.ActiveStrainAlert,
                MessageSeverity.Warning, "MainScreen", "F1_STRAIN", Channel: ""));
            Assert.Equal(FeedbackDecisionKind.Approved, strain.Kind);

            var pause = guard.Submit(new FeedbackCandidate(
                "t", "F1_PAUSE", FeedbackPriority.PauseRecommendation,
                MessageSeverity.Warning, "MainScreen", "F1_PAUSE", Channel: ""));
            Assert.Equal(FeedbackDecisionKind.Approved, pause.Kind);

            var hydration = guard.Submit(new FeedbackCandidate(
                "t", "F1_HYDRATION", FeedbackPriority.HydrationSuggestion,
                MessageSeverity.Info, "MainScreen", "F1_HYDRATION", Channel: ""));
            Assert.Equal(FeedbackDecisionKind.Approved, hydration.Kind);
        }

        [Fact]
        public void Guard_PerChannel_DoesNotBypassClinicalMatrix()
        {
            // Kliniske suppresjonen er kanal-agnostisk: en praise-kandidat på en egen
            // kanal undertrykkes fortsatt under aktiv strain.
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            var praise = guard.Submit(
                MainScreen(FeedbackPriority.PerformancePraise, "MAINSCREEN_SESSION_PRAISE", "MainScreen:SessionFeedback"),
                new FeedbackGuardContext(IsActiveStrainAlert: true));

            Assert.Equal(FeedbackDecisionKind.Suppressed, praise.Kind);
        }

        [Fact]
        public void Guard_SafetyFreezeChannel_AlwaysApproved()
        {
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = GuardWithControllableClock(() => now);

            // Hold den globale anti-flommen varm med en teknikk-melding først.
            guard.Submit(MainScreen(FeedbackPriority.TechniqueCorrection, "MAINSCREEN_PITCH_ZONE", "MainScreen:RealtimeFeedback"));

            var safety = guard.Submit(
                MainScreen(FeedbackPriority.SafetyFreeze, "MAINSCREEN_SAFETY_LOCK", "MainScreen:StatusText"));
            Assert.Equal(FeedbackDecisionKind.Approved, safety.Kind);
        }

        // ── Fix A: trådsikker debouncer ───────────────────────────────────────────

        [Fact]
        public void Debouncer_DistinctBindings_BothPass()
        {
            var clock = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var deb = new MainScreenFeedbackDebouncer(() => clock, TimeSpan.FromSeconds(1));

            Assert.True(deb.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "a"));
            Assert.True(deb.ShouldSubmit(MainScreenFeedbackBinding.CoachExplanation, "b"));
        }

        [Fact]
        public void Debouncer_IdenticalKeyWithinWindow_Swallowed()
        {
            var clock = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var deb = new MainScreenFeedbackDebouncer(() => clock, TimeSpan.FromSeconds(1));

            Assert.True(deb.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "same"));
            Assert.False(deb.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "same"));
        }

        [Fact]
        public void Debouncer_ConcurrentAccess_DoesNotThrowOrCorrupt()
        {
            // Reproduserer den samtidige tilgangen (UI-tråd + audio-trådpool-tråd) som
            // muterte den ikke-trådsikre Dictionary-en før fiksen. Skal ikke kaste
            // (InvalidOperationException / korrupt bucket) under parallell last.
            var deb = new MainScreenFeedbackDebouncer(() => DateTime.UtcNow, TimeSpan.FromMilliseconds(1));
            var ex = Record.Exception(() =>
            {
                System.Threading.Tasks.Parallel.For(0, 20000, i =>
                {
                    var binding = (i % 2 == 0)
                        ? MainScreenFeedbackBinding.RealtimeFeedback
                        : MainScreenFeedbackBinding.CoachExplanation;
                    deb.ShouldSubmit(binding, "k" + (i % 7));
                    if (i % 500 == 0) deb.Reset();
                });
            });
            Assert.Null(ex);
        }

        // ── Fix C: stil-resonansdeltaen er RELATIV ⇒ må anvendes på en stil-nøytral base ──

        [Fact]
        public void Personalize_DarkFeminineResonanceDelta_IsRelative_AndCompoundsIfReapplied()
        {
            // Dette beviser ROTÅRSAKEN bak base-profil-rutingen i ExerciseWindow:
            // stil-deltaen (-0.08) er RELATIV til inn-profilen. Hvis den personaliserte
            // profilen mates inn igjen (slik override-rundturen gjorde FØR fiksen),
            // trekkes -0.08 fra på nytt og resonansmålet drifter mot gulvet.
            var adapter = new TargetProfileAdapter();
            var user = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.DarkFeminine };
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels(); // min 0.58

            var once = adapter.Personalize(baseProfile, user, recoveryActive: false);
            var twice = adapter.Personalize(once, user, recoveryActive: false);

            // Én anvendelse: -0.08.
            Assert.Equal(baseProfile.TargetResonanceMin - 0.08, once.TargetResonanceMin, 3);
            // To anvendelser kompounderer (-0.16) — derfor MÅ progresjons-autoriteten
            // evaluere/persistere mot den stil-nøytrale base-profilen, ikke den
            // personaliserte (ExerciseWindow._activeBaseProfile-invarianten).
            Assert.True(twice.TargetResonanceMin < once.TargetResonanceMin);
            Assert.Equal(once.TargetResonanceMin - 0.08, twice.TargetResonanceMin, 3);
        }

        [Fact]
        public void Personalize_NeutralBase_AppliedOnce_IsStable_AcrossReloads()
        {
            // Med base-profil-rutingen lastes ALLTID den stil-nøytrale base-profilen, og
            // Personalize anvender deltaen ÉN gang per lasting ⇒ deterministisk og stabilt
            // resultat økt etter økt (ingen drift).
            var adapter = new TargetProfileAdapter();
            var user = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.DarkFeminine };
            var neutralBase = ExerciseTargetProfile.CreateResonanceVowels();

            var load1 = adapter.Personalize(neutralBase, user, recoveryActive: false);
            var load2 = adapter.Personalize(neutralBase, user, recoveryActive: false);

            Assert.Equal(load1.TargetResonanceMin, load2.TargetResonanceMin, 6);
            Assert.Equal(neutralBase.TargetResonanceMin - 0.08, load2.TargetResonanceMin, 3);
        }
    }
}
