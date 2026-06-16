using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Lightweight descriptor for a named indicator package used by the UI.
    /// Carries a resource summary key and the typed set of indicators included.
    /// </summary>
    public sealed class IndicatorPackage
    {
        public string? SummaryKey { get; }
        public IReadOnlyList<IndicatorType> Indicators { get; }

        public IndicatorPackage(string? summaryKey, IReadOnlyList<IndicatorType>? indicators)
        {
            SummaryKey = summaryKey;
            Indicators = indicators ?? Array.Empty<IndicatorType>();
        }
    }
}
