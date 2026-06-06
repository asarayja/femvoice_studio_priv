using FemVoiceStudio.Models;
using Range = FemVoiceStudio.Models.Range;

namespace FemVoiceStudio.Services
{
    public static class PitchTargetZonePolicy
    {
        private const double AbsoluteMinimum = 150;
        private const double AbsoluteMaximum = 240;

        public static Range ForDifficulty(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.Nybegynner => new Range(160, 230, 195),
                DifficultyLevel.Middels => new Range(165, 230, 197.5),
                DifficultyLevel.Avansert => new Range(175, 240, 207.5),
                _ => new Range(160, 230, 195)
            };
        }

        public static Range ClampForDifficulty(DifficultyLevel difficulty, double requestedMin, double requestedMax)
        {
            var levelZone = ForDifficulty(difficulty);
            var min = Math.Clamp(requestedMin, AbsoluteMinimum, levelZone.Max);
            var max = Math.Clamp(requestedMax, min + 20, levelZone.Max);

            if (min < levelZone.Min - 10)
                min = levelZone.Min - 10;

            max = Math.Min(max, AbsoluteMaximum);
            if (max - min > 70)
                min = max - 70;

            return new Range(min, max, (min + max) / 2.0);
        }
    }
}
