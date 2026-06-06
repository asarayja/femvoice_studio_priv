using System;
using System.IO;
using Microsoft.Data.Sqlite;
using FemVoiceStudio.Services;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Data
{
    /// <summary>
    /// Database service for SQLite.
    /// Håndterer lagring av treningsøkter, progresjon og brukerinnstillinger.
    /// </summary>
    public class DatabaseService : IDatabaseService, IDisposable
    {
        private readonly string _connectionString;
        private SqliteConnection? _connection;
        
        // Static field to track and close shared connections
        private static SqliteConnection? _staticConnection;
        
        // Guard: InitializeDatabase() must run exactly once per instance.
        // ResetDatabase() is the only legitimate second caller — it sets this back to false first.
        private bool _initialized = false;
        
        public DatabaseService(string databasePath = "femvoice.db")
        {
            // Lag database i brukerens Documents-mappe
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
                
            Directory.CreateDirectory(appDataPath);
            var fullPath = Path.Combine(appDataPath, databasePath);
            _connectionString = $"Data Source={fullPath}";
            
            InitializeDatabase();
        }
        
        /// <summary>
        /// Connection string for database access
        /// </summary>
        public string ConnectionString => _connectionString;
        
        /// <summary>
        /// Initialiserer database og tabeller i en deterministisk rekkefølge:
        /// 1. CreateSchema     – alle CREATE TABLE-setninger, ingen INSERTs
        /// 2. RunMigrations    – RecreateTableIfIncomplete, AddColumnIfNotExists, ValidateCriticalSchema
        /// 3. SeedInitialData  – INSERT OR IGNORE, kjøres etter at tabeller er garantert ferdige
        /// 4. CreateIndexes    – alle indekser
        /// </summary>
        private void InitializeDatabase()
        {
            // Guard: prevent accidental re-entrancy. ResetDatabase() deliberately
            // sets _initialized = false before calling this, so that path still works.
            if (_initialized) return;
            _initialized = true;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            EnableForeignKeys(connection);

            CreateSchema(connection);
            RunMigrations(connection);
            SeedInitialData(connection);
            CreateIndexes(connection);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 1 – Schema: alle CREATE TABLE-setninger, ingen INSERTs
        // ─────────────────────────────────────────────────────────────
        private void CreateSchema(SqliteConnection connection)
        {
            var schemaBatch = @"
                CREATE TABLE IF NOT EXISTS UserSettings (
                    Id INTEGER PRIMARY KEY DEFAULT 1,
                    CurrentDifficulty INTEGER DEFAULT 1,
                    PreferredMinPitch REAL DEFAULT 165,
                    PreferredMaxPitch REAL DEFAULT 255,
                    AveragePitchLast7Days REAL DEFAULT 0,
                    ConsistencyScore REAL DEFAULT 0,
                    TotalSessionsCompleted INTEGER DEFAULT 0,
                    CurrentStreak INTEGER DEFAULT 0,
                    LastSessionDate TEXT,
                    SessionsAtCurrentLevel INTEGER DEFAULT 0,
                    VolumeThreshold REAL DEFAULT 0.01,
                    AutoAdvanceLevel INTEGER DEFAULT 1,
                    HearOwnVoice INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS TrainingSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT,
                    ExerciseTextId INTEGER,
                    AveragePitch REAL,
                    MinPitch REAL,
                    MaxPitch REAL,
                    PitchVariation REAL,
                    IntonationScore REAL,
                    OverallScore REAL,
                    Feedback TEXT,
                    DifficultyLevel INTEGER,
                    ResonanceScore REAL DEFAULT 0,
                    AverageF1 REAL DEFAULT 0,
                    AverageF2 REAL DEFAULT 0,
                    AverageF3 REAL DEFAULT 0,
                    ResonanceCategory INTEGER DEFAULT 0,
                    SpectralCentroid REAL DEFAULT 0,
                    IsRecoveryPractice INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS ProgressionHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    DifficultyLevel INTEGER,
                    AveragePitch REAL,
                    SessionCount INTEGER,
                    BestScore REAL
                );

                CREATE TABLE IF NOT EXISTS Achievements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT UNIQUE NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Icon TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    XPReward INTEGER DEFAULT 0,
                    IsUnlocked INTEGER DEFAULT 0,
                    UnlockedAt TEXT,
                    UserId INTEGER DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS DailyStreaks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL UNIQUE,
                    SessionsCompleted INTEGER DEFAULT 0,
                    TotalMinutes INTEGER DEFAULT 0,
                    StreakDay INTEGER DEFAULT 0,
                    TargetMet INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS UserProgress (
                    Id INTEGER PRIMARY KEY DEFAULT 1,
                    TotalXP INTEGER DEFAULT 0,
                    Level INTEGER DEFAULT 1,
                    XPForCurrentLevel INTEGER DEFAULT 0,
                    XPToNextLevel INTEGER DEFAULT 100,
                    TotalSessions INTEGER DEFAULT 0,
                    TotalMinutes INTEGER DEFAULT 0,
                    CurrentStreak INTEGER DEFAULT 0,
                    LongestStreak INTEGER DEFAULT 0,
                    LastSessionDate TEXT
                );

                CREATE TABLE IF NOT EXISTS WeeklyGoals (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WeekStart TEXT NOT NULL,
                    TargetSessions INTEGER DEFAULT 3,
                    TargetMinutes INTEGER DEFAULT 30,
                    ActualSessions INTEGER DEFAULT 0,
                    ActualMinutes INTEGER DEFAULT 0,
                    Completed INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS CalendarData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL UNIQUE,
                    SessionsCompleted INTEGER DEFAULT 0,
                    TotalMinutes INTEGER DEFAULT 0,
                    AverageScore REAL DEFAULT 0
                );

                PRAGMA user_version = 2;

                CREATE TABLE IF NOT EXISTS FemVoiceScores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER,
                    UserId INTEGER DEFAULT 1,
                    OverallScore REAL NOT NULL,
                    ResonanceScore REAL,
                    PitchScore REAL,
                    IntonationScore REAL,
                    VoiceHealthScore REAL,
                    CalculatedAt TEXT NOT NULL,
                    WarningFlags TEXT,
                    FOREIGN KEY (SessionId) REFERENCES TrainingSessions(Id)
                );

                CREATE TABLE IF NOT EXISTS TrainingLevels (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Level INTEGER NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT,
                    UpgradeReason TEXT,
                    DowngradeReason TEXT
                );

                CREATE TABLE IF NOT EXISTS DirectionRecommendations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Timestamp TEXT NOT NULL,
                    Parameter TEXT NOT NULL,
                    Direction INTEGER NOT NULL,
                    CurrentValue REAL,
                    TargetValue REAL,
                    ChangeAmount REAL,
                    Reason TEXT,
                    SafetyNote TEXT
                );

                CREATE TABLE IF NOT EXISTS VoiceProfiles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1 UNIQUE,
                    CreatedAt TEXT NOT NULL,
                    LastUpdated TEXT NOT NULL,
                    BaselinePitch REAL,
                    BaselineF1 REAL,
                    BaselineF2 REAL,
                    TargetMinPitch REAL DEFAULT 165,
                    TargetMaxPitch REAL DEFAULT 255,
                    TargetMinF1 REAL DEFAULT 400,
                    TargetMinF2 REAL DEFAULT 1400,
                    CurrentLevel INTEGER DEFAULT 1,
                    LastLevelChange TEXT,
                    OptimalSessionMinutes INTEGER DEFAULT 5,
                    WeeklyProgressionRate REAL DEFAULT 2.0,
                    ResonanceStrength REAL DEFAULT 50,
                    PitchStrength REAL DEFAULT 50,
                    IntonationStrength REAL DEFAULT 50,
                    MostImprovedParameter TEXT
                );

                CREATE TABLE IF NOT EXISTS ExerciseEffectiveness (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    ExerciseType TEXT NOT NULL,
                    TimesCompleted INTEGER DEFAULT 0,
                    AverageScoreDelta REAL DEFAULT 0,
                    LastUsed TEXT,
                    WinRate REAL DEFAULT 0,
                    UNIQUE(UserId, ExerciseType)
                );

                CREATE TABLE IF NOT EXISTS DailyProgress (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Date TEXT NOT NULL,
                    FemVoiceScore REAL,
                    ResonanceScore REAL,
                    PitchScore REAL,
                    IntonationScore REAL,
                    VoiceHealthScore REAL,
                    SessionMinutes INTEGER DEFAULT 0,
                    ExerciseType TEXT,
                    UNIQUE(UserId, Date)
                );

                CREATE TABLE IF NOT EXISTS SmartCoachBaselines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    BaselinePitch REAL,
                    BaselineF1 REAL,
                    BaselineF2 REAL,
                    BaselineIntonation REAL,
                    BaselineResonanceScore REAL,
                    CalculatedAt TEXT,
                    TrainingDaysCount INTEGER DEFAULT 0,
                    ConfidenceLevel TEXT DEFAULT 'low'
                );

                CREATE TABLE IF NOT EXISTS SmartCoachWeeklyProgress (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    WeekStart TEXT NOT NULL,
                    WeekEnd TEXT NOT NULL,
                    SessionsCount INTEGER DEFAULT 0,
                    TotalMinutes INTEGER DEFAULT 0,
                    AverageScore REAL,
                    AveragePitch REAL,
                    AverageResonance REAL,
                    AverageIntonation REAL,
                    PitchChange REAL,
                    ResonanceChange REAL,
                    IntonationChange REAL,
                    HealthScore REAL DEFAULT 100,
                    Notes TEXT,
                    IsCompleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS SmartCoachGoals (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    GoalType TEXT NOT NULL,
                    TargetValue REAL,
                    CurrentValue REAL,
                    StartDate TEXT NOT NULL,
                    TargetDate TEXT,
                    -- IsAchieved/AchievedAt are the canonical columns used by all SQL in SaveSmartCoachGoal/GetSmartCoachGoals
                    IsAchieved INTEGER DEFAULT 0,
                    AchievedAt TEXT,
                    Priority INTEGER DEFAULT 1,
                    -- IsCompleted kept for migration compatibility (AddColumnIfNotExists in RunMigrations)
                    IsCompleted INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS SmartCoachDailyRecommendations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Date TEXT NOT NULL,
                    FocusArea TEXT,
                    RecommendationText TEXT,
                    RecommendedExerciseId INTEGER,
                    RecommendedDurationMinutes INTEGER DEFAULT 5,
                    -- IsCompleted and CompletedAt are required by SaveDailyRecommendation/GetDailyRecommendation
                    IsCompleted INTEGER DEFAULT 0,
                    CompletedAt TEXT,
                    HealthWarning INTEGER DEFAULT 0,
                    HealthWarningText TEXT,
                    UNIQUE(UserId, Date)
                );

                CREATE TABLE IF NOT EXISTS SmartCoachHealthMonitoring (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Date TEXT NOT NULL,
                    SessionId INTEGER,
                    StrainDetected INTEGER DEFAULT 0,
                    StrainType TEXT,
                    StrainLevel REAL DEFAULT 0,
                    Recommendation TEXT,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SmartCoachMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    Date TEXT NOT NULL,
                    MessageType TEXT,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    IsRead INTEGER DEFAULT 0,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Exercises (
                    ExerciseId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    StepsJson TEXT NOT NULL DEFAULT '[]',
                    DurationMinutes INTEGER DEFAULT 5,
                    FrequencyType INTEGER DEFAULT 1,
                    DifficultyLevel INTEGER DEFAULT 1,
                    MetricsJson TEXT NOT NULL DEFAULT '[]',
                    Category TEXT NOT NULL DEFAULT '',
                    Icon TEXT DEFAULT '🎤',
                    SortOrder INTEGER DEFAULT 0,
                    IsActive INTEGER DEFAULT 1,
                    Goal INTEGER DEFAULT 0,
                    GoalIcon TEXT DEFAULT '🎵',
                    ScientificRationale TEXT DEFAULT '',
                    FrequencyText TEXT DEFAULT 'Daglig',
                    TargetPitchMin REAL DEFAULT 140.0,
                    TargetPitchMax REAL DEFAULT 220.0
                );

                CREATE TABLE IF NOT EXISTS ExerciseSessions (
                    SessionId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExerciseId INTEGER NOT NULL,
                    UserId INTEGER DEFAULT 1,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT,
                    DurationSeconds INTEGER DEFAULT 0,
                    Completed INTEGER DEFAULT 0,
                    Score REAL DEFAULT 0,
                    Notes TEXT DEFAULT '',
                    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
                );

                CREATE TABLE IF NOT EXISTS ExerciseProgress (
                    ProgressId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExerciseId INTEGER NOT NULL UNIQUE,
                    UserId INTEGER DEFAULT 1,
                    TotalSessions INTEGER DEFAULT 0,
                    LastSessionDate TEXT,
                    TotalMinutes INTEGER DEFAULT 0,
                    BestScore REAL DEFAULT 0,
                    AverageScore REAL DEFAULT 0,
                    CurrentStreak INTEGER DEFAULT 0,
                    LongestStreak INTEGER DEFAULT 0,
                    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
                );

                CREATE TABLE IF NOT EXISTS SmartCoachBaseline (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    BaselinePitch REAL DEFAULT 0,
                    BaselineF1 REAL DEFAULT 0,
                    BaselineF2 REAL DEFAULT 0,
                    BaselineIntonation REAL DEFAULT 0,
                    BaselineResonanceScore REAL DEFAULT 0,
                    CalculatedAt TEXT,
                    TrainingDaysCount INTEGER DEFAULT 0,
                    ConfidenceLevel TEXT DEFAULT 'low'
                );

                CREATE TABLE IF NOT EXISTS SmartCoachMilestones (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    MilestoneType TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    AchievedAt TEXT,
                    UNIQUE(UserId, MilestoneType)
                );

                CREATE TABLE IF NOT EXISTS ComplexityProgress (
                    UserId INTEGER PRIMARY KEY DEFAULT 1,
                    CurrentLevel INTEGER DEFAULT 0,
                    SessionsAtLevel INTEGER DEFAULT 0,
                    SuccessRate REAL DEFAULT 0,
                    LastEvaluationDate TEXT,
                    IsReadyForNext INTEGER DEFAULT 0
                );
            ";

            var statements = schemaBatch
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--"));

            foreach (var stmt in statements)
            {
                var sql = stmt + ";";
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (SqliteException ex)
                {
                    throw new Exception(
                        $"Schema initialization failed executing SQL: {sql.Substring(0, Math.Min(200, sql.Length))}: {ex.Message}", ex);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 2 – Migrations: RecreateTableIfIncomplete + ALTER TABLEs
        // ─────────────────────────────────────────────────────────────
        private void RunMigrations(SqliteConnection connection)
        {
            // Recreate incomplete tables that may exist from older schema versions
            RecreateTableIfIncomplete(connection, "ComplexityProgress", @"
                CREATE TABLE ComplexityProgress (
                    UserId INTEGER PRIMARY KEY DEFAULT 1,
                    CurrentLevel INTEGER DEFAULT 0,
                    SessionsAtLevel INTEGER DEFAULT 0,
                    SuccessRate REAL DEFAULT 0,
                    LastEvaluationDate TEXT,
                    IsReadyForNext INTEGER DEFAULT 0
                )");

            RecreateTableIfIncomplete(connection, "FemVoiceScores", @"
                CREATE TABLE FemVoiceScores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER,
                    UserId INTEGER DEFAULT 1,
                    OverallScore REAL NOT NULL,
                    ResonanceScore REAL,
                    PitchScore REAL,
                    IntonationScore REAL,
                    VoiceHealthScore REAL,
                    CalculatedAt TEXT NOT NULL,
                    WarningFlags TEXT,
                    FOREIGN KEY (SessionId) REFERENCES TrainingSessions(Id)
                )");

            RecreateTableIfIncomplete(connection, "ExerciseSessions", @"
                CREATE TABLE ExerciseSessions (
                    SessionId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExerciseId INTEGER NOT NULL,
                    UserId INTEGER DEFAULT 1,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT,
                    DurationSeconds INTEGER DEFAULT 0,
                    Completed INTEGER DEFAULT 0,
                    Score REAL DEFAULT 0,
                    Notes TEXT DEFAULT '',
                    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
                )");

            RecreateTableIfIncomplete(connection, "ExerciseProgress", @"
                CREATE TABLE ExerciseProgress (
                    ProgressId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExerciseId INTEGER NOT NULL UNIQUE,
                    UserId INTEGER DEFAULT 1,
                    TotalSessions INTEGER DEFAULT 0,
                    LastSessionDate TEXT,
                    TotalMinutes INTEGER DEFAULT 0,
                    BestScore REAL DEFAULT 0,
                    AverageScore REAL DEFAULT 0,
                    CurrentStreak INTEGER DEFAULT 0,
                    LongestStreak INTEGER DEFAULT 0,
                    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
                )");

            // SmartCoachWeeklyProgress: recreate if severely truncated (< 13 columns).
            // Normal missing-column drift (e.g. only Notes or IsCompleted absent) is handled
            // by the EnsureColumnExists calls in Migration 3 below — no data loss there.
            // This guard catches catastrophically incomplete legacy rows only.
            RecreateTableIfIncomplete(connection, "SmartCoachWeeklyProgress", @"
                CREATE TABLE SmartCoachWeeklyProgress (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER DEFAULT 1,
                    WeekStart TEXT NOT NULL,
                    WeekEnd TEXT NOT NULL,
                    SessionsCount INTEGER DEFAULT 0,
                    TotalMinutes INTEGER DEFAULT 0,
                    AverageScore REAL,
                    AveragePitch REAL,
                    AverageResonance REAL,
                    AverageIntonation REAL,
                    PitchChange REAL,
                    ResonanceChange REAL,
                    IntonationChange REAL,
                    HealthScore REAL DEFAULT 100,
                    Notes TEXT,
                    IsCompleted INTEGER NOT NULL DEFAULT 0
                )");

            // Migration 1: resonance columns on TrainingSessions
            AddColumnIfNotExists(connection, "TrainingSessions", "ResonanceScore", "REAL DEFAULT 0");
            AddColumnIfNotExists(connection, "TrainingSessions", "AverageF1", "REAL DEFAULT 0");
            AddColumnIfNotExists(connection, "TrainingSessions", "AverageF2", "REAL DEFAULT 0");
            AddColumnIfNotExists(connection, "TrainingSessions", "AverageF3", "REAL DEFAULT 0");
            AddColumnIfNotExists(connection, "TrainingSessions", "ResonanceCategory", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "TrainingSessions", "SpectralCentroid", "REAL DEFAULT 0");

            // Migration 2: UserId and VoiceHealthScore on TrainingSessions
            AddColumnIfNotExists(connection, "TrainingSessions", "UserId", "INTEGER DEFAULT 1");
            AddColumnIfNotExists(connection, "TrainingSessions", "VoiceHealthScore", "REAL DEFAULT 100");

            // Migration 3: SmartCoachWeeklyProgress – Notes and IsCompleted
            EnsureColumnExists(connection, "SmartCoachWeeklyProgress", "Notes", "TEXT");
            EnsureColumnExists(connection, "SmartCoachWeeklyProgress", "IsCompleted", "INTEGER NOT NULL DEFAULT 0");

            // Migration 4: IsCompleted naming variant on SmartCoachGoals
            AddColumnIfNotExists(connection, "SmartCoachGoals", "IsCompleted", "INTEGER DEFAULT 0");

            // Migration 5: SmartCoachGoals – IsAchieved and AchievedAt were missing from older schema versions.
            // SaveSmartCoachGoal and GetSmartCoachGoals use IsAchieved (not IsCompleted) as the canonical field.
            AddColumnIfNotExists(connection, "SmartCoachGoals", "IsAchieved", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "SmartCoachGoals", "AchievedAt", "TEXT");

            // Migration 6: SmartCoachDailyRecommendations – IsCompleted and CompletedAt were absent in early
            // schema versions; SaveDailyRecommendation and GetDailyRecommendation require both columns.
            AddColumnIfNotExists(connection, "SmartCoachDailyRecommendations", "IsCompleted", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "SmartCoachDailyRecommendations", "CompletedAt", "TEXT");

            // Migration 7: Recovery sessions count toward training frequency without affecting performance averages.
            AddColumnIfNotExists(connection, "TrainingSessions", "IsRecoveryPractice", "INTEGER DEFAULT 0");

            ValidateCriticalSchema(connection);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 3 – Seed: alle INSERT OR IGNORE, kun etter tabeller er klare
        // Hver tabell er guard-et med TableExists() for ekstra sikkerhet.
        // ─────────────────────────────────────────────────────────────
        private void SeedInitialData(SqliteConnection connection)
        {
            if (TableExists(connection, "UserSettings"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO UserSettings
                    (Id, CurrentDifficulty, PreferredMinPitch, PreferredMaxPitch, HearOwnVoice)
                    VALUES (1, 1, 165, 255, 0);";
                cmd.ExecuteNonQuery();
            }

            if (TableExists(connection, "UserProgress"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO UserProgress (Id) VALUES (1);";
                cmd.ExecuteNonQuery();
            }

            if (TableExists(connection, "VoiceProfiles"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO VoiceProfiles
                    (UserId, CreatedAt, LastUpdated)
                    VALUES (1, datetime('now'), datetime('now'));";
                cmd.ExecuteNonQuery();
            }

            if (TableExists(connection, "TrainingLevels"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO TrainingLevels
                    (UserId, Level, StartDate) VALUES (1, 1, datetime('now'));";
                cmd.ExecuteNonQuery();
            }

            if (TableExists(connection, "Achievements"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT OR IGNORE INTO Achievements
                    (Code, Name, Description, Icon, Category, XPReward) VALUES
                    ('FIRST_STREAK',    'Første steg',         'Fullfør din første dag på rad',       '🌱', 'streak',    10),
                    ('WEEK_STREAK',     'Ukeholder',           '7 dager på rad',                      '🔥', 'streak',    50),
                    ('MONTH_STREAK',    'Månedsmester',        '30 dager på rad',                     '💎', 'streak',   200),
                    ('QUARTER_STREAK',  'Dedikert',            '90 dager på rad',                     '👑', 'streak',   500),
                    ('FIRST_SESSION',   'Første økt',          'Fullfør din første treningsøkt',      '🎯', 'progress',  10),
                    ('SESSION_10',      'Ti-er',               'Fullfør 10 økter',                    '⭐', 'progress',  25),
                    ('SESSION_50',      'Femti-lapp',          'Fullfør 50 økter',                    '🌟', 'progress',  75),
                    ('SESSION_100',     'Hundremetersprint',   'Fullfør 100 økter',                   '🏆', 'progress', 150),
                    ('SESSION_500',     'Mester',              'Fullfør 500 økter',                   '💯', 'progress', 500),
                    ('PITCH_PERFECT',   'Punktsikkert',        'Få 100% pitch-score',                 '🎤', 'skill',     30),
                    ('CONSISTENT',      'Konsistent',          '50 økter med over 70% konsistens',    '📈', 'skill',    100),
                    ('ADVANCED_MASTER', 'Mester',              'Fullfør alle avanserte tekster',      '🎓', 'skill',    150),
                    ('EARLY_BIRD',      'Morgenfugl',          'Tren før kl. 09:00',                  '🌅', 'milestone', 20),
                    ('NIGHT_OWL',       'Nattravn',            'Tren etter kl. 21:00',                '🌙', 'milestone', 20),
                    ('WEEKEND_WARRIOR', 'Helgekriger',         'Tren i helgen',                       '🗓️', 'milestone', 15);";
                cmd.ExecuteNonQuery();
            }
        }

        
        /// <summary>
        /// Recreates a table if it exists but appears incomplete (missing columns or structure issues).
        /// This handles the case where old databases have tables without proper columns or constraints.
        /// </summary>
        private void RecreateTableIfIncomplete(SqliteConnection connection, string tableName, string createSql)
        {
            try
            {
                // Check if table exists
                if (!TableExists(connection, tableName))
                    return;
                
                // Check if table has any columns - if not, it's incomplete
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"PRAGMA table_info({tableName})";
                using var reader = checkCmd.ExecuteReader();
                int columnCount = 0;
                while (reader.Read())
                    columnCount++;
                reader.Close();
                
                // If table has very few columns (incomplete), drop and recreate
                // Use reasonable thresholds: FemVoiceScores should have 10+ columns
                // ExerciseSessions should have 9+ columns, ExerciseProgress should have 9+ columns
                int minColumns = tableName switch
                {
                    "FemVoiceScores"             => 8,
                    "ExerciseSessions"           => 7,
                    "ExerciseProgress"           => 7,
                    // SmartCoachWeeklyProgress has 16 canonical columns (including Notes and IsCompleted).
                    // A threshold of 13 catches severely truncated legacy rows while tolerating minor drift.
                    // EnsureColumnExists in RunMigrations handles the common case of 1-2 missing columns;
                    // RecreateTableIfIncomplete only fires when the table is fundamentally unusable.
                    "SmartCoachWeeklyProgress"   => 13,
                    _ => 5
                };
                
                if (columnCount < minColumns)
                {
                    // Table is incomplete, drop it and recreate
                    var dropCmd = connection.CreateCommand();
                    dropCmd.CommandText = $"DROP TABLE IF EXISTS {tableName}";
                    dropCmd.ExecuteNonQuery();
                    
                    // Recreate the table
                    var createCmd = connection.CreateCommand();
                    createCmd.CommandText = createSql;
                    createCmd.ExecuteNonQuery();
                }
            }
            catch (SqliteException)
            {
                // Table recreation failed, ignore and continue
            }
        }
        
        /// <summary>
        /// Helper method to check if a table exists in the database.
        /// </summary>
        private bool TableExists(SqliteConnection connection, string tableName)
        {
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name = @tableName";
            checkCmd.Parameters.AddWithValue("@tableName", tableName);
            var result = checkCmd.ExecuteScalar();
            return result != null && result.ToString() == tableName;
        }

        /// <summary>
        /// Check whether a given table contains the specified column.
        /// </summary>
        private bool TableHasColumn(SqliteConnection connection, string tableName, string columnName)
        {
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var col = reader.GetString(1);
                    if (string.Equals(col, columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // If the table doesn't exist or PRAGMA fails, return false
            }
            return false;
        }

        /// <summary>
        /// Validates critical schema columns after all migrations have run.
        ///
        /// Design principle: HEAL first, warn on failure — never throw on a missing column
        /// that can be added. A throw here would prevent the app from launching on any
        /// legacy database. Throwing is only appropriate if the *table itself* is absent
        /// (unrecoverable without destructive action).
        ///
        /// Post-migration verification log is written after every launch so ops/debug
        /// logs confirm column presence without requiring a debugger.
        /// </summary>
        private void ValidateCriticalSchema(SqliteConnection connection)
        {
            // ── Gate: table must exist (CreateSchema should have created it) ─────────
            if (!TableExists(connection, "SmartCoachWeeklyProgress"))
                throw new Exception("Critical table SmartCoachWeeklyProgress is missing after initialization.");

            // ── Heal pass: add any missing critical columns before any DML runs ──────

            // Notes (nullable) – was absent in very early schema versions
            if (!TableHasColumn(connection, "SmartCoachWeeklyProgress", "Notes"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachWeeklyProgress.Notes absent — adding column now.");
                EnsureColumnExists(connection, "SmartCoachWeeklyProgress", "Notes", "TEXT");
            }

            // IsCompleted – the primary crash column; heal here as a last-resort backstop
            // even though RunMigrations already calls EnsureColumnExists for it.
            if (!TableHasColumn(connection, "SmartCoachWeeklyProgress", "IsCompleted"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachWeeklyProgress.IsCompleted absent — adding column now. " +
                    "This is a legacy database that missed the RunMigrations heal path.");
                EnsureColumnExists(connection, "SmartCoachWeeklyProgress", "IsCompleted", "INTEGER NOT NULL DEFAULT 0");
            }

            // ── SmartCoachGoals heal pass ─────────────────────────────────────────────
            if (!TableHasColumn(connection, "SmartCoachGoals", "IsAchieved"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachGoals.IsAchieved absent — adding column now.");
                EnsureColumnExists(connection, "SmartCoachGoals", "IsAchieved", "INTEGER DEFAULT 0");
            }
            if (!TableHasColumn(connection, "SmartCoachGoals", "AchievedAt"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachGoals.AchievedAt absent — adding column now.");
                EnsureColumnExists(connection, "SmartCoachGoals", "AchievedAt", "TEXT");
            }

            // ── SmartCoachDailyRecommendations heal pass ──────────────────────────────
            if (!TableHasColumn(connection, "SmartCoachDailyRecommendations", "IsCompleted"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachDailyRecommendations.IsCompleted absent — adding column now.");
                EnsureColumnExists(connection, "SmartCoachDailyRecommendations", "IsCompleted", "INTEGER DEFAULT 0");
            }
            if (!TableHasColumn(connection, "SmartCoachDailyRecommendations", "CompletedAt"))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[FemVoice][DB] LATE-HEAL: SmartCoachDailyRecommendations.CompletedAt absent — adding column now.");
                EnsureColumnExists(connection, "SmartCoachDailyRecommendations", "CompletedAt", "TEXT");
            }

            // ── Post-migration verification log ───────────────────────────────────────
            // Runs on every launch. Structured warning if IsCompleted is still absent
            // after all heal attempts (indicates ALTER TABLE silently failed).
            VerifyPostMigrationSchema(connection);
        }

        /// <summary>
        /// Queries PRAGMA table_info for critical columns after all migrations and heal
        /// attempts have completed. Logs a structured warning if any are still absent.
        /// This is a runtime safety net — it does NOT throw; callers must not depend on it.
        /// </summary>
        private void VerifyPostMigrationSchema(SqliteConnection connection)
        {
            void CheckColumn(string table, string column)
            {
                bool present = TableHasColumn(connection, table, column);
                if (!present)
                {
                    // Structured warning — visible in device logs and Debug Output
                    System.Diagnostics.Debug.WriteLine(
                        $"[FemVoice][DB][WARNING] Post-migration check FAILED: {table}.{column} is still absent. " +
                        $"SaveWeeklyProgress/SaveDailyRecommendation will throw until the next cold launch heals this.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FemVoice][DB] Post-migration check OK: {table}.{column} present.");
                }
            }

            CheckColumn("SmartCoachWeeklyProgress",      "IsCompleted");
            CheckColumn("SmartCoachWeeklyProgress",      "Notes");
            CheckColumn("SmartCoachGoals",               "IsAchieved");
            CheckColumn("SmartCoachGoals",               "AchievedAt");
            CheckColumn("SmartCoachDailyRecommendations","IsCompleted");
            CheckColumn("SmartCoachDailyRecommendations","CompletedAt");
        }

        /// <summary>
        /// Create indexes after schema/tables are finalized. Guards with TableExists to avoid "no such table" errors.
        /// </summary>
        private void CreateIndexes(SqliteConnection connection)
        {
            void TryExec(string table, string sql)
            {
                if (!TableExists(connection, table))
                    return;
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            // FemVoiceScores
            TryExec("FemVoiceScores", "CREATE INDEX IF NOT EXISTS idx_femvoicescores_session ON FemVoiceScores(SessionId)");
            TryExec("FemVoiceScores", "CREATE INDEX IF NOT EXISTS idx_femvoicescores_date ON FemVoiceScores(CalculatedAt)");

            // Training & general
            TryExec("TrainingLevels", "CREATE INDEX IF NOT EXISTS idx_traininglevels_user ON TrainingLevels(UserId)");
            TryExec("VoiceProfiles", "CREATE INDEX IF NOT EXISTS idx_voiceprofiles_user ON VoiceProfiles(UserId)");
            TryExec("DailyProgress", "CREATE INDEX IF NOT EXISTS idx_dailyprogress_user ON DailyProgress(UserId, Date)");
            TryExec("ExerciseEffectiveness", "CREATE INDEX IF NOT EXISTS idx_exerciseeffectiveness_user ON ExerciseEffectiveness(UserId)");
            TryExec("TrainingSessions", "CREATE INDEX IF NOT EXISTS idx_sessions_date ON TrainingSessions(StartTime)");
            TryExec("CalendarData", "CREATE INDEX IF NOT EXISTS idx_calendar_date ON CalendarData(Date)");
            TryExec("Achievements", "CREATE INDEX IF NOT EXISTS idx_achievements_code ON Achievements(Code)");
            TryExec("DailyStreaks", "CREATE INDEX IF NOT EXISTS idx_dailystreaks_date ON DailyStreaks(Date)");

            // Exercises
            TryExec("ExerciseSessions", "CREATE INDEX IF NOT EXISTS idx_exercisesession_exerciseid ON ExerciseSessions(ExerciseId)");
            TryExec("ExerciseSessions", "CREATE INDEX IF NOT EXISTS idx_exercisesession_date ON ExerciseSessions(StartTime)");
            TryExec("ExerciseProgress", "CREATE INDEX IF NOT EXISTS idx_exerciseprogress_exerciseid ON ExerciseProgress(ExerciseId)");

            // SmartCoach indexes
            TryExec("SmartCoachBaseline", "CREATE INDEX IF NOT EXISTS idx_smartcoach_baseline_user ON SmartCoachBaseline(UserId)");
            TryExec("SmartCoachGoals", "CREATE INDEX IF NOT EXISTS idx_smartcoach_goals_user ON SmartCoachGoals(UserId)");
            TryExec("SmartCoachDailyRecommendations", "CREATE INDEX IF NOT EXISTS idx_smartcoach_recommendations_date ON SmartCoachDailyRecommendations(Date)");
            TryExec("SmartCoachWeeklyProgress", "CREATE INDEX IF NOT EXISTS idx_smartcoach_weekly_weekstart ON SmartCoachWeeklyProgress(WeekStart)");
            TryExec("SmartCoachHealthMonitoring", "CREATE INDEX IF NOT EXISTS idx_smartcoach_health_date ON SmartCoachHealthMonitoring(Date)");
            TryExec("SmartCoachMessages", "CREATE INDEX IF NOT EXISTS idx_smartcoach_messages_date ON SmartCoachMessages(Date)");

            // Misc
            TryExec("SmartCoachMilestones", "CREATE INDEX IF NOT EXISTS idx_milestones_user ON SmartCoachMilestones(UserId)");
            TryExec("DailyProgress", "CREATE INDEX IF NOT EXISTS idx_dailyprogress_user_date ON DailyProgress(UserId, Date)");
        }

        /// <summary>
        /// Helper method to add a column to a table if it doesn't exist.
        /// </summary>
        private void AddColumnIfNotExists(SqliteConnection connection, string tableName, string columnName, string definition)
        {
            try
            {
                // First check if column already exists
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"PRAGMA table_info({tableName})";
                using var reader = checkCmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Column already exists
                        return;
                    }
                }
                reader.Close();
                
                // Add the column
                var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists or other error, ignore
            }
        }

        /// <summary>
        /// Ensure a column exists on a table. Wrapper naming to centralize migrations.
        /// Matches existing migration helper style and delegates to AddColumnIfNotExists.
        /// </summary>
        private void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName, string definition)
        {
            // Delegate to existing implementation for safety and idempotency
            AddColumnIfNotExists(connection, tableName, columnName, definition);
        }

        /// <summary>
        /// Enable foreign key enforcement on a per-connection basis.
        /// Must be called immediately after opening a new SqliteConnection.
        /// </summary>
        private void EnableForeignKeys(SqliteConnection connection)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Best-effort; if enabling fails continue without throwing to avoid startup hard-fail.
            }
        }
        
        /// <summary>
        /// Åpner databaseforbindelsen
        /// </summary>
        public SqliteConnection GetConnection()
        {
            if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
                EnableForeignKeys(_connection);
            }
            // Also track for static cleanup
            _staticConnection = _connection;
            return _connection;
        }
        
        /// <summary>
        /// Close all database connections (call before resetting)
        /// </summary>
        public static void CloseAllConnections()
        {
            _staticConnection?.Dispose();
            _staticConnection = null;
        }
        
        /// <summary>
        /// Lagrer en treningsøkt
        /// </summary>
        public int SaveTrainingSession(Models.TrainingSession session)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO TrainingSessions 
                (StartTime, EndTime, ExerciseTextId, AveragePitch, MinPitch, MaxPitch, 
                 PitchVariation, IntonationScore, OverallScore, Feedback, DifficultyLevel, IsRecoveryPractice)
                VALUES 
                (@StartTime, @EndTime, @ExerciseTextId, @AveragePitch, @MinPitch, @MaxPitch,
                 @PitchVariation, @IntonationScore, @OverallScore, @Feedback, @DifficultyLevel, @IsRecoveryPractice);
                SELECT last_insert_rowid();
            ";
            
            command.Parameters.AddWithValue("@StartTime", session.StartTime.ToString("o"));
            command.Parameters.AddWithValue("@EndTime", session.EndTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ExerciseTextId", session.ExerciseTextId);
            command.Parameters.AddWithValue("@AveragePitch", session.AveragePitch);
            command.Parameters.AddWithValue("@MinPitch", session.MinPitch);
            command.Parameters.AddWithValue("@MaxPitch", session.MaxPitch);
            command.Parameters.AddWithValue("@PitchVariation", session.PitchVariation);
            command.Parameters.AddWithValue("@IntonationScore", session.IntonationScore);
            command.Parameters.AddWithValue("@OverallScore", session.OverallScore);
            command.Parameters.AddWithValue("@Feedback", session.Feedback ?? "");
            command.Parameters.AddWithValue("@DifficultyLevel", (int)session.DifficultyLevel);
            command.Parameters.AddWithValue("@IsRecoveryPractice", session.IsRecoveryPractice ? 1 : 0);
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        /// <summary>
        /// Oppdaterer en eksisterende treningsøkt
        /// </summary>
        public void UpdateTrainingSession(Models.TrainingSession session)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE TrainingSessions SET
                    EndTime = @EndTime,
                    AveragePitch = @AveragePitch,
                    MinPitch = @MinPitch,
                    MaxPitch = @MaxPitch,
                    PitchVariation = @PitchVariation,
                    IntonationScore = @IntonationScore,
                    OverallScore = @OverallScore,
                    Feedback = @Feedback,
                    DifficultyLevel = @DifficultyLevel,
                    ResonanceScore = @ResonanceScore,
                    AverageF1 = @AverageF1,
                    AverageF2 = @AverageF2,
                    AverageF3 = @AverageF3,
                    SpectralCentroid = @SpectralCentroid,
                    IsRecoveryPractice = @IsRecoveryPractice
                WHERE Id = @Id
            ";
            
            command.Parameters.AddWithValue("@Id", session.Id);
            command.Parameters.AddWithValue("@EndTime", session.EndTime?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AveragePitch", session.AveragePitch);
            command.Parameters.AddWithValue("@MinPitch", session.MinPitch);
            command.Parameters.AddWithValue("@MaxPitch", session.MaxPitch);
            command.Parameters.AddWithValue("@PitchVariation", session.PitchVariation);
            command.Parameters.AddWithValue("@IntonationScore", session.IntonationScore);
            command.Parameters.AddWithValue("@OverallScore", session.OverallScore);
            command.Parameters.AddWithValue("@Feedback", session.Feedback ?? "");
            command.Parameters.AddWithValue("@DifficultyLevel", (int)session.DifficultyLevel);
            command.Parameters.AddWithValue("@ResonanceScore", session.ResonanceScore);
            command.Parameters.AddWithValue("@AverageF1", session.AverageF1);
            command.Parameters.AddWithValue("@AverageF2", session.AverageF2);
            command.Parameters.AddWithValue("@AverageF3", session.AverageF3);
            command.Parameters.AddWithValue("@SpectralCentroid", session.SpectralCentroid);
            command.Parameters.AddWithValue("@IsRecoveryPractice", session.IsRecoveryPractice ? 1 : 0);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Sletter en treningsøkt
        /// </summary>
        public void DeleteTrainingSession(int sessionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TrainingSessions WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", sessionId);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Lagrer brukerinnstillinger (alias for UpdateUserSettings)
        /// </summary>
        public void SaveUserSettings(Models.UserSettings settings)
        {
            UpdateUserSettings(settings);
        }
        
        /// <summary>
        /// Henter brukerinnstillinger
        /// </summary>
        public Models.UserSettings GetUserSettings()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM UserSettings WHERE Id = 1";
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Models.UserSettings
                {
                    Id = reader.GetInt32(0),
                    CurrentDifficulty = (Models.DifficultyLevel)reader.GetInt32(1),
                    PreferredMinPitch = reader.GetDouble(2),
                    PreferredMaxPitch = reader.GetDouble(3),
                    AveragePitchLast7Days = reader.GetDouble(4),
                    ConsistencyScore = reader.GetDouble(5),
                    TotalSessionsCompleted = reader.GetInt32(6),
                    CurrentStreak = reader.GetInt32(7),
                    LastSessionDate = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    SessionsAtCurrentLevel = reader.GetInt32(9),
                    VolumeThreshold = reader.GetDouble(10),
                    AutoAdvanceLevel = reader.GetInt32(11) == 1,
                    HearOwnVoice = reader.GetInt32(12) == 1
                };
            }
            
            return new Models.UserSettings();
        }
        
        /// <summary>
        /// Oppdaterer brukerinnstillinger
        /// </summary>
        public virtual void UpdateUserSettings(Models.UserSettings settings)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE UserSettings SET
                    CurrentDifficulty = @CurrentDifficulty,
                    PreferredMinPitch = @PreferredMinPitch,
                    PreferredMaxPitch = @PreferredMaxPitch,
                    AveragePitchLast7Days = @AveragePitchLast7Days,
                    ConsistencyScore = @ConsistencyScore,
                    TotalSessionsCompleted = @TotalSessionsCompleted,
                    CurrentStreak = @CurrentStreak,
                    LastSessionDate = @LastSessionDate,
                    SessionsAtCurrentLevel = @SessionsAtCurrentLevel,
                    VolumeThreshold = @VolumeThreshold,
                    AutoAdvanceLevel = @AutoAdvanceLevel,
                    HearOwnVoice = @HearOwnVoice
                WHERE Id = 1
            ";
            
            command.Parameters.AddWithValue("@CurrentDifficulty", (int)settings.CurrentDifficulty);
            command.Parameters.AddWithValue("@PreferredMinPitch", settings.PreferredMinPitch);
            command.Parameters.AddWithValue("@PreferredMaxPitch", settings.PreferredMaxPitch);
            command.Parameters.AddWithValue("@AveragePitchLast7Days", settings.AveragePitchLast7Days);
            command.Parameters.AddWithValue("@ConsistencyScore", settings.ConsistencyScore);
            command.Parameters.AddWithValue("@TotalSessionsCompleted", settings.TotalSessionsCompleted);
            command.Parameters.AddWithValue("@CurrentStreak", settings.CurrentStreak);
            command.Parameters.AddWithValue("@LastSessionDate", settings.LastSessionDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SessionsAtCurrentLevel", settings.SessionsAtCurrentLevel);
            command.Parameters.AddWithValue("@VolumeThreshold", settings.VolumeThreshold);
            command.Parameters.AddWithValue("@AutoAdvanceLevel", settings.AutoAdvanceLevel ? 1 : 0);
            command.Parameters.AddWithValue("@HearOwnVoice", settings.HearOwnVoice ? 1 : 0);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter treningsøkter for en gitt periode
        /// </summary>
        public virtual List<Models.TrainingSession> GetTrainingSessions(DateTime from, DateTime to)
        {
            var sessions = new List<Models.TrainingSession>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StartTime, EndTime, ExerciseTextId, AveragePitch, MinPitch, MaxPitch,
                       PitchVariation, IntonationScore, OverallScore, Feedback, DifficultyLevel,
                       ResonanceScore, AverageF1, AverageF2, AverageF3, IsRecoveryPractice
                FROM TrainingSessions 
                WHERE StartTime >= @From AND StartTime <= @To
                ORDER BY StartTime DESC
            ";
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new Models.TrainingSession
                {
                    Id = reader.GetInt32(0),
                    StartTime = DateTime.Parse(reader.GetString(1)),
                    EndTime = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    ExerciseTextId = reader.GetInt32(3),
                    AveragePitch = reader.GetDouble(4),
                    MinPitch = reader.GetDouble(5),
                    MaxPitch = reader.GetDouble(6),
                    PitchVariation = reader.GetDouble(7),
                    IntonationScore = reader.GetDouble(8),
                    OverallScore = reader.GetDouble(9),
                    Feedback = reader.GetString(10),
                    DifficultyLevel = (Models.DifficultyLevel)reader.GetInt32(11),
                    ResonanceScore = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                    AverageF1 = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                    AverageF2 = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                    AverageF3 = reader.IsDBNull(15) ? 0 : reader.GetDouble(15),
                    IsRecoveryPractice = !reader.IsDBNull(16) && reader.GetInt32(16) == 1
                });
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Lagrer kalenderdata for en dag
        /// </summary>
        public void SaveCalendarData(DateTime date, int sessionsCompleted, int totalMinutes, double averageScore)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            // Try update first
            command.CommandText = @"
                UPDATE CalendarData SET 
                    SessionsCompleted = @Sessions,
                    TotalMinutes = @Minutes,
                    AverageScore = @Score
                WHERE Date = @Date";
            command.Parameters.AddWithValue("@Date", date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@Sessions", sessionsCompleted);
            command.Parameters.AddWithValue("@Minutes", totalMinutes);
            command.Parameters.AddWithValue("@Score", averageScore);
            
            var rowsAffected = command.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                command.CommandText = @"
                    INSERT INTO CalendarData (Date, SessionsCompleted, TotalMinutes, AverageScore)
                    VALUES (@Date, @Sessions, @Minutes, @Score)";
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Henter kalenderdata for en måned
        /// </summary>
        public Dictionary<DateTime, CalendarDayData> GetCalendarData(int year, int month)
        {
            var result = new Dictionary<DateTime, CalendarDayData>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Date, SessionsCompleted, TotalMinutes, AverageScore
                FROM CalendarData
                WHERE Date >= @Start AND Date <= @End
            ";
            command.Parameters.AddWithValue("@Start", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@End", endDate.ToString("yyyy-MM-dd"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var date = DateTime.Parse(reader.GetString(0)).Date;
                result[date] = new CalendarDayData
                {
                    Sessions = reader.GetInt32(1),
                    Minutes = reader.GetInt32(2),
                    Score = reader.GetDouble(3)
                };
            }
            
            // Hent også pitch og resonans-data for hver dag
            var sessionsCommand = connection.CreateCommand();
            sessionsCommand.CommandText = @"
                SELECT 
                    date(StartTime) as SessionDate,
                    AVG(OverallScore) as AvgOverall,
                    AVG(ResonanceScore) as AvgResonance,
                    AVG(AveragePitch) as AvgPitch
                FROM TrainingSessions
                WHERE date(StartTime) >= @Start AND date(StartTime) <= @End
                GROUP BY date(StartTime)
            ";
            sessionsCommand.Parameters.AddWithValue("@Start", startDate.ToString("yyyy-MM-dd"));
            sessionsCommand.Parameters.AddWithValue("@End", endDate.ToString("yyyy-MM-dd"));
            
            using var sessionsReader = sessionsCommand.ExecuteReader();
            while (sessionsReader.Read())
            {
                var sessionDate = DateTime.Parse(sessionsReader.GetString(0)).Date;
                
                if (result.TryGetValue(sessionDate, out var data))
                {
                    data.PitchScore = sessionsReader.IsDBNull(3) ? 0 : sessionsReader.GetDouble(3);
                    data.ResonanceScore = sessionsReader.IsDBNull(2) ? 0 : sessionsReader.GetDouble(2);
                }
                else
                {
                    // Create new entry if not exists
                    result[sessionDate] = new CalendarDayData
                    {
                        Sessions = 0,
                        Minutes = 0,
                        Score = 0,
                        PitchScore = sessionsReader.IsDBNull(3) ? 0 : sessionsReader.GetDouble(3),
                        ResonanceScore = sessionsReader.IsDBNull(2) ? 0 : sessionsReader.GetDouble(2)
                    };
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Henter detaljerte treningsøkter for en spesifikk dag.
        /// </summary>
        /// <param name="date">Datoen å hente økter for</param>
        /// <returns>Liste med treningsøkter inkludert pitch og resonans-scores</returns>
        public List<Models.TrainingSession> GetDetailedSessionsForDay(DateTime date)
        {
            var sessions = new List<Models.TrainingSession>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StartTime, EndTime, ExerciseTextId, AveragePitch, MinPitch, MaxPitch,
                       PitchVariation, IntonationScore, OverallScore, Feedback, DifficultyLevel,
                       ResonanceScore, AverageF1, AverageF2, AverageF3, ResonanceCategory
                FROM TrainingSessions 
                WHERE StartTime >= @StartOfDay AND StartTime < @EndOfDay
                ORDER BY StartTime DESC
            ";
            command.Parameters.AddWithValue("@StartOfDay", startOfDay.ToString("o"));
            command.Parameters.AddWithValue("@EndOfDay", endOfDay.ToString("o"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new Models.TrainingSession
                {
                    Id = reader.GetInt32(0),
                    StartTime = DateTime.Parse(reader.GetString(1)),
                    EndTime = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    ExerciseTextId = reader.GetInt32(3),
                    AveragePitch = reader.GetDouble(4),
                    MinPitch = reader.GetDouble(5),
                    MaxPitch = reader.GetDouble(6),
                    PitchVariation = reader.GetDouble(7),
                    IntonationScore = reader.GetDouble(8),
                    OverallScore = reader.GetDouble(9),
                    Feedback = reader.GetString(10),
                    DifficultyLevel = (Models.DifficultyLevel)reader.GetInt32(11),
                    ResonanceScore = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                    AverageF1 = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                    AverageF2 = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                    AverageF3 = reader.IsDBNull(15) ? 0 : reader.GetDouble(15),
                    ResonanceCategory = (Subsystems.Analysis.ResonanceCategory)(reader.IsDBNull(16) ? 0 : reader.GetInt32(16))
                });
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Henter detaljerte treningsøkter for en hel måned.
        /// </summary>
        /// <param name="year">År</param>
        /// <param name="month">Måned (1-12)</param>
        /// <returns>Liste med alle treningsøkter i måneden</returns>
        public List<Models.TrainingSession> GetDetailedSessionsForMonth(int year, int month)
        {
            var sessions = new List<Models.TrainingSession>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StartTime, EndTime, ExerciseTextId, AveragePitch, MinPitch, MaxPitch,
                       PitchVariation, IntonationScore, OverallScore, Feedback, DifficultyLevel,
                       ResonanceScore, AverageF1, AverageF2, AverageF3, ResonanceCategory
                FROM TrainingSessions 
                WHERE StartTime >= @StartDate AND StartTime < @EndDate
                ORDER BY StartTime DESC
            ";
            command.Parameters.AddWithValue("@StartDate", startDate.ToString("o"));
            command.Parameters.AddWithValue("@EndDate", endDate.ToString("o"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new Models.TrainingSession
                {
                    Id = reader.GetInt32(0),
                    StartTime = DateTime.Parse(reader.GetString(1)),
                    EndTime = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    ExerciseTextId = reader.GetInt32(3),
                    AveragePitch = reader.GetDouble(4),
                    MinPitch = reader.GetDouble(5),
                    MaxPitch = reader.GetDouble(6),
                    PitchVariation = reader.GetDouble(7),
                    IntonationScore = reader.GetDouble(8),
                    OverallScore = reader.GetDouble(9),
                    Feedback = reader.GetString(10),
                    DifficultyLevel = (Models.DifficultyLevel)reader.GetInt32(11),
                    ResonanceScore = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                    AverageF1 = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                    AverageF2 = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                    AverageF3 = reader.IsDBNull(15) ? 0 : reader.GetDouble(15),
                    ResonanceCategory = (Subsystems.Analysis.ResonanceCategory)(reader.IsDBNull(16) ? 0 : reader.GetInt32(16))
                });
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Beregner progresjonsstatistikk</summary>
        /// </summary>
        public (double AvgPitch, double Consistency, int Streak) GetProgressionStats()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            // Hent gjennomsnittlig pitch og konsistens fra siste 30 dager
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    AVG(AveragePitch) as AvgPitch,
                    AVG(PitchVariation) as Consistency,
                    COUNT(*) as SessionCount
                FROM TrainingSessions
                WHERE StartTime >= datetime('now', '-30 days')
            ";
            
            using var reader = command.ExecuteReader();
            double avgPitch = 0, consistency = 0;
            if (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    avgPitch = reader.GetDouble(0);
                if (!reader.IsDBNull(1))
                    consistency = reader.GetDouble(1);
            }
            
            // Beregn streak
            int streak = 0;
            var checkDate = DateTime.Today;
            while (true)
            {
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) FROM TrainingSessions
                    WHERE date(StartTime) = @Date
                ";
                checkCommand.Parameters.AddWithValue("@Date", checkDate.ToString("yyyy-MM-dd"));
                
                var count = Convert.ToInt32(checkCommand.ExecuteScalar());
                if (count > 0)
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (checkDate == DateTime.Today)
                {
                    // Ikke fullført noe i dag ennå, sjekk fra i går
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }
            
            return (avgPitch, consistency, streak);
        }
        
        /// <summary>
        /// Hent brukerprogress (XP og nivå)
        /// </summary>
        public UserProgressData GetUserProgress()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM UserProgress WHERE Id = 1";
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new UserProgressData
                {
                    Id = reader.GetInt32(0),
                    TotalXP = reader.GetInt32(1),
                    Level = reader.GetInt32(2),
                    XPForCurrentLevel = reader.GetInt32(3),
                    XPToNextLevel = reader.GetInt32(4),
                    TotalSessions = reader.GetInt32(5),
                    TotalMinutes = reader.GetInt32(6),
                    CurrentStreak = reader.GetInt32(7),
                    LongestStreak = reader.GetInt32(8)
                };
            }
            
            return new UserProgressData();
        }
        
        /// <summary>
        /// Oppdater brukerprogress
        /// </summary>
        public void UpdateUserProgress(UserProgressData progress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE UserProgress SET 
                    TotalXP = @TotalXP, Level = @Level, XPForCurrentLevel = @XPForCurrent,
                    XPToNextLevel = @XPToNext, TotalSessions = @Sessions, TotalMinutes = @Minutes,
                    CurrentStreak = @Streak, LongestStreak = @LongestStreak
                WHERE Id = 1";
            
            command.Parameters.AddWithValue("@TotalXP", progress.TotalXP);
            command.Parameters.AddWithValue("@Level", progress.Level);
            command.Parameters.AddWithValue("@XPForCurrent", progress.XPForCurrentLevel);
            command.Parameters.AddWithValue("@XPToNext", progress.XPToNextLevel);
            command.Parameters.AddWithValue("@Sessions", progress.TotalSessions);
            command.Parameters.AddWithValue("@Minutes", progress.TotalMinutes);
            command.Parameters.AddWithValue("@Streak", progress.CurrentStreak);
            command.Parameters.AddWithValue("@LongestStreak", progress.LongestStreak);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Hent alle achievements
        /// </summary>
        public List<AchievementData> GetAchievements()
        {
            var achievements = new List<AchievementData>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Achievements ORDER BY Category, Id";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                achievements.Add(new AchievementData
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.GetString(3),
                    Icon = reader.GetString(4),
                    Category = reader.GetString(5),
                    XPReward = reader.GetInt32(6),
                    IsUnlocked = reader.GetInt32(7) == 1,
                    UnlockedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8))
                });
            }
            
            return achievements;
        }
        
        /// <summary>
        /// Lås opp et achievement
        /// </summary>
        public void UnlockAchievement(string code)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Achievements SET IsUnlocked = 1, UnlockedAt = @UnlockedAt 
                WHERE Code = @Code AND IsUnlocked = 0";
            command.Parameters.AddWithValue("@Code", code);
            command.Parameters.AddWithValue("@UnlockedAt", DateTime.Now.ToString("o"));
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Lagre daglig streak
        /// </summary>
        public void SaveDailyStreak(DateTime date, int sessionsCompleted, int totalMinutes, int streakDay)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            // Try update first
            command.CommandText = @"
                UPDATE DailyStreaks SET 
                    SessionsCompleted = @Sessions,
                    TotalMinutes = @Minutes,
                    StreakDay = @StreakDay,
                    TargetMet = @TargetMet
                WHERE Date = @Date";
            command.Parameters.AddWithValue("@Date", date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@Sessions", sessionsCompleted);
            command.Parameters.AddWithValue("@Minutes", totalMinutes);
            command.Parameters.AddWithValue("@StreakDay", streakDay);
            command.Parameters.AddWithValue("@TargetMet", sessionsCompleted >= 1 ? 1 : 0);
            
            var rowsAffected = command.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                command.CommandText = @"
                    INSERT INTO DailyStreaks (Date, SessionsCompleted, TotalMinutes, StreakDay, TargetMet)
                    VALUES (@Date, @Sessions, @Minutes, @StreakDay, @TargetMet)";
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Tømmer hele databasen og starter på nytt
        /// </summary>
        public void ResetDatabase()
        {
            // Hent database path
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            var dbPath = Path.Combine(appDataPath, "femvoice.db");
            
            // Lukk eksisterende tilkobling
            _connection?.Dispose();
            _connection = null;
            
            // Slett databasefilen hvis den eksisterer
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
            
            // Reset the initialization guard so InitializeDatabase() will run again
            // on this fresh empty database. This is the only legitimate second call site.
            _initialized = false;
            InitializeDatabase();
        }
        
        #region Smart Coach Data Access Methods
        
        /// <summary>
        /// Lagrer eller oppdaterer brukerens baseline
        /// </summary>
        public virtual void SaveSmartCoachBaseline(SmartCoachBaseline baseline)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            // Try update first
            command.CommandText = @"
                UPDATE SmartCoachBaseline SET 
                    BaselinePitch = @BaselinePitch,
                    BaselineF1 = @BaselineF1,
                    BaselineF2 = @BaselineF2,
                    BaselineIntonation = @BaselineIntonation,
                    BaselineResonanceScore = @BaselineResonance,
                    CalculatedAt = @CalculatedAt,
                    TrainingDaysCount = @DaysCount,
                    ConfidenceLevel = @Confidence
                WHERE UserId = @UserId";
            
            command.Parameters.AddWithValue("@UserId", baseline.UserId);
            command.Parameters.AddWithValue("@BaselinePitch", baseline.BaselinePitch);
            command.Parameters.AddWithValue("@BaselineF1", baseline.BaselineF1);
            command.Parameters.AddWithValue("@BaselineF2", baseline.BaselineF2);
            command.Parameters.AddWithValue("@BaselineIntonation", baseline.BaselineIntonation);
            command.Parameters.AddWithValue("@BaselineResonance", baseline.BaselineResonanceScore);
            command.Parameters.AddWithValue("@CalculatedAt", baseline.CalculatedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DaysCount", baseline.TrainingDaysCount);
            command.Parameters.AddWithValue("@Confidence", baseline.ConfidenceLevel);
            
            var rowsAffected = command.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                command.CommandText = @"
                    INSERT INTO SmartCoachBaseline 
                    (UserId, BaselinePitch, BaselineF1, BaselineF2, BaselineIntonation, 
                     BaselineResonanceScore, CalculatedAt, TrainingDaysCount, ConfidenceLevel)
                    VALUES (@UserId, @BaselinePitch, @BaselineF1, @BaselineF2, @BaselineIntonation,
                            @BaselineResonance, @CalculatedAt, @DaysCount, @Confidence)";
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Henter brukerens baseline (returnerer null hvis ikke beregnet)
        /// </summary>
        public virtual SmartCoachBaseline? GetSmartCoachBaseline(int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SmartCoachBaseline WHERE UserId = @UserId ORDER BY Id DESC LIMIT 1";
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new SmartCoachBaseline
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    BaselinePitch = reader.GetDouble(2),
                    BaselineF1 = reader.GetDouble(3),
                    BaselineF2 = reader.GetDouble(4),
                    BaselineIntonation = reader.GetDouble(5),
                    BaselineResonanceScore = reader.GetDouble(6),
                    CalculatedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                    TrainingDaysCount = reader.GetInt32(8),
                    ConfidenceLevel = reader.GetString(9)
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Lagrer eller oppdaterer et mål
        /// </summary>
        public virtual void SaveSmartCoachGoal(SmartCoachGoal goal)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            if (goal.Id == 0)
            {
                command.CommandText = @"
                    INSERT INTO SmartCoachGoals 
                    (UserId, GoalType, TargetValue, CurrentValue, StartDate, TargetDate, IsAchieved, AchievedAt, Priority)
                    VALUES (@UserId, @GoalType, @TargetValue, @CurrentValue, @StartDate, @TargetDate, @IsAchieved, @AchievedAt, @Priority)";
            }
            else
            {
                command.CommandText = @"
                    UPDATE SmartCoachGoals SET
                        TargetValue = @TargetValue,
                        CurrentValue = @CurrentValue,
                        TargetDate = @TargetDate,
                        IsAchieved = @IsAchieved,
                        AchievedAt = @AchievedAt,
                        Priority = @Priority
                    WHERE Id = @Id";
                command.Parameters.AddWithValue("@Id", goal.Id);
            }
            
            command.Parameters.AddWithValue("@UserId", goal.UserId);
            command.Parameters.AddWithValue("@GoalType", goal.GoalType);
            command.Parameters.AddWithValue("@TargetValue", goal.TargetValue);
            command.Parameters.AddWithValue("@CurrentValue", goal.CurrentValue);
            command.Parameters.AddWithValue("@StartDate", goal.StartDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TargetDate", goal.TargetDate?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsAchieved", goal.IsAchieved ? 1 : 0);
            command.Parameters.AddWithValue("@AchievedAt", goal.AchievedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Priority", goal.Priority);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter alle mål for en bruker
        /// </summary>
        public virtual List<SmartCoachGoal> GetSmartCoachGoals(int userId = 1, bool activeOnly = true)
        {
            var goals = new List<SmartCoachGoal>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = activeOnly 
                ? "SELECT * FROM SmartCoachGoals WHERE UserId = @UserId AND IsAchieved = 0 ORDER BY Priority DESC, TargetDate ASC"
                : "SELECT * FROM SmartCoachGoals WHERE UserId = @UserId ORDER BY Priority DESC, TargetDate ASC";
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                goals.Add(new SmartCoachGoal
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    GoalType = reader.GetString(2),
                    TargetValue = reader.GetDouble(3),
                    CurrentValue = reader.GetDouble(4),
                    StartDate = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                    TargetDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                    IsAchieved = reader.GetInt32(7) == 1,
                    AchievedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    Priority = reader.GetInt32(9)
                });
            }
            
            return goals;
        }
        
        /// <summary>
        /// Lagrer en daglig anbefaling
        /// </summary>
        public virtual void SaveDailyRecommendation(SmartCoachDailyRecommendation recommendation)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            // First try to update existing record
            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE SmartCoachDailyRecommendations 
                SET FocusArea = @FocusArea,
                    RecommendationText = @RecommendationText,
                    RecommendedExerciseId = @ExerciseId,
                    RecommendedDurationMinutes = @Duration,
                    IsCompleted = @IsCompleted,
                    CompletedAt = @CompletedAt,
                    HealthWarning = @HealthWarning,
                    HealthWarningText = @HealthWarningText
                WHERE Date = @Date AND UserId = @UserId";
            
            updateCmd.Parameters.AddWithValue("@UserId", recommendation.UserId);
            updateCmd.Parameters.AddWithValue("@Date", recommendation.Date.Date.ToString("yyyy-MM-dd"));
            updateCmd.Parameters.AddWithValue("@FocusArea", recommendation.FocusArea);
            updateCmd.Parameters.AddWithValue("@RecommendationText", recommendation.RecommendationText);
            updateCmd.Parameters.AddWithValue("@ExerciseId", recommendation.RecommendedExerciseId ?? (object)DBNull.Value);
            updateCmd.Parameters.AddWithValue("@Duration", recommendation.RecommendedDurationMinutes);
            updateCmd.Parameters.AddWithValue("@IsCompleted", recommendation.IsCompleted ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@CompletedAt", recommendation.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
            updateCmd.Parameters.AddWithValue("@HealthWarning", recommendation.HealthWarning ? 1 : 0);
            updateCmd.Parameters.AddWithValue("@HealthWarningText", recommendation.HealthWarningText ?? (object)DBNull.Value);
            
            var rowsAffected = updateCmd.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO SmartCoachDailyRecommendations 
                    (UserId, Date, FocusArea, RecommendationText, RecommendedExerciseId, 
                     RecommendedDurationMinutes, IsCompleted, CompletedAt, HealthWarning, HealthWarningText)
                    VALUES (@UserId, @Date, @FocusArea, @RecommendationText, @ExerciseId, 
                            @Duration, @IsCompleted, @CompletedAt, @HealthWarning, @HealthWarningText)";
                
                insertCmd.Parameters.AddWithValue("@UserId", recommendation.UserId);
                insertCmd.Parameters.AddWithValue("@Date", recommendation.Date.Date.ToString("yyyy-MM-dd"));
                insertCmd.Parameters.AddWithValue("@FocusArea", recommendation.FocusArea);
                insertCmd.Parameters.AddWithValue("@RecommendationText", recommendation.RecommendationText);
                insertCmd.Parameters.AddWithValue("@ExerciseId", recommendation.RecommendedExerciseId ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Duration", recommendation.RecommendedDurationMinutes);
                insertCmd.Parameters.AddWithValue("@IsCompleted", recommendation.IsCompleted ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@CompletedAt", recommendation.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@HealthWarning", recommendation.HealthWarning ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@HealthWarningText", recommendation.HealthWarningText ?? (object)DBNull.Value);
                
                insertCmd.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Henter daglig anbefaling for en spesifikk dato
        /// </summary>
        public virtual SmartCoachDailyRecommendation? GetDailyRecommendation(DateTime date, int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SmartCoachDailyRecommendations WHERE Date = @Date AND UserId = @UserId";
            command.Parameters.AddWithValue("@Date", date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new SmartCoachDailyRecommendation
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    FocusArea = reader.GetString(3),
                    RecommendationText = reader.GetString(4),
                    RecommendedExerciseId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    RecommendedDurationMinutes = reader.GetInt32(6),
                    IsCompleted = reader.GetInt32(7) == 1,
                    CompletedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                    HealthWarning = reader.GetInt32(9) == 1,
                    HealthWarningText = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Lagrer ukentlig progresjon
        /// </summary>
        public virtual void SaveWeeklyProgress(SmartCoachWeeklyProgress progress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            // Try update first
            command.CommandText = @"
                UPDATE SmartCoachWeeklyProgress SET 
                    WeekEnd = @WeekEnd,
                    SessionsCount = @Sessions,
                    TotalMinutes = @Minutes,
                    AverageScore = @AvgScore,
                    AveragePitch = @AvgPitch,
                    AverageResonance = @AvgResonance,
                    AverageIntonation = @AvgIntonation,
                    PitchChange = @PitchChange,
                    ResonanceChange = @ResonanceChange,
                    IntonationChange = @IntonationChange,
                        HealthScore = @HealthScore,
                        Notes = @Notes,
                        IsCompleted = @IsCompleted
                WHERE WeekStart = @WeekStart AND UserId = @UserId";
            
            command.Parameters.AddWithValue("@UserId", progress.UserId);
            command.Parameters.AddWithValue("@WeekStart", progress.WeekStart.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@WeekEnd", progress.WeekEnd?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Sessions", progress.SessionsCount);
            command.Parameters.AddWithValue("@Minutes", progress.TotalMinutes);
            command.Parameters.AddWithValue("@AvgScore", progress.AverageScore);
            command.Parameters.AddWithValue("@AvgPitch", progress.AveragePitch);
            command.Parameters.AddWithValue("@AvgResonance", progress.AverageResonance);
            command.Parameters.AddWithValue("@AvgIntonation", progress.AverageIntonation);
            command.Parameters.AddWithValue("@PitchChange", progress.PitchChange);
            command.Parameters.AddWithValue("@ResonanceChange", progress.ResonanceChange);
            command.Parameters.AddWithValue("@IntonationChange", progress.IntonationChange);
            command.Parameters.AddWithValue("@HealthScore", progress.HealthScore);
            command.Parameters.AddWithValue("@Notes", progress.Notes ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsCompleted", progress.IsCompleted ? 1 : 0);
            
            var rowsAffected = command.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                command.CommandText = @"
                    INSERT INTO SmartCoachWeeklyProgress 
                    (UserId, WeekStart, WeekEnd, SessionsCount, TotalMinutes, AverageScore,
                         AveragePitch, AverageResonance, AverageIntonation, PitchChange, ResonanceChange,
                         IntonationChange, HealthScore, Notes, IsCompleted)
                        VALUES (@UserId, @WeekStart, @WeekEnd, @Sessions, @Minutes, @AvgScore,
                                @AvgPitch, @AvgResonance, @AvgIntonation, @PitchChange, @ResonanceChange,
                                @IntonationChange, @HealthScore, @Notes, @IsCompleted)";
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Henter ukentlig progresjon for en gitt uke
        /// </summary>
        public virtual SmartCoachWeeklyProgress? GetWeeklyProgress(DateTime weekStart, int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM SmartCoachWeeklyProgress WHERE WeekStart = @WeekStart AND UserId = @UserId";
            command.Parameters.AddWithValue("@WeekStart", weekStart.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var result = new SmartCoachWeeklyProgress
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    WeekStart = DateTime.Parse(reader.GetString(2)),
                    WeekEnd = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    SessionsCount = reader.GetInt32(4),
                    TotalMinutes = reader.GetInt32(5),
                    AverageScore = reader.GetDouble(6),
                    AveragePitch = reader.GetDouble(7),
                    AverageResonance = reader.GetDouble(8),
                    AverageIntonation = reader.GetDouble(9),
                    PitchChange = reader.GetDouble(10),
                    ResonanceChange = reader.GetDouble(11),
                    IntonationChange = reader.GetDouble(12),
                    HealthScore = reader.GetDouble(13),
                    Notes = reader.IsDBNull(14) ? null : reader.GetString(14)
                };
                try
                {
                    var idx = reader.GetOrdinal("IsCompleted");
                    result.IsCompleted = reader.GetInt32(idx) == 1;
                }
                catch
                {
                    result.IsCompleted = false;
                }
                return result;
            }

            return null;
        }
        
        /// <summary>
        /// Henter de siste N ukene med progresjon
        /// </summary>
        public virtual List<SmartCoachWeeklyProgress> GetRecentWeeklyProgress(int weeks = 4, int userId = 1)
        {
            var progressList = new List<SmartCoachWeeklyProgress>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SmartCoachWeeklyProgress 
                WHERE UserId = @UserId 
                ORDER BY WeekStart DESC 
                LIMIT @Weeks";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Weeks", weeks);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var result = new SmartCoachWeeklyProgress
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    WeekStart = DateTime.Parse(reader.GetString(2)),
                    WeekEnd = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    SessionsCount = reader.GetInt32(4),
                    TotalMinutes = reader.GetInt32(5),
                    AverageScore = reader.GetDouble(6),
                    AveragePitch = reader.GetDouble(7),
                    AverageResonance = reader.GetDouble(8),
                    AverageIntonation = reader.GetDouble(9),
                    PitchChange = reader.GetDouble(10),
                    ResonanceChange = reader.GetDouble(11),
                    IntonationChange = reader.GetDouble(12),
                    HealthScore = reader.GetDouble(13),
                    Notes = reader.IsDBNull(14) ? null : reader.GetString(14)
                };
                try
                {
                    var idx = reader.GetOrdinal("IsCompleted");
                    result.IsCompleted = reader.GetInt32(idx) == 1;
                }
                catch
                {
                    result.IsCompleted = false;
                }
                progressList.Add(result);
            }
            
            return progressList;
        }
        
        /// <summary>
        /// Lagrer helseovervåkingsdata
        /// <summary>
        /// Lagrer helseovervåkingsdata
        /// </summary>
        public virtual void SaveHealthMonitoring(SmartCoachHealthMonitoring health)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SmartCoachHealthMonitoring 
                (UserId, Date, SessionId, StrainDetected, StrainType, StrainLevel, Recommendation, IsRead, CreatedAt)
                VALUES (@UserId, @Date, @SessionId, @StrainDetected, @StrainType, @StrainLevel, @Recommendation, @IsRead, @CreatedAt)";
            
            command.Parameters.AddWithValue("@UserId", health.UserId);
            command.Parameters.AddWithValue("@Date", health.Date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@SessionId", health.SessionId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StrainDetected", health.StrainDetected ? 1 : 0);
            command.Parameters.AddWithValue("@StrainType", health.StrainType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StrainLevel", health.StrainLevel);
            command.Parameters.AddWithValue("@Recommendation", health.Recommendation ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsRead", health.IsRead ? 1 : 0);
            command.Parameters.AddWithValue("@CreatedAt", health.CreatedAt?.ToString("o") ?? DateTime.Now.ToString("o"));
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter nylige helseproblemer
        /// </summary>
        public virtual List<SmartCoachHealthMonitoring> GetRecentHealthIssues(int days = 7, int userId = 1)
        {
            var issues = new List<SmartCoachHealthMonitoring>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SmartCoachHealthMonitoring 
                WHERE UserId = @UserId AND Date >= @StartDate
                ORDER BY Date DESC, StrainLevel DESC";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@StartDate", DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                issues.Add(new SmartCoachHealthMonitoring
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    SessionId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    StrainDetected = reader.GetInt32(4) == 1,
                    StrainType = reader.IsDBNull(5) ? null : reader.GetString(5),
                    StrainLevel = reader.GetDouble(6),
                    Recommendation = reader.IsDBNull(7) ? null : reader.GetString(7),
                    IsRead = reader.GetInt32(8) == 1,
                    CreatedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9))
                });
            }
            
            return issues;
        }
        
        /// <summary>
        /// Lagrer en coach-melding
        /// </summary>
        public virtual void SaveCoachMessage(SmartCoachMessage message)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SmartCoachMessages 
                (UserId, Date, MessageType, Title, Message, IsRead, CreatedAt)
                VALUES (@UserId, @Date, @MessageType, @Title, @Message, @IsRead, @CreatedAt)";
            
            command.Parameters.AddWithValue("@UserId", message.UserId);
            command.Parameters.AddWithValue("@Date", message.Date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@MessageType", message.MessageType);
            command.Parameters.AddWithValue("@Title", message.Title);
            command.Parameters.AddWithValue("@Message", message.Message);
            command.Parameters.AddWithValue("@IsRead", message.IsRead ? 1 : 0);
            command.Parameters.AddWithValue("@CreatedAt", message.CreatedAt?.ToString("o") ?? DateTime.Now.ToString("o"));
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter uleste meldinger
        /// </summary>
        public List<SmartCoachMessage> GetUnreadMessages(int userId = 1)
        {
            var messages = new List<SmartCoachMessage>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM SmartCoachMessages 
                WHERE UserId = @UserId AND IsRead = 0
                ORDER BY CreatedAt DESC
                LIMIT 10";
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(new SmartCoachMessage
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    MessageType = reader.GetString(3),
                    Title = reader.GetString(4),
                    Message = reader.GetString(5),
                    IsRead = reader.GetInt32(6) == 1,
                    CreatedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7))
                });
            }
            
            return messages;
        }
        
        /// <summary>
        /// Henter antall uleste meldinger
        /// </summary>
        public virtual int GetUnreadMessageCount(int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SmartCoachMessages WHERE UserId = @UserId AND IsRead = 0";
            command.Parameters.AddWithValue("@UserId", userId);
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        /// <summary>
        /// Markerer en melding som lest
        /// </summary>
        public void MarkMessageAsRead(int messageId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE SmartCoachMessages SET IsRead = 1 WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", messageId);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter treningsstatistikk for en periode
        /// </summary>
        public (int Sessions, int Minutes, double AvgScore, double AvgPitch, double AvgResonance, double AvgIntonation) GetTrainingStats(DateTime from, DateTime to, int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*) as Sessions,
                    COALESCE(SUM(CAST((julianday(EndTime) - julianday(StartTime)) * 24 * 60 AS INTEGER)), 0) as Minutes,
                    AVG(CASE WHEN IsRecoveryPractice = 0 THEN OverallScore END) as AvgScore,
                    AVG(CASE WHEN IsRecoveryPractice = 0 THEN AveragePitch END) as AvgPitch,
                    AVG(CASE WHEN IsRecoveryPractice = 0 THEN ResonanceScore END) as AvgResonance,
                    AVG(CASE WHEN IsRecoveryPractice = 0 THEN IntonationScore END) as AvgIntonation
                FROM TrainingSessions 
                WHERE StartTime >= @From AND StartTime <= @To";
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return (
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
                );
            }
            
            return (0, 0, 0, 0, 0, 0);
        }
        
        /// <summary>
        /// Henter unike treningsdager
        /// </summary>
        public virtual int GetTrainingDaysCount(DateTime from, DateTime to, int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(DISTINCT date(StartTime)) 
                FROM TrainingSessions 
                WHERE StartTime >= @From AND StartTime <= @To";
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        /// <summary>
        /// Henter siste N treningsøkter
        /// </summary>
        public virtual List<Models.TrainingSession> GetRecentSessions(int count = 10, int userId = 1)
        {
            var sessions = new List<Models.TrainingSession>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StartTime, EndTime, ExerciseTextId, AveragePitch, MinPitch, MaxPitch,
                       PitchVariation, IntonationScore, OverallScore, Feedback, DifficultyLevel,
                       ResonanceScore, AverageF1, AverageF2, AverageF3, IsRecoveryPractice
                FROM TrainingSessions 
                ORDER BY StartTime DESC
                LIMIT @Count";
            command.Parameters.AddWithValue("@Count", count);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(new Models.TrainingSession
                {
                    Id = reader.GetInt32(0),
                    StartTime = DateTime.Parse(reader.GetString(1)),
                    EndTime = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    ExerciseTextId = reader.GetInt32(3),
                    AveragePitch = reader.GetDouble(4),
                    MinPitch = reader.GetDouble(5),
                    MaxPitch = reader.GetDouble(6),
                    PitchVariation = reader.GetDouble(7),
                    IntonationScore = reader.GetDouble(8),
                    OverallScore = reader.GetDouble(9),
                    Feedback = reader.GetString(10),
                    DifficultyLevel = (Models.DifficultyLevel)reader.GetInt32(11),
                    ResonanceScore = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                    AverageF1 = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                    AverageF2 = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                    AverageF3 = reader.IsDBNull(15) ? 0 : reader.GetDouble(15),
                    IsRecoveryPractice = !reader.IsDBNull(16) && reader.GetInt32(16) == 1
                });
            }
            
            return sessions;
        }
        
        /// <summary>
        /// Henter nylige FemVoiceScore-resultater
        /// </summary>
        public List<FemVoiceScoreResult> GetRecentFemVoiceScores(int count = 10, int userId = 1)
        {
            var scores = new List<FemVoiceScoreResult>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, SessionId, UserId, OverallScore, ResonanceScore, PitchScore, 
                       IntonationScore, VoiceHealthScore, CalculatedAt, WarningFlags
                FROM FemVoiceScores 
                WHERE UserId = @UserId
                ORDER BY CalculatedAt DESC
                LIMIT @Count";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Count", count);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                scores.Add(new FemVoiceScoreResult
                {
                    Id = reader.GetInt32(0),
                    SessionId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    OverallScore = reader.GetDouble(3),
                    ResonanceScore = reader.IsDBNull(4) ? 50 : reader.GetDouble(4),
                    PitchScore = reader.IsDBNull(5) ? 50 : reader.GetDouble(5),
                    IntonationScore = reader.IsDBNull(6) ? 50 : reader.GetDouble(6),
                    VoiceHealthScore = reader.IsDBNull(7) ? 100 : reader.GetDouble(7),
                    CalculatedAt = DateTime.Parse(reader.GetString(8)),
                    WarningFlags = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
            
            return scores;
        }
        
        /// <summary>
        /// Henter FemVoiceScore-resultater for en periode
        /// </summary>
        public List<FemVoiceScoreResult> GetFemVoiceScores(DateTime from, DateTime to, int userId = 1)
        {
            var scores = new List<FemVoiceScoreResult>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, SessionId, UserId, OverallScore, ResonanceScore, PitchScore, 
                       IntonationScore, VoiceHealthScore, CalculatedAt, WarningFlags
                FROM FemVoiceScores 
                WHERE UserId = @UserId AND CalculatedAt >= @From AND CalculatedAt <= @To
                ORDER BY CalculatedAt DESC";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                scores.Add(new FemVoiceScoreResult
                {
                    Id = reader.GetInt32(0),
                    SessionId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    OverallScore = reader.GetDouble(3),
                    ResonanceScore = reader.IsDBNull(4) ? 50 : reader.GetDouble(4),
                    PitchScore = reader.IsDBNull(5) ? 50 : reader.GetDouble(5),
                    IntonationScore = reader.IsDBNull(6) ? 50 : reader.GetDouble(6),
                    VoiceHealthScore = reader.IsDBNull(7) ? 100 : reader.GetDouble(7),
                    CalculatedAt = DateTime.Parse(reader.GetString(8)),
                    WarningFlags = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
            
            return scores;
        }
        
        /// <summary>
        /// Lagrer et FemVoiceScore-resultat
        /// </summary>
        public void SaveFemVoiceScore(FemVoiceScoreResult score)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO FemVoiceScores 
                (SessionId, UserId, OverallScore, ResonanceScore, PitchScore, 
                 IntonationScore, VoiceHealthScore, CalculatedAt, WarningFlags)
                VALUES 
                (@SessionId, @UserId, @OverallScore, @ResonanceScore, @PitchScore,
                 @IntonationScore, @VoiceHealthScore, @CalculatedAt, @WarningFlags)";
            
            command.Parameters.AddWithValue("@SessionId", score.SessionId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@UserId", score.UserId);
            command.Parameters.AddWithValue("@OverallScore", score.OverallScore);
            command.Parameters.AddWithValue("@ResonanceScore", score.ResonanceScore);
            command.Parameters.AddWithValue("@PitchScore", score.PitchScore);
            command.Parameters.AddWithValue("@IntonationScore", score.IntonationScore);
            command.Parameters.AddWithValue("@VoiceHealthScore", score.VoiceHealthScore);
            command.Parameters.AddWithValue("@CalculatedAt", score.CalculatedAt.ToString("o"));
            command.Parameters.AddWithValue("@WarningFlags", score.WarningFlags ?? (object)DBNull.Value);
            
            command.ExecuteNonQuery();
        }
        
        #region Milestones and Daily Progress
        
        /// <summary>
        /// Lagrer en milepæl
        /// </summary>
        public void SaveMilestone(Milestone milestone)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO SmartCoachMilestones 
                (UserId, MilestoneType, Description, AchievedAt)
                VALUES (@UserId, @MilestoneType, @Description, @AchievedAt)";
            
            command.Parameters.AddWithValue("@UserId", milestone.UserId);
            command.Parameters.AddWithValue("@MilestoneType", milestone.MilestoneType);
            command.Parameters.AddWithValue("@Description", milestone.Description);
            command.Parameters.AddWithValue("@AchievedAt", milestone.AchievedAt?.ToString("o") ?? DateTime.Now.ToString("o"));
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Henter alle milepæler for en bruker
        /// </summary>
        public List<Milestone> GetMilestones(int userId = 1)
        {
            var milestones = new List<Milestone>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, MilestoneType, Description, AchievedAt
                FROM SmartCoachMilestones 
                WHERE UserId = @UserId
                ORDER BY AchievedAt DESC";
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                milestones.Add(new Milestone
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    MilestoneType = reader.GetString(2),
                    Description = reader.GetString(3),
                    AchievedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
                });
            }
            
            return milestones;
        }
        
        /// <summary>
        /// Lagrer daglig progresjon
        /// </summary>
        public void SaveDailyProgress(DailyProgressEntry progress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            // Try update first
            command.CommandText = @"
                UPDATE DailyProgress SET 
                    FemVoiceScore = @FemVoiceScore,
                    ResonanceScore = @ResonanceScore,
                    PitchScore = @PitchScore,
                    IntonationScore = @IntonationScore,
                    VoiceHealthScore = @VoiceHealthScore,
                    SessionMinutes = @SessionMinutes,
                    ExerciseType = @ExerciseType
                WHERE UserId = @UserId AND Date = @Date";
            
            command.Parameters.AddWithValue("@UserId", progress.UserId);
            command.Parameters.AddWithValue("@Date", progress.Date.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@FemVoiceScore", progress.FemVoiceScore);
            command.Parameters.AddWithValue("@ResonanceScore", progress.ResonanceScore);
            command.Parameters.AddWithValue("@PitchScore", progress.PitchScore);
            command.Parameters.AddWithValue("@IntonationScore", progress.IntonationScore);
            command.Parameters.AddWithValue("@VoiceHealthScore", progress.VoiceHealthScore);
            command.Parameters.AddWithValue("@SessionMinutes", progress.SessionMinutes);
            command.Parameters.AddWithValue("@ExerciseType", progress.ExerciseType ?? (object)DBNull.Value);
            
            var rowsAffected = command.ExecuteNonQuery();
            
            // If no row was updated, insert new record
            if (rowsAffected == 0)
            {
                command.CommandText = @"
                    INSERT INTO DailyProgress 
                    (UserId, Date, FemVoiceScore, ResonanceScore, PitchScore, IntonationScore, 
                     VoiceHealthScore, SessionMinutes, ExerciseType)
                    VALUES (@UserId, @Date, @FemVoiceScore, @ResonanceScore, @PitchScore, @IntonationScore,
                            @VoiceHealthScore, @SessionMinutes, @ExerciseType)";
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Henter daglig progresjon for en spesifikk dato
        /// </summary>
        public DailyProgressEntry? GetDailyProgress(DateTime date, int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, Date, FemVoiceScore, ResonanceScore, PitchScore, 
                       IntonationScore, VoiceHealthScore, SessionMinutes, ExerciseType
                FROM DailyProgress 
                WHERE UserId = @UserId AND Date = @Date";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Date", date.Date.ToString("yyyy-MM-dd"));
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new DailyProgressEntry
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    FemVoiceScore = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    ResonanceScore = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    PitchScore = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    IntonationScore = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                    VoiceHealthScore = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                    SessionMinutes = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    ExerciseType = reader.IsDBNull(9) ? null : reader.GetString(9)
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Henter daglig progresjon for en periode
        /// </summary>
        public List<DailyProgressEntry> GetDailyProgressRange(DateTime from, DateTime to, int userId = 1)
        {
            var entries = new List<DailyProgressEntry>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, Date, FemVoiceScore, ResonanceScore, PitchScore, 
                       IntonationScore, VoiceHealthScore, SessionMinutes, ExerciseType
                FROM DailyProgress 
                WHERE UserId = @UserId AND Date >= @From AND Date <= @To
                ORDER BY Date DESC";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@From", from.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@To", to.Date.ToString("yyyy-MM-dd"));
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(new DailyProgressEntry
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    FemVoiceScore = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    ResonanceScore = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    PitchScore = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    IntonationScore = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                    VoiceHealthScore = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                    SessionMinutes = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    ExerciseType = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
            
            return entries;
        }
        
        #region ComplexityProgress Methods
        
        /// <summary>
        /// Henter complexity progress for en bruker
        /// </summary>
        public virtual ComplexityProgress? GetComplexityProgress(int userId = 1)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT UserId, CurrentLevel, SessionsAtLevel, SuccessRate, LastEvaluationDate, IsReadyForNext
                FROM ComplexityProgress 
                WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ComplexityProgress
                {
                    UserId = reader.GetInt32(0),
                    CurrentLevel = reader.GetInt32(1),
                    SessionsAtLevel = reader.GetInt32(2),
                    SuccessRate = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    LastEvaluationDate = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    IsReadyForNext = reader.IsDBNull(5) ? false : reader.GetInt32(5) == 1
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Lagrer eller oppdaterer complexity progress
        /// </summary>
        public virtual void SaveComplexityProgress(ComplexityProgress progress)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ComplexityProgress (UserId, CurrentLevel, SessionsAtLevel, SuccessRate, LastEvaluationDate, IsReadyForNext)
                VALUES (@UserId, @CurrentLevel, @SessionsAtLevel, @SuccessRate, @LastEvaluationDate, @IsReadyForNext)
                ON CONFLICT(UserId) DO UPDATE SET
                    CurrentLevel = @CurrentLevel,
                    SessionsAtLevel = @SessionsAtLevel,
                    SuccessRate = @SuccessRate,
                    LastEvaluationDate = @LastEvaluationDate,
                    IsReadyForNext = @IsReadyForNext";
            
            command.Parameters.AddWithValue("@UserId", progress.UserId);
            command.Parameters.AddWithValue("@CurrentLevel", progress.CurrentLevel);
            command.Parameters.AddWithValue("@SessionsAtLevel", progress.SessionsAtLevel);
            command.Parameters.AddWithValue("@SuccessRate", progress.SuccessRate);
            command.Parameters.AddWithValue("@LastEvaluationDate", progress.LastEvaluationDate);
            command.Parameters.AddWithValue("@IsReadyForNext", progress.IsReadyForNext ? 1 : 0);
            
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Initialiserer complexity progress for ny bruker
        /// </summary>
        public virtual void InitializeComplexityProgress(int userId = 1)
        {
            var existing = GetComplexityProgress(userId);
            if (existing == null)
            {
                SaveComplexityProgress(new ComplexityProgress
                {
                    UserId = userId,
                    CurrentLevel = 0, // IsolatedSounds
                    SessionsAtLevel = 0,
                    SuccessRate = 0,
                    LastEvaluationDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    IsReadyForNext = false
                });
            }
        }
        
        #endregion
        
        #endregion
        
        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
    
    /// <summary>
    /// Data model for user progress (XP, level)
    
    /// <summary>
    /// Data model for user progress (XP, level)
    /// </summary>
    public class UserProgressData
    {
        public int Id { get; set; } = 1;
        public int TotalXP { get; set; }
        public int Level { get; set; } = 1;
        public int XPForCurrentLevel { get; set; }
        public int XPToNextLevel { get; set; } = 100;
        public int TotalSessions { get; set; }
        public int TotalMinutes { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
    }
    
    /// <summary>
    /// Data model for achievements
    /// </summary>
    public class AchievementData
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Category { get; set; } = "";
        public int XPReward { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
    }
    
    /// <summary>
    /// Data model for calendar day data (sessions, scores, pitch and resonance)
    /// </summary>
    public class CalendarDayData
    {
        public int Sessions { get; set; }
        public int Minutes { get; set; }
        public double Score { get; set; }
        public double PitchScore { get; set; }
        public double ResonanceScore { get; set; }
    }
    
    #endregion
    
    #region Smart Coach Data Models
    
    /// <summary>
    /// Data model for Smart Coach baseline
    /// </summary>
    public class SmartCoachBaseline
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public double BaselinePitch { get; set; }
        public double BaselineF1 { get; set; }
        public double BaselineF2 { get; set; }
        public double BaselineIntonation { get; set; }
        public double BaselineResonanceScore { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public int TrainingDaysCount { get; set; }
        public string ConfidenceLevel { get; set; } = "low";
    }
    
    /// <summary>
    /// Data model for Smart Coach goals
    /// </summary>
    public class SmartCoachGoal
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public string GoalType { get; set; } = ""; // pitch, resonance, intonation
        public double TargetValue { get; set; }
        public double CurrentValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsAchieved { get; set; }
        public DateTime? AchievedAt { get; set; }
        public int Priority { get; set; }
    }
    
    /// <summary>
    /// Data model for daily recommendations
    /// </summary>
    public class SmartCoachDailyRecommendation
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public DateTime Date { get; set; }
        public string FocusArea { get; set; } = ""; // resonance, pitch, intonation, breathing
        public string RecommendationText { get; set; } = "";
        public int? RecommendedExerciseId { get; set; }
        public int RecommendedDurationMinutes { get; set; } = 5;
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool HealthWarning { get; set; }
        public string? HealthWarningText { get; set; }
    }
    
    /// <summary>
    /// Data model for weekly progress
    /// </summary>
    public class SmartCoachWeeklyProgress
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public DateTime WeekStart { get; set; }
        public DateTime? WeekEnd { get; set; }
        public int SessionsCount { get; set; }
        public int TotalMinutes { get; set; }
        public double AverageScore { get; set; }
        public double AveragePitch { get; set; }
        public double AverageResonance { get; set; }
        public double AverageIntonation { get; set; }
        public double PitchChange { get; set; }
        public double ResonanceChange { get; set; }
        public double IntonationChange { get; set; }
        public double HealthScore { get; set; } = 100;
        public string? Notes { get; set; }
        public bool IsCompleted { get; set; }
    }
    
    /// <summary>
    /// Data model for health monitoring
    /// </summary>
    public class SmartCoachHealthMonitoring
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public DateTime Date { get; set; }
        public int? SessionId { get; set; }
        public bool StrainDetected { get; set; }
        public string? StrainType { get; set; }
        public double StrainLevel { get; set; }
        public string? Recommendation { get; set; }
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    
    /// <summary>
    /// Data model for coach messages
    /// </summary>
    public class SmartCoachMessage
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        public DateTime Date { get; set; }
        public string MessageType { get; set; } = ""; // motivation, tip, achievement, health_warning
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    
    #endregion
}
