using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Ønsket stilmål for stemmen. Komplementerer VoiceGoalProfile.GoalStyleKey
    /// (som lever videre uendret) med en typestrek enum som SmartCoach/adaptasjon
    /// kan resonnere over uten å parse rå strenger.
    /// </summary>
    public enum VoiceStyleGoal
    {
        Feminine,
        Androgynous,
        DarkFeminine,
        Situational,
        Custom
    }

    /// <summary>
    /// Personlig brukerprofil med baselines, preferanser og tilgjengelighetsvalg.
    /// KOMPLEMENTERER VoiceGoalProfile (målstil/fokus) — overlapper ikke; denne
    /// modellen eier kalibrerte baselines, komfortsone og accessibility-flagg.
    ///
    /// Klinisk sikkerhetshierarki gjelder: baselines/komfortsone er beskrivende
    /// data, ikke mål som kan overstyre helse/sikkerhet.
    /// </summary>
    public sealed class UserVoiceProfile
    {
        public int UserId { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>Kalibrert baseline-pitch (rå Hz lagres internt, ALDRI eksponert mot bruker).</summary>
        public double BaselinePitch { get; set; }

        /// <summary>Baseline-resonans, normalisert 0-1.</summary>
        public double BaselineResonance { get; set; }

        /// <summary>Baseline-komfort, normalisert 0-1.</summary>
        public double BaselineComfort { get; set; }

        /// <summary>Baseline-helse, 0-100.</summary>
        public double BaselineHealth { get; set; }

        /// <summary>Ønsket stilmål. Mappes fra/til VoiceGoalProfile.GoalStyleKey via hjelpemetodene under.</summary>
        public VoiceStyleGoal PreferredVoiceStyle { get; set; } = VoiceStyleGoal.Feminine;

        /// <summary>Antall treningsøkter per uke brukeren sikter mot.</summary>
        public int TrainingFrequencyPerWeek { get; set; } = 3;

        /// <summary>Personlig komfortsone — nedre grense. null = ikke kalibrert ennå.</summary>
        public double? ComfortZoneMinPitch { get; set; }

        /// <summary>Personlig komfortsone — øvre grense. null = ikke kalibrert ennå.</summary>
        public double? ComfortZoneMaxPitch { get; set; }

        /// <summary>Personlig komfortsone — optimalt punkt. null = ikke kalibrert ennå.</summary>
        public double? ComfortZoneOptimalPitch { get; set; }

        /// <summary>Roligere, mindre pågående coaching ved stress/overveldelse.</summary>
        public bool StressSensitiveMode { get; set; } = false;

        /// <summary>Redusert visuell feedback for brukere som blir distrahert/overstimulert.</summary>
        public bool ReducedVisualFeedback { get; set; } = false;

        /// <summary>
        /// Mapper en VoiceGoalProfile.GoalStyleKey-streng til VoiceStyleGoal.
        /// Ukjente/tomme nøkler faller tilbake til Custom (trygt standardvalg).
        /// </summary>
        public static VoiceStyleGoal FromGoalStyleKey(string? goalStyleKey)
        {
            if (string.IsNullOrWhiteSpace(goalStyleKey))
                return VoiceStyleGoal.Custom;

            return goalStyleKey.Trim().ToLowerInvariant() switch
            {
                "soft_feminine" => VoiceStyleGoal.Feminine,
                "androgynous"   => VoiceStyleGoal.Androgynous,
                "dark_feminine" => VoiceStyleGoal.DarkFeminine,
                "situational"   => VoiceStyleGoal.Situational,
                _               => VoiceStyleGoal.Custom
            };
        }

        /// <summary>
        /// Mapper en VoiceStyleGoal tilbake til en GoalStyleKey-streng som
        /// VoiceGoalProfile forstår. Custom har ingen kanonisk nøkkel og gir "custom".
        /// </summary>
        public static string ToGoalStyleKey(VoiceStyleGoal style)
            => style switch
            {
                VoiceStyleGoal.Feminine     => "soft_feminine",
                VoiceStyleGoal.Androgynous  => "androgynous",
                VoiceStyleGoal.DarkFeminine => "dark_feminine",
                VoiceStyleGoal.Situational  => "situational",
                _                           => "custom"
            };
    }
}
