using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester helse-gaten i <see cref="AdaptiveComfortZoneService.CanProgress"/> og at den
    /// deler ÉN helsekilde med <see cref="AdaptiveComfortZoneService.GetRecommendedSessionType"/>
    /// (den ukentlige SmartCoach-aggregerte HealthScore) — ingen ny/parallell helsekilde.
    ///
    /// Klinisk invariant: sone-progresjon krever at den ukentlige helsestatusen tillater
    /// det (Safety/Health &gt; Progression). Faller helsen under terskelen skal vi aldri
    /// anbefale en Progressive økt.
    ///
    /// HealthScore beregnes som 100 − snitt(StrainLevel) over helseproblemene siste 7 dager
    /// (SmartCoachEngine.CalculateWeeklyProgress). Vi seeder StrainLevel for å styre den.
    /// Ekte klasser + in-memory-fake (TestDatabaseService), ingen mocks.
    /// </summary>
    public class AdaptiveComfortZoneHealthGateTests
    {
        private static SmartCoachBaseline HealthyResonanceBaseline()
            => new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 185,
                BaselineResonanceScore = 80,   // over MinResonanceForProgression (60)
                ConfidenceLevel = "high"
            };

        private static (AdaptiveComfortZoneService Service, TestDatabaseService Db) Build(
            SmartCoachBaseline? baseline)
        {
            var db = new TestDatabaseService();
            if (baseline != null)
                db.SetSmartCoachBaseline(baseline);

            var smartCoach = new SmartCoachEngine(db);
            return (new AdaptiveComfortZoneService(smartCoach, db), db);
        }

        /// <summary>Seeder ett helseproblem denne uken med gitt StrainLevel ⇒ HealthScore = 100 − strain.</summary>
        private static void SeedStrain(TestDatabaseService db, double strainLevel)
            => db.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Now,
                StrainDetected = true,
                StrainType = "TEST",
                StrainLevel = strainLevel
            });

        // ── GetRecommendedSessionType: helsekilden gater rutingen ────────────────

        [Fact]
        public void RecommendedSessionType_HealthyAndCalibrated_NeverRecommendsRecoveryFromHealth()
        {
            // Ingen strain ⇒ HealthScore = 100 ⇒ helse-grenen utløses ikke.
            var (service, _) = Build(HealthyResonanceBaseline());

            var sessionType = service.GetRecommendedSessionType(1);

            Assert.NotEqual(SessionType.Recovery, sessionType);
        }

        [Fact]
        public void RecommendedSessionType_LowHealth_ReturnsRecovery()
        {
            // Strain 50 ⇒ HealthScore = 50 (< 60) ⇒ Recovery, uansett baseline-kvalitet.
            var (service, db) = Build(HealthyResonanceBaseline());
            SeedStrain(db, 50);

            var sessionType = service.GetRecommendedSessionType(1);

            Assert.Equal(SessionType.Recovery, sessionType);
        }

        [Fact]
        public void RecommendedSessionType_ModerateHealth_ReturnsMaintenanceNotProgressive()
        {
            // Strain 30 ⇒ HealthScore = 70 (>= 60 men < 80) ⇒ Maintenance, aldri Progressive.
            var (service, db) = Build(HealthyResonanceBaseline());
            SeedStrain(db, 30);

            var sessionType = service.GetRecommendedSessionType(1);

            Assert.Equal(SessionType.Maintenance, sessionType);
            Assert.NotEqual(SessionType.Progressive, sessionType);
        }

        // ── CanProgress: helse-gaten ─────────────────────────────────────────────

        [Fact]
        public void CanProgress_LowHealth_IsBlockedEvenWithGoodResonance()
        {
            // Strain 40 ⇒ HealthScore = 60 (< MinHealthForProgression 70) ⇒ progresjon nektes,
            // selv om resonansen er godt over sin egen terskel.
            var (service, db) = Build(HealthyResonanceBaseline());
            SeedStrain(db, 40);

            Assert.False(service.CanProgress(1));
        }

        [Fact]
        public void CanProgress_LowResonance_IsBlockedBeforeHealthCheck()
        {
            // Resonans under terskel (50 < 60) ⇒ alltid blokkert, helsen er irrelevant her.
            var lowResonance = HealthyResonanceBaseline();
            lowResonance.BaselineResonanceScore = 50;
            var (service, _) = Build(lowResonance);

            Assert.False(service.CanProgress(1));
        }

        [Fact]
        public void CanProgress_NoSeededBaseline_IsBlocked()
        {
            // Ingen seedet baseline ⇒ SmartCoach beregner en "low"-confidence baseline med
            // resonans 0 ⇒ blokkert allerede på resonans-gaten (bevarer eksisterende kontrakt;
            // helse-gaten endrer ikke denne stien).
            var (service, _) = Build(baseline: null);

            Assert.False(service.CanProgress(1));
        }
    }
}
