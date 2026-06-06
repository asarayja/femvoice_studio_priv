using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Zone change action types for the comfort zone controller.
    /// </summary>
    public enum ZoneChangeAction
    {
        /// <summary>Maintain current zone boundaries.</summary>
        Maintain,
        
        /// <summary>Expand zone boundaries (stable performance).</summary>
        Expand,
        
        /// <summary>Limited expansion due to rapid score increase.</summary>
        LimitedExpansion,
        
        /// <summary>Contract zone due to health/stability concerns.</summary>
        Contract,
        
        /// <summary>Freeze expansion but maintain current zone.</summary>
        Freeze,
        
        /// <summary>Lock zone due to safety concerns.</summary>
        Lock
    }

    /// <summary>
    /// Request object for zone changes.
    /// </summary>
    public class ZoneChangeRequest
    {
        public ZoneChangeAction Action { get; set; } = ZoneChangeAction.Maintain;
        public string Reason { get; set; } = string.Empty;
        public double ExpansionAmount { get; set; }
        public int ConsecutiveStableDays { get; set; }
    }

    /// <summary>
    /// Configuration for zone controller behavior.
    /// </summary>
    public class ZoneConfiguration
    {
        public double MinPitch { get; set; } = ComfortZoneController.DefaultMinPitch;
        public double MaxPitch { get; set; } = ComfortZoneController.DefaultMaxPitch;
        public double ZoneWidth { get; set; } = ComfortZoneController.DefaultZoneWidth;
        public int RequiredStableDays { get; set; } = ComfortZoneController.RequiredStableDaysForExpansion;
        public double MaxWeeklyExpansion { get; set; } = ComfortZoneController.MaxWeeklyExpansionRate;
        public int SafetyLockDays { get; set; } = ComfortZoneController.SafetyLockDurationDays;
        public double HealthThreshold { get; set; } = ComfortZoneController.HealthScoreContractionThreshold;
        public double StabilityThreshold { get; set; } = ComfortZoneController.StabilityThresholdForExpansion;
    }
}
