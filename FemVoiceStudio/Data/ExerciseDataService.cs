using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Data
{
    /// <summary>
    /// Service for database-operasjoner knyttet til Ã¸velser og treningsÃ¸kter
    /// </summary>
    public class ExerciseDataService
    {
        private readonly string _connectionString;
        
                private SqliteConnection OpenConnection()
                {
                    var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    try
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "PRAGMA foreign_keys = ON;";
                        cmd.ExecuteNonQuery();
                    }
                    catch { }
                    return connection;
                }
        
        public ExerciseDataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>Robust dato-lesing: legacy-databaser kan ha 0/''/ugyldig i datokolonner.</summary>
        private static DateTime? ReadDateOrNull(SqliteDataReader reader, int ordinal)
            => !reader.IsDBNull(ordinal) && DateTime.TryParse(reader.GetString(ordinal), out var dt)
                ? dt : (DateTime?)null;
        
        /// <summary>
        /// Hent alle tilgjengelige Ã¸velser
        /// </summary>
        public List<Exercise> GetAllExercises()
        {
            var exercises = new List<Exercise>();
            
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.*, 
                       COALESCE(ep.TotalSessions, 0) as TotalSessions,
                       ep.LastSessionDate,
                       COALESCE(ep.AverageScore, 0) as AverageScore
                FROM Exercises e
                LEFT JOIN ExerciseProgress ep ON e.ExerciseId = ep.ExerciseId AND ep.UserId = 1
                WHERE e.IsActive = 1
                ORDER BY e.SortOrder";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                exercises.Add(MapToExercise(reader));
            }
            
            return exercises;
        }
        
        /// <summary>
        /// Hent en spesifikk Ã¸velse etter ID
        /// </summary>
        public Exercise? GetExerciseById(int exerciseId)
        {
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.*, 
                       COALESCE(ep.TotalSessions, 0) as TotalSessions,
                       ep.LastSessionDate,
                       COALESCE(ep.AverageScore, 0) as AverageScore
                FROM Exercises e
                LEFT JOIN ExerciseProgress ep ON e.ExerciseId = ep.ExerciseId AND ep.UserId = 1
                WHERE e.ExerciseId = @ExerciseId";
            command.Parameters.AddWithValue("@ExerciseId", exerciseId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return MapToExercise(reader);
            }
            
            return null;
        }
        
        /// <summary>
        /// Hent Ã¸velser etter vanskelighetsnivÃ¥
        /// </summary>
        public List<Exercise> GetExercisesByDifficulty(DifficultyLevel difficulty)
        {
            var exercises = new List<Exercise>();
            
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.*, 
                       COALESCE(ep.TotalSessions, 0) as TotalSessions,
                       ep.LastSessionDate,
                       COALESCE(ep.AverageScore, 0) as AverageScore
                FROM Exercises e
                LEFT JOIN ExerciseProgress ep ON e.ExerciseId = ep.ExerciseId AND ep.UserId = 1
                WHERE e.IsActive = 1 AND e.DifficultyLevel = @Difficulty
                ORDER BY e.SortOrder";
            command.Parameters.AddWithValue("@Difficulty", (int)difficulty);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                exercises.Add(MapToExercise(reader));
            }
            
            return exercises;
        }
        
        /// <summary>
        /// Hent Ã¸velser etter kategori
        /// </summary>
        public List<Exercise> GetExercisesByCategory(string category)
        {
            var exercises = new List<Exercise>();
            
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.*, 
                       COALESCE(ep.TotalSessions, 0) as TotalSessions,
                       ep.LastSessionDate,
                       COALESCE(ep.AverageScore, 0) as AverageScore
                FROM Exercises e
                LEFT JOIN ExerciseProgress ep ON e.ExerciseId = ep.ExerciseId AND ep.UserId = 1
                WHERE e.IsActive = 1 AND e.Category = @Category
                ORDER BY e.SortOrder";
            command.Parameters.AddWithValue("@Category", category);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                exercises.Add(MapToExercise(reader));
            }
            
            return exercises;
        }
        
        /// <summary>
        /// Start en ny Ã¸velsesÃ¸kt
        /// </summary>
        public int StartSession(int exerciseId)
        {
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ExerciseSessions (ExerciseId, UserId, StartTime, Completed)
                VALUES (@ExerciseId, 1, @StartTime, 0);
                SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@ExerciseId", exerciseId);
            command.Parameters.AddWithValue("@StartTime", DateTime.Now.ToString("o"));
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        /// <summary>
        /// FullfÃ¸r en Ã¸velsesÃ¸kt
        /// </summary>
        public void CompleteSession(int sessionId, int durationSeconds, double score, string notes = "")
        {
            using var connection = OpenConnection();
            
            // Oppdater Ã¸kten
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE ExerciseSessions 
                SET EndTime = @EndTime, DurationSeconds = @Duration, Completed = 1, Score = @Score, Notes = @Notes
                WHERE SessionId = @SessionId";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.Parameters.AddWithValue("@EndTime", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("@Duration", durationSeconds);
            command.Parameters.AddWithValue("@Score", score);
            command.Parameters.AddWithValue("@Notes", notes);
            command.ExecuteNonQuery();
            
            // Oppdater progresjon
            UpdateExerciseProgress(connection, sessionId, durationSeconds, score);
        }
        
        /// <summary>
        /// Avbryt en Ã¸velsesÃ¸kt (ikke fullfÃ¸rt)
        /// </summary>
        public void CancelSession(int sessionId)
        {
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE ExerciseSessions 
                SET EndTime = @EndTime, Completed = 0
                WHERE SessionId = @SessionId AND Completed = 0";
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.Parameters.AddWithValue("@EndTime", DateTime.Now.ToString("o"));
            command.ExecuteNonQuery();
        }
        
        /// <summary>
        /// Hent progresjon for en spesifikk Ã¸velse
        /// </summary>
        public ExerciseProgress? GetExerciseProgress(int exerciseId)
        {
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ExerciseProgress WHERE ExerciseId = @ExerciseId AND UserId = 1";
            command.Parameters.AddWithValue("@ExerciseId", exerciseId);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ExerciseProgress
                {
                    ProgressId = reader.GetInt32(0),
                    ExerciseId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    TotalSessions = reader.GetInt32(3),
                    LastSessionDate = ReadDateOrNull(reader, 4),
                    TotalMinutes = reader.GetInt32(5),
                    BestScore = reader.GetDouble(6),
                    AverageScore = reader.GetDouble(7),
                    CurrentStreak = reader.GetInt32(8),
                    LongestStreak = reader.GetInt32(9)
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// Hent alle Ã¸velser med progresjon
        /// </summary>
        public List<ExerciseProgress> GetAllExerciseProgress()
        {
            var progressList = new List<ExerciseProgress>();
            
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ExerciseProgress WHERE UserId = 1";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                progressList.Add(new ExerciseProgress
                {
                    ProgressId = reader.GetInt32(0),
                    ExerciseId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    TotalSessions = reader.GetInt32(3),
                    LastSessionDate = ReadDateOrNull(reader, 4),
                    TotalMinutes = reader.GetInt32(5),
                    BestScore = reader.GetDouble(6),
                    AverageScore = reader.GetDouble(7),
                    CurrentStreak = reader.GetInt32(8),
                    LongestStreak = reader.GetInt32(9)
                });
            }
            
            return progressList;
        }
        
        /// <summary>
        /// Sjekk om en Ã¸velse er fullfÃ¸rt i dag
        /// </summary>
        public bool IsExerciseCompletedToday(int exerciseId)
        {
            using var connection = OpenConnection();
            
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM ExerciseSessions 
                WHERE ExerciseId = @ExerciseId 
                AND Completed = 1 
                AND date(StartTime) = @Today";
            command.Parameters.AddWithValue("@ExerciseId", exerciseId);
            command.Parameters.AddWithValue("@Today", today);
            
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        
        /// <summary>
        /// Initialiserer standard Ã¸velser i databasen
        /// </summary>
        public void InitializeExercises()
        {
            using var connection = OpenConnection();

            // Migrer nye kolonner hvis de ikke eksisterer
            MigrateExerciseColumns(connection);

            // Engangs-nullstilling av tidsbaserte scores (klinisk score-migrering)
            EnsureClinicalScoreMigration(connection);

            // Slett eksisterende Ã¸velser og legg til alle pÃ¥ nytt inside a transaction
            using var tran = connection.BeginTransaction();
            try
            {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = tran;
                deleteCmd.CommandText = "DELETE FROM Exercises";
                deleteCmd.ExecuteNonQuery();
            
            // Definer standard Ã¸velser med utvidet metadata for voice feminization
            var exercises = new[] {
                // Ã˜VELSE 1: Grunnleggende humming - Fremre resonans + pitch-bevissthet
                new { 
                    Name = "Grunnleggende humming", 
                    Description = "LÃ¦r Ã¥ kjenne pÃ¥ vibrasjonen i stemmen og flytt den fremover i munnen. Humming aktiverer fremre resonatorer.",
                    Steps = new[] { "Slapp av i skuldrene og nakken", "Pust dypt inn gjennom nesen", "Hum en behagelig tone", "Flytt hummotsetningen mot nesen og leppene", "Hold tonen i 5-10 sekunder" },
                    Duration = 5, Frequency = 1, Difficulty = 1, 
                    Metrics = new[] { "pitch", "resonance" },
                    Category = "Oppvarming",
                    Icon = "ðŸŽµ",
                    SortOrder = 1,
                    Goal = 1, // Resonance
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 140.0,
                    TargetPitchMax = 180.0,
                    ProfileType = Models.ExerciseProfileType.ResonanceHumming,
                },
                // Ã˜VELSE 2: Vokallyder - Fremre resonans
                new { 
                    Name = "Vokallyder - Fremre resonans", 
                    Description = "Utforsk ulike vokallyder med fokus pÃ¥ fremre munnresonans. Ã…pne vokaler fremmer fremre resonans.",
                    Steps = new[] { "Si 'ahhh' - Ã¥pne munnen bred", "Si 'eee' - trekk munnvikene bakover", "Si 'ooo' - runde lepper", "Kombiner: 'ah-ee-oo'", "Gjenta mÃ¸nsteret 5 ganger" },
                    Duration = 7, Frequency = 1, Difficulty = 1,
                    Metrics = new[] { "pitch", "resonance", "intensity" },
                    Category = "Oppvarming",
                    Icon = "ðŸ—£ï¸",
                    SortOrder = 2,
                    Goal = 1, // Resonance
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 150.0,
                    TargetPitchMax = 190.0,
                    ProfileType = Models.ExerciseProfileType.ResonanceVowels,
                },
                // Ã˜VELSE 3: Stigende Toner - Pitch Glide Up
                new { 
                    Name = "Stigende toner (Glide Up)", 
                    Description = "Tren pÃ¥ Ã¥ bevege deg jevnt fra lavere til hÃ¸yere pitch. Gliding gir mer naturlig progresjon.",
                    Steps = new[] { "Start pÃ¥ en behagelig tone", "Glid sakte opp over 3 sekunder", "Hold topptonen i 2 sekunder", "Glid sakte ned igjen", "FÃ¸lg med pÃ¥ pitch-displayet" },
                    Duration = 8, Frequency = 1, Difficulty = 1,
                    Metrics = new[] { "pitch", "smoothness" },
                    Category = "Pitch-kontroll",
                    Icon = "ðŸ“ˆ",
                    SortOrder = 3,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 140.0,
                    TargetPitchMax = 200.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 4: Synkende Toner - Pitch Glide Down
                new { 
                    Name = "Synkende toner (Glide Down)", 
                    Description = "Tren pÃ¥ kontrollert nedgliding i pitch for Ã¥ styrke musklene og lÃ¦re mÃ¥l-pitch-omrÃ¥det.",
                    Steps = new[] { "Start hÃ¸yt og komfortabelt", "Glid sakte ned over 3 sekunder", "Hold den lave tonen i 2 sekunder", "Gjenta med ulike startpunkter", "Fokuser pÃ¥ jevn glide" },
                    Duration = 6, Frequency = 2, Difficulty = 1,
                    Metrics = new[] { "pitch", "smoothness", "consistency" },
                    Category = "Pitch-kontroll",
                    Icon = "ðŸ“‰",
                    SortOrder = 4,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 160.0,
                    TargetPitchMax = 220.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 5: Konsistens-Trening
                new { 
                    Name = "Konsistens-trening", 
                    Description = "FokusÃ©r pÃ¥ Ã¥ holde samme tone stabil over tid. Stabil pitch er grunnleggende for naturlig feminin stemme.",
                    Steps = new[] { "Finn din target-tone", "Hold tonen i 5 sekunder", "Sjekk pitch-grafen for stabilitet", "Ta en pust", "Gjenta 5 ganger" },
                    Duration = 8, Frequency = 1, Difficulty = 1,
                    Metrics = new[] { "pitch", "consistency" },
                    Category = "Stabilitet",
                    Icon = "ðŸ“Š",
                    SortOrder = 5,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 165.0,
                    TargetPitchMax = 180.0,
                    ProfileType = Models.ExerciseProfileType.StabilityTraining,
                },
                // Ã˜VELSE 6: S-Lyder
                new { 
                    Name = "S-lyder (Ustemt hold)", 
                    Description = "Tren pÃ¥ Ã¥ opprettholde pitch gjennom ustemte lyder for bedre kontroll.",
                    Steps = new[] { "Si 'ssssssss' - lang S-lyd", "FÃ¸lg med pÃ¥ pitch-grafen", "PrÃ¸v Ã¥ holde lyden stabil", "Ã˜k gradvis til 10 sekunder", "Gjenta 3 ganger" },
                    Duration = 6, Frequency = 2, Difficulty = 2,
                    Metrics = new[] { "pitch", "consistency" },
                    Category = "Stabilitet",
                    Icon = "ðŸ’¨",
                    SortOrder = 6,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 160.0,
                    TargetPitchMax = 200.0,
                    ProfileType = Models.ExerciseProfileType.StabilityTraining,
                },
                // Ã˜VELSE 7: SpÃ¸rsmÃ¥ls-Intonasjon
                new { 
                    Name = "SpÃ¸rsmÃ¥lsmelodi", 
                    Description = "LÃ¦r naturlig stigende intonasjon. I norsk stiger pitch 2-4 semitoner pÃ¥ slutten av spÃ¸rsmÃ¥l.",
                    Steps = new[] { "Si 'Hva heter du?'", "Legg merke til stigningen pÃ¥ slutten", "Ã˜v pÃ¥ 5 ulike spÃ¸rsmÃ¥l", "PrÃ¸v jevn stigning", "Varier styrken" },
                    Duration = 7, Frequency = 2, Difficulty = 2,
                    Metrics = new[] { "intonation", "pitch" },
                    Category = "Intonasjon",
                    Icon = "â“",
                    SortOrder = 7,
                    Goal = 2, // Intonation
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 165.0,
                    TargetPitchMax = 220.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 8: Utsagns-Intonasjon
                new { 
                    Name = "Utsagnsmelodi", 
                    Description = "Tren pÃ¥ Ã¥ avslutte setninger med synkende tone. Kritisk for Ã¥ unngÃ¥ 'spÃ¸rsmÃ¥lslyd'.",
                    Steps = new[] { "Si 'Jeg heter Marie.'", "Legg merke til fallet pÃ¥ slutten", "Ã˜v pÃ¥ 5 ulike utsagn", "PrÃ¸v naturlig fall", "Varier start-pitch" },
                    Duration = 7, Frequency = 2, Difficulty = 2,
                    Metrics = new[] { "intonation", "pitch" },
                    Category = "Intonasjon",
                    Icon = "ðŸ“¢",
                    SortOrder = 8,
                    Goal = 2, // Intonation
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 155.0,
                    TargetPitchMax = 210.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 9: Fraselesing
                new { 
                    Name = "Fraselesing", 
                    Description = "Kombiner alle ferdigheter i sammenhengende talesprÃ¥k med pitch i mÃ¥l-omrÃ¥det.",
                    Steps = new[] { "Velg en tekst", "Les med fokus pÃ¥ pitch innen mÃ¥lomrÃ¥det", "Stopp ved vanskelige ord", "Les hele teksten", "Spill av og vurder" },
                    Duration = 10, Frequency = 2, Difficulty = 2,
                    Metrics = new[] { "pitch", "intonation", "consistency" },
                    Category = "Praksis",
                    Icon = "ðŸ“–",
                    SortOrder = 9,
                    Goal = 4, // Combined
                    GoalIcon = "â­",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 165.0,
                    TargetPitchMax = 220.0,
                    ProfileType = Models.ExerciseProfileType.ResonanceVowels,
                },
                // Ã˜VELSE 10: Samtale-Simulasjon
                new { 
                    Name = "Samtale-simulasjon", 
                    Description = "Simuler vanlige samtalesituasjoner. OverfÃ¸ring fra Ã¸velse til spontant sprÃ¥k er nÃ¸kkelen.",
                    Steps = new[] { "Tenk pÃ¥ vanlige spÃ¸rsmÃ¥l", "Svar med naturlig intonasjon", "Varier mellom spÃ¸rsmÃ¥l/utsagn", "FÃ¸lg med pÃ¥ pitch", "PrÃ¸v 5 ulike scenarier" },
                    Duration = 10, Frequency = 3, Difficulty = 3,
                    Metrics = new[] { "pitch", "intonation", "resonance" },
                    Category = "Avansert",
                    Icon = "ðŸ’¬",
                    SortOrder = 10,
                    Goal = 4, // Combined
                    GoalIcon = "â­",
                    ScientificRationale = "",
                    FrequencyText = "2Ã—/uke",
                    TargetPitchMin = 165.0,
                    TargetPitchMax = 230.0,
                    ProfileType = Models.ExerciseProfileType.ResonanceVowels,
                },
                // NYE Ã˜VELSER 11-15
                // Ã˜VELSE 11: Resonans-Skift
                new { 
                    Name = "Resonans-skift: Fremre plassering", 
                    Description = "Tren pÃ¥ Ã¥ flytte resonansen fra bakre til fremre plassering. Kritisk for feminin klang.",
                    Steps = new[] { "Hum 'mmm' - kjenn lepper/nese vibrere", "OverfÃ¸r til 'nnn'", "Deretter 'yyy' (norsk 'j')", "PrÃ¸v 'ene'-stavelser", "Gjenta med korte ord" },
                    Duration = 7, Frequency = 1, Difficulty = 1,
                    Metrics = new[] { "resonance", "intensity" },
                    Category = "Resonans",
                    Icon = "ðŸ”Š",
                    SortOrder = 11,
                    Goal = 1, // Resonance
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 150.0,
                    TargetPitchMax = 200.0,
                    ProfileType = Models.ExerciseProfileType.ResonanceHumming,
                },
                // Ã˜VELSE 12: Starter-Pitch Memorisering
                new { 
                    Name = "Starter-pitch memorisering", 
                    Description = "Automatiser start-pitch i mÃ¥lomrÃ¥det. De fleste starter for lavt - dette er kritisk Ã¥ rette opp.",
                    Steps = new[] { "Syng en referansetone som fÃ¸les komfortabel", "Start tale pÃ¥ denne tonen", "Sjekk start-pitch med appen", "Juster opp hvis nÃ¸dvendig", "Gjenta til automatisk" },
                    Duration = 8, Frequency = 1, Difficulty = 1,
                    Metrics = new[] { "pitch", "consistency" },
                    Category = "Pitch-kontroll",
                    Icon = "ðŸŽ¯",
                    SortOrder = 12,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "Daglig",
                    TargetPitchMin = 165.0,
                    TargetPitchMax = 180.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 13: Pitch Slide i Fraser
                new { 
                    Name = "Pitch slide i fraser", 
                    Description = "Bruk glide-teknikk naturlig i setninger istedet for Ã¥ 'spre' stemmen.",
                    Steps = new[] { "'Hallo' med stigende glide", "'Hei' med fallende glide", "'Ja?' som spÃ¸rsmÃ¥l", "Korte fraser med glide", "Varier stigende/synkende" },
                    Duration = 8, Frequency = 2, Difficulty = 2,
                    Metrics = new[] { "pitch", "intonation", "smoothness" },
                    Category = "Pitch-kontroll",
                    Icon = "ðŸŽ¢",
                    SortOrder = 13,
                    Goal = 0, // Pitch
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "",
                    FrequencyText = "3Ã—/uke",
                    TargetPitchMin = 150.0,
                    TargetPitchMax = 220.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                },
                // Ã˜VELSE 14: Straw Phonation
                new { 
                    Name = "Straw phonation (Halmsfonasjon)", 
                    Description = "Reduser stemmeslitasje og styrk airflow-kontroll med halm-teknikk. Tvinger frem semi-okkludert vokaltrakt.",
                    Steps = new[] { "Ta en sugerÃ¸r", "BlÃ¥s lett gjennom", "Syng 'ooo' gjennom strÃ¥et", "Hold i 5 sekunder", "Gjenta med ulike toner" },
                    Duration = 5, Frequency = 3, Difficulty = 2,
                    Metrics = new[] { "intensity", "consistency" },
                    Category = "Pust",
                    Icon = "ðŸ¥¤",
                    SortOrder = 14,
                    Goal = 3, // Breathing
                    GoalIcon = "ðŸ’¨",
                    ScientificRationale = "",
                    FrequencyText = "2Ã—/uke",
                    TargetPitchMin = 140.0,
                    TargetPitchMax = 180.0,
                    ProfileType = Models.ExerciseProfileType.StabilityTraining,
                },
                // Ã˜VELSE 15: Intonasjons-Variasjon
                new { 
                    Name = "Intonasjons-variasjon", 
                    Description = "Varier intonasjon for naturlig, ekspressiv tale. Kvinner bruker stÃ¸rre variasjon enn menn.",
                    Steps = new[] { "'Nei' - nÃ¸ytralt", "'Nei?' - overrasket", "'Nei!' - frustrert", "'Nei...' - resignert", "Gjenta med ulike setninger" },
                    Duration = 10, Frequency = 3, Difficulty = 3,
                    Metrics = new[] { "intonation", "pitchVariability" },
                    Category = "Avansert",
                    Icon = "ðŸŽ­",
                    SortOrder = 15,
                    Goal = 2, // Intonation
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "",
                    FrequencyText = "2Ã—/uke",
                    TargetPitchMin = 150.0,
                    TargetPitchMax = 250.0,
                    ProfileType = Models.ExerciseProfileType.CoordinatedGlideUp,
                }
            };
            
            foreach (var ex in exercises)
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Exercises (Name, Description, StepsJson, DurationMinutes, FrequencyType, DifficultyLevel, MetricsJson, Category, Icon, SortOrder, Goal, GoalIcon, ScientificRationale, FrequencyText, TargetPitchMin, TargetPitchMax, ProfileType)
                    VALUES (@Name, @Desc, @Steps, @Duration, @Frequency, @Difficulty, @Metrics, @Category, @Icon, @SortOrder, @Goal, @GoalIcon, @Rationale, @FreqText, @PitchMin, @PitchMax, @ProfileType)";
                
                insertCmd.Parameters.AddWithValue("@Name", ex.Name);
                insertCmd.Parameters.AddWithValue("@Desc", ex.Description);
                insertCmd.Parameters.AddWithValue("@Steps", JsonSerializer.Serialize(ex.Steps));
                insertCmd.Parameters.AddWithValue("@Duration", ex.Duration);
                insertCmd.Parameters.AddWithValue("@Frequency", ex.Frequency);
                insertCmd.Parameters.AddWithValue("@Difficulty", ex.Difficulty);
                insertCmd.Parameters.AddWithValue("@Metrics", JsonSerializer.Serialize(ex.Metrics));
                insertCmd.Parameters.AddWithValue("@Category", ex.Category);
                insertCmd.Parameters.AddWithValue("@Icon", ex.Icon);
                insertCmd.Parameters.AddWithValue("@SortOrder", ex.SortOrder);
                insertCmd.Parameters.AddWithValue("@Goal", ex.Goal);
                insertCmd.Parameters.AddWithValue("@GoalIcon", ex.GoalIcon);
                insertCmd.Parameters.AddWithValue("@Rationale", ex.ScientificRationale);
                insertCmd.Parameters.AddWithValue("@FreqText", ex.FrequencyText);
                insertCmd.Parameters.AddWithValue("@PitchMin", ex.TargetPitchMin);
                insertCmd.Parameters.AddWithValue("@PitchMax", ex.TargetPitchMax);
                insertCmd.Parameters.AddWithValue("@ProfileType", (int)ex.ProfileType);
                
                insertCmd.ExecuteNonQuery();
            }
                tran.Commit();
            }
            catch
            {
                try { tran.Rollback(); } catch { }
                // If seeding fails, do not leave an incomplete delete; abort seeding silently.
            }
        }

        /// <summary>
        /// Hent Ã¸velser som bÃ¸r gjÃ¸res i dag basert pÃ¥ frekvens
        /// </summary>
        public List<Exercise> GetTodaysRecommendedExercises()
        {
            var exercises = new List<Exercise>();
            var today = DateTime.Today;
            var dayOfWeek = (int)today.DayOfWeek;
            
            using var connection = OpenConnection();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.*, 
                       COALESCE(ep.TotalSessions, 0) as TotalSessions,
                       ep.LastSessionDate,
                       COALESCE(ep.AverageScore, 0) as AverageScore
                FROM Exercises e
                LEFT JOIN ExerciseProgress ep ON e.ExerciseId = ep.ExerciseId AND ep.UserId = 1
                WHERE e.IsActive = 1
                AND (
                    (e.FrequencyType = 1) OR  -- Daglig
                    (e.FrequencyType = 2 AND @DayOfWeek IN (0, 2, 4)) OR  -- 3x/uke (man, ons, fre)
                    (e.FrequencyType = 3 AND @DayOfWeek IN (1, 4)) OR  -- 2x/uke (tir, fre)
                    (e.FrequencyType = 4 AND @DayOfWeek = 6)  -- Ukentlig (sÃ¸n)
                )
                ORDER BY e.FrequencyType, e.SortOrder";
            command.Parameters.AddWithValue("@DayOfWeek", dayOfWeek);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                exercises.Add(MapToExercise(reader));
            }
            
            return exercises;
        }
        
        /// <summary>
        /// Hent antall fullfÃ¸rte Ã¸kter i dag
        /// </summary>
        public int GetCompletedSessionsToday()
        {
            using var connection = OpenConnection();
            
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM ExerciseSessions 
                WHERE Completed = 1 AND date(StartTime) = @Today";
            command.Parameters.AddWithValue("@Today", today);
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        /// <summary>
        /// Hent total treningstid i dag (minutter)
        /// </summary>
        public int GetTotalMinutesToday()
        {
            using var connection = OpenConnection();
            
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COALESCE(SUM(DurationSeconds), 0) / 60 FROM ExerciseSessions 
                WHERE Completed = 1 AND date(StartTime) = @Today";
            command.Parameters.AddWithValue("@Today", today);
            
            return Convert.ToInt32(command.ExecuteScalar());
        }
        
        #region Private Helpers
        
        private Exercise MapToExercise(SqliteDataReader reader)
        {
            var exercise = new Exercise
            {
                ExerciseId = reader.GetInt32(reader.GetOrdinal("ExerciseId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                StepsJson = reader.GetString(reader.GetOrdinal("StepsJson")),
                DurationMinutes = reader.GetInt32(reader.GetOrdinal("DurationMinutes")),
                Frequency = (FrequencyType)reader.GetInt32(reader.GetOrdinal("FrequencyType")),
                DifficultyLevel = (DifficultyLevel)reader.GetInt32(reader.GetOrdinal("DifficultyLevel")),
                MetricsJson = reader.GetString(reader.GetOrdinal("MetricsJson")),
                Category = reader.GetString(reader.GetOrdinal("Category")),
                Icon = reader.IsDBNull(reader.GetOrdinal("Icon")) ? "ðŸŽ¤" : reader.GetString(reader.GetOrdinal("Icon")),
                TotalSessions = reader.GetInt32(reader.GetOrdinal("TotalSessions")),
                LastSessionDate = ReadDateOrNull(reader, reader.GetOrdinal("LastSessionDate")),
                AverageScore = reader.GetDouble(reader.GetOrdinal("AverageScore")),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
            };
            
            // Nye felt for voice feminization - med try-catch for bakoverkompatibilitet
            try { exercise.Goal = (GoalCategory)reader.GetInt32(reader.GetOrdinal("Goal")); } catch { exercise.Goal = GoalCategory.Pitch; }
            try { exercise.GoalIcon = reader.IsDBNull(reader.GetOrdinal("GoalIcon")) ? "ðŸŽµ" : reader.GetString(reader.GetOrdinal("GoalIcon")); } catch { exercise.GoalIcon = "ðŸŽµ"; }
            try { exercise.ScientificRationale = reader.IsDBNull(reader.GetOrdinal("ScientificRationale")) ? "" : reader.GetString(reader.GetOrdinal("ScientificRationale")); } catch { exercise.ScientificRationale = ""; }
            try { exercise.FrequencyText = reader.IsDBNull(reader.GetOrdinal("FrequencyText")) ? "Daglig" : reader.GetString(reader.GetOrdinal("FrequencyText")); } catch { exercise.FrequencyText = "Daglig"; }
            try { exercise.TargetPitchMin = reader.IsDBNull(reader.GetOrdinal("TargetPitchMin")) ? 140.0 : reader.GetDouble(reader.GetOrdinal("TargetPitchMin")); } catch { exercise.TargetPitchMin = 140.0; }
            try { exercise.TargetPitchMax = reader.IsDBNull(reader.GetOrdinal("TargetPitchMax")) ? 220.0 : reader.GetDouble(reader.GetOrdinal("TargetPitchMax")); } catch { exercise.TargetPitchMax = 220.0; }
            try { exercise.ProfileType = (Models.ExerciseProfileType)reader.GetInt32(reader.GetOrdinal("ProfileType")); } catch { exercise.ProfileType = Models.ExerciseProfileType.ResonanceHumming; }
            
            // Parse JSON til lister
            try
            {
                if (!string.IsNullOrEmpty(exercise.StepsJson) && exercise.StepsJson != "[]")
                {
                    exercise.Steps = JsonSerializer.Deserialize<List<string>>(exercise.StepsJson) ?? new List<string>();
                }
            }
            catch { exercise.Steps = new List<string>(); }
            
            return exercise;
        }
        
        /// <summary>
        /// Engangs-migrering: nullstiller AverageScore/BestScore i ExerciseProgress.
        ///
        /// Bakgrunn: Frem til den kliniske score-fiksen var øktscore ren tid +
        /// oppmøtebonus — verdiene var systematisk inflaterte og uten stemmedata.
        /// Etter avklaring med bruker nullstilles de én gang slik at mastery bygges
        /// på reelle kliniske data. TotalSessions/streaks beholdes. Idempotent via
        /// markørrad i SchemaMeta.
        /// </summary>
        private void EnsureClinicalScoreMigration(SqliteConnection connection)
        {
            try
            {
                using var transaction = connection.BeginTransaction();

                var createCmd = connection.CreateCommand();
                createCmd.Transaction = transaction;
                createCmd.CommandText = "CREATE TABLE IF NOT EXISTS SchemaMeta (Key TEXT PRIMARY KEY, Value TEXT)";
                createCmd.ExecuteNonQuery();

                var checkCmd = connection.CreateCommand();
                checkCmd.Transaction = transaction;
                checkCmd.CommandText = "SELECT Value FROM SchemaMeta WHERE Key = 'ClinicalScoreReset_v1'";
                if (checkCmd.ExecuteScalar() == null)
                {
                    var resetCmd = connection.CreateCommand();
                    resetCmd.Transaction = transaction;
                    resetCmd.CommandText = "UPDATE ExerciseProgress SET AverageScore = 0, BestScore = 0";
                    resetCmd.ExecuteNonQuery();

                    var markCmd = connection.CreateCommand();
                    markCmd.Transaction = transaction;
                    markCmd.CommandText = @"
                        INSERT INTO SchemaMeta (Key, Value)
                        VALUES ('ClinicalScoreReset_v1', @Now)";
                    markCmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("o"));
                    markCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                // Migreringsfeil skal aldri blokkere oppstart — neste kjøring prøver igjen.
            }
        }

        private void UpdateExerciseProgress(SqliteConnection connection, int sessionId, int durationSeconds, double score)
        {
            // Hent ExerciseId fra session
            var getCmd = connection.CreateCommand();
            getCmd.CommandText = "SELECT ExerciseId FROM ExerciseSessions WHERE SessionId = @SessionId";
            getCmd.Parameters.AddWithValue("@SessionId", sessionId);
            var exerciseId = Convert.ToInt32(getCmd.ExecuteScalar());
            
            // Sjekk om progresjon eksisterer
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT ProgressId FROM ExerciseProgress WHERE ExerciseId = @ExerciseId AND UserId = 1";
            checkCmd.Parameters.AddWithValue("@ExerciseId", exerciseId);
            var exists = checkCmd.ExecuteScalar() != null;
            
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            
            if (exists)
            {
                // Oppdater eksisterende progresjon
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE ExerciseProgress SET
                        TotalSessions = TotalSessions + 1,
                        LastSessionDate = @LastDate,
                        TotalMinutes = TotalMinutes + @Minutes,
                        BestScore = CASE WHEN @Score > BestScore THEN @Score ELSE BestScore END,
                        AverageScore = ((AverageScore * TotalSessions) + @Score) / (TotalSessions + 1),
                        CurrentStreak = CASE 
                            WHEN date(LastSessionDate) = @Yesterday THEN CurrentStreak + 1
                            WHEN date(LastSessionDate) = @Today THEN CurrentStreak
                            ELSE 1 END,
                        LongestStreak = CASE 
                            WHEN ((CurrentStreak + 1) > LongestStreak) THEN (CurrentStreak + 1) 
                            ELSE LongestStreak END
                    WHERE ExerciseId = @ExerciseId AND UserId = 1";
                updateCmd.Parameters.AddWithValue("@ExerciseId", exerciseId);
                updateCmd.Parameters.AddWithValue("@LastDate", today.ToString("o"));
                updateCmd.Parameters.AddWithValue("@Minutes", durationSeconds / 60);
                updateCmd.Parameters.AddWithValue("@Score", score);
                updateCmd.Parameters.AddWithValue("@Yesterday", yesterday.ToString("yyyy-MM-dd"));
                updateCmd.Parameters.AddWithValue("@Today", today.ToString("yyyy-MM-dd"));
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                // Opprett ny progresjon
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO ExerciseProgress (ExerciseId, UserId, TotalSessions, LastSessionDate, TotalMinutes, BestScore, AverageScore, CurrentStreak, LongestStreak)
                    VALUES (@ExerciseId, 1, 1, @LastDate, @Minutes, @Score, @Score, 1, 1)";
                insertCmd.Parameters.AddWithValue("@ExerciseId", exerciseId);
                insertCmd.Parameters.AddWithValue("@LastDate", today.ToString("o"));
                insertCmd.Parameters.AddWithValue("@Minutes", durationSeconds / 60);
                insertCmd.Parameters.AddWithValue("@Score", score);
                insertCmd.ExecuteNonQuery();
            }
        }
        
        #endregion
        
        /// <summary>
        /// Migrer nye kolonner til Exercises tabell for bakoverkompatibilitet
        /// </summary>
        private void MigrateExerciseColumns(SqliteConnection connection)
        {
            try
            {
                var columns = new[] {
                    ("Goal", "INTEGER DEFAULT 0"),
                    ("GoalIcon", "TEXT DEFAULT 'ðŸŽµ'"),
                    ("ScientificRationale", "TEXT DEFAULT ''"),
                    ("FrequencyText", "TEXT DEFAULT 'Daglig'"),
                    ("TargetPitchMin", "REAL DEFAULT 140.0"),
                    ("TargetPitchMax", "REAL DEFAULT 220.0"),
                    // DEFAULT 0 = ResonanceHumming â€” safe baseline for pre-existing rows
                    ("ProfileType", "INTEGER NOT NULL DEFAULT 0")
                };
                
                foreach (var (columnName, columnDef) in columns)
                {
                    try
                    {
                        // Sjekk om kolonnen allerede eksisterer
                        var checkCmd = connection.CreateCommand();
                        checkCmd.CommandText = $"SELECT {columnName} FROM Exercises LIMIT 1";
                        checkCmd.ExecuteScalar();
                    }
                    catch
                    {
                        // Kolonnen eksisterer ikke - legg den til
                        var addCmd = connection.CreateCommand();
                        addCmd.CommandText = $"ALTER TABLE Exercises ADD COLUMN {columnName} {columnDef}";
                        addCmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // Ignorer migreringsfeil - fortsett uansett
            }
        }
    }
}

