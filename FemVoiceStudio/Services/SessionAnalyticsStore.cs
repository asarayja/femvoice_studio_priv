using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace FemVoiceStudio.Services
{
    public enum HealthAnalyticsEventType
    {
        SafetyFreeze,
        StrainPeriod,
        PauseRecommended,
        HydrationSuggested,
        HealthTrendUpdated,

        /// <summary>A session with repeated comfort-zone breaches (journaled once per session).</summary>
        ComfortZoneBreach
    }

    public sealed record SessionAnalyticsRecord
    {
        public int SessionId { get; init; }
        public int UserId { get; init; } = 1;
        public DateTime StartedAt { get; init; }
        public DateTime? EndedAt { get; init; }
        public int ExerciseCount { get; init; }
        public double AverageResonance { get; init; }
        public double AverageStability { get; init; }
        public double AveragePitchComfort { get; init; }
        public double AverageHealthScore { get; init; }
        public int SafetyEventsCount { get; init; }
        public int PauseRecommendationsCount { get; init; }
        public int HydrationSuggestionsCount { get; init; }
        public int FatigueIndicatorCount { get; init; }

        public TimeSpan Duration =>
            EndedAt.HasValue && EndedAt.Value >= StartedAt
                ? EndedAt.Value - StartedAt
                : TimeSpan.Zero;
    }

    public sealed record ExercisePerformanceSummary
    {
        public int SessionId { get; init; }
        public int UserId { get; init; } = 1;
        public int ExerciseId { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime? EndedAt { get; init; }
        public double HoldCompletionRate { get; init; }
        public double ResonanceQualityIndex { get; init; }
        public double StabilityConsistency { get; init; }
        public int SafetyEventsCount { get; init; }
        public int FatigueIndicators { get; init; }
        public int CoachingHintsTriggered { get; init; }
    }

    public sealed record HealthAnalyticsEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public int SessionId { get; init; }
        public int UserId { get; init; } = 1;
        public HealthAnalyticsEventType EventType { get; init; }
        public DateTime OccurredAt { get; init; }
        public double Severity { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
    }

    public sealed record DailyAnalyticsSummary
    {
        public DateOnly Day { get; init; }
        public int UserId { get; init; }
        public int SessionCount { get; init; }
        public int ExerciseCount { get; init; }
        public TimeSpan TotalDuration { get; init; }
        public double AverageResonance { get; init; }
        public double AverageStability { get; init; }
        public double AveragePitchComfort { get; init; }
        public double AverageHealthScore { get; init; }
        public double HoldCompletionRate { get; init; }
        public int SafetyEventsCount { get; init; }
        public int StrainPeriodsCount { get; init; }
        public int PauseRecommendationsCount { get; init; }
        public int HydrationSuggestionsCount { get; init; }
        public int FatigueIndicatorCount { get; init; }
    }

    public sealed record WeeklyAnalyticsTrend
    {
        public DateOnly WeekStart { get; init; }
        public int UserId { get; init; }
        public IReadOnlyList<DailyAnalyticsSummary> Days { get; init; } = Array.Empty<DailyAnalyticsSummary>();
        public double AverageResonance => Average(Days.Select(d => d.AverageResonance));
        public double AverageStability => Average(Days.Select(d => d.AverageStability));
        public double AverageHoldCompletion => Average(Days.Select(d => d.HoldCompletionRate));
        public int SafetyEventsCount => Days.Sum(d => d.SafetyEventsCount);
        public int PauseRecommendationsCount => Days.Sum(d => d.PauseRecommendationsCount);
        public int HydrationSuggestionsCount => Days.Sum(d => d.HydrationSuggestionsCount);

        private static double Average(IEnumerable<double> values)
        {
            var activeValues = values.Where(v => v > 0).ToList();
            return activeValues.Count == 0 ? 0 : activeValues.Average();
        }
    }

    public interface ISessionAnalyticsRepository
    {
        Task SaveSessionAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default);
        Task SaveExerciseSummaryAsync(ExercisePerformanceSummary summary, CancellationToken cancellationToken = default);
        Task SaveHealthEventAsync(HealthAnalyticsEvent healthEvent, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SessionAnalyticsRecord>> GetSessionsAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ExercisePerformanceSummary>> GetExerciseSummariesAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HealthAnalyticsEvent>> GetHealthEventsAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }

    public sealed class SqliteSessionAnalyticsRepository : ISessionAnalyticsRepository
    {
        private readonly string _connectionString;

        public SqliteSessionAnalyticsRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveSessionAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO SessionAnalyticsSessions (
                    SessionId, UserId, StartedAt, EndedAt, ExerciseCount,
                    AverageResonance, AverageStability, AveragePitchComfort, AverageHealthScore,
                    SafetyEventsCount, PauseRecommendationsCount, HydrationSuggestionsCount, FatigueIndicatorCount)
                VALUES (
                    @SessionId, @UserId, @StartedAt, @EndedAt, @ExerciseCount,
                    @AverageResonance, @AverageStability, @AveragePitchComfort, @AverageHealthScore,
                    @SafetyEventsCount, @PauseRecommendationsCount, @HydrationSuggestionsCount, @FatigueIndicatorCount)
                ON CONFLICT(SessionId) DO UPDATE SET
                    UserId = excluded.UserId,
                    StartedAt = excluded.StartedAt,
                    EndedAt = excluded.EndedAt,
                    ExerciseCount = excluded.ExerciseCount,
                    AverageResonance = excluded.AverageResonance,
                    AverageStability = excluded.AverageStability,
                    AveragePitchComfort = excluded.AveragePitchComfort,
                    AverageHealthScore = excluded.AverageHealthScore,
                    SafetyEventsCount = excluded.SafetyEventsCount,
                    PauseRecommendationsCount = excluded.PauseRecommendationsCount,
                    HydrationSuggestionsCount = excluded.HydrationSuggestionsCount,
                    FatigueIndicatorCount = excluded.FatigueIndicatorCount;";

            AddSessionParameters(command, session);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task SaveExerciseSummaryAsync(ExercisePerformanceSummary summary, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO SessionAnalyticsExerciseSummaries (
                    SessionId, UserId, ExerciseId, StartedAt, EndedAt,
                    HoldCompletionRate, ResonanceQualityIndex, StabilityConsistency,
                    SafetyEventsCount, FatigueIndicators, CoachingHintsTriggered)
                VALUES (
                    @SessionId, @UserId, @ExerciseId, @StartedAt, @EndedAt,
                    @HoldCompletionRate, @ResonanceQualityIndex, @StabilityConsistency,
                    @SafetyEventsCount, @FatigueIndicators, @CoachingHintsTriggered)
                ON CONFLICT(SessionId, ExerciseId) DO UPDATE SET
                    UserId = excluded.UserId,
                    StartedAt = excluded.StartedAt,
                    EndedAt = excluded.EndedAt,
                    HoldCompletionRate = excluded.HoldCompletionRate,
                    ResonanceQualityIndex = excluded.ResonanceQualityIndex,
                    StabilityConsistency = excluded.StabilityConsistency,
                    SafetyEventsCount = excluded.SafetyEventsCount,
                    FatigueIndicators = excluded.FatigueIndicators,
                    CoachingHintsTriggered = excluded.CoachingHintsTriggered;";

            AddExerciseParameters(command, summary);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task SaveHealthEventAsync(HealthAnalyticsEvent healthEvent, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO SessionAnalyticsHealthEvents (
                    EventId, SessionId, UserId, EventType, OccurredAt, Severity, ReasonCode)
                VALUES (
                    @EventId, @SessionId, @UserId, @EventType, @OccurredAt, @Severity, @ReasonCode)
                ON CONFLICT(EventId) DO UPDATE SET
                    SessionId = excluded.SessionId,
                    UserId = excluded.UserId,
                    EventType = excluded.EventType,
                    OccurredAt = excluded.OccurredAt,
                    Severity = excluded.Severity,
                    ReasonCode = excluded.ReasonCode;";

            command.Parameters.AddWithValue("@EventId", healthEvent.EventId.ToString("D"));
            command.Parameters.AddWithValue("@SessionId", healthEvent.SessionId);
            command.Parameters.AddWithValue("@UserId", healthEvent.UserId);
            command.Parameters.AddWithValue("@EventType", (int)healthEvent.EventType);
            command.Parameters.AddWithValue("@OccurredAt", healthEvent.OccurredAt.ToString("o"));
            command.Parameters.AddWithValue("@Severity", healthEvent.Severity);
            command.Parameters.AddWithValue("@ReasonCode", healthEvent.ReasonCode);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SessionAnalyticsRecord>> GetSessionsAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SessionAnalyticsSessions
                WHERE UserId = @UserId AND StartedAt >= @From AND StartedAt < @To
                ORDER BY StartedAt;";
            AddRangeParameters(command, userId, from, to);

            var sessions = new List<SessionAnalyticsRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sessions.Add(MapSession(reader));
            }

            return sessions;
        }

        public async Task<IReadOnlyList<ExercisePerformanceSummary>> GetExerciseSummariesAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SessionAnalyticsExerciseSummaries
                WHERE UserId = @UserId AND StartedAt >= @From AND StartedAt < @To
                ORDER BY StartedAt, ExerciseId;";
            AddRangeParameters(command, userId, from, to);

            var summaries = new List<ExercisePerformanceSummary>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                summaries.Add(MapExercise(reader));
            }

            return summaries;
        }

        public async Task<IReadOnlyList<HealthAnalyticsEvent>> GetHealthEventsAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SessionAnalyticsHealthEvents
                WHERE UserId = @UserId AND OccurredAt >= @From AND OccurredAt < @To
                ORDER BY OccurredAt;";
            AddRangeParameters(command, userId, from, to);

            var events = new List<HealthAnalyticsEvent>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(MapHealthEvent(reader));
            }

            return events;
        }

        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SessionAnalyticsSessions (
                    SessionId INTEGER PRIMARY KEY,
                    UserId INTEGER NOT NULL DEFAULT 1,
                    StartedAt TEXT NOT NULL,
                    EndedAt TEXT,
                    ExerciseCount INTEGER NOT NULL DEFAULT 0,
                    AverageResonance REAL NOT NULL DEFAULT 0,
                    AverageStability REAL NOT NULL DEFAULT 0,
                    AveragePitchComfort REAL NOT NULL DEFAULT 0,
                    AverageHealthScore REAL NOT NULL DEFAULT 0,
                    SafetyEventsCount INTEGER NOT NULL DEFAULT 0,
                    PauseRecommendationsCount INTEGER NOT NULL DEFAULT 0,
                    HydrationSuggestionsCount INTEGER NOT NULL DEFAULT 0,
                    FatigueIndicatorCount INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS SessionAnalyticsExerciseSummaries (
                    SessionId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL DEFAULT 1,
                    ExerciseId INTEGER NOT NULL,
                    StartedAt TEXT NOT NULL,
                    EndedAt TEXT,
                    HoldCompletionRate REAL NOT NULL DEFAULT 0,
                    ResonanceQualityIndex REAL NOT NULL DEFAULT 0,
                    StabilityConsistency REAL NOT NULL DEFAULT 0,
                    SafetyEventsCount INTEGER NOT NULL DEFAULT 0,
                    FatigueIndicators INTEGER NOT NULL DEFAULT 0,
                    CoachingHintsTriggered INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (SessionId, ExerciseId)
                );

                CREATE TABLE IF NOT EXISTS SessionAnalyticsHealthEvents (
                    EventId TEXT PRIMARY KEY,
                    SessionId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL DEFAULT 1,
                    EventType INTEGER NOT NULL,
                    OccurredAt TEXT NOT NULL,
                    Severity REAL NOT NULL DEFAULT 0,
                    ReasonCode TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS idx_sessionanalytics_sessions_user_started
                    ON SessionAnalyticsSessions(UserId, StartedAt);
                CREATE INDEX IF NOT EXISTS idx_sessionanalytics_exercises_user_started
                    ON SessionAnalyticsExerciseSummaries(UserId, StartedAt);
                CREATE INDEX IF NOT EXISTS idx_sessionanalytics_events_user_occurred
                    ON SessionAnalyticsHealthEvents(UserId, OccurredAt);";
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private static void AddRangeParameters(SqliteCommand command, int userId, DateTime from, DateTime to)
        {
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));
        }

        private static void AddSessionParameters(SqliteCommand command, SessionAnalyticsRecord session)
        {
            command.Parameters.AddWithValue("@SessionId", session.SessionId);
            command.Parameters.AddWithValue("@UserId", session.UserId);
            command.Parameters.AddWithValue("@StartedAt", session.StartedAt.ToString("o"));
            command.Parameters.AddWithValue("@EndedAt", session.EndedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ExerciseCount", session.ExerciseCount);
            command.Parameters.AddWithValue("@AverageResonance", session.AverageResonance);
            command.Parameters.AddWithValue("@AverageStability", session.AverageStability);
            command.Parameters.AddWithValue("@AveragePitchComfort", session.AveragePitchComfort);
            command.Parameters.AddWithValue("@AverageHealthScore", session.AverageHealthScore);
            command.Parameters.AddWithValue("@SafetyEventsCount", session.SafetyEventsCount);
            command.Parameters.AddWithValue("@PauseRecommendationsCount", session.PauseRecommendationsCount);
            command.Parameters.AddWithValue("@HydrationSuggestionsCount", session.HydrationSuggestionsCount);
            command.Parameters.AddWithValue("@FatigueIndicatorCount", session.FatigueIndicatorCount);
        }

        private static void AddExerciseParameters(SqliteCommand command, ExercisePerformanceSummary summary)
        {
            command.Parameters.AddWithValue("@SessionId", summary.SessionId);
            command.Parameters.AddWithValue("@UserId", summary.UserId);
            command.Parameters.AddWithValue("@ExerciseId", summary.ExerciseId);
            command.Parameters.AddWithValue("@StartedAt", summary.StartedAt.ToString("o"));
            command.Parameters.AddWithValue("@EndedAt", summary.EndedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@HoldCompletionRate", summary.HoldCompletionRate);
            command.Parameters.AddWithValue("@ResonanceQualityIndex", summary.ResonanceQualityIndex);
            command.Parameters.AddWithValue("@StabilityConsistency", summary.StabilityConsistency);
            command.Parameters.AddWithValue("@SafetyEventsCount", summary.SafetyEventsCount);
            command.Parameters.AddWithValue("@FatigueIndicators", summary.FatigueIndicators);
            command.Parameters.AddWithValue("@CoachingHintsTriggered", summary.CoachingHintsTriggered);
        }

        private static SessionAnalyticsRecord MapSession(SqliteDataReader reader)
        {
            return new SessionAnalyticsRecord
            {
                SessionId = ReadInt(reader, "SessionId"),
                UserId = ReadInt(reader, "UserId"),
                StartedAt = ReadDateTime(reader, "StartedAt"),
                EndedAt = ReadNullableDateTime(reader, "EndedAt"),
                ExerciseCount = ReadInt(reader, "ExerciseCount"),
                AverageResonance = ReadDouble(reader, "AverageResonance"),
                AverageStability = ReadDouble(reader, "AverageStability"),
                AveragePitchComfort = ReadDouble(reader, "AveragePitchComfort"),
                AverageHealthScore = ReadDouble(reader, "AverageHealthScore"),
                SafetyEventsCount = ReadInt(reader, "SafetyEventsCount"),
                PauseRecommendationsCount = ReadInt(reader, "PauseRecommendationsCount"),
                HydrationSuggestionsCount = ReadInt(reader, "HydrationSuggestionsCount"),
                FatigueIndicatorCount = ReadInt(reader, "FatigueIndicatorCount")
            };
        }

        private static ExercisePerformanceSummary MapExercise(SqliteDataReader reader)
        {
            return new ExercisePerformanceSummary
            {
                SessionId = ReadInt(reader, "SessionId"),
                UserId = ReadInt(reader, "UserId"),
                ExerciseId = ReadInt(reader, "ExerciseId"),
                StartedAt = ReadDateTime(reader, "StartedAt"),
                EndedAt = ReadNullableDateTime(reader, "EndedAt"),
                HoldCompletionRate = ReadDouble(reader, "HoldCompletionRate"),
                ResonanceQualityIndex = ReadDouble(reader, "ResonanceQualityIndex"),
                StabilityConsistency = ReadDouble(reader, "StabilityConsistency"),
                SafetyEventsCount = ReadInt(reader, "SafetyEventsCount"),
                FatigueIndicators = ReadInt(reader, "FatigueIndicators"),
                CoachingHintsTriggered = ReadInt(reader, "CoachingHintsTriggered")
            };
        }

        private static HealthAnalyticsEvent MapHealthEvent(SqliteDataReader reader)
        {
            return new HealthAnalyticsEvent
            {
                EventId = Guid.Parse(reader.GetString(reader.GetOrdinal("EventId"))),
                SessionId = ReadInt(reader, "SessionId"),
                UserId = ReadInt(reader, "UserId"),
                EventType = (HealthAnalyticsEventType)ReadInt(reader, "EventType"),
                OccurredAt = ReadDateTime(reader, "OccurredAt"),
                Severity = ReadDouble(reader, "Severity"),
                ReasonCode = reader.GetString(reader.GetOrdinal("ReasonCode"))
            };
        }

        private static int ReadInt(SqliteDataReader reader, string column)
            => reader.GetInt32(reader.GetOrdinal(column));

        private static double ReadDouble(SqliteDataReader reader, string column)
            => reader.GetDouble(reader.GetOrdinal(column));

        private static DateTime ReadDateTime(SqliteDataReader reader, string column)
            => DateTime.Parse(reader.GetString(reader.GetOrdinal(column)), null, System.Globalization.DateTimeStyles.RoundtripKind);

        private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal)
                ? null
                : DateTime.Parse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
    }

    public sealed class InMemorySessionAnalyticsRepository : ISessionAnalyticsRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, SessionAnalyticsRecord> _sessions = new();
        private readonly Dictionary<(int SessionId, int ExerciseId), ExercisePerformanceSummary> _exerciseSummaries = new();
        private readonly Dictionary<Guid, HealthAnalyticsEvent> _healthEvents = new();

        public Task SaveSessionAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                _sessions[session.SessionId] = session;
            }

            return Task.CompletedTask;
        }

        public Task SaveExerciseSummaryAsync(ExercisePerformanceSummary summary, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                _exerciseSummaries[(summary.SessionId, summary.ExerciseId)] = summary;
            }

            return Task.CompletedTask;
        }

        public Task SaveHealthEventAsync(HealthAnalyticsEvent healthEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                _healthEvents[healthEvent.EventId] = healthEvent;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionAnalyticsRecord>> GetSessionsAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                var result = _sessions.Values
                    .Where(s => s.UserId == userId && s.StartedAt >= from && s.StartedAt < to)
                    .OrderBy(s => s.StartedAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<SessionAnalyticsRecord>>(result);
            }
        }

        public Task<IReadOnlyList<ExercisePerformanceSummary>> GetExerciseSummariesAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                var result = _exerciseSummaries.Values
                    .Where(s => s.UserId == userId && s.StartedAt >= from && s.StartedAt < to)
                    .OrderBy(s => s.StartedAt)
                    .ThenBy(s => s.ExerciseId)
                    .ToList();
                return Task.FromResult<IReadOnlyList<ExercisePerformanceSummary>>(result);
            }
        }

        public Task<IReadOnlyList<HealthAnalyticsEvent>> GetHealthEventsAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                var result = _healthEvents.Values
                    .Where(e => e.UserId == userId && e.OccurredAt >= from && e.OccurredAt < to)
                    .OrderBy(e => e.OccurredAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<HealthAnalyticsEvent>>(result);
            }
        }
    }

    public sealed class SessionAnalyticsStore
    {
        private readonly ISessionAnalyticsRepository _repository;

        public SessionAnalyticsStore(ISessionAnalyticsRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public Task RecordSessionStartedAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default)
        {
            return _repository.SaveSessionAsync(Normalize(session with
            {
                EndedAt = null,
                ExerciseCount = 0,
                AverageResonance = 0,
                AverageStability = 0,
                AveragePitchComfort = 0,
                AverageHealthScore = 0,
                SafetyEventsCount = 0,
                PauseRecommendationsCount = 0,
                HydrationSuggestionsCount = 0,
                FatigueIndicatorCount = 0
            }), cancellationToken);
        }

        public Task RecordSessionCompletedAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default)
        {
            return _repository.SaveSessionAsync(Normalize(session), cancellationToken);
        }

        public Task RecordExercisePerformanceAsync(ExercisePerformanceSummary summary, CancellationToken cancellationToken = default)
        {
            return _repository.SaveExerciseSummaryAsync(Normalize(summary), cancellationToken);
        }

        public Task RecordHealthEventAsync(HealthAnalyticsEvent healthEvent, CancellationToken cancellationToken = default)
        {
            return _repository.SaveHealthEventAsync(Normalize(healthEvent), cancellationToken);
        }

        public async Task<DailyAnalyticsSummary> GetDailySummaryAsync(
            DateOnly day,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var from = day.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);
            var sessions = await _repository.GetSessionsAsync(userId, from, to, cancellationToken);
            var exercises = await _repository.GetExerciseSummariesAsync(userId, from, to, cancellationToken);
            var events = await _repository.GetHealthEventsAsync(userId, from, to, cancellationToken);
            var sessionSafetyCount = sessions.Sum(s => s.SafetyEventsCount);
            var eventSafetyCount = events.Count(e => e.EventType == HealthAnalyticsEventType.SafetyFreeze);
            var sessionPauseCount = sessions.Sum(s => s.PauseRecommendationsCount);
            var eventPauseCount = events.Count(e => e.EventType == HealthAnalyticsEventType.PauseRecommended);
            var sessionHydrationCount = sessions.Sum(s => s.HydrationSuggestionsCount);
            var eventHydrationCount = events.Count(e => e.EventType == HealthAnalyticsEventType.HydrationSuggested);

            return new DailyAnalyticsSummary
            {
                Day = day,
                UserId = userId,
                SessionCount = sessions.Count,
                ExerciseCount = sessions.Sum(s => Math.Max(0, s.ExerciseCount)),
                TotalDuration = TimeSpan.FromSeconds(sessions.Sum(s => s.Duration.TotalSeconds)),
                AverageResonance = Average(sessions.Select(s => s.AverageResonance)),
                AverageStability = Average(sessions.Select(s => s.AverageStability)),
                AveragePitchComfort = Average(sessions.Select(s => s.AveragePitchComfort)),
                AverageHealthScore = Average(sessions.Select(s => s.AverageHealthScore)),
                HoldCompletionRate = Average(exercises.Select(e => e.HoldCompletionRate)),
                SafetyEventsCount = Math.Max(sessionSafetyCount, eventSafetyCount),
                StrainPeriodsCount = events.Count(e => e.EventType == HealthAnalyticsEventType.StrainPeriod),
                PauseRecommendationsCount = Math.Max(sessionPauseCount, eventPauseCount),
                HydrationSuggestionsCount = Math.Max(sessionHydrationCount, eventHydrationCount),
                FatigueIndicatorCount = sessions.Sum(s => s.FatigueIndicatorCount) + exercises.Sum(e => e.FatigueIndicators)
            };
        }

        public async Task<WeeklyAnalyticsTrend> GetWeeklyTrendAsync(
            DateOnly weekStart,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var days = new List<DailyAnalyticsSummary>(7);
            for (var i = 0; i < 7; i++)
            {
                days.Add(await GetDailySummaryAsync(weekStart.AddDays(i), userId, cancellationToken));
            }

            return new WeeklyAnalyticsTrend
            {
                WeekStart = weekStart,
                UserId = userId,
                Days = days
            };
        }

        public async Task<IReadOnlyList<ExercisePerformanceSummary>> GetExerciseTrendAsync(
            int exerciseId,
            DateTime from,
            DateTime to,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var summaries = await _repository.GetExerciseSummariesAsync(userId, from, to, cancellationToken);
            return summaries
                .Where(s => s.ExerciseId == exerciseId)
                .OrderBy(s => s.StartedAt)
                .ToList();
        }

        public async Task<IReadOnlyList<ExercisePerformanceSummary>> GetExerciseSummariesAsync(
            DateTime from,
            DateTime to,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var summaries = await _repository.GetExerciseSummariesAsync(userId, from, to, cancellationToken);
            return summaries
                .OrderBy(s => s.StartedAt)
                .ThenBy(s => s.ExerciseId)
                .ToList();
        }

        public async Task<IReadOnlyList<HealthAnalyticsEvent>> GetHealthEventsAsync(
            DateTime from,
            DateTime to,
            int userId = 1,
            CancellationToken cancellationToken = default)
        {
            var events = await _repository.GetHealthEventsAsync(userId, from, to, cancellationToken);
            return events
                .OrderBy(e => e.OccurredAt)
                .ToList();
        }

        private static SessionAnalyticsRecord Normalize(SessionAnalyticsRecord session)
        {
            return session with
            {
                AverageResonance = Clamp01(session.AverageResonance),
                AverageStability = Clamp01(session.AverageStability),
                AveragePitchComfort = Clamp01(session.AveragePitchComfort),
                AverageHealthScore = Clamp01(session.AverageHealthScore),
                ExerciseCount = Math.Max(0, session.ExerciseCount),
                SafetyEventsCount = Math.Max(0, session.SafetyEventsCount),
                PauseRecommendationsCount = Math.Max(0, session.PauseRecommendationsCount),
                HydrationSuggestionsCount = Math.Max(0, session.HydrationSuggestionsCount),
                FatigueIndicatorCount = Math.Max(0, session.FatigueIndicatorCount)
            };
        }

        private static ExercisePerformanceSummary Normalize(ExercisePerformanceSummary summary)
        {
            return summary with
            {
                HoldCompletionRate = Clamp01(summary.HoldCompletionRate),
                ResonanceQualityIndex = Clamp01(summary.ResonanceQualityIndex),
                StabilityConsistency = Clamp01(summary.StabilityConsistency),
                SafetyEventsCount = Math.Max(0, summary.SafetyEventsCount),
                FatigueIndicators = Math.Max(0, summary.FatigueIndicators),
                CoachingHintsTriggered = Math.Max(0, summary.CoachingHintsTriggered)
            };
        }

        private static HealthAnalyticsEvent Normalize(HealthAnalyticsEvent healthEvent)
        {
            return healthEvent with
            {
                EventId = healthEvent.EventId == Guid.Empty ? Guid.NewGuid() : healthEvent.EventId,
                Severity = Clamp01(healthEvent.Severity),
                ReasonCode = healthEvent.ReasonCode ?? string.Empty
            };
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            return Math.Clamp(value, 0, 1);
        }

        private static double Average(IEnumerable<double> values)
        {
            var normalizedValues = values.Where(v => v > 0).ToList();
            return normalizedValues.Count == 0 ? 0 : normalizedValues.Average();
        }
    }
}
