using System;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Represents the current state of the user's comfort zone for pitch training.
    /// This is a immutable snapshot used for zone management and UI updates.
    /// </summary>
    public sealed class ComfortZoneState
    {
        /// <summary>
        /// The minimum pitch in Hz for the current training zone.
        /// </summary>
        public double MinPitch { get; init; }

        /// <summary>
        /// The maximum pitch in Hz for the current training zone.
        /// </summary>
        public double MaxPitch { get; init; }

        /// <summary>
        /// The width of the zone in Hz (MaxPitch - MinPitch).
        /// </summary>
        public double ZoneWidth { get; init; }

        /// <summary>
        /// The optimal/center pitch within the zone.
        /// </summary>
        public double OptimalPitch { get; init; }

        /// <summary>
        /// Whether zone expansion is currently allowed based on user's stability.
        /// </summary>
        public bool IsExpansionAllowed { get; init; }

        /// <summary>
        /// Whether the zone is locked due to safety concerns (strain incident, health issues).
        /// </summary>
        public bool IsSafetyLocked { get; init; }

        /// <summary>
        /// Human-readable explanation of the current zone state.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        /// Timestamp when this state was calculated.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// User ID this zone state belongs to.
        /// </summary>
        public int UserId { get; init; }

        /// <summary>
        /// Days remaining in safety lock (0 if not locked).
        /// </summary>
        public int SafetyLockDaysRemaining { get; init; }

        /// <summary>
        /// Number of consecutive stable days contributing to potential expansion.
        /// </summary>
        public int ConsecutiveStableDays { get; init; }

        /// <summary>
        /// Creates the default comfort zone for new users.
        /// </summary>
        public static ComfortZoneState Default => new()
        {
            MinPitch = 165,
            MaxPitch = 255,
            ZoneWidth = 90,
            OptimalPitch = 210,
            IsExpansionAllowed = false,
            IsSafetyLocked = false,
            Reason = "New user - default zone",
            Timestamp = DateTime.Now,
            UserId = 1,
            SafetyLockDaysRemaining = 0,
            ConsecutiveStableDays = 0
        };

        /// <summary>
        /// Creates an empty/initial state.
        /// </summary>
        public static ComfortZoneState Empty => new()
        {
            MinPitch = 0,
            MaxPitch = 0,
            ZoneWidth = 0,
            OptimalPitch = 0,
            IsExpansionAllowed = false,
            IsSafetyLocked = false,
            Reason = "Not initialized",
            Timestamp = DateTime.MinValue,
            UserId = 0,
            SafetyLockDaysRemaining = 0,
            ConsecutiveStableDays = 0
        };
    }
}
