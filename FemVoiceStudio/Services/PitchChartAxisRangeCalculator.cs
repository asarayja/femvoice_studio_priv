using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Services
{
    public readonly record struct PitchChartAxisRange(double Minimum, double Maximum);

    public static class PitchChartAxisRangeCalculator
    {
        public static PitchChartAxisRange Calculate(
            IEnumerable<double> visiblePitches,
            double targetMin,
            double targetMax,
            double absoluteMin = 60,
            double absoluteMax = 500,
            double minimumRange = 50,
            double padding = 25)
        {
            var values = visiblePitches
                .Where(value => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
                .Concat(new[] { targetMin, targetMax })
                .ToArray();

            var rawMin = values.Min();
            var rawMax = values.Max();
            var min = Math.Max(absoluteMin, rawMin - padding);
            var max = Math.Min(absoluteMax, rawMax + padding);

            if (max - min < minimumRange)
            {
                var center = (min + max) / 2.0;
                min = center - minimumRange / 2.0;
                max = center + minimumRange / 2.0;
            }

            if (min < absoluteMin)
            {
                max = Math.Min(absoluteMax, max + (absoluteMin - min));
                min = absoluteMin;
            }

            if (max > absoluteMax)
            {
                min = Math.Max(absoluteMin, min - (max - absoluteMax));
                max = absoluteMax;
            }

            return new PitchChartAxisRange(min, max);
        }
    }
}
