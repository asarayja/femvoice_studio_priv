-- =====================================================
-- Database Migration Script for Exercise Feedback System
-- FemVoice Studio - femvoice.db
-- =====================================================

-- This script adds the necessary tables and columns for:
-- 1. Exercise definitions with target parameters
-- 2. Evaluation results logging
-- 3. User level tracking
-- 4. Health warnings history

-- =====================================================
-- TABLE: ExerciseDefinitions
-- Stores exercise target parameters and scaling factors
-- =====================================================
CREATE TABLE IF NOT EXISTS ExerciseDefinitions (
    DefinitionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ExerciseId INTEGER NOT NULL,
    
    -- Target pitch range (Hz)
    TargetPitchMin REAL DEFAULT 165.0,
    TargetPitchMax REAL DEFAULT 220.0,
    
    -- Target formant ranges (Hz)
    TargetF1Min REAL DEFAULT 300.0,
    TargetF1Max REAL DEFAULT 800.0,
    TargetF2Min REAL DEFAULT 1800.0,
    TargetF2Max REAL DEFAULT 2600.0,
    TargetF3Min REAL DEFAULT 2700.0,
    
    -- Stability threshold (jitter %)
    StabilityThreshold REAL DEFAULT 2.0,
    
    -- Strain limits
    MaxShimmerPercent REAL DEFAULT 4.0,
    MaxAmplitudeSpikePercent REAL DEFAULT 20.0,
    
    -- Requires intonation
    RequiresIntonation INTEGER DEFAULT 0,
    
    -- Goal category (0=Pitch, 1=Resonance, 2=Intonation, 3=Breathing, 4=Combined)
    GoalCategory INTEGER DEFAULT 0,
    
    -- Level scaling factors (JSON)
    NybegynnerFactors TEXT DEFAULT '{}',
    MiddelsFactors TEXT DEFAULT '{}',
    AvansertFactors TEXT DEFAULT '{}',
    
    -- Health thresholds (JSON)
    HealthThresholds TEXT DEFAULT '{}',
    
    -- Feedback rules (JSON)
    FeedbackRules TEXT DEFAULT '[]',
    
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
);

-- =====================================================
-- TABLE: ExerciseEvaluationLogs
-- Stores individual evaluation results during sessions
-- =====================================================
CREATE TABLE IF NOT EXISTS ExerciseEvaluationLogs (
    LogId INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId INTEGER NOT NULL,
    Timestamp TEXT NOT NULL,
    
    -- Status values (1=Correct, 2=Adjust, 3=Stop)
    OverallStatus INTEGER DEFAULT 1,
    ResonanceStatus INTEGER DEFAULT 1,
    PitchStatus INTEGER DEFAULT 1,
    StabilityStatus INTEGER DEFAULT 1,
    IntonationStatus INTEGER DEFAULT 0,
    HealthIndicator INTEGER DEFAULT 0,
    
    -- Coach hint key
    CoachHintKey TEXT DEFAULT '',
    
    -- Voice metrics at this point
    Pitch REAL,
    F1 REAL,
    F2 REAL,
    F3 REAL,
    Jitter REAL,
    Shimmer REAL,
    StrainLevel REAL,
    
    -- Target ranges used (JSON)
    TargetRanges TEXT DEFAULT '{}',
    
    FOREIGN KEY (SessionId) REFERENCES ExerciseSessions(SessionId)
);

-- =====================================================
-- TABLE: UserExerciseLevels
-- Tracks user level per exercise
-- =====================================================
CREATE TABLE IF NOT EXISTS UserExerciseLevels (
    LevelId INTEGER PRIMARY KEY AUTOINCREMENT,
    ExerciseId INTEGER NOT NULL,
    UserId INTEGER DEFAULT 1,
    
    -- Current level (1=Nybegynner, 2=Middels, 3=Avansert)
    CurrentLevel INTEGER DEFAULT 1,
    
    -- Statistics for level calculation
    TotalSessions INTEGER DEFAULT 0,
    AverageScore REAL DEFAULT 0,
    LastSessionDate TEXT,
    LastScore REAL DEFAULT 0,
    
    -- Temporary adjustments
    TemporaryPitchOffset REAL DEFAULT 0,
    LastAdjustmentReason TEXT,
    AdjustmentExpiry TEXT,
    
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    
    UNIQUE(ExerciseId, UserId),
    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
);

-- =====================================================
-- TABLE: HealthWarningHistory
-- Tracks health warnings for pattern analysis
-- =====================================================
CREATE TABLE IF NOT EXISTS HealthWarningHistory (
    WarningId INTEGER PRIMARY KEY AUTOINCREMENT,
    SessionId INTEGER,
    ExerciseId INTEGER NOT NULL,
    UserId INTEGER DEFAULT 1,
    
    -- Warning type
    WarningType TEXT NOT NULL, -- 'jitter', 'shimmer', 'amplitude', 'pitchPress'
    WarningLevel TEXT NOT NULL, -- 'warning', 'critical'
    
    -- Metrics at time of warning
    JitterValue REAL,
    ShimmerValue REAL,
    AmplitudeChange REAL,
    PitchValue REAL,
    
    -- Resolution
    Resolved INTEGER DEFAULT 0,
    Resolution TEXT, -- 'paused', 'stopped', 'continued'
    
    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (SessionId) REFERENCES ExerciseSessions(SessionId),
    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
);

-- =====================================================
-- TABLE: ExerciseAdaptationLogs
-- Logs all adaptations made by SmartCoach
-- =====================================================
CREATE TABLE IF NOT EXISTS ExerciseAdaptationLogs (
    LogId INTEGER PRIMARY KEY AUTOINCREMENT,
    ExerciseId INTEGER NOT NULL,
    UserId INTEGER DEFAULT 1,
    
    -- Adaptation type
    AdaptationType TEXT NOT NULL, -- 'level_change', 'pitch_adjustment', 'tolerance_change'
    
    -- Before and after values (JSON)
    BeforeValues TEXT DEFAULT '{}',
    AfterValues TEXT DEFAULT '{}',
    
    -- Reason
    Reason TEXT,
    
    Timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (ExerciseId) REFERENCES Exercises(ExerciseId)
);

-- =====================================================
-- INDEXES for performance
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_eval_logs_session ON ExerciseEvaluationLogs(SessionId);
CREATE INDEX IF NOT EXISTS idx_eval_logs_timestamp ON ExerciseEvaluationLogs(Timestamp);
CREATE INDEX IF NOT EXISTS idx_user_levels_exercise ON UserExerciseLevels(ExerciseId, UserId);
CREATE INDEX IF NOT EXISTS idx_health_warnings_session ON HealthWarningHistory(SessionId);
CREATE INDEX IF NOT EXISTS idx_health_warnings_type ON HealthWarningHistory(WarningType);

-- =====================================================
-- MIGRATION: Add new columns to existing tables
-- =====================================================

-- Add columns to Exercises table if not exists
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS TargetF1Min REAL DEFAULT 300.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS TargetF1Max REAL DEFAULT 800.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS TargetF2Min REAL DEFAULT 1800.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS TargetF2Max REAL DEFAULT 2600.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS TargetF3Min REAL DEFAULT 2700.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS StabilityThreshold REAL DEFAULT 2.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS MaxShimmerPercent REAL DEFAULT 4.0;
ALTER TABLE Exercises ADD COLUMN IF NOT EXISTS RequiresIntonation INTEGER DEFAULT 0;

-- =====================================================
-- SEED DATA: Insert default exercise definitions
-- =====================================================
INSERT OR IGNORE INTO ExerciseDefinitions (ExerciseId, TargetPitchMin, TargetPitchMax, TargetF1Min, TargetF1Max, TargetF2Min, TargetF2Max, TargetF3Min, GoalCategory)
SELECT ExerciseId, TargetPitchMin, TargetPitchMax, 300, 800, 1800, 2600, 2700, 
    CASE 
        WHEN Category = 'Pitch-kontroll' THEN 0
        WHEN Category = 'Resonans' THEN 1
        WHEN Category = 'Intonasjon' THEN 2
        WHEN Category = 'Pust' THEN 3
        WHEN Category = 'Avansert' THEN 4
        ELSE 0
    END
FROM Exercises
WHERE NOT EXISTS (SELECT 1 FROM ExerciseDefinitions WHERE ExerciseDefinitions.ExerciseId = Exercises.ExerciseId);

-- =====================================================
-- VIEWS for common queries
-- =====================================================
CREATE VIEW IF NOT EXISTS vw_RecentExercisePerformance AS
SELECT 
    es.ExerciseId,
    e.Name as ExerciseName,
    COUNT(es.SessionId) as SessionCount,
    AVG(es.Score) as AverageScore,
    MAX(es.StartTime) as LastSessionDate,
    MIN(es.StartTime) as FirstSessionDate
FROM ExerciseSessions es
JOIN Exercises e ON es.ExerciseId = e.ExerciseId
WHERE es.Completed = 1
GROUP BY es.ExerciseId;

CREATE VIEW IF NOT EXISTS vw_HealthWarningSummary AS
SELECT 
    ExerciseId,
    WarningType,
    COUNT(*) as WarningCount,
    MIN(Timestamp) as FirstOccurrence,
    MAX(Timestamp) as LastOccurrence
FROM HealthWarningHistory
GROUP BY ExerciseId, WarningType;

-- =====================================================
-- MIGRATION COMPLETE
-- =====================================================
-- Version: 1.0
-- Date: 2026-02-17
