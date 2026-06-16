using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester broen mellom AdaptiveComfortZoneService og UserVoiceProfile:
    ///   • komfortsone-persistering (kalibrert-vs-default-skille, avviks-terskel,
    ///     bevaring av øvrige felter, baseline-snapshot), og
    ///   • baseline-fallback-leseren (kaldstart bruker persistert BaselinePitch).
    /// Bruker TestDatabaseService-mønsteret (ekte klasser + in-memory-fake, ingen mocks).
    /// </summary>
    public class AdaptiveComfortZonePersistenceTests
    {
        private static SmartCoachBaseline CalibratedBaseline(double pitch = 185, double resonance = 65)
            => new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = pitch,
                BaselineResonanceScore = resonance,
                ConfidenceLevel = "high"
                // CalculatedAt bevisst null ⇒ GetOrCalculateBaseline returnerer raden uendret.
            };

        private static (AdaptiveComfortZoneService Service, TestDatabaseService Db) BuildService(
            SmartCoachBaseline? baseline = null)
        {
            var db = new TestDatabaseService();
            if (baseline != null)
                db.SetSmartCoachBaseline(baseline);

            var smartCoach = new SmartCoachEngine(db);
            var service = new AdaptiveComfortZoneService(smartCoach, db);
            return (service, db);
        }

        // ── Kalibrert-vs-default-skille ──────────────────────────────────────────

        [Fact]
        public void Persist_WithoutBaseline_DoesNotWriteDefaultZone()
        {
            // Ingen baseline, ingen økter ⇒ "low"/ingen kalibrering ⇒ ren default.
            var (service, db) = BuildService();

            var result = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.9, 95);

            Assert.Null(result);
            Assert.Null(db.GetUserVoiceProfile(1)); // generisk default skal ALDRI persisteres
        }

        [Fact]
        public void Persist_WithCalibratedBaseline_WritesComfortZone()
        {
            var (service, db) = BuildService(CalibratedBaseline());

            var result = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);

            Assert.NotNull(result);
            var saved = db.GetUserVoiceProfile(1);
            Assert.NotNull(saved);
            Assert.NotNull(saved!.ComfortZoneMinPitch);
            Assert.NotNull(saved.ComfortZoneMaxPitch);
            Assert.NotNull(saved.ComfortZoneOptimalPitch);
            Assert.True(saved.ComfortZoneMaxPitch > saved.ComfortZoneMinPitch);
        }

        [Fact]
        public void ComputeComfortZone_DefaultZone_IsMarkedNotCalibrated()
        {
            var (service, _) = BuildService();

            var (_, calibrated) = service.ComputeComfortZone(1, SessionType.Progressive);

            Assert.False(calibrated);
        }

        [Fact]
        public void ComputeComfortZone_RealBaseline_IsMarkedCalibrated()
        {
            var (service, _) = BuildService(CalibratedBaseline());

            var (_, calibrated) = service.ComputeComfortZone(1, SessionType.Progressive);

            Assert.True(calibrated);
        }

        // ── Baseline-snapshot fra eksisterende beregnede kilder ──────────────────

        [Fact]
        public void Persist_SnapshotsBaselinePitchResonanceComfortAndHealth()
        {
            var (service, db) = BuildService(CalibratedBaseline(pitch: 190, resonance: 72));

            service.PersistCalibratedProfile(1, SessionType.Progressive,
                sessionComfortRatio: 0.8, sessionHealthScore: 88);

            var saved = db.GetUserVoiceProfile(1);
            Assert.NotNull(saved);
            Assert.Equal(190, saved!.BaselinePitch);          // fra SmartCoach-baseline
            Assert.Equal(72, saved.BaselineResonance);        // fra SmartCoach-baseline
            Assert.Equal(0.8, saved.BaselineComfort);         // fra øktens komfort-ratio
            Assert.Equal(88, saved.BaselineHealth);           // fra øktens helsescore
        }

        // ── Bevaring av øvrige felter ────────────────────────────────────────────

        [Fact]
        public void Persist_PreservesUnrelatedProfileFields()
        {
            var (service, db) = BuildService(CalibratedBaseline());

            // Forhåndslagret profil med accessibility-/stil-/frekvens-valg som IKKE skal røres.
            db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                PreferredVoiceStyle = VoiceStyleGoal.DarkFeminine,
                TrainingFrequencyPerWeek = 5,
                StressSensitiveMode = true,
                ReducedVisualFeedback = true
            });

            service.PersistCalibratedProfile(1, SessionType.Progressive, 0.7, 80);

            var saved = db.GetUserVoiceProfile(1);
            Assert.NotNull(saved);
            Assert.Equal(VoiceStyleGoal.DarkFeminine, saved!.PreferredVoiceStyle);
            Assert.Equal(5, saved.TrainingFrequencyPerWeek);
            Assert.True(saved.StressSensitiveMode);
            Assert.True(saved.ReducedVisualFeedback);
            // ...men sonen SKAL nå være satt.
            Assert.NotNull(saved.ComfortZoneMinPitch);
        }

        // ── Avviks-terskel: ingen skriving per tick ──────────────────────────────

        [Fact]
        public void Persist_SecondCallWithUnchangedState_DoesNotRewrite()
        {
            var (service, db) = BuildService(CalibratedBaseline());

            var first = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);
            Assert.NotNull(first);
            var firstWriteTime = db.GetUserVoiceProfile(1)!.LastUpdated;

            // Identisk kalibrering + identiske øktverdier ⇒ ingen meningsfull endring.
            var second = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);

            Assert.Null(second); // ikke skrevet på nytt
            Assert.Equal(firstWriteTime, db.GetUserVoiceProfile(1)!.LastUpdated);
        }

        [Fact]
        public void Persist_WhenBaselineMovesMeaningfully_Rewrites()
        {
            var (service, db) = BuildService(CalibratedBaseline(pitch: 180));

            var first = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);
            Assert.NotNull(first);

            // Baseline flytter seg klart (180 → 210) ⇒ ny sone ⇒ skal skrives.
            db.SetSmartCoachBaseline(CalibratedBaseline(pitch: 210));

            var second = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);

            Assert.NotNull(second);
            var saved = db.GetUserVoiceProfile(1);
            Assert.Equal(210, saved!.BaselinePitch);
        }

        [Fact]
        public void Persist_WhenOnlySessionComfortChanges_Rewrites()
        {
            var (service, db) = BuildService(CalibratedBaseline());

            service.PersistCalibratedProfile(1, SessionType.Progressive, 0.85, 90);

            // Samme sone/baseline, men øktens komfort-ratio endrer seg ⇒ snapshot oppdateres.
            var second = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.50, 90);

            Assert.NotNull(second);
            Assert.Equal(0.50, db.GetUserVoiceProfile(1)!.BaselineComfort);
        }

        // ── Robusthet ────────────────────────────────────────────────────────────

        [Fact]
        public void Persist_WithoutDatabase_ReturnsNull()
        {
            // Bakoverkompatibel ctor uten DB ⇒ persistering er en no-op (ingen krasj).
            var smartCoach = new SmartCoachEngine(new TestDatabaseService());
            var service = new AdaptiveComfortZoneService(smartCoach);

            var result = service.PersistCalibratedProfile(1, SessionType.Progressive, 0.9, 95);

            Assert.Null(result);
        }

        // ── Baseline-fallback-leseren (Oppgave B) ────────────────────────────────

        [Fact]
        public void ComputeComfortZone_NoSmartCoachBaseline_FallsBackToPersistedBaselinePitch()
        {
            // Ingen SmartCoach-baseline (kaldstart etter DB-bytte), men profilen har et
            // persistert BaselinePitch-snapshot ⇒ sonen skal bygges fra snapshotet.
            var (service, db) = BuildService();
            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, BaselinePitch = 200 });

            var (zone, calibrated) = service.ComputeComfortZone(1, SessionType.Progressive);

            Assert.True(calibrated, "Persistert baseline-snapshot skal regnes som kalibrert kilde.");
            // Progressive: minPitch = baseline - 10 = 190.
            Assert.Equal(190, zone.Min, 3);
        }

        [Fact]
        public void ComputeComfortZone_NoBaselineAndNoSnapshot_UsesDefaultUncalibrated()
        {
            var (service, db) = BuildService();
            // Profil finnes, men BaselinePitch er 0 (aldri kalibrert) ⇒ ingen fallback.
            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, BaselinePitch = 0 });

            var (zone, calibrated) = service.ComputeComfortZone(1, SessionType.Progressive);

            Assert.False(calibrated);
            Assert.Equal(165, zone.Min, 3);   // generisk default progressive-sone
            Assert.Equal(255, zone.Max, 3);
        }

        [Fact]
        public void ComputeComfortZone_RealBaselineWins_OverPersistedSnapshot()
        {
            // Når BÅDE ekte SmartCoach-baseline og persistert snapshot finnes, vinner den
            // ekte (snapshotet er kun en kaldstart-fallback).
            var (service, db) = BuildService(CalibratedBaseline(pitch: 185));
            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, BaselinePitch = 240 });

            var (zone, _) = service.ComputeComfortZone(1, SessionType.Progressive);

            // Progressive med baseline 185 ⇒ minPitch = 175 (ikke 230 fra snapshotet).
            Assert.Equal(175, zone.Min, 3);
        }
    }
}
