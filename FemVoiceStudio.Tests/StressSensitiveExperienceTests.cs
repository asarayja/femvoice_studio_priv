using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester for tilgjengelighets-dempingen (Agent 3). Klinisk invariant gjennom alle
    /// testene: safety/helse-INFORMASJONEN består — kun PRESENTASJONEN dempes. En lås
    /// gir fortsatt en synlig shield-tilstand; bare fargen/severity dempes.
    /// </summary>
    public class StressSensitiveExperienceTests
    {
        private static TestDatabaseService DbWith(bool stress = false, bool reducedVisual = false)
        {
            var db = new TestDatabaseService();
            db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                StressSensitiveMode = stress,
                ReducedVisualFeedback = reducedVisual
            });
            return db;
        }

        // ── SoftenBrushKey ────────────────────────────────────────────────────────

        [Fact]
        public void SoftenBrushKey_Off_LeavesAllKeysUnchanged()
        {
            var svc = new StressSensitiveExperience(DbWith(stress: false));

            Assert.False(svc.IsStressSensitive);
            Assert.Equal("ErrorBrush", svc.SoftenBrushKey("ErrorBrush"));
            Assert.Equal("QualityBrush_Poor", svc.SoftenBrushKey("QualityBrush_Poor"));
            Assert.Equal("SuccessBrush", svc.SoftenBrushKey("SuccessBrush"));
        }

        [Fact]
        public void SoftenBrushKey_On_RemapsAlarmKeysToWarmKeys()
        {
            var svc = new StressSensitiveExperience(DbWith(stress: true));

            Assert.True(svc.IsStressSensitive);
            Assert.Equal("WarningBrush", svc.SoftenBrushKey("ErrorBrush"));
            Assert.Equal("QualityBrush_Fair", svc.SoftenBrushKey("QualityBrush_Poor"));
            // Allerede-rolige nøkler passerer uendret — informasjonen er den samme.
            Assert.Equal("SuccessBrush", svc.SoftenBrushKey("SuccessBrush"));
            Assert.Equal("WarningBrush", svc.SoftenBrushKey("WarningBrush"));
        }

        // ── SoftenSeverity ────────────────────────────────────────────────────────

        [Fact]
        public void SoftenSeverity_Off_LeavesSeverityUnchanged()
        {
            var svc = new StressSensitiveExperience(DbWith(stress: false));

            Assert.Equal(MessageSeverity.Warning, svc.SoftenSeverity(MessageSeverity.Warning));
            Assert.Equal(MessageSeverity.Suggestion, svc.SoftenSeverity(MessageSeverity.Suggestion));
            Assert.Equal(MessageSeverity.Info, svc.SoftenSeverity(MessageSeverity.Info));
        }

        [Fact]
        public void SoftenSeverity_On_DemotesWarningToSuggestion_ButKeepsRest()
        {
            var svc = new StressSensitiveExperience(DbWith(stress: true));

            Assert.Equal(MessageSeverity.Suggestion, svc.SoftenSeverity(MessageSeverity.Warning));
            Assert.Equal(MessageSeverity.Suggestion, svc.SoftenSeverity(MessageSeverity.Suggestion));
            Assert.Equal(MessageSeverity.Info, svc.SoftenSeverity(MessageSeverity.Info));
        }

        // ── Robusthet ─────────────────────────────────────────────────────────────

        [Fact]
        public void NullDatabase_DefaultsToOff_AndNeverThrows()
        {
            var svc = new StressSensitiveExperience(null);

            Assert.False(svc.IsStressSensitive);
            Assert.False(svc.IsReducedVisual);
            Assert.Equal("ErrorBrush", svc.SoftenBrushKey("ErrorBrush"));
            Assert.Equal(MessageSeverity.Warning, svc.SoftenSeverity(MessageSeverity.Warning));
        }

        [Fact]
        public void NoProfilePersisted_DefaultsToOff()
        {
            // Tom database (ingen lagret profil) skal gi uendret oppførsel.
            var svc = new StressSensitiveExperience(new TestDatabaseService());

            Assert.False(svc.IsStressSensitive);
            Assert.False(svc.IsReducedVisual);
            Assert.Equal("ErrorBrush", svc.SoftenBrushKey("ErrorBrush"));
        }

        [Fact]
        public void Refresh_PicksUpChangedProfile()
        {
            var db = DbWith(stress: false);
            var svc = new StressSensitiveExperience(db);

            // Første lasting: av.
            Assert.False(svc.IsStressSensitive);

            // Bruker slår på modus i Settings og lagrer.
            db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                StressSensitiveMode = true,
                ReducedVisualFeedback = true
            });

            // Uten Refresh er cachen fortsatt "av".
            Assert.False(svc.IsStressSensitive);

            svc.Refresh();

            Assert.True(svc.IsStressSensitive);
            Assert.True(svc.IsReducedVisual);
        }

        // ── ExerciseDetailViewModel-integrasjon ─────────────────────────────────────

        private static ExerciseDetailViewModel CreateViewModel(StressSensitiveExperience? stress)
            => new(
                new ExerciseIntelligenceCoordinator(),
                LocalizationService.Instance,
                new ExerciseProfileFactory(),
                feedbackPipeline: null,
                inlineCoachFeedbackMapper: null,
                masteryEvaluator: null,
                stressSensitive: stress);

        [Fact]
        public void Edvm_WithStressSensitive_NeverReturnsErrorBrushOnLock()
        {
            using var vm = CreateViewModel(new StressSensitiveExperience(DbWith(stress: true)));
            vm.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming());
            vm.StartExerciseCommand.Execute(null);

            // Health < 70 → safety-lås. Informasjonen (Locked-tilstand) består fortsatt.
            vm.UpdateLiveMetrics(resonanceScore: 0.6, pitch: 0, stability: 0.6, health: 50);

            Assert.True(vm.IsSafetyLocked);
            Assert.Equal(ShieldDisplayState.Locked, vm.ShieldState);
            // Rød alarm dempes til varm advarsel — men shielden er fortsatt synlig/låst.
            Assert.NotEqual("ErrorBrush", vm.ShieldBrushKey);
            Assert.Equal("WarningBrush", vm.ShieldBrushKey);
            Assert.NotEqual("ErrorBrush", vm.LiveCompositeScoreBrushKey);
        }

        [Fact]
        public void Edvm_WithoutService_KeepsErrorBrushOnLock_BackwardCompatible()
        {
            using var vm = CreateViewModel(stress: null);
            vm.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming());
            vm.StartExerciseCommand.Execute(null);

            vm.UpdateLiveMetrics(resonanceScore: 0.6, pitch: 0, stability: 0.6, health: 50);

            Assert.True(vm.IsSafetyLocked);
            Assert.Equal(ShieldDisplayState.Locked, vm.ShieldState);
            // Uendret bakoverkompatibel oppførsel: rød ved lås.
            Assert.Equal("ErrorBrush", vm.ShieldBrushKey);
            Assert.Equal("ErrorBrush", vm.LiveCompositeScoreBrushKey);
        }

        [Fact]
        public void Edvm_WithStressSensitiveOff_KeepsErrorBrushOnLock()
        {
            using var vm = CreateViewModel(new StressSensitiveExperience(DbWith(stress: false)));
            vm.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming());
            vm.StartExerciseCommand.Execute(null);

            vm.UpdateLiveMetrics(resonanceScore: 0.6, pitch: 0, stability: 0.6, health: 50);

            Assert.True(vm.IsSafetyLocked);
            Assert.Equal("ErrorBrush", vm.ShieldBrushKey);
        }
    }
}
