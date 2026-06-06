using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using FemVoiceStudio.Data;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Håndterer XP, nivå, achievements og streaks
    /// </summary>
    public class GamificationService
    {
        private readonly string _connectionString;
        
        // XP per aktivitet
        private const int XP_PER_SESSION = 15;
        private const int XP_PER_MINUTE = 2;
        private const int XP_PER_STREAK_DAY = 10;
        
        public event EventHandler<AchievementUnlockedEventArgs>? AchievementUnlocked;
        public event EventHandler<LevelUpEventArgs>? LevelUp;
        
        public GamificationService(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        /// <summary>
        /// Registrer fullført økt og beregn XP
        /// </summary>
        public SessionReward RecordSession(int durationMinutes, double score)
        {
            var reward = new SessionReward();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var progress = GetUserProgress(connection);
            
            // Beregn XP
            int xpEarned = XP_PER_SESSION + (durationMinutes * XP_PER_MINUTE);
            
            // Bonus for score
            xpEarned += (int)(score * 0.5);
            
            // Sjekk streak bonus
            int streakBonus = CheckAndUpdateStreak(connection, progress);
            if (streakBonus > 0)
            {
                xpEarned += streakBonus * XP_PER_STREAK_DAY;
                reward.StreakBonus = streakBonus;
            }
            
            // Oppdater total XP
            progress.TotalXP += xpEarned;
            reward.TotalXP = progress.TotalXP;
            
            // Sjekk for level up
            while (progress.TotalXP >= progress.XPToNextLevel)
            {
                progress.TotalXP -= progress.XPToNextLevel;
                progress.Level++;
                progress.XPToNextLevel = CalculateXPForLevel(progress.Level + 1);
                progress.XPForCurrentLevel = 0;
                
                LevelUp?.Invoke(this, new LevelUpEventArgs(progress.Level));
            }
            
            progress.XPForCurrentLevel = progress.TotalXP;
            progress.TotalSessions++;
            progress.TotalMinutes += durationMinutes;
            
            // Lagre
            SaveUserProgress(connection, progress);
            
            // Sjekk achievements
            var newAchievements = CheckAchievements(connection, progress);
            reward.NewAchievements = newAchievements;
            
            foreach (var achievement in newAchievements)
            {
                progress.TotalXP += achievement.XPReward;
                AchievementUnlocked?.Invoke(this, new AchievementUnlockedEventArgs(achievement));
            }
            
            SaveUserProgress(connection, progress);
            
            reward.XPEarned = xpEarned;
            reward.NewLevel = progress.Level;
            
            return reward;
        }
        
        private int CalculateXPForLevel(int level)
        {
            return (int)(100 * Math.Pow(level, 1.5));
        }
        
        private UserProgressData GetUserProgress(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM UserProgress WHERE Id = 1";
            
            using var reader = cmd.ExecuteReader();
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
        
        private int CheckAndUpdateStreak(SqliteConnection connection, UserProgressData progress)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            
            // Sjekk om det er en aktiv streak
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT StreakDay FROM DailyStreaks WHERE Date = @Date";
            cmd.Parameters.AddWithValue("@Date", yesterday.ToString("yyyy-MM-dd"));
            
            var yesterdayResult = cmd.ExecuteScalar();
            
            int currentStreak = 0;
            if (yesterdayResult != null)
            {
                currentStreak = Convert.ToInt32(yesterdayResult);
            }
            
            // Sjekk om bruker har trent i dag
            cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM TrainingSessions WHERE date(StartTime) = @Date";
            cmd.Parameters.AddWithValue("@Date", today.ToString("yyyy-MM-dd"));
            
            var todayCount = Convert.ToInt32(cmd.ExecuteScalar());
            
            if (todayCount == 0)
            {
                return 0; // Ikke trent i dag enda
            }
            
            // Oppdater streak
            currentStreak++;
            progress.CurrentStreak = currentStreak;
            if (currentStreak > progress.LongestStreak)
            {
                progress.LongestStreak = currentStreak;
            }
            
            // Lagre dagens streak
            SaveDailyStreak(connection, today, 1, currentStreak);
            
            return currentStreak;
        }
        
        private void SaveDailyStreak(SqliteConnection connection, DateTime date, int sessions, int streakDay)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DailyStreaks (Date, SessionsCompleted, StreakDay, TargetMet)
                VALUES (@Date, @Sessions, @StreakDay, 1)
                ON CONFLICT(Date) DO UPDATE SET
                    SessionsCompleted = SessionsCompleted + @Sessions,
                    StreakDay = @StreakDay,
                    TargetMet = 1";
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Sessions", sessions);
            cmd.Parameters.AddWithValue("@StreakDay", streakDay);
            
            cmd.ExecuteNonQuery();
        }
        
        private List<AchievementData> CheckAchievements(SqliteConnection connection, UserProgressData progress)
        {
            var newAchievements = new List<AchievementData>();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Achievements WHERE IsUnlocked = 0";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var achievement = new AchievementData
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.GetString(3),
                    Icon = reader.GetString(4),
                    Category = reader.GetString(5),
                    XPReward = reader.GetInt32(6)
                };
                
                bool shouldUnlock = EvaluateAchievement(achievement.Code, progress);
                
                if (shouldUnlock)
                {
                    achievement.IsUnlocked = true;
                    achievement.UnlockedAt = DateTime.Now;
                    
                    var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE Achievements SET IsUnlocked = 1, UnlockedAt = @UnlockedAt WHERE Id = @Id";
                    updateCmd.Parameters.AddWithValue("@UnlockedAt", achievement.UnlockedAt.Value.ToString("o"));
                    updateCmd.Parameters.AddWithValue("@Id", achievement.Id);
                    updateCmd.ExecuteNonQuery();
                    
                    newAchievements.Add(achievement);
                }
            }
            
            return newAchievements;
        }
        
        private bool EvaluateAchievement(string code, UserProgressData progress)
        {
            return code switch
            {
                "FIRST_SESSION" => progress.TotalSessions >= 1,
                "SESSION_10" => progress.TotalSessions >= 10,
                "SESSION_50" => progress.TotalSessions >= 50,
                "SESSION_100" => progress.TotalSessions >= 100,
                "SESSION_500" => progress.TotalSessions >= 500,
                "WEEK_STREAK" => progress.CurrentStreak >= 7,
                "MONTH_STREAK" => progress.CurrentStreak >= 30,
                "QUARTER_STREAK" => progress.CurrentStreak >= 90,
                _ => false
            };
        }
        
        private void SaveUserProgress(SqliteConnection connection, UserProgressData progress)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE UserProgress SET 
                    TotalXP = @TotalXP, Level = @Level, XPForCurrentLevel = @XPForCurrent,
                    XPToNextLevel = @XPToNext, TotalSessions = @Sessions, TotalMinutes = @Minutes,
                    CurrentStreak = @Streak, LongestStreak = @LongestStreak
                WHERE Id = 1";
            
            cmd.Parameters.AddWithValue("@TotalXP", progress.TotalXP);
            cmd.Parameters.AddWithValue("@Level", progress.Level);
            cmd.Parameters.AddWithValue("@XPForCurrent", progress.XPForCurrentLevel);
            cmd.Parameters.AddWithValue("@XPToNext", progress.XPToNextLevel);
            cmd.Parameters.AddWithValue("@Sessions", progress.TotalSessions);
            cmd.Parameters.AddWithValue("@Minutes", progress.TotalMinutes);
            cmd.Parameters.AddWithValue("@Streak", progress.CurrentStreak);
            cmd.Parameters.AddWithValue("@LongestStreak", progress.LongestStreak);
            
            cmd.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Hent alle achievements
        /// </summary>
        public List<AchievementData> GetAllAchievements()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Achievements ORDER BY Category, Id";
            
            var achievements = new List<AchievementData>();
            using var reader = cmd.ExecuteReader();
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
    }
    
    public class SessionReward
    {
        public int XPEarned { get; set; }
        public int TotalXP { get; set; }
        public int NewLevel { get; set; }
        public int StreakBonus { get; set; }
        public List<AchievementData> NewAchievements { get; set; } = new();
    }
    
    public class AchievementUnlockedEventArgs : EventArgs
    {
        public AchievementData Achievement { get; }
        public AchievementUnlockedEventArgs(AchievementData achievement) => Achievement = achievement;
    }
    
    public class LevelUpEventArgs : EventArgs
    {
        public int NewLevel { get; }
        public LevelUpEventArgs(int level) => NewLevel = level;
    }
}
