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
        public string Icon { get; set; } = ExerciseIconGlyphs.DefaultExercise;
        public GoalCategory Goal { get; set; }
        public string GoalIcon { get; set; } = ExerciseIconGlyphs.Pitch;
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
                FrequencyType.TreGangerUkentlig => "3x/uke",
                FrequencyType.ToGangerUkentlig => "2x/uke",
                FrequencyType.Ukentlig => "Ukentlig",
                _ => "Daglig"
            };
        }
    }
    
    /// <summary>
    /// Service for evidensbaserte stemmetreningsøvelser for transfeminine personer.
    /// Inneholder reviderte og nye øvelser basert på dokumenterte voice feminization-teknikker.
    /// </summary>
    public class VoiceFeminizationExerciseService
    {
        /// <summary>
        /// Hent alle evidensbaserte øvelser med full metadata
        /// </summary>
        public List<EnhancedExercise> GetAllEnhancedExercises()
        {
            return new List<EnhancedExercise>
            {
                // ØVELSE 1: Grunnleggende Humming - Fremre resonans + pitch-bevissthet
                new EnhancedExercise
                {
                    Id = 1,
                    Name = "Grunnleggende humming",
                    Description = "Lær å kjenne på vibrasjonen i stemmen og flytt den fremover i munnen. Humming aktiverer fremre resonatorer.",
                    Steps = new List<string> {
                        "Slapp av i skuldrene og nakken",
                        "Pust dypt inn gjennom nesen",
                        "Hum en behagelig tone",
                        "Flytt hummingen mot nesen og leppene",
                        "Hold tonen i 5-10 sekunder"
                    },
                    DurationMinutes = 5,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Oppvarming",
                    Icon = "\uE9D9",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "\uE9D9",
                    ScientificRationale = "Humming aktiverer fremre resonatorer og forbereder stemmen på lysere pitch uten spenning. Vibrasjon i lepper/nese indikerer fremre resonans.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Resonance }
                },

                // ØVELSE 2: Vokallyder - Fremre resonans
                new EnhancedExercise
                {
                    Id = 2,
                    Name = "Vokallyder - Fremre resonans",
                    Description = "Utforsk ulike vokallyder med fokus på fremre munnresonans. Åpne vokaler fremmer fremre resonans.",
                    Steps = new List<string> {
                        "Si 'ahhh' - åpne munnen bred",
                        "Si 'eee' - trekk munnvikene bakover",
                        "Si 'ooo' - runde lepper",
                        "Kombiner: 'ah-ee-oo'",
                        "Gjenta mønsteret 5 ganger"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Oppvarming",
                    Icon = "\uE720",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "\uE9D9",
                    ScientificRationale = "Åpnere vokaler (a, æ) fremmer fremre resonans, mens 'eee' hever tungryggen og skaper lysere klang.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 190,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Resonance, MetricType.Intensity }
                },

                // ØVELSE 3: Stigende Toner - Pitch Glide Up
                new EnhancedExercise
                {
                    Id = 3,
                    Name = "Stigende toner (Glide Up)",
                    Description = "Tren på å bevege deg jevnt fra lavere til lysere pitch. Gliding gir mer naturlig progresjon.",
                    Steps = new List<string> {
                        "Start på en behagelig tone",
                        "Glid sakte opp over 3 sekunder",
                        "Hold topptonen i 2 sekunder",
                        "Glid sakte ned igjen",
                        "Følg med på pitch-displayet"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "\uE8E1",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Gliding gir jevnere pitch-overgang og mindre spenning enn å presse stemmen til en ny tone. Kontrollert stigning støtter fleksibel pitch-kontroll.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Smoothness }
                },

                // ØVELSE 4: Synkende Toner - Pitch Glide Down
                new EnhancedExercise
                {
                    Id = 4,
                    Name = "Synkende toner (Glide Down)",
                    Description = "Tren på kontrollert nedgliding i pitch for å bygge kontroll og lære målområdet.",
                    Steps = new List<string> {
                        "Start lyst og komfortabelt",
                        "Glid sakte ned over 3 sekunder",
                        "Hold den lave tonen i 2 sekunder",
                        "Gjenta med ulike startpunkter",
                        "Fokuser på jevn glide"
                    },
                    DurationMinutes = 6,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "\uE8E1",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Kontrollert nedgliding støtter pitch-kontroll og etablerer muskelminne for målområdet.",
                    TargetPitchMin = 160,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Smoothness, MetricType.Consistency }
                },

                // ØVELSE 5: Konsistens-Trening
                new EnhancedExercise
                {
                    Id = 5,
                    Name = "Konsistens-trening",
                    Description = "Fokuser på å holde samme tone stabil over tid. Stabil pitch støtter en mer kontrollert stemme.",
                    Steps = new List<string> {
                        "Finn din måltone",
                        "Hold tonen i 5 sekunder",
                        "Sjekk pitch-grafen for stabilitet",
                        "Ta en pust",
                        "Gjenta 5 ganger"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Stabilitet",
                    Icon = "\uEA86",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Stabil pitch i målområdet støtter feminisering sammen med resonans, komfort og intonasjon. Konsistent fonasjon gir mer forutsigbar stemmekontroll.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // ØVELSE 6: S-Lyder
                new EnhancedExercise
                {
                    Id = 6,
                    Name = "S-lyder (Ustemt hold)",
                    Description = "Tren på å opprettholde kontroll gjennom ustemte lyder.",
                    Steps = new List<string> {
                        "Si 'ssssssss' - lang S-lyd",
                        "Følg med på pitch-grafen",
                        "Prøv å holde lyden stabil",
                        "Øk gradvis til 10 sekunder",
                        "Gjenta 3 ganger"
                    },
                    DurationMinutes = 6,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Stabilitet",
                    Icon = "\uE81C",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Ustemte lyder isolerer luftstrøm og kontroll uten stemmebåndsvibrasjoner. Det kan støtte stabilitet med lav stemmebelastning.",
                    TargetPitchMin = 160,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // ØVELSE 7: Spørsmåls-Intonasjon
                new EnhancedExercise
                {
                    Id = 7,
                    Name = "Spørsmålsmelodi",
                    Description = "Lær naturlig stigende intonasjon. I norsk kan pitch stige noen semitoner på slutten av spørsmål.",
                    Steps = new List<string> {
                        "Si 'Hva heter du?'",
                        "Legg merke til stigningen på slutten",
                        "Øv på 5 ulike spørsmål",
                        "Prøv jevn stigning",
                        "Varier styrken"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Intonasjon",
                    Icon = "\uE945",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "\uE8E1",
                    ScientificRationale = "Spørsmål i norsk har ofte stigende intonasjon. Variert intonasjon kan gjøre stemmen mer naturlig og uttrykksfull.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Intonation, MetricType.Pitch }
                },

                // ØVELSE 8: Utsagns-Intonasjon
                new EnhancedExercise
                {
                    Id = 8,
                    Name = "Utsagnsmelodi",
                    Description = "Tren på å avslutte setninger med kontrollert, naturlig fallende tone.",
                    Steps = new List<string> {
                        "Si 'Jeg heter Marie.'",
                        "Legg merke til fallet på slutten",
                        "Øv på 5 ulike utsagn",
                        "Prøv naturlig fall",
                        "Varier start-pitch"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Intonasjon",
                    Icon = "\uE787",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "\uE8E1",
                    ScientificRationale = "Utsagn avsluttes ofte med fallende tone. Å kunne variere avslutningen gir mer fleksibel og naturlig intonasjon.",
                    TargetPitchMin = 155,
                    TargetPitchMax = 210,
                    Metrics = new List<MetricType> { MetricType.Intonation, MetricType.Pitch }
                },

                // ØVELSE 9: Fraselesing
                new EnhancedExercise
                {
                    Id = 9,
                    Name = "Fraselesing",
                    Description = "Kombiner alle ferdigheter i sammenhengende talespråk med pitch i målområdet.",
                    Steps = new List<string> {
                        "Velg en tekst",
                        "Les med fokus på pitch innen målområdet",
                        "Stopp ved vanskelige ord",
                        "Les hele teksten",
                        "Spill av og vurder"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Praksis",
                    Icon = "\uE8D6",
                    Goal = GoalCategory.Combined,
                    GoalIcon = "\uE7BC",
                    ScientificRationale = "Kontinuerlig tale i målområdet er ett steg i stemmefeminisering. Overføring til spontant språk er viktig for hverdagsbruk.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Consistency }
                },

                // ØVELSE 10: Samtale-Simulasjon
                new EnhancedExercise
                {
                    Id = 10,
                    Name = "Samtale-simulasjon",
                    Description = "Simuler vanlige samtalesituasjoner. Overføring fra øvelse til spontant språk er viktig.",
                    Steps = new List<string> {
                        "Tenk på vanlige spørsmål",
                        "Svar med naturlig intonasjon",
                        "Varier mellom spørsmål/utsagn",
                        "Følg med på pitch",
                        "Prøv 5 ulike scenarier"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Avansert,
                    Category = "Avansert",
                    Icon = "\uE720",
                    Goal = GoalCategory.Combined,
                    GoalIcon = "\uE7BC",
                    ScientificRationale = "Å bruke målområdet i spontant språk er en nyttig overføringsøvelse, sammen med resonans, komfort og stabilitet.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 230,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Resonance }
                },

                // ØVELSE 11: Resonans-Skift - Fremre plassering (NY)
                new EnhancedExercise
                {
                    Id = 11,
                    Name = "Resonans-skift: Fremre plassering",
                    Description = "Tren på å flytte resonansen fra bakre til fremre plassering. Dette kan støtte lysere og mer fremoverrettet klang.",
                    Steps = new List<string> {
                        "Hum 'mmm' - kjenn lepper/nese vibrere",
                        "Overfør til 'nnn'",
                        "Deretter 'yyy' (norsk 'j')",
                        "Prøv 'ene'-stavelser",
                        "Gjenta med korte ord"
                    },
                    DurationMinutes = 7,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Resonans",
                    Icon = "\uE9D9",
                    Goal = GoalCategory.Resonance,
                    GoalIcon = "\uE9D9",
                    ScientificRationale = "Fremre resonans kan forsterke lysere overtoner. Den kan følges indirekte gjennom formant-frekvenser, særlig F2.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 200,
                    Metrics = new List<MetricType> { MetricType.Resonance, MetricType.Intensity }
                },

                // ØVELSE 12: Starter-Pitch Memorisering (NY)
                new EnhancedExercise
                {
                    Id = 12,
                    Name = "Starter-pitch memorisering",
                    Description = "Øv på å finne en komfortabel start-pitch i målområdet uten å presse stemmen.",
                    Steps = new List<string> {
                        "Syng en referansetone som føles komfortabel",
                        "Start tale på denne tonen",
                        "Sjekk start-pitch med appen",
                        "Juster forsiktig hvis nødvendig",
                        "Gjenta til automatisk"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.Daglig,
                    Difficulty = DifficultyLevel.Nybegynner,
                    Category = "Pitch-kontroll",
                    Icon = "\uE916",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Muskelminne for en komfortabel start-pitch kan gjøre tale mer stabil. Målet er en lett og trygg start, ikke maksimal høyde.",
                    TargetPitchMin = 165,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Consistency }
                },

                // ØVELSE 13: Pitch Slide i Fraser (NY)
                new EnhancedExercise
                {
                    Id = 13,
                    Name = "Pitch slide i fraser",
                    Description = "Bruk glide-teknikk naturlig i setninger i stedet for å presse stemmen til ny pitch.",
                    Steps = new List<string> {
                        "'Hallo' med stigende glide",
                        "'Hei' med fallende glide",
                        "'Ja?' som spørsmål",
                        "Korte fraser med glide",
                        "Varier stigende/synkende"
                    },
                    DurationMinutes = 8,
                    Frequency = FrequencyType.TreGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Pitch-kontroll",
                    Icon = "\uE8E1",
                    Goal = GoalCategory.Pitch,
                    GoalIcon = "\uE8D6",
                    ScientificRationale = "Glides mellom ord kan gi naturligere progresjon og mindre spenning enn å presse stemmen til ny pitch.",
                    TargetPitchMin = 150,
                    TargetPitchMax = 220,
                    Metrics = new List<MetricType> { MetricType.Pitch, MetricType.Intonation, MetricType.Smoothness }
                },

                // ØVELSE 14: Straw Phonation (NY)
                new EnhancedExercise
                {
                    Id = 14,
                    Name = "Straw phonation (Halmsfonasjon)",
                    Description = "Øv på lett luftstrøm og komfortabel fonasjon med sugerør-teknikk. Dette støtter semi-okkludert vokaltrakt uten press.",
                    Steps = new List<string> {
                        "Ta et sugerør",
                        "Blås lett gjennom",
                        "Syng 'ooo' gjennom strået",
                        "Hold i 5 sekunder",
                        "Gjenta med ulike toner"
                    },
                    DurationMinutes = 5,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Middels,
                    Category = "Pust",
                    Icon = "\uE81C",
                    Goal = GoalCategory.Breathing,
                    GoalIcon = "\uE81C",
                    ScientificRationale = "Semi-occluded vocal tract (SOVT) kan redusere belastning på stemmebåndene og støtte luftstrømkontroll.",
                    TargetPitchMin = 140,
                    TargetPitchMax = 180,
                    Metrics = new List<MetricType> { MetricType.Intensity, MetricType.Consistency }
                },

                // ØVELSE 15: Intonasjons-Variasjon (NY)
                new EnhancedExercise
                {
                    Id = 15,
                    Name = "Intonasjons-variasjon",
                    Description = "Varier intonasjon for naturlig, ekspressiv tale. Utforsk variasjon uten å presse stemmen.",
                    Steps = new List<string> {
                        "'Nei' - nøytralt",
                        "'Nei?' - overrasket",
                        "'Nei!' - frustrert",
                        "'Nei...' - resignert",
                        "Gjenta med ulike setninger"
                    },
                    DurationMinutes = 10,
                    Frequency = FrequencyType.ToGangerUkentlig,
                    Difficulty = DifficultyLevel.Avansert,
                    Category = "Avansert",
                    Icon = "\uE7BC",
                    Goal = GoalCategory.Intonation,
                    GoalIcon = "\uE8E1",
                    ScientificRationale = "Større intonasjonsvariasjon kan bidra til en mer uttrykksfull stemme. Målet er fleksibilitet og komfort.",
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
            
            // Tirsdag, lørdag - 2x/uke øvelser
            plan.Tuesday = all.FindAll(e => e.Frequency == FrequencyType.ToGangerUkentlig);
            plan.Saturday = all.FindAll(e => e.Frequency == FrequencyType.ToGangerUkentlig);
            
            // Daglige øvelser hver dag
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
