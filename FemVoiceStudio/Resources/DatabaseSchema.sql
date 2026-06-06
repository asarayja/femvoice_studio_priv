-- =============================================================================
-- FemVoice Studio Database Schema
-- Smart Coach System Tables
-- =============================================================================
-- This file contains the complete database schema for the FemVoice Studio
-- application, including the Smart Coach system tables.
--
-- Version: 2.0
-- Date: February 2026
-- =============================================================================

-- =============================================================================
-- SECTION 1: Core Tables (Existing)
-- =============================================================================

-- User Settings Table
-- Stores user preferences and progression data
-- -----------------------------------------------------------------------------
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
    HearOwnVoice INTEGER DEFAULT 0,
    Theme TEXT DEFAULT 'System',
    -- Smart Coach specific fields
    CurrentTrainingLevel INTEGER DEFAULT 1,
    LastLevelChangeDate TEXT,
    BaselinePitch REAL DEFAULT 0,
    BaselineF1 REAL DEFAULT 0,
    BaselineF2 REAL DEFAULT 0,
    LastBaselineCalculation TEXT
);

-- Training Sessions Table
-- Stores individual training session data
-- -----------------------------------------------------------------------------
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
    -- Resonance fields
    ResonanceScore REAL DEFAULT 0,
    AverageF1 REAL DEFAULT 0,
    AverageF2 REAL DEFAULT 0,
    AverageF3 REAL DEFAULT 0,
    ResonanceCategory INTEGER DEFAULT 0,
    SpectralCentroid REAL DEFAULT 0,
    -- Voice health
    StrainLevel REAL DEFAULT 0,
    -- Additional analysis
    TempoWPM REAL DEFAULT 0,
    DurationSeconds INTEGER DEFAULT 0
);

-- Progression History Table
-- Stores daily progression snapshots
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ProgressionHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL,
    DifficultyLevel INTEGER,
    AveragePitch REAL,
    SessionCount INTEGER,
    BestScore REAL,
    AverageScore REAL,
    TotalMinutes INTEGER DEFAULT 0
);

-- Achievements Table
-- Stores earned achievements/badges
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Achievements (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT UNIQUE NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    Icon TEXT NOT NULL,
    Category TEXT NOT NULL,
    UnlockedAt TEXT,
    UserId INTEGER DEFAULT 1
);

-- Exercise Texts Table
-- Stores available exercise texts
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ExerciseTexts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL,
    DifficultyLevel INTEGER NOT NULL,
    Category TEXT,
    Language TEXT DEFAULT 'nb-NO'
);

-- =============================================================================
-- SECTION 2: Smart Coach Tables (NEW)
-- =============================================================================

-- FemVoice Scores Table
-- Stores calculated FemVoice scores with timestamps
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS FemVoiceScores (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    SessionId INTEGER,
    OverallScore REAL NOT NULL,
    ResonanceScore REAL NOT NULL,
    PitchScore REAL NOT NULL,
    IntonationScore REAL NOT NULL,
    VoiceHealthScore REAL NOT NULL,
    CalculatedAt TEXT NOT NULL,
    WarningFlags TEXT,
    -- Reference to TrainingSession
    FOREIGN KEY (SessionId) REFERENCES TrainingSessions(Id)
);

-- Training Levels Table
-- Tracks user training level progression
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS TrainingLevels (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    Level INTEGER NOT NULL,  -- 1=Beginner, 2=Intermediate, 3=Advanced
    StartDate TEXT NOT NULL,
    EndDate TEXT,
    UpgradeReason TEXT,
    DowngradeReason TEXT,
    IsCurrent INTEGER DEFAULT 1
);

-- Direction Recommendations Table
-- Stores coaching direction recommendations
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS DirectionRecommendations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    SessionId INTEGER,
    Timestamp TEXT NOT NULL,
    Parameter TEXT NOT NULL,  -- 'Pitch', 'Resonance', 'Intonation', 'VoiceHealth'
    Direction INTEGER NOT NULL,  -- 0=Increase, 1=Decrease, 2=Stabilize, 3=Maintain
    CurrentValue REAL,
    TargetValue REAL,
    ChangeAmount REAL,
    Reason TEXT,
    SafetyNote TEXT,
    FOREIGN KEY (SessionId) REFERENCES TrainingSessions(Id)
);

-- Voice Profiles Table
-- Stores personalized learning data
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VoiceProfiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1 UNIQUE,
    CreatedAt TEXT NOT NULL,
    LastUpdated TEXT NOT NULL,
    -- Baseline measurements
    BaselinePitch REAL DEFAULT 0,
    BaselineF1 REAL DEFAULT 0,
    BaselineF2 REAL DEFAULT 0,
    -- Current targets
    TargetMinPitch REAL DEFAULT 165,
    TargetMaxPitch REAL DEFAULT 255,
    TargetMinF1 REAL DEFAULT 400,
    TargetMinF2 REAL DEFAULT 1400,
    -- Learning data
    CurrentLevel INTEGER DEFAULT 1,
    LastLevelChange TEXT,
    OptimalSessionMinutes INTEGER DEFAULT 5,
    WeeklyProgressionRate REAL DEFAULT 2.0,
    -- Strengths and weaknesses (0-100)
    ResonanceStrength REAL DEFAULT 50,
    PitchStrength REAL DEFAULT 50,
    IntonationStrength REAL DEFAULT 50,
    MostImprovedParameter TEXT
);

-- Exercise Effectiveness Table
-- Tracks which exercises work best for each user
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ExerciseEffectiveness (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    ExerciseType TEXT NOT NULL,
    TimesCompleted INTEGER DEFAULT 0,
    AverageScoreDelta REAL DEFAULT 0,
    LastUsed TEXT,
    WinRate REAL DEFAULT 0,  -- Percentage of times score > 60
    UNIQUE(UserId, ExerciseType)
);

-- Daily Progress Table
-- Stores daily progress snapshots for trend analysis
-- -----------------------------------------------------------------------------
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

-- Smart Coach Baseline Table
-- Stores calculated baselines for users
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SmartCoachBaselines (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    CalculatedAt TEXT NOT NULL,
    BaselinePitch REAL DEFAULT 0,
    BaselineF1 REAL DEFAULT 0,
    BaselineF2 REAL DEFAULT 0,
    BaselineIntonation REAL DEFAULT 0,
    BaselineResonanceScore REAL DEFAULT 0,
    TrainingDaysCount INTEGER DEFAULT 0,
    ConfidenceLevel TEXT DEFAULT 'low',  -- 'low', 'medium', 'high'
    UNIQUE(UserId, date(CalculatedAt))
);

-- Smart Coach Goals Table
-- Stores user-specific goals
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SmartCoachGoals (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    GoalType TEXT NOT NULL,  -- 'pitch', 'resonance', 'intonation'
    TargetValue REAL NOT NULL,
    CurrentValue REAL NOT NULL,
    StartDate TEXT NOT NULL,
    TargetDate TEXT NOT NULL,
    Priority INTEGER DEFAULT 1,
    IsCompleted INTEGER DEFAULT 0,
    CompletedAt TEXT
);

-- Smart Coach Weekly Progress Table
-- Stores weekly progress snapshots
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SmartCoachWeeklyProgress (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    WeekStart TEXT NOT NULL,
    WeekEnd TEXT NOT NULL,
    SessionsCount INTEGER DEFAULT 0,
    TotalMinutes INTEGER DEFAULT 0,
    AverageScore REAL DEFAULT 0,
    AveragePitch REAL DEFAULT 0,
    AverageResonance REAL DEFAULT 0,
    AverageIntonation REAL DEFAULT 0,
    PitchChange REAL DEFAULT 0,
    ResonanceChange REAL DEFAULT 0,
    IntonationChange REAL DEFAULT 0,
    HealthScore REAL DEFAULT 100,
    UNIQUE(UserId, WeekStart)
);

-- Smart Coach Daily Recommendations Table
-- Stores generated daily recommendations
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SmartCoachDailyRecommendations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    Date TEXT NOT NULL,
    FocusArea TEXT NOT NULL,  -- 'resonance', 'pitch', 'intonation', 'balanced', 'recovery'
    RecommendationText TEXT,
    RecommendedExerciseId INTEGER,
    RecommendedDurationMinutes INTEGER DEFAULT 5,
    HealthWarning INTEGER DEFAULT 0,
    HealthWarningText TEXT,
    UNIQUE(UserId, Date)
);

-- Health Issues Table
-- Tracks voice health concerns
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS HealthIssues (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER DEFAULT 1,
    SessionId INTEGER,
    DetectedAt TEXT NOT NULL,
    StrainLevel REAL DEFAULT 0,
    StrainType TEXT,
    StrainDetected INTEGER DEFAULT 0,
    Recommendation TEXT,
    FOREIGN KEY (SessionId) REFERENCES TrainingSessions(Id)
);

-- =============================================================================
-- SECTION 3: Indexes for Performance
-- =============================================================================

CREATE INDEX IF NOT EXISTS idx_femvoicescores_userid ON FemVoiceScores(UserId);
CREATE INDEX IF NOT EXISTS idx_femvoicescores_calculatedat ON FemVoiceScores(CalculatedAt);
CREATE INDEX IF NOT EXISTS idx_trainingsessions_starttime ON TrainingSessions(StartTime);
CREATE INDEX IF NOT EXISTS idx_trainingsessions_userid ON TrainingSessions(Id);
CREATE INDEX IF NOT EXISTS idx_directionrecommendations_timestamp ON DirectionRecommendations(Timestamp);
CREATE INDEX IF NOT EXISTS idx_dailyprogress_date ON DailyProgress(Date);
CREATE INDEX IF NOT EXISTS idx_voiceprofiles_userid ON VoiceProfiles(UserId);

-- =============================================================================
-- SECTION 4: Initial Data (Defaults)
-- =============================================================================

-- Insert default user settings if not exists
INSERT OR IGNORE INTO UserSettings (Id) VALUES (1);

-- Insert default training level if not exists
INSERT OR IGNORE INTO TrainingLevels (UserId, Level, StartDate, IsCurrent) 
VALUES (1, 1, datetime('now'), 1);

-- Insert default voice profile if not exists
INSERT OR IGNORE INTO VoiceProfiles (UserId, CreatedAt, LastUpdated)
VALUES (1, datetime('now'), datetime('now'));

-- =============================================================================
-- SECTION 5: Upgrade Scripts (for existing databases)
-- =============================================================================

-- Add Smart Coach columns to existing tables if they don't exist
-- These are safe to run on existing databases

-- ALTER TABLE UserSettings ADD COLUMN CurrentTrainingLevel INTEGER DEFAULT 1;
-- ALTER TABLE UserSettings ADD COLUMN LastLevelChangeDate TEXT;
-- ALTER TABLE UserSettings ADD COLUMN BaselinePitch REAL DEFAULT 0;
-- ALTER TABLE UserSettings ADD COLUMN BaselineF1 REAL DEFAULT 0;
-- ALTER TABLE UserSettings ADD COLUMN BaselineF2 REAL DEFAULT 0;
-- ALTER TABLE UserSettings ADD COLUMN LastBaselineCalculation TEXT;

-- ALTER TABLE TrainingSessions ADD COLUMN StrainLevel REAL DEFAULT 0;
-- ALTER TABLE TrainingSessions ADD COLUMN TempoWPM REAL DEFAULT 0;
-- ALTER TABLE TrainingSessions ADD COLUMN DurationSeconds INTEGER DEFAULT 0;

-- =============================================================================
-- END OF SCHEMA
-- =============================================================================
