using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester for stress-sensitiv presentasjon av SmartCoach-helsevarselet (Agent U).
    ///
    /// Klinisk invariant gjennom HELE suiten (Safety &gt; Health &gt; ... &gt; UI):
    /// StressSensitiveMode/ReducedVisualFeedback endrer KUN HVORDAN helsevarselet vises
    /// (brush-nøkkel/severity/sekundære badges) — ALDRI OM. HealthWarningText og
    /// HasHealthWarning er alltid bevart; et helsevarsel formidles uansett.
    ///
    /// Testene bruker den ekte <see cref="StressSensitiveExperience"/> med en in-memory
    /// <see cref="TestDatabaseService"/> (ingen mocking-rammeverk). ViewModelens rene
    /// presentasjons-egenskaper (HealthWarningBrushKey / HealthWarningSeverity /
    /// ShowSecondaryBadges) testes uten å instansiere WPF.
    /// </summary>
    public class SmartCoachStressSensitiveTests
    {
        private static StressSensitiveExperience StressService(bool stress = false, bool reducedVisual = false)
        {
            var db = new TestDatabaseService();
            db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                StressSensitiveMode = stress,
                ReducedVisualFeedback = reducedVisual
            });
            return new StressSensitiveExperience(db);
        }

        // ViewModel-konstruksjon uten DB/engine-arbeid: ctor kaller aldri LoadData.
        // Tilgjengelighets-tjenesten injiseres direkte (tredje, valgfrie parameter), så
        // ingen App.Services / WPF kreves.
        private static SmartCoachViewModel CreateViewModel(StressSensitiveExperience? stress)
            => new(database: null!, engine: null!, stressSensitive: stress);

        // ── Helsevarsel-fargen dempes ved StressSensitiveMode ────────────────────────

        [Fact]
        public void HealthWarningBrushKey_StressSensitiveOn_SoftensErrorToWarning()
        {
            var vm = CreateViewModel(StressService(stress: true));

            // Rød alarm (ErrorBrush) dempes til varm advarsel (WarningBrush).
            Assert.Equal("WarningBrush", vm.HealthWarningBrushKey);
            Assert.NotEqual("ErrorBrush", vm.HealthWarningBrushKey);
        }

        [Fact]
        public void HealthWarningSeverity_StressSensitiveOn_SoftensWarningToSuggestion()
        {
            var vm = CreateViewModel(StressService(stress: true));

            // Severity dempes: Warning → Suggestion. Innholdet (teksten) er uberørt.
            Assert.Equal(MessageSeverity.Suggestion, vm.HealthWarningSeverity);
        }

        // ── Bakoverkompatibilitet: av / ingen tjeneste → rød alarm beholdes ──────────

        [Fact]
        public void HealthWarningBrushKey_StressSensitiveOff_KeepsErrorBrush()
        {
            var vm = CreateViewModel(StressService(stress: false));

            Assert.Equal("ErrorBrush", vm.HealthWarningBrushKey);
            Assert.Equal(MessageSeverity.Warning, vm.HealthWarningSeverity);
        }

        [Fact]
        public void HealthWarningBrushKey_NoService_KeepsErrorBrush_BackwardCompatible()
        {
            // Ingen tilgjengelighets-tjeneste (eldre DI / rene tester) → uendret oppførsel.
            var vm = CreateViewModel(stress: null);

            Assert.Equal("ErrorBrush", vm.HealthWarningBrushKey);
            Assert.Equal(MessageSeverity.Warning, vm.HealthWarningSeverity);
        }

        // ── KLINISK INVARIANT: HealthWarningText er ALLTID bevart (Safety > UI) ──────

        [Fact]
        public void HealthWarningText_Preserved_WhenStressSensitiveOn()
        {
            var vm = CreateViewModel(StressService(stress: true));
            const string warning = "Stemmen din viser tegn til anstrengelse — ta en pause.";

            vm.HasHealthWarning = true;
            vm.HealthWarningText = warning;

            // Demping endrer KUN presentasjonen (farge/severity) — teksten og selve
            // varselet består uendret.
            Assert.True(vm.HasHealthWarning);
            Assert.Equal(warning, vm.HealthWarningText);
            Assert.False(string.IsNullOrWhiteSpace(vm.HealthWarningText));
            // ...selv om fargen er dempet:
            Assert.Equal("WarningBrush", vm.HealthWarningBrushKey);
        }

        [Fact]
        public void HealthWarningText_Preserved_WhenStressSensitiveOff()
        {
            var vm = CreateViewModel(StressService(stress: false));
            const string warning = "Stemmen din viser tegn til anstrengelse — ta en pause.";

            vm.HasHealthWarning = true;
            vm.HealthWarningText = warning;

            Assert.True(vm.HasHealthWarning);
            Assert.Equal(warning, vm.HealthWarningText);
            Assert.False(string.IsNullOrWhiteSpace(vm.HealthWarningText));
            // Uendret bakoverkompatibel rød alarm.
            Assert.Equal("ErrorBrush", vm.HealthWarningBrushKey);
        }

        // ── ReducedVisualFeedback: sekundære badges dempes, helse-info beholdes ──────

        [Fact]
        public void ShowSecondaryBadges_ReducedVisualOn_HidesSecondaryButKeepsHealthWarning()
        {
            var vm = CreateViewModel(StressService(reducedVisual: true));

            const string warning = "Helsevarsel som alltid skal vises.";
            vm.HasHealthWarning = true;
            vm.HealthWarningText = warning;

            // Sekundære (ikke-kritiske) badges skjules...
            Assert.True(vm.IsReducedVisual);
            Assert.False(vm.ShowSecondaryBadges);

            // ...men helsevarselet (safety/health) består fullt ut.
            Assert.True(vm.HasHealthWarning);
            Assert.Equal(warning, vm.HealthWarningText);
        }

        [Fact]
        public void ShowSecondaryBadges_ReducedVisualOff_ShowsSecondary()
        {
            var vm = CreateViewModel(StressService(reducedVisual: false));

            Assert.False(vm.IsReducedVisual);
            Assert.True(vm.ShowSecondaryBadges);
        }

        [Fact]
        public void ShowSecondaryBadges_NoService_ShowsSecondary_BackwardCompatible()
        {
            var vm = CreateViewModel(stress: null);

            Assert.False(vm.IsReducedVisual);
            Assert.True(vm.ShowSecondaryBadges);
        }

        // ── Kombinert: stress + reduced er ortogonale flagg, begge respekteres ───────

        [Fact]
        public void StressAndReducedVisual_BothOn_SoftenColorAndHideSecondary_ButKeepHealthText()
        {
            var vm = CreateViewModel(StressService(stress: true, reducedVisual: true));

            const string warning = "Ta en pause — stemmen trenger hvile.";
            vm.HasHealthWarning = true;
            vm.HealthWarningText = warning;

            // Farge/severity dempet:
            Assert.Equal("WarningBrush", vm.HealthWarningBrushKey);
            Assert.Equal(MessageSeverity.Suggestion, vm.HealthWarningSeverity);
            // Sekundære badges skjult:
            Assert.False(vm.ShowSecondaryBadges);
            // Men helsevarsel-INNHOLDET er uberørt (Safety > UI):
            Assert.True(vm.HasHealthWarning);
            Assert.Equal(warning, vm.HealthWarningText);
        }
    }
}
