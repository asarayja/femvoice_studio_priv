using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Subsystems.SmartCoach;
using Microsoft.Data.Sqlite;

namespace FemVoiceStudio.Subsystems.Data
{
    /// <summary>
    /// Data Subsystem - handles data persistence and repository operations
    /// Wraps existing DatabaseService with async operations and repository pattern
    /// </summary>
    public class DataSubsystem : IDataSubsystem
    {
        private readonly DatabaseService _databaseService;
        private readonly string _databasePath;
        private bool _disposed;

        public DataSubsystem(string? databasePath = null)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");
            
            Directory.CreateDirectory(appDataPath);
            
            _databasePath = databasePath ?? Path.Combine(appDataPath, "femvoice.db");
            _databaseService = new DatabaseService(_databasePath);
        }

        public async Task<IEnumerable<TrainingSession>> GetSessionsAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            return await Task.Run(() => _databaseService.GetTrainingSessions(from, to), ct);
        }

        public async Task<TrainingSession?> GetSessionAsync(int sessionId, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var sessions = _databaseService.GetTrainingSessions(
                    DateTime.MinValue, 
                    DateTime.MaxValue);
                
                foreach (var session in sessions)
                {
                    if (session.Id == sessionId)
                        return session;
                }
                
                return null;
            }, ct);
        }

        public async Task<int> SaveSessionAsync(TrainingSession session, CancellationToken ct = default)
        {
            return await Task.Run(() => _databaseService.SaveTrainingSession(session), ct);
        }

        public async Task UpdateSessionAsync(TrainingSession session, CancellationToken ct = default)
        {
            await Task.Run(() => _databaseService.UpdateTrainingSession(session), ct);
        }

        public async Task DeleteSessionAsync(int sessionId, CancellationToken ct = default)
        {
            await Task.Run(() => _databaseService.DeleteTrainingSession(sessionId), ct);
        }

        public async Task<UserProfile> GetUserProfileAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var settings = _databaseService.GetUserSettings();
                
                return new UserProfile
                {
                    UserId = settings.Id,
                    CurrentDifficulty = settings.CurrentDifficulty,
                    TotalSessionsCompleted = settings.TotalSessionsCompleted,
                    CurrentStreak = settings.CurrentStreak,
                    LastActiveAt = settings.LastSessionDate ?? DateTime.MinValue
                };
            }, ct);
        }

        public async Task SaveUserProfileAsync(UserProfile profile, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                var settings = _databaseService.GetUserSettings();
                
                settings.CurrentDifficulty = profile.CurrentDifficulty;
                settings.TotalSessionsCompleted = profile.TotalSessionsCompleted;
                settings.CurrentStreak = profile.CurrentStreak;
                settings.LastSessionDate = profile.LastActiveAt;
                
                _databaseService.SaveUserSettings(settings);
            }, ct);
        }

        public async Task<UserSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            return await Task.Run(() => _databaseService.GetUserSettings(), ct);
        }

        public async Task SaveSettingsAsync(UserSettings settings, CancellationToken ct = default)
        {
            await Task.Run(() => _databaseService.SaveUserSettings(settings), ct);
        }

        public async Task<SmartCoach.VoiceProfile> GetVoiceProfileAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                // Load from existing voice profile storage or create new
                var profile = new SmartCoach.VoiceProfile();
                
                // Try to load from database
                try
                {
                    var settings = _databaseService.GetUserSettings();
                    
                    // Map settings to voice profile
                    profile.TargetMinPitch = settings.PreferredMinPitch;
                    profile.TargetMaxPitch = settings.PreferredMaxPitch;
                    profile.CurrentLevel = (Progression.TrainingLevel)(int)settings.CurrentDifficulty;
                }
                catch
                {
                    // Return default profile
                }
                
                return profile;
            }, ct);
        }

        public async Task SaveVoiceProfileAsync(SmartCoach.VoiceProfile profile, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                var settings = _databaseService.GetUserSettings();
                
                settings.PreferredMinPitch = profile.TargetMinPitch;
                settings.PreferredMaxPitch = profile.TargetMaxPitch;
                settings.CurrentDifficulty = (Models.DifficultyLevel)(int)profile.CurrentLevel;
                
                _databaseService.SaveUserSettings(settings);
            }, ct);
        }

        public async Task ExportDataAsync(string format, string filePath, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                var sessions = _databaseService.GetTrainingSessions(DateTime.MinValue, DateTime.MaxValue);
                var settings = _databaseService.GetUserSettings();

                if (format.ToLower() == "json")
                {
                    var exportData = new
                    {
                        ExportedAt = DateTime.Now,
                        Settings = settings,
                        Sessions = sessions,
                        TotalSessions = ((List<TrainingSession>)sessions).Count
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(exportData, options);
                    File.WriteAllText(filePath, json);
                }
                else if (format.ToLower() == "csv")
                {
                    var lines = new List<string>
                    {
                        "Id,StartTime,EndTime,AveragePitch,MinPitch,MaxPitch,PitchVariation,IntonationScore,OverallScore,ResonanceScore"
                    };

                    foreach (var session in sessions)
                    {
                        lines.Add($"{session.Id},{session.StartTime},{session.EndTime},{session.AveragePitch},{session.MinPitch},{session.MaxPitch},{session.PitchVariation},{session.IntonationScore},{session.OverallScore},{session.ResonanceScore}");
                    }

                    File.WriteAllLines(filePath, lines);
                }
            }, ct);
        }

        public async Task ImportDataAsync(string format, string filePath, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Import file not found: {filePath}");
                }

                if (format.ToLower() == "json")
                {
                    var json = File.ReadAllText(filePath);
                    var importData = JsonSerializer.Deserialize<JsonElement>(json);

                    if (importData.TryGetProperty("Sessions", out var sessionsElement))
                    {
                        var sessions = JsonSerializer.Deserialize<List<TrainingSession>>(sessionsElement.GetRawText());
                        if (sessions != null)
                        {
                            foreach (var session in sessions)
                            {
                                _databaseService.SaveTrainingSession(session);
                            }
                        }
                    }
                }
                // CSV import would require more complex parsing
            }, ct);
        }

        public async Task<string> CreateBackupAsync(string? customPath = null, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var backupDir = customPath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "FemVoiceStudio", "backups");
                
                Directory.CreateDirectory(backupDir);
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupDir, $"femvoice_backup_{timestamp}.db");
                
                File.Copy(_databasePath, backupPath, overwrite: true);
                
                return backupPath;
            }, ct);
        }

        public async Task RestoreBackupAsync(string backupPath, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException($"Backup file not found: {backupPath}");
                }

                // Close existing connection
                _databaseService.Dispose();
                
                // Copy backup over current database
                File.Copy(backupPath, _databasePath, overwrite: true);
                
                // Reinitialize
                _databaseService.Dispose();
            }, ct);
        }

        public async Task<StatisticsSummary> GetStatisticsSummaryAsync(CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                var settings = _databaseService.GetUserSettings();
                var sessions = _databaseService.GetTrainingSessions(
                    DateTime.MinValue, 
                    DateTime.MaxValue);
                
                var sessionList = sessions is List<TrainingSession> list ? list : new List<TrainingSession>(sessions);

                double avgScore = 0;
                double bestScore = 0;
                
                if (sessionList.Count > 0)
                {
                    avgScore = sessionList.Average(s => s.OverallScore);
                    bestScore = sessionList.Max(s => s.OverallScore);
                }

                return new StatisticsSummary
                {
                    TotalSessions = sessionList.Count,
                    TotalMinutes = settings.TotalSessionsCompleted * 5, // Approximate
                    AverageScore = avgScore,
                    BestScore = bestScore,
                    CurrentStreak = settings.CurrentStreak,
                    LongestStreak = settings.CurrentStreak, // Would need separate tracking
                    FirstSessionDate = sessionList.FirstOrDefault()?.StartTime,
                    LastSessionDate = settings.LastSessionDate
                };
            }, ct);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _databaseService.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
