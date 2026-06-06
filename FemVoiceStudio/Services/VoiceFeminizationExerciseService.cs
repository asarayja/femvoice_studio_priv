using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Enhanced exercise model with voice feminization-specific fields
    /// </summary>
    public class EnhancedExercise
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Steps { get; set; } = new();
        public int DurationMinutes { get; set; }
        public FrequencyType Frequency { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public string Category { get; set; } = "";
        public string Icon { get; set; } = "ðŸŽ¤";
        public GoalCategory Goal { get; set; }
        public string GoalIcon { get; set; } = "ðŸŽµ";
        public string ScientificRationale { get; set; } = "";
        public double TargetPitchMin { get; set; }
        public double TargetPitchMax { get; set; }
        public List<MetricType> Metrics { get; set; } = new();
        
        /// <summary>
        /// Convert to Exercise for database compatibility
        /// </summary>
        public Exercise ToExercise()
        {
            return new Exercise
            {
                ExerciseId = Id,
                Name = Name,
                Description = Description,
                Steps = Steps,
                StepsJson = System.Text.Json.JsonSerializer.Serialize(Steps),
                DurationMinutes = DurationMinutes,
                Frequency = Frequency,
                DifficultyLevel = Difficulty,
                Category = Category,
                Icon = Icon,
                Goal = Goal,
                GoalIcon = GoalIcon,
                ScientificRationale = ScientificRationale,
                MetricsToTrack = Metrics,
                MetricsJson = System.Text.Json.JsonSerializer.Serialize(Metrics),
                FrequencyText = GetFrequencyText()
            };
        }
        
        private string GetFrequencyText()
        {
            return Frequency switch
            {
                FrequencyType.Daglig => "Daglig",
                FrequencyType.TreGangerUkentlig => "3Ã—/uke",
                FrequencyType.ToGangerUkentlig => "2Ã—/uke",
                FrequencyType.Ukentlig => "Ukentlig",
                _ => "Daglig"
            };
        }
    }
    
    /// <summary>
    /// Service for evidensbaserte stemmetreningsÃ¸velser for transfeminine personer.
    /// Inneholder reviderte og nye Ã¸velser basert pÃ¥ dokumenterte voice feminization-teknikker.
    /// </summary>
    public class VoiceFeminizationExerciseService
    {
        /// <summary>
        /// Hent alle evidensbaserte Ã¸velser med full metadata
        /// </summary>
        public List<EnhancedExercise> GetAllEnhancedExercises()
        {
            return new List<EnhancedExercise>
            {
                // Ã˜VELSE 1: Grunnleggende Humming - Fremre resonans + pitch-bevissthet
                new EnhancedExercise
                {
                    Id = 1,
                    Name = "Grunnleggende humming",
                    Description = "LÃ¦r Ã¥ kjenne pÃ¥ vibrasjonen i stemmen og flytt den fremover i munnen. Humming aktiverer fremre resonatorer.",
                    Steps = new List<string> {
                        "Slapp av i skuldrene og nakken",
                        "Pust dypt inn gjennom nesen",
                        "Hum en behagelig tone",
                        "Flytt hummotsetningen mot nesen og leppene",
                        "Hold tonen i 5-10 sekunder"
                    },
                    DurationMinutes = 5,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Oppvarming",
                    Icon = "ðŸŽµ",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "Humming aktiverer fremre resonatorer og forbereder stemmen pÃ¥ hÃ¸yere pitch uten spenning. Vibrasjon i lepper/nese indikerer korrekt fremre resonans.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Resonance }
                },

                // Ã˜VELSE 2: Vokallyder - Fremre resonans
                new EnhancedExercise
                {
                    Id = 2,
                    Name = "Vokallyder - Fremre resonans",
                    Description = "Utforsk ulike vokallyder med fokus pÃ¥ fremre munnresonans. Ã…pne vokaler fremmer fremre resonans.",
                    Steps = new List<string> {
                        "Si 'ahhh' - Ã¥pne munnen bred",
                        "Si 'eee' - trekk munnvikene bakover",
                        "Si 'ooo' - runde lepper",
                        "Kombiner: 'ah-ee-oo'",
                        "Gjenta mÃ¸nsteret 5 ganger"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Oppvarming",
                    Icon = "ðŸ—£ï¸",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "Ã…pnere vokaler (a, Ã¦) fremmer fremre resonans, mens 'eee' hever tungryggen og skaper 'head voice'-klang.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 190,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Resonance, MetricType.Intensity }
                },

                // Ã˜VELSE 3: Stigende Toner - Pitch Glide Up
                new EnhancedExercise
                {
                    Id = 3,
                    Name = "Stigende toner (Glide Up)",
                    Description = "Tren pÃ¥ Ã¥ bevege deg jevnt fra lavere til hÃ¸yere pitch. Gliding gir mer naturlig progresjon.",
                    Steps = new List<string> {
                        "Start pÃ¥ en behagelig tone",
                        "Glid sakte opp over 3 sekunder",
                        "Hold topptonen i 2 sekunder",
                        "Glid sakte ned igjen",
                        "FÃ¸lg med pÃ¥ pitch-displayet"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "ðŸ“ˆ",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Gliding fremfor Ã¥ 'spre' stemmen gir jevnere pitch-overgang og mindre spenning. Kontrollert stigning er nÃ¸kkelen til feminin pitch.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Smoothness }
                },

                // Ã˜VELSE 4: Synkende Toner - Pitch Glide Down
                new EnhancedExercise
                {
                    Id = 4,
                    Name = "Synkende toner (Glide Down)",
                    Description = "Tren pÃ¥ kontrollert nedgliding i pitch for Ã¥ styrke musklene og lÃ¦re mÃ¥l-pitch-omrÃ¥det.",
                    Steps = new List<string> {
                        "Start hÃ¸yt og komfortabelt",
                        "Glid sakte ned over 3 sekunder",
                        "Hold den lave tonen i 2 sekunder",
                        "Gjenta med ulike startpunkter",
                        "Fokuser pÃ¥ jevn glide"
                    },
                    DurationMinutes = 6,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "ðŸ“‰",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Kontrollert nedgliding styrker musklene for pitch-kontroll og etablerer muskelminne for mÃ¥l-omrÃ¥det.",
                    TargetPitchMin = 160,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Smoothness, MetricType.Consistency }
                },

                // Ã˜VELSE 5: Konsistens-Trening
                new EnhancedExercise
                {
                    Id = 5,
                    Name = "Konsistens-trening",
                    Description = "FokusÃ©r pÃ¥ Ã¥ holde samme tone stabil over tid. Stabil pitch er grunnleggende for naturlig feminin stemme.",
                    Steps = new List<string> {
                        "Finn din target-tone",
                        "Hold tonen i 5 sekunder",
                        "Sjekk pitch-grafen for stabilitet",
                        "Ta en pust",
                        "Gjenta 5 ganger"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Stabilitet",
                    Icon = "ðŸ“Š",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Stabil pitch i mÃ¥l-omrÃ¥det er kritisk for feminisering. Konsistent fonasjon gir naturlig stemme.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // Ã˜VELSE 6: S-Lyder
                new EnhancedExercise
                {
                    Id = 6,
                    Name = "S-lyder (Ustemt hold)",
                    Description = "Tren pÃ¥ Ã¥ opprettholde pitch gjennom ustemte lyder for bedre kontroll.",
                    Steps = new List<string> {
                        "Si 'ssssssss' - lang S-lyd",
                        "FÃ¸lg med pÃ¥ pitch-grafen",
                        "PrÃ¸v Ã¥ holde lyden stabil",
                        "Ã˜k gradvis til 10 sekunder",
                        "Gjenta 3 ganger"
                    },
                    DurationMinutes = 6,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Stabilitet",
                    Icon = "ðŸ’¨",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Ustemte lyder isolerer fonasjonskontroll uten stemmebÃ¥ndsvibrasjoner. Utmerket for stabil pitch uten stemmebelastning.",
                    TargetPitchMin = 160,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // Ã˜VELSE 7: SpÃ¸rsmÃ¥ls-Intonasjon
                new EnhancedExercise
                {
                    Id = 7,
                    Name = "SpÃ¸rsmÃ¥lsmelodi",
                    Description = "LÃ¦r naturlig stigende intonasjon. I norsk stiger pitch 2-4 semitoner pÃ¥ slutten av spÃ¸rsmÃ¥l.",
                    Steps = new List<string> {
                        "Si 'Hva heter du?'",
                        "Legg merke til stigningen pÃ¥ slutten",
                        "Ã˜v pÃ¥ 5 ulike spÃ¸rsmÃ¥l",
                        "PrÃ¸v jevn stigning",
                        "Varier styrken"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Intonasjon",
                    Icon = "â“",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "SpÃ¸rsmÃ¥l i norsk stiger 2-4 semitoner. Riktig intonasjon er nÃ¸kkelen - feil intonasjon kan avslÃ¸re stemmen.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Intonation, MetricType.Pitch }
                },

                // Ã˜VELSE 8: Utsagns-Intonasjon
                new EnhancedExercise
                {
                    Id = 8,
                    Name = "Utsagnsmelodi",
                    Description = "Tren pÃ¥ Ã¥ avslutte setninger med synkende tone. Kritisk for Ã¥ unngÃ¥ 'spÃ¸rsmÃ¥lslyd'.",
                    Steps = new List<string> {
                        "Si 'Jeg heter Marie.'",
                        "Legg merke til fallet pÃ¥ slutten",
                        "Ã˜v pÃ¥ 5 ulike utsagn",
                        "PrÃ¸v naturlig fall",
                        "Varier start-pitch"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Intonasjon",
                    Icon = "ðŸ“¢",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "Utsagn avsluttes med fallende tone. Ã… avslutte med stigende tone kan fÃ¥ deg til Ã¥ virke usikker.",
                    TargetPitchMin = 155,
                    TargetPitchMax = 210,
                    Metrics = new List<MetricType> { MetricType.Intonation, MetricType.Pitch }
                },

                // Ã˜VELSE 9: Fraselesing
                new EnhancedExercise
                {
                    Id = 9,
                    Name = "Fraselesing",
                    Description = "Kombiner alle ferdigheter i sammenhengende talesprÃ¥k med pitch i mÃ¥l-omrÃ¥det.",
                    Steps = new List<string> {
                        "Velg en tekst",
                        "Les med fokus pÃ¥ pitch innen mÃ¥lomrÃ¥det",
                        "Stopp ved vanskelige ord",
                        "Les hele teksten",
                        "Spill av og vurder"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Praksis",
                    Icon = "ðŸ“–",
                    Goal = GoalCategory.Combined,
                    GoalIcon = "â­",
                    ScientificRationale = "Kontinuerlig tale i mÃ¥l-pitch-omrÃ¥det er slutten for feminisering. OverfÃ¸ring til spontant sprÃ¥k er viktigst.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Consistency }
                },

                // Ã˜VELSE 10: Samtale-Simulasjon
                new EnhancedExercise
                {
                    Id = 10,
                    Name = "Samtale-simulasjon",
                    Description = "Simuler vanlige samtalesituasjoner. OverfÃ¸ring fra Ã¸velse til spontant sprÃ¥k er nÃ¸kkelen.",
                    Steps = new List<string> {
                        "Tenk pÃ¥ vanlige spÃ¸rsmÃ¥l",
                        "Svar med naturlig intonasjon",
                        "Varier mellom spÃ¸rsmÃ¥l/utsagn",
                        "FÃ¸lg med pÃ¥ pitch",
                        "PrÃ¸v 5 ulike scenarier"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Avansert,
                    Category = "Avansert",
                    Icon = "ðŸ’¬",
                    Goal = GoalCategory.Combined,
                    GoalIcon = "â­",
                    ScientificRationale = "Ã… opprettholde feminin pitch i spontant sprÃ¥k er mÃ¥let. Den viktigste overfÃ¸ringsÃ¸velsen.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 230,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Resonance }
                },

                // Ã˜VELSE 11: Resonans-Skift - Fremre plassering (NY)
                new EnhancedExercise
                {
                    Id = 11,
                    Name = "Resonans-skift: Fremre plassering",
                    Description = "Tren pÃ¥ Ã¥ flytte resonansen fra bakre til fremre plassering. Kritisk for feminin klang.",
                    Steps = new List<string> {
                        "Hum 'mmm' - kjenn lepper/nese vibrere",
                        "OverfÃ¸r til 'nnn'",
                        "Deretter 'yyy' (norsk 'j')",
                        "PrÃ¸v 'ene'-stavelser",
                        "Gjenta med korte ord"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Resonans",
                    Icon = "ðŸ”Š",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "ðŸ”Š",
                    ScientificRationale = "Fremre resonans skaper 'head voice' og forsterker hÃ¸yere overtoner. MÃ¥les ved hÃ¸yere formant-frekvenser (spesielt F2).",
                    TargetPitchMin = 150,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Resonance, MetricType.Intensity }
                },

                // Ã˜VELSE 12: Starter-Pitch Memorisering (NY)
                new EnhancedExercise
                {
                    Id = 12,
                    Name = "Starter-pitch memorisering",
                    Description = "Automatiser start-pitch i mÃ¥lomrÃ¥det. De fleste starter for lavt - dette er kritisk Ã¥ rette opp.",
                    Steps = new List<string> {
                        "Syng en referansetone som fÃ¸les komfortabel",
                        "Start tale pÃ¥ denne tonen",
                        "Sjekk start-pitch med appen",
                        "Juster opp hvis nÃ¸dvendig",
                        "Gjenta til automatisk"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "ðŸŽ¯",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Muskelminne for start-pitch er kritisk. De fleste starter for lavt og 'raser' videre - automatiser riktig start-pitch.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // Ã˜VELSE 13: Pitch Slide i Fraser (NY)
                new EnhancedExercise
                {
                    Id = 13,
                    Name = "Pitch slide i fraser",
                    Description = "Bruk glide-teknikk naturlig i setninger istedet for Ã¥ 'spre' stemmen.",
                    Steps = new List<string> {
                        "'Hallo' med stigende glide",
                        "'Hei' med fallende glide",
                        "'Ja?' som spÃ¸rsmÃ¥l",
                        "Korte fraser med glide",
                        "Varier stigende/synkende"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Pitch-kontroll",
                    Icon = "ðŸŽ¢",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "ðŸŽµ",
                    ScientificRationale = "Glides mellom ord gir naturligere progresjon og mindre spenning enn Ã¥ 'spre' stemmen til ny pitch.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Smoothness }
                },

                // Ã˜VELSE 14: Straw Phonation (NY)
                new EnhancedExercise
                {
                    Id = 14,
                    Name = "Straw phonation (Halmsfonasjon)",
                    Description = "Reduser stemmeslitasje og styrk airflow-kontroll med halm-teknikk. Tvinger frem semi-okkludert vokaltrakt.",
                    Steps = new List<string> {
                        "Ta en sugerÃ¸r",
                        "BlÃ¥s lett gjennom",
                        "Syng 'ooo' gjennom strÃ¥et",
                        "Hold i 5 sekunder",
                        "Gjenta med ulike toner"
                    },
                    DurationMinutes = 5,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Pust",
                    Icon = "ðŸ¥¤",
                    Goal = GoalCategory.Breathing,
                    GoalIcon = "ðŸ’¨",
                    ScientificRationale = "Semi-occluded vocal tract (SOVT) reduserer belastning pÃ¥ stemmebÃ¥ndene og forbedrer airflow-kontroll.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Intensity, MetricType.Consistency }
                },

                // Ã˜VELSE 15: Intonasjons-Variasjon (NY)
                new EnhancedExercise
                {
                    Id = 15,
                    Name = "Intonasjons-variasjon",
                    Description = "Varier intonasjon for naturlig, ekspressiv tale. Kvinner bruker stÃ¸rre variasjon enn menn.",
                    Steps = new List<string> {
                        "'Nei' - nÃ¸ytralt",
                        "'Nei?' - overrasket",
                        "'Nei!' - frustrert",
                        "'Nei...' - resignert",
                        "Gjenta med ulike setninger"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Avansert,
                    Category = "Avansert",
                    Icon = "ðŸŽ­",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "ðŸ“ˆ",
                    ScientificRationale = "StÃ¸rre intonasjonsvariasjon er karakteristisk for feminin tale. Dette hjelper med Ã¥ 'passe inn' sosialt.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 250,
                    Metrics = new List<MetricType> { MetricType.Intonation, MetricType.PitchVariability }
                }
            };
        }
        
        /// <summary>
        /// Get exercises by goal category
        /// </summary>
        public List<EnhancedExercise> GetExercisesByGoal(GoalCategory goal)
        {
            return GetAllEnhancedExercises().FindAll(e => e.Goal == goal);
        }
        
        /// <summary>
        /// Get exercises by frequency
        /// </summary>
        public List<EnhancedExercise> GetExercisesByFrequency(FrequencyType frequency)
        {
            return GetAllEnhancedExercises().FindAll(e => e.Frequency == frequency);
        }
        
        /// <summary>
        /// Get daily recommended exercises
        /// </summary>
        public List<EnhancedExercise> GetDailyExercises()
        {
            return GetExercisesByFrequency(FrequencyType.Daglig);
        }
        
        /// <summary>
        /// Get weekly training plan
        /// </summary>
        public TrainingPlan GetWeeklyPlan()
        {
            var plan = new TrainingPlan();
            var all = GetAllEnhancedExercises();
            
            // Mandag, onsdag, fredag - full program
            plan.Monday = all.FindAll(e => e.Frequency == FrequencyType.Daglig || e.Frequency == FrequencyType.TreGangerUkentlig);
            plan.Wednesday = all.FindAll(e => e.Frequency == FrequencyType.Daglig || e.Frequency == FrequencyType.TreGangerUkentlig);
            plan.Friday = all.FindAll(e => e.Frequency == FrequencyType.Daglig || e.Frequency == FrequencyType.TreGangerUkentlig);
            
            // Tirsdag, lÃ¸rdag - 2x/uke Ã¸velser
            plan.Tuesday = all.FindAll(e => e.Frequency == FrequencyType.ToGangerUkentlig);
            plan.Saturday = all.FindAll(e => e.Frequency == FrequencyType.ToGangerUkentlig);
            
            // Daglige Ã¸velser hver dag
            plan.Daily = all.FindAll(e => e.Frequency == FrequencyType.Daglig);
            
            return plan;
        }
    }
    
    /// <summary>
    /// Weekly training plan structure
    /// </summary>
    public class TrainingPlan
    {
        public List<EnhancedExercise> Monday { get; set; } = new();
        public List<EnhancedExercise> Tuesday { get; set; } = new();
        public List<EnhancedExercise> Wednesday { get; set; } = new();
        public List<EnhancedExercise> Thursday { get; set; } = new();
        public List<EnhancedExercise> Friday { get; set; } = new();
        public List<EnhancedExercise> Saturday { get; set; } = new();
        public List<EnhancedExercise> Sunday { get; set; } = new();
        public List<EnhancedExercise> Daily { get; set; } = new();
    }
}