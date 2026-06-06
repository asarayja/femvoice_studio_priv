using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service som leverer øvelsestekster organisert etter vanskelighetsgrad.
    /// Title, Description og Category lastes fra ressursfiler for støtte av flere språk.
    /// </summary>
    public class ExerciseTextService
    {
        private readonly List<ExerciseText> _allTexts;
        private readonly ILocalizationService _localization;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public ExerciseTextService()
            : this(null)
        {
        }
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public ExerciseTextService(ILocalizationService? localization)
        {
            _localization = localization ?? LocalizationService.Instance;
            _allTexts = InitializeExerciseTexts();
        }
        
        /// <summary>
        /// Hent alle tilgjengelige tekster
        /// </summary>
        public List<ExerciseText> GetAllTexts() => new List<ExerciseText>(_allTexts);
        
        /// <summary>
        /// Hent tekster etter vanskelighetsgrad
        /// </summary>
        public List<ExerciseText> GetTextsByDifficulty(DifficultyLevel difficulty)
        {
            return _allTexts.FindAll(t => t.Difficulty == difficulty);
        }
        
        /// <summary>
        /// Hent en tilfeldig tekst for en gitt vanskelighetsgrad
        /// </summary>
        public ExerciseText GetRandomText(DifficultyLevel difficulty)
        {
            var texts = GetTextsByDifficulty(difficulty);
            if (texts.Count == 0)
                return GetDefaultText(difficulty);
                
            var random = new Random();
            return texts[random.Next(texts.Count)];
        }
        
        /// <summary>
        /// Hent neste tekst i progresjonen
        /// </summary>
        public ExerciseText GetNextText(DifficultyLevel currentDifficulty, int exerciseIndex)
        {
            var texts = GetTextsByDifficulty(currentDifficulty);
            if (texts.Count == 0)
                return GetDefaultText(currentDifficulty);
                
            var nextIndex = (exerciseIndex + 1) % texts.Count;
            return texts[nextIndex];
        }
        
        private ExerciseText GetDefaultText(DifficultyLevel difficulty)
        {
            return new ExerciseText
            {
                Id = 0,
                Title = _localization["Exercise_Default_Title"],
                Content = "Hei! Jeg heter [navn] og jeg øver på å snakke med en mer feminin stemme.",
                Difficulty = difficulty,
                Category = _localization["Category_Basic"],
                Description = _localization["Exercise_Default_Description"]
            };
        }
        
        /// <summary>
        /// Get localized title for an exercise
        /// </summary>
        public string GetLocalizedTitle(int exerciseId)
        {
            return _localization[$"Exercise_{exerciseId}_Title"];
        }
        
        /// <summary>
        /// Get localized description for an exercise
        /// </summary>
        public string GetLocalizedDescription(int exerciseId)
        {
            return _localization[$"Exercise_{exerciseId}_Description"];
        }
        
        /// <summary>
        /// Get localized steps for an exercise (pipe-separated)
        /// </summary>
        public string GetLocalizedSteps(int exerciseId)
        {
            return _localization[$"Exercise_{exerciseId}_Steps"];
        }
        
        /// <summary>
        /// Get localized steps as a list
        /// </summary>
        public List<string> GetLocalizedStepsList(int exerciseId)
        {
            var stepsStr = GetLocalizedSteps(exerciseId);
            if (string.IsNullOrEmpty(stepsStr))
                return new List<string>();
            return stepsStr.Split('|').ToList();
        }
        
        /// <summary>
        /// Get localized scientific rationale for an exercise
        /// </summary>
        public string GetLocalizedRationale(int exerciseId)
        {
            return _localization[$"Exercise_{exerciseId}_Rationale"];
        }
        
        /// <summary>
        /// Get localized content for an exercise
        /// </summary>
        public string GetLocalizedContent(int exerciseId)
        {
            return _localization[$"Exercise_{exerciseId}_Content"];
        }
        
        /// <summary>
        /// Get localized category for an exercise
        /// </summary>
        public string GetLocalizedCategory(int exerciseId)
        {
            var categoryKey = _localization[$"Exercise_{exerciseId}_Category"];
            // Check if it's a category key like "Category_Basic" or direct value
            if (categoryKey.StartsWith("Category_"))
            {
                return _localization[categoryKey];
            }
            return categoryKey;
        }
        
        /// <summary>
        /// Initialiserer alle øvelsestekster med norsk innhold
        /// </summary>
        private List<ExerciseText> InitializeExerciseTexts()
        {
            var texts = new List<ExerciseText>();
            
            // ============================================
            // NYBEGYNNER - Grunnleggende setninger
            // Fokus på: Konsistent pitch i målområdet (165-255 Hz)
            // ============================================
            
            texts.Add(new ExerciseText
            {
                Id = 1,
                Title = "Hilsener",
                Content = "Hei! God morgen! Hyggelig å møte deg! Velkommen tilbake. Ha en fin dag!",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle hilsener med jevn intonasjon",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });
            
            texts.Add(new ExerciseText
            {
                Id = 2,
                Title = "Introduksjon",
                Content = "Hei! Jeg heter Marie og jeg er fra Oslo. Jeg liker å synge og danse. Jeg jobber som lærer.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Presentere seg selv med enkle setninger",
                TargetMinPitch = 170,
                TargetMaxPitch = 230,
                TargetPitchVariation = 18,
                TargetIntonationRise = 0.15
            });
            
            texts.Add(new ExerciseText
            {
                Id = 3,
                Title = "Dagligdagse ting",
                Content = "I dag er det mandag. Jeg skal på jobb. Det er sol ute. Været er fint. Jeg trener tre ganger i uken.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle utsagn om hverdagen",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });
            
            texts.Add(new ExerciseText
            {
                Id = 4,
                Title = "Følelser",
                Content = "Jeg er glad. Jeg er sliten. Jeg er nervøs. Hun er veldig snill. Han er lat.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Følelser",
                Description = "Utsagn om følelser med fokus på pitch-kontroll",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 20,
                TargetIntonationRise = 0.15
            });

            // ============================================
            // FLERE NYBEGYNNER TEKSTER
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 25,
                Title = "Tall og telling",
                Content = "En, to, tre, fire, fem. Seks, sju, åtte, ni, ti. Jeg har to barn. Hun er tretti år gammel. Det koster hundre kroner. Vi møtes klokken fem.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger med tall og telling",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 26,
                Title = "Handlinger",
                Content = "Jeg spiser frokost. Hun drikker kaffe. Vi går til jobben. Barna leker ute. Katten sover på sengen.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om daglige aktiviteter",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 27,
                Title = "Steder",
                Content = "Jeg bor i Oslo. Hun jobber på sykehuset. Vi skal til butikken. Barna er på skolen. Han er hjemme.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Beskrivelse",
                Description = "Enkle setninger om steder",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 28,
                Title = "Familie",
                Content = "Dette er min mor. Han er min bror. Vi er søstre. Foreldrene mine bor i Bergen. Jeg har to søsken.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om familie",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 16,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 29,
                Title = "Mat og drikke",
                Content = "Jeg liker epler. Hun drikker te. Vi spiser middag klokken seks. Brødet er ferskt. Melken er kald.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om mat og drikke",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 30,
                Title = "Tid",
                Content = "I dag er mandag. I går var det søndag. I morgen er det tirsdag. Nå er det kveld. Snart er det helg.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om tid",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            // ============================================
            // EKSTRA NYBEGYNNER TEKSTER (31-50)
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 31,
                Title = "Dyrene",
                Content = "Hunden er stor. Katten er liten. Fuglen flyr. Fiskene svømmer. Kaninen hopper.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om dyr",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 32,
                Title = "Farger",
                Content = "Eplet er rødt. Bananen er gul. Gresset er grønt. Himmelen er blå. Snøen er hvit.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om farger",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 33,
                Title = "Ordensformer",
                Content = "Jeg løper fort. Hun synger høyt. Vi jobber hardt. Barna leker pent. Katten sover mye.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger med adverb",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 34,
                Title = "Størrelser",
                Content = "Huset er stort. Boken er liten. Bilen er ny. Bordet er gammelt. Treet er høyt.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Beskrivelse",
                Description = "Enkle setninger om størrelser",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 35,
                Title = "Vær og natur",
                Content = "Solen skinner. Det regner. Vinden blåser. Skyene er hvite. Det er kaldt.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om været",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 36,
                Title = "Klær",
                Content = "Jeg har på meg en kjole. Han bruker genser. Skoene er svarte. Buksene er blå. Hatten er rød.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om klær",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 37,
                Title = "Retninger",
                Content = "Gå til høyre. Gå til venstre. Gå rett fram. Gå bakover. Kom hit.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om retninger",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 38,
                Title = "Ukedagene",
                Content = "Mandag er første dag. Tirsdag er andre dag. Onsdag er tredje dag. Torsdag er fjerde dag. Fredag er femte dag.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Lære ukedagene",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 39,
                Title = "Måneder",
                Content = "Januar er første måned. Juli er sommermåned. Desember er siste måned. April er vårmåned. Oktober er høstmåned.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Lære månedene",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 40,
                Title = "Kroppen",
                Content = "Jeg har to hender. Hun har to føtter. Hode er på toppen. Øynene er på ansiktet. Hjertet er i brystet.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om kroppen",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 41,
                Title = "Hjemmet",
                Content = "Soverommet er over. Kjøkkenet er der. Stua er stor. Badet er lite. Hagen er bak.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Beskrivelse",
                Description = "Enkle setninger om rom i huset",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 42,
                Title = "Trafikk",
                Content = "Bussen kommer. Bilen kjører. Toget er raskt. Sykkelen er sunn. Flyet er høyt.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om transport",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 43,
                Title = "Skolen",
                Content = "Boka er på bordet. Pennen er i vesken. Læreren snakker. Elevene lærer. Skolen er stor.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om skolen",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 44,
                Title = "Sport",
                Content = "Fotball er morsomt. Svømming er sunt. Løping er hardt. Dansing er kult. Tennis er spennende.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om sport",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 45,
                Title = "Årstidene",
                Content = "Våren er grønn. Sommeren er varm. Høsten er gyllen. Vinteren er hvit. Årstidene skifter.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger om årstidene",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 46,
                Title = "Følelser 2",
                Content = "Jeg føler meg glad. Hun føler seg trist. Vi føler oss trøtte. De føler seg glade. Det føles godt.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Følelser",
                Description = "Enkle setninger om følelser",
                TargetMinPitch = 165,
                TargetMaxPitch = 230,
                TargetPitchVariation = 16,
                TargetIntonationRise = 0.15
            });

            texts.Add(new ExerciseText
            {
                Id = 47,
                Title = "Tall 2",
                Content = "Jeg har tre epler. Hun har fem kroner. Vi er to personer. Det er ti mil. Boka koster femti.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Grunnleggende",
                Description = "Enkle setninger med tall",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            texts.Add(new ExerciseText
            {
                Id = 48,
                Title = "Jobb",
                Content = "Jeg jobber som lærer. Hun jobber på sykehuset. Han jobber med data. Vi jobber sammen. Tiden går fort.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om jobb",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 49,
                Title = "Kjøkkenet",
                Content = "Jeg lager mat. Kokken er het. Vannet er varmt. Brødet er stekt. Maten er klar.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om kjøkkenet",
                TargetMinPitch = 165,
                TargetMaxPitch = 225,
                TargetPitchVariation = 15,
                TargetIntonationRise = 0.12
            });

            texts.Add(new ExerciseText
            {
                Id = 50,
                Title = "Butikken",
                Content = "Butikken er åpen. Varene er ferske. Prisen er lav. Kassa er der. Jeg betaler kontant.",
                Difficulty = DifficultyLevel.Nybegynner,
                Category = "Hverdagslig",
                Description = "Enkle setninger om butikken",
                TargetMinPitch = 165,
                TargetMaxPitch = 220,
                TargetPitchVariation = 14,
                TargetIntonationRise = 0.1
            });

            // ============================================
            // MIDDELS - Setninger med mer variasjon
            // Fokus på: Pitch-variasjon og intonasjonsmønstre
            // ============================================
            
            texts.Add(new ExerciseText
            {
                Id = 5,
                Title = "Spørsmål",
                Content = "Hvordan har du det? Hva skal du gjøre i dag? Har du sett den filmen? Kan du hjelpe meg? Skal vi gå en tur?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Spørsmål",
                Description = "Åpne og lukkede spørsmål med stigende intonasjon",
                TargetMinPitch = 175,
                TargetMaxPitch = 255,
                TargetPitchVariation = 25,
                TargetIntonationRise = 0.4
            });
            
            texts.Add(new ExerciseText
            {
                Id = 6,
                Title = "Butikken",
                Content = "Jeg skal kjøpe melk, brød og epler. Hvor mye koster det? Det er dyrt. Har dere dette på lager? Jeg tar to stykker.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Hverdagslig",
                Description = "Shopping-dialog med varierende intonasjon",
                TargetMinPitch = 170,
                TargetMaxPitch = 250,
                TargetPitchVariation = 22,
                TargetIntonationRise = 0.25
            });
            
            texts.Add(new ExerciseText
            {
                Id = 7,
                Title = "Planer",
                Content = "I helgen skal jeg besøke vennene mine. Kanskje vi drar på kino? Kommer du også? Vi kan spise pizza sammen etterpå.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Fremtid",
                Description = "Planer og framtid med naturlig intonasjon",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.3
            });
            
            texts.Add(new ExerciseText
            {
                Id = 8,
                Title = "Beskrivelser",
                Content = "Huset er stort og fint. Hagen har mange blomster. Det er et vakkert sted. Solen skinner gjennom vinduet. Barna leker i gresset.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Beskrivelse",
                Description = "Beskrive steder og ting med ekspressiv intonasjon",
                TargetMinPitch = 180,
                TargetMaxPitch = 255,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.25
            });

            // ============================================
            // FLERE MIDDELS TEKSTER
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 31,
                Title = "Historier",
                Content = "Da jeg var ung, bodde jeg i Trondheim. Det var en fin by. Hver dag gikk jeg til sentrum. Der møtte jeg mange venner. Vi hadde det koselig.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Fortelling",
                Description = "Korte historier med enkle fortelleelementer",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 32,
                Title = "Fritid",
                Content = "På fritiden liker jeg å lese bøker. Noen ganger ser jeg på TV. Vi drar ofte på tur i skogen. Helt koselig! Det er så avslappende.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Hverdagslig",
                Description = "Beskrive fritidsaktiviteter med variert intonasjon",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 22,
                TargetIntonationRise = 0.2
            });

            texts.Add(new ExerciseText
            {
                Id = 33,
                Title = "Meninger",
                Content = "Jeg synes dette er interessant. Det er viktig å huske. Vi bør tenke på fremtiden. Er du enig? Hva mener du om det?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Argumentasjon",
                Description = "Utsagn med personlige meninger",
                TargetMinPitch = 175,
                TargetMaxPitch = 250,
                TargetPitchVariation = 26,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 34,
                Title = "Reise",
                Content = "Jeg har vært i Sverige flere ganger. Det er så nydelig der! Fjellene er høye. Lakene er blå. Maten er annerledes. Men folket er vennlig.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Beskrivelse",
                Description = "Reisebeskrivelser med positive og negative elementer",
                TargetMinPitch = 170,
                TargetMaxPitch = 255,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 35,
                Title = "Sammenligning",
                Content = "Hun er eldre enn meg. Boken var bedre enn filmen. Det er like dyrt her som der. Jo mer jo bedre! Er du like glad som før?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Grunnleggende",
                Description = "Setninger med sammenligninger",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 36,
                Title = "Klage",
                Content = "Dette er altfor dyrt! Det tar altfor lang tid. Jeg er lei av å vente. Kan du ikke bare si ifra? Det er urettferdig!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Følelser",
                Description = "Utsagn med negative følelser og klager",
                TargetMinPitch = 180,
                TargetMaxPitch = 260,
                TargetPitchVariation = 30,
                TargetIntonationRise = 0.35
            });

            // ============================================
            // EKSTRA MIDDELS TEKSTER (37-50)
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 37,
                Title = "Beklagelse",
                Content = "Beklager at jeg kom for sent. Det var ikke meningen å såre deg. Jeg angrer virkelig. Gi meg en ny sjanse. Det skal ikke skje igjen.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Følelser",
                Description = "Unnskyldninger og beklagelser",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 25,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 38,
                Title = "Invitasjon",
                Content = "Vil du komme på kaffe i helgen? Kanskje vi kan gå på kino? Hva med å spise middag sammen? Jeg lager mat hvis du vil. Bare si ifra når du kan.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Samtale",
                Description = "Invitasjoner og forslag",
                TargetMinPitch = 175,
                TargetMaxPitch = 250,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 39,
                Title = "Drømmer",
                Content = "Jeg drømmer om å reise verden rundt. En dag vil jeg lære å spille gitar. Kanskje jeg starter egen bedrift. Drømmer holder oss i live. Tro på din drøm!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Fremtid",
                Description = "Drømmer og håp",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 26,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 40,
                Title = "Sammenligning 2",
                Content = "Hun løper fortere enn meg. Dette er bedre enn det. Jo eldre jo visere. Det er like farlig som før. Ingenting er så godt som dette.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Grunnleggende",
                Description = "Flere sammenligninger",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 41,
                Title = "Hobbyer",
                Content = "På fritiden driver jeg med foto. Det er så avslappende å ta bilder. Jeg liker å fange øyeblikk. Hver gang blir et minne. Har du noen hobby?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Beskrivelse",
                Description = "Beskrive hobbyer",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 42,
                Title = "Utdanning",
                Content = "Jeg studerer medisin på universitetet. Det er krevende men spennende. Hver dag lærer jeg noe nytt. Eksamen nærmer seg fort. Jeg gleder meg til å bli ferdig.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Hverdagslig",
                Description = "Snakk om utdanning",
                TargetMinPitch = 170,
                TargetMaxPitch = 245,
                TargetPitchVariation = 26,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 43,
                Title = "Helse",
                Content = "Jeg føler meg litt syk i dag. Kanskje jeg skal ta det rolig? Det er viktig å høre på kroppen. Jeg har vært trøtt lenge. Skal jeg ringe legen?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Hverdagslig",
                Description = "Snakk om helse",
                TargetMinPitch = 175,
                TargetMaxPitch = 250,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 44,
                Title = "Feiring",
                Content = "Gratulerer med dagen! Skål for brudparet! Til lykke med eksamen! Hurra for jubileet! Dette er verdt å feire!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Følelser",
                Description = "Feiring og gratulasjoner",
                TargetMinPitch = 180,
                TargetMaxPitch = 260,
                TargetPitchVariation = 30,
                TargetIntonationRise = 0.45
            });

            texts.Add(new ExerciseText
            {
                Id = 45,
                Title = "Usikkerhet",
                Content = "Jeg er ikke sikker på dette. Kanskje jeg tar feil? Hva tror du jeg bør gjøre? Er det riktig valg? Kan du hjelpe meg å bestemme?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Følelser",
                Description = "Uttrykk av usikkerhet",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 46,
                Title = "Oppfordring",
                Content = "Kom igjen, vi må skynde oss! Prøv dette, det er godt! Hør etter nå! Gjør det ordentlig! Tenk positivt!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Samtale",
                Description = "Oppfordringer og oppfordringer",
                TargetMinPitch = 180,
                TargetMaxPitch = 255,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 47,
                Title = "Bekymring",
                Content = "Jeg er bekymret for fremtiden. Er alt i orden med deg? Det ser ikke bra ut. Vi må gjøre noe! Hva om det går galt?",
                Difficulty = DifficultyLevel.Middels,
                Category = "Følelser",
                Description = "Uttrykk av bekymring",
                TargetMinPitch = 175,
                TargetMaxPitch = 250,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 48,
                Title = "Tilbud",
                Content = "Vil du ha litt mer? Kan jeg hjelpe deg med noe? Skal jeg kjøre deg hjem? Trenger du hjelp? Jeg kan ordne det for deg.",
                Difficulty = DifficultyLevel.Middels,
                Category = "Samtale",
                Description = "Tilbud og hjelp",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 26,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 49,
                Title = "Besøk",
                Content = "Velkommen til mitt hjem! Sett deg gjerne ned. Vil du ha noe å drikke? Hvordan har du hatt det? Det er lenge siden sist!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Samtale",
                Description = "Besøk og gjestfrihet",
                TargetMinPitch = 170,
                TargetMaxPitch = 240,
                TargetPitchVariation = 24,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 50,
                Title = "Avskjed",
                Content = "Takk for i dag! Det var hyggelig å treffe deg. Ha det bra! Vi sees snart! Glem ikke å ringe!",
                Difficulty = DifficultyLevel.Middels,
                Category = "Samtale",
                Description = "Avskjed og avslutning",
                TargetMinPitch = 175,
                TargetMaxPitch = 245,
                TargetPitchVariation = 26,
                TargetIntonationRise = 0.35
            });

            // ============================================
            // AVANSERT - Komplekse tekster med naturlig prosodi
            // Fokus på: Melodisk tale, emosjonell intonasjon
            // ============================================
            
            texts.Add(new ExerciseText
            {
                Id = 9,
                Title = "Eventyr",
                Content = "Det var en gang en liten jente som het Søster. Hun gikk gjennom skogen alene. 'Hvor skal du?' spurte ulven. 'Til bestemor,' svarte hun. 'Da skal jeg spise deg,' sa ulven. 'Å nei!' ropte hun. Men heldigvis kom jegerne og reddet henne. Og så levde hun lykkelig alle sine dager.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Fortelling med ulike karakterstemmer og intonasjoner",
                TargetMinPitch = 165,
                TargetMaxPitch = 280,
                TargetPitchVariation = 35,
                TargetIntonationRise = 0.4
            });
            
            texts.Add(new ExerciseText
            {
                Id = 10,
                Title = "Telefonsamtale",
                Content = "Halløiya! Nei, det er jeg. Åh, hi! Ja, jeg kommer definitivt! Hva tid passer? Så tidlig? Okei, da sees vi! Ha det! Vi sees senere. Ha en fin kveld! Klem fra meg!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Samtale",
                Description = "Uformell telefonsamtale med naturlig flyt",
                TargetMinPitch = 170,
                TargetMaxPitch = 275,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.45
            });
            
            texts.Add(new ExerciseText
            {
                Id = 11,
                Title = "Følelsesuttrykk",
                Content = "Åh, så fint! Virkelig? Nei, det kan jeg ikke tro! Hvor synd! Jippi, endelig! Å nei, så leit! Wow, det var utrolig! Helt fantastisk! Veldig bra gjort!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Følelser",
                Description = "Utsagn med sterke følelser og stor pitch-variasjon",
                TargetMinPitch = 165,
                TargetMaxPitch = 300,
                TargetPitchVariation = 45,
                TargetIntonationRise = 0.5
            });
            
            texts.Add(new ExerciseText
            {
                Id = 12,
                Title = "Argumentasjon",
                Content = "Jeg mener vi bør tenke annerledes. Er du enig? Fordi hvis ikke, så blir det feil. Vi må handle nå! La oss diskutere dette mer. Du må forstå mitt synspunkt. Tenk over det.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Argumentasjon",
                Description = "Overbevisende tale med overbevisende intonasjon",
                TargetMinPitch = 175,
                TargetMaxPitch = 265,
                TargetPitchVariation = 32,
                TargetIntonationRise = 0.35
            });
            
            texts.Add(new ExerciseText
            {
                Id = 13,
                Title = "Emosjonell historie",
                Content = "Da jeg var liten, bodde jeg ved sjøen. Hver morgen våknet jeg av bølgene. Det var så fredelig. Jeg savner det fortsatt noen ganger. Minneriene tar meg tilbake til den tiden. Der lærte jeg å svømme. Der møtte jeg mine beste venner. Det var en spesiell tid i mitt liv.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Personlig historie med reflekterende intonasjon",
                TargetMinPitch = 165,
                TargetMaxPitch = 260,
                TargetPitchVariation = 35,
                TargetIntonationRise = 0.3
            });

            // ============================================
            // FLERE AVANSERTE TEKSTER - Lengre historier og variert innhold
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 14,
                Title = "Snedronningen - Eventyr",
                Content = "Der var en gang to søstre, Gerda og Rosa, som bodde i et lite hus ved skogen. En vinterdag forsvant broren deres, Kay. Han hadde blitt tatt av Snedronningen. 'Jeg må finne ham,' sa Gerda. Så dro hun ut i den kalde verden. Underveis møtte hun en rev, en prinsesse og en tyv. Alle prøvde å hjelpe henne. Endelig kom hun til Snedronningens slott. Der fant hun Kay. De kysste hverandre, og alt ble varmt igjen. Tårene deres rant nedover, og de reiste hjem sammen. Og de levde lykkelig alle sine dager.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Klassisk eventyr med spennende handlingsforløp",
                TargetMinPitch = 165,
                TargetMaxPitch = 290,
                TargetPitchVariation = 40,
                TargetIntonationRise = 0.45
            });

            texts.Add(new ExerciseText
            {
                Id = 15,
                Title = "En dag på jobben",
                Content = "Jeg våknet tidlig i dag. Klokken var halv sju. Solen skinte inn gjennom gardinen. Jeg dusjet og spiste frokost. Så tok jeg bussen til jobben. På kontoret møtte jeg kollegene mine. Vi hadde et møte klokken ni. Etter lunsj jobbet jeg med rapporten. Det var mye å gjøre. Klokken fire dro jeg hjem. På veien handlet jeg mat. Nå er jeg sliten, men fornøyd. Imorgen blir en ny dag.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Strukturert daglig fortelling med rolig flyt",
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                TargetPitchVariation = 30,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 16,
                Title = "Vennskap",
                Content = "Jeg har hatt samme venninne siden barneskolen. Vi møttes første gang på fritidsklubben. Hun het Anna. Vi ble med en gang bestevenner. Sammen lærte vi å sykle. Sammen dro vi på ferie med familiene våre. Da vi ble eldre, flyttet hun til en annen by. Likevel holder vi kontakten. Vi ringer hver helg. Noen ganger besøker vi hverandre. Sann vennskap varer for evigheten. Det er noe av det fineste i verden.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Følelsesladet historie om vennskap",
                TargetMinPitch = 160,
                TargetMaxPitch = 270,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 17,
                Title = "Drømmeferien",
                Content = "Drømmeferien min er å reise til Italia. Jeg vil besøke Roma, Firenze og Venezia. I Roma vil jeg se Colosseum. Det må være fantastisk! I Firenze vil jeg spise ekte italiensk pizza. Og i Venezia vil jeg ro i gondol. Jeg vil bo i en liten leilighet ved stranden. Hver morgen vil jeg våkne til lyden av bølger. Om kveldene vil jeg sitte på en restaurant og se solen gå ned. Matlysten vil bli stor! Jeg elsker italiensk mat. Pasta, pizza, gelato! Mmm! Dette blir drømmen min en dag.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fremtid",
                Description = "Entusiastisk drømmefortelling med levende bilder",
                TargetMinPitch = 170,
                TargetMaxPitch = 285,
                TargetPitchVariation = 42,
                TargetIntonationRise = 0.5
            });

            texts.Add(new ExerciseText
            {
                Id = 18,
                Title = "Familiemiddag",
                Content = "I helgen hadde vi familiemiddag. Bestemor hadde laget kjøttkaker. Det var så godt! Onkel Per kom fra Bergen. Tanten min, Kari, kom fra Sverige. Fetteren min, Erik, var også der. Vi satt rundt det store bordet. Alle snakket og ler. Bestemor fortalte historier fra gamle dager. Moren min viste frem nye bilder. Faren min diskuterte fotball med onkel Per. Etter middag spilte vi kort. Barna løp rundt i huset. Det var en koselig kveld. Sånne stunder er dyrebare. Familie betyr alt for meg.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Livlig familiært scenario med mange stemninger",
                TargetMinPitch = 165,
                TargetMaxPitch = 275,
                TargetPitchVariation = 36,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 19,
                Title = "Shoppingtur",
                Content = "I dag dro jeg på kjøpesenteret. Jeg trengte nye klær. Først gikk jeg inn i en butikk som het Modehuset. Der fant jeg en fin kjole. Den var blå med hvite blomster. Prisen var 499 kroner. Jeg prøvde den på. Den passet perfekt! Så gikk jeg til skobutikken. Der kjøpte jeg et par svarte støvletter. De var på salg! Jeg sparer to hundre kroner. På veien ut stoppet jeg på caféen. Jeg drakk en kopp kaffe og spiste en wienerbrød. Det var en fin dag.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Hverdagslig",
                Description = "Shoppingfortelling med detaljer og spontane innfall",
                TargetMinPitch = 170,
                TargetMaxPitch = 265,
                TargetPitchVariation = 32,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 20,
                Title = "En uvanlig dag",
                Content = "Noen dager er helt spesielle. I går skjedde det noe utrolig. Jeg våknet av at telefonen ringte. Det var en ukjent nummer. 'Hei, du har vunnet førstepris!' sa stemmen. Jeg trodde det var en spøk. Men det var sant! Jeg hadde vunnet en reise til Paris! Jeg kunne ikke tro det. Hjertet mitt slo fort. Jeg var så spent! Nå drar jeg om to uker. Jeg gleder meg så mye! Paris er drømmen min. Æøå!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Spennende overraskelse med høy emosjonell intensitet",
                TargetMinPitch = 165,
                TargetMaxPitch = 310,
                TargetPitchVariation = 50,
                TargetIntonationRise = 0.55
            });

            texts.Add(new ExerciseText
            {
                Id = 21,
                Title = "Første gang",
                Content = "Jeg husker første gangen jeg prøvde å lage mat. Det var da jeg var tolv år gammel. Jeg ville lage pannekaker til familien. Oppskriften sa to egg, tre dl melk og litt salt. Jeg blandet alt sammen. Så kom mamma inn på kjøkkenet. 'Det ser ut til å bli litt tykt,' sa hun. Hun hadde rett. Pannekakene ble som gummi. De smakte forferdelig! Men alle ler av det nå. Det var en god lærdom. Siden da har jeg blitt mye bedre på kjøkkenet.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Morsom tilbakeblikk med reflekterende tone",
                TargetMinPitch = 160,
                TargetMaxPitch = 260,
                TargetPitchVariation = 34,
                TargetIntonationRise = 0.35
            });

            texts.Add(new ExerciseText
            {
                Id = 22,
                Title = "Hobbyer og interesser",
                Content = "På fritiden liker jeg å male bilder. Det er så avslappende. Jeg bruker akrylmaling og lerret. Hver helg setter jeg av tid til å male. Det er min måte å slappe av på. Noen ganger maler jeg landskap. Andre ganger maler jeg portretter. Fargene inspirerer meg. Blå, gul, rød, grønn. Kombinasjonene er uendelige. Kunsten gir meg glede. Jeg har til og med solgt noen bilder! Det var en stor ære. Å skape noe vakkert er meningsfullt for meg.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Passionert beskrivelse av hobby med rolig intonasjon",
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 23,
                Title = "Et minne fra barndommen",
                Content = "Da jeg var barn, pleide vi å dra til hytta hver sommer. Den lå ved en innsjø i Telemark. Huset var lite og rødt. Det hadde bare to rom. Men vi elsket det! Hver morgen våknet jeg av fuglesang. Utenfor vinduet var det skog og fjell. Vi badet i det kalde vannet hver dag. Far lærte meg å fiske. Mor laget bål og vaffler. Om kveldene satt vi på verandaen og så stjernene. Det var magisk. Jeg savner de dagene. Barna mine får oppleve det samme nå. Tradisjoner holder seg i live.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Nostalgisk barndomsminne med varm tone",
                TargetMinPitch = 160,
                TargetMaxPitch = 270,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 24,
                Title = "Restaurantbesøk",
                Content = "I går kveld dro vi ut på restaurant. Det var en spansk restaurant i sentrum. Den het La Bodega. Vi hadde bestilt bord på forhånd. Da vi kom, var det fullt av folk. Servitøren viste oss til bordet. Menyen var tykk og spennende. Jeg bestilte paella. Det var nydelig! Mannen min tok biff. Barna delte på pizza. Til dessert hadde vi churros med sjokolade. Det var himmelsk! Regningen ble litt dyr, men det var verdt det. Vi hadde en fin kveld sammen. Sånne øyeblikk er dyrebare.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Sensorisk matbeskrivelse med sosialt element",
                TargetMinPitch = 165,
                TargetMaxPitch = 265,
                TargetPitchVariation = 32,
                TargetIntonationRise = 0.3
            });

            // ============================================
            // FLERE AVANSERTE TEKSTER
            // ============================================

            texts.Add(new ExerciseText
            {
                Id = 37,
                Title = "Livshendelse",
                Content = "For fem år siden flyttet jeg til en ny by. Det var både spennende og skummelt. Jeg kjente ingen der borte. Men jeg bestemte meg for å prøve noe nytt. Etter hvert fant jeg nye venner. nå er dette mitt hjem. Endringer kan være vanskelige, men også berikende. Det viktigste er å våge å ta sjanser. Livet er for kort til å være redd.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Personlig veksthistorie med reflekterende tone",
                TargetMinPitch = 160,
                TargetMaxPitch = 270,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 38,
                Title = "Motivationstalen",
                Content = "Du kan klare alt du setter deg fore! Ikke gi opp når det blir vanskelig. Hver dag er en ny sjanse til å bli bedre. Tro på deg selv! Andre tror på deg også. Reist deg og fortsett å gå. Suksess er ikke gitt, den er fortjent. Jobb hardt og vær tålmodig. Drømmene dine er verdt å kjempe for. Ikke la noen fortelle deg at du ikke kan. Du kan alt du vil!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Argumentasjon",
                Description = "Inspirerende tale med høy energi",
                TargetMinPitch = 175,
                TargetMaxPitch = 295,
                TargetPitchVariation = 45,
                TargetIntonationRise = 0.5
            });

            texts.Add(new ExerciseText
            {
                Id = 39,
                Title = "Værmelding",
                Content = "God morgen! Her er værmeldingen for i dag. Det blir delvis skyet med perioder av sol. Temperaturen ligger rundt fem plussgrader. Det blir litt regn mot kvelden. Vinden kommer fra sør og blir moderat. I morgen blir det kaldere med snø i fjellet. Helgen ser bra ut med sol og opphold. Ha en fin dag, og husk å kle deg etter været!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Strukturert værmelding med profesjonell tone",
                TargetMinPitch = 165,
                TargetMaxPitch = 250,
                TargetPitchVariation = 28,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 40,
                Title = "Brødtekst",
                Content = "En gang for lenge siden, i et land langt borte, levde en ung prinsesse ved navn Aurora. Hun hadde langt gyllent hår og øyne som skinte som stjernene. Slottet hennes var bygget av gull og krystall. Hagen var fylt med roser i alle farger. Men prinsessen var ensom. Hun ønsket seg en venn å dele livet med. En dag kom en ung prins til slottet. Han hadde reist over syv fjell og sju elver. Da de møttes, visste de med en gang. Dette var kjærlighet ved første blikk. Og så levde de lykkelig alle sine dager.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Eventyrfortelling med romantisk stemning",
                TargetMinPitch = 160,
                TargetMaxPitch = 280,
                TargetPitchVariation = 40,
                TargetIntonationRise = 0.45
            });

            texts.Add(new ExerciseText
            {
                Id = 41,
                Title = "En sommerdag",
                Content = "Det var en varm sommerdag. Solen skinte fra en skyfri himmel. Fuglene sang i trærne. Barna lekte i hagen. Lukten av blomster fylte luften. Jeg satt på verandaen og nøt stillheten. Plenen var nyklippet og grønn. Innimellom kom naboen bort for en prat. Vi snakket om været og de nye blomstene. Etter en stund dro barna til sjøen. De lo og plasket i vannet. Jeg fulgte etter med håndkleet. Saltvannet var deilig mot huden. Vi ble der helt til solen gikk ned. Da pakket vi sammen og gikk hjem. Det var en perfekt dag.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Lengre sommerfortelling med rolig tempo",
                TargetMinPitch = 160,
                TargetMaxPitch = 260,
                TargetPitchVariation = 35,
                TargetIntonationRise = 0.3
            });

            texts.Add(new ExerciseText
            {
                Id = 42,
                Title = "Karrierereise",
                Content = "Da jeg var ferdig med studiene, hadde jeg ingen aning om hva jeg ville bli. Jeg søkte på mange jobber, men fikk avslag etter avslag. Det var demotiverende å si det mildt. Men jeg ga ikke opp. En dag fikk jeg en uventet telefon. Det var fra et firma i en annen by. De hadde sett CV-en min og ville ha meg til intervju. Jeg dro dit og fikk jobben! De første månedene var krevende. Alt var nytt og fremmed. Kollegene var vennlige og hjalp meg. Etter hvert vokste jeg inn i rollen. Nå er det fem år siden. Jeg har hatt flere forfremmelser og elsker jobben min. Alt kan skje hvis man ikke gir opp.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Karrierereise med opp- og nedturer",
                TargetMinPitch = 160,
                TargetMaxPitch = 270,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 43,
                Title = "Første kjærlighet",
                Content = "Jeg møtte henne første gang på en fest. Hun hadde langt mørkt hår og en smittende latter. Vi ble stående å snakke hele kvelden. Det var som om tiden stoppet. Da festen var over, byttet vi telefonnumre. De neste ukene sendte vi meldinger hver dag. Hjertet mitt banket hver gang telefonen vibrerte. Endelig spurte jeg henne på date. Vi dro på en liten restaurant ved sjøen. Månen skinte på vannet. Vi snakket om alt og ingenting. Det var den beste kvelden i mitt liv. Hun ble min første kjærlighet. Selv om det ikke varte, vil jeg aldri glemme henne.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Romantisk historie om første kjærlighet",
                TargetMinPitch = 160,
                TargetMaxPitch = 280,
                TargetPitchVariation = 42,
                TargetIntonationRise = 0.45
            });

            texts.Add(new ExerciseText
            {
                Id = 44,
                Title = "Morgenrutine",
                Content = "Klokken ringer halv sju. Jeg våkner trøtt, men klarer å stå opp. Først dusjer jeg med varmt vann. Det vekker meg skikkelig. Deretter lager jeg frokost. Eggerøre med bacon og toast. Jeg drikker en stor kopp kaffe. Sånn starter jeg alltid dagen. Etter frokost sjekker jeg meldinger. Det er alltid noe nytt. Deretter kler jeg meg og drar til jobben. Bussen kommer presis halv åtte. Reisen tar tretti minutter. På jobben møter jeg hyggelige kolleger. Vi har et godt arbeidsmiljø. Klokken tolv tar vi lunsj sammen. Pausen er viktig for å lade batteriene. Etter jobben drar jeg på treningssenteret. En time med styrke og cardio. Det føles godt å bevege kroppen. Så drar jeg hjem og lager middag. Kvelden tilbringer jeg med å slappe av. Kanskje se på TV eller lese en bok. Sånn er en vanlig dag for meg.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Detaljert daglig rutine",
                TargetMinPitch = 165,
                TargetMaxPitch = 250,
                TargetPitchVariation = 30,
                TargetIntonationRise = 0.25
            });

            texts.Add(new ExerciseText
            {
                Id = 45,
                Title = "Reiseeventyr",
                Content = "For to år siden bestemte jeg meg for å reise alene til Asia. Det var det beste valget jeg noen gang har tatt. Jeg startet i Thailand, der jeg møtte så mange interessante mennesker. Deretter dro jeg til Vietnam, der maten var utrolig god. I Kina ble jeg fasinert av den lange historien. Templene var praktfulle og stemningsfulle. Japan overrasket meg med sin teknologi og kultur. Koreanerne var utrolig vennlige og hjelpsomme. Hver dag lærte jeg noe nytt om meg selv. Reisen endret måten jeg ser verden på. Jeg ble mer åpen og tolerant. Nå drømmer jeg om å reise tilbake igjen. Kanskje neste gang med noen jeg er glad i. Reiser er virkelig den beste investeringen.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Reiseeventyr med refleksjoner",
                TargetMinPitch = 160,
                TargetMaxPitch = 275,
                TargetPitchVariation = 40,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 46,
                Title = "Familiehemmeligheter",
                Content = "Min bestefar var en mystisk mann. Han snakket sjelden om fortiden. En dag fant jeg en gammel eske på loftet. Inni var det bilder og brev fra krigen. Jeg ble nysgjerrig og spurte bestefar. Først ville han ikke snakke. Men til slutt fortalte han alt. Han hadde vært i kamp i mange år. Han hadde sett ting han aldri glemte. Det var derfor han var så stille. Krigstraumer fulgte ham hele livet. Jeg forsto ham bedre etter den samtalen. Han var en sterk mann som hadde mye å bære. Vi ble nærmere etter det. Jeg besøkte ham hver helg. Han døde fredelig to år senere. Jeg er takknemlig for at jeg fikk vite sannheten.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Familiefortelling med følelsesmessig dybde",
                TargetMinPitch = 160,
                TargetMaxPitch = 275,
                TargetPitchVariation = 42,
                TargetIntonationRise = 0.45
            });

            texts.Add(new ExerciseText
            {
                Id = 47,
                Title = "Mestringshistorie",
                Content = "Da jeg var femten, var jeg veldig usikker på meg selv. Jeg hadde ingen venner og følte meg alene. På skolen ble jeg mobbet fordi jeg var annerledes. Det var en veldig mørk tid i mitt liv. En dag bestemte jeg meg for å gjøre noe. Jeg begynte på en idrett jeg alltid hadde drømt om. Svømming ble min vei ut av mørket. Treningen ga meg selvtillit og nye venner. Jeg lærte at jeg var sterkere enn jeg trodde. Etter hvert ble jeg til og med god nok til å konkurrere. Nå er jeg trener og hjelper andre ungdommer. Min historie viser at det alltid er håp. Uansett hvor mørkt det ser ut, kan du komme tilbake. Tro på deg selv og gi aldri opp.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Inspirativ mestringshistorie",
                TargetMinPitch = 165,
                TargetMaxPitch = 280,
                TargetPitchVariation = 45,
                TargetIntonationRise = 0.5
            });

            texts.Add(new ExerciseText
            {
                Id = 48,
                Title = "Naturens skjønnhet",
                Content = "Jeg våknet tidlig for å se soloppgangen. Det var kaldt ute, men verdt det. Utenfor vinduet kunne jeg se fjellene. Disse gamle fjellene hadde sett så mye. Langt nede i dalen lå tåken tung. Etter hvert begynte solen å stige. Først kom det et rødt skjær over toppene. Så spredte lyset seg sakte nedover. Fargene skiftet fra rødt til orange til gult. Det var som å se magi skje. Fuglene begynte å synge. Naturen våknet til liv. Jeg satt der i stillhet og nøt øyeblikket. Det var en påminnelse om hvor liten vi er. Samtidig følte jeg meg forbundet med alt. Slik ro finnes det ikke i byen. Jeg ønsket at jeg kunne bli der for alltid. Men livet måtte gå videre. Jeg tok med meg minnene hjem.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Beskrivelse",
                Description = "Poetisk naturbeskrivelse",
                TargetMinPitch = 160,
                TargetMaxPitch = 270,
                TargetPitchVariation = 38,
                TargetIntonationRise = 0.4
            });

            texts.Add(new ExerciseText
            {
                Id = 49,
                Title = "Vennskap gjennom livet",
                Content = "Vi møttes som små barn i nabolaget. Det var en gang vi lekte i sandkassen. Siden den dag har vi vært uatskillelige. Vi delte alt med hverandre. Gleder og sorger, hemmeligheter og drømmer. Vi vokste opp sammen og ble voksne. Vi valgte ulike veier i livet. Hun flyttet til en annen by for studier. Jeg ble der jeg var og startet karriere. Selv med avstand holdt vi kontakten. Hver helg ringte vi i timevis. Vi snakket om alt som betydde noe. Nå er vi begge gifte med barn. Vennskapet er like sterkt som før. Barna våre leker sammen som vi gjorde. Det er fantastisk å se. Noen vennskap varer for evigheten. Det er noe av det dyrebareste i livet. Sann vennskap er en gave man må ta vare på.",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fortelling",
                Description = "Historien om et varig vennskap",
                TargetMinPitch = 160,
                TargetMaxPitch = 275,
                TargetPitchVariation = 40,
                TargetIntonationRise = 0.42
            });

            texts.Add(new ExerciseText
            {
                Id = 50,
                Title = "Drømmereise",
                Content = "Drømmereisen min er alltid å reise til Nordlyset. Jeg har sett bilder av det grønne lyset på himmelen. Det ser ut som naturens eget lysshow. Jeg vil dra til Norge i vinter. Der skal jeg bo i en hytte med utsikt. Kanskje jeg får se det berømte lyset. Jeg vil ta bilder som varer evig. Hvis jeg er heldig, kan jeg se det danse. Det må være magisk å oppleve det. I tillegg vil jeg prøve hundekjøring. Det må være spennende å kjøre med Siberian Huskies. Jeg vil også besøke en isbre. Disse gamle ismassene er imponerende. Matlysten vil bli stor i kulden. Jeg vil prøve reinsdyrkjøtt og andre retter. Kanskje jeg får med meg en suvenir. Alt i alt blir dette drømmen min. En dag skal jeg dra dit. Jeg gleder meg bare til å tenke på det!",
                Difficulty = DifficultyLevel.Avansert,
                Category = "Fremtid",
                Description = "Entusiastisk drømmefortelling",
                TargetMinPitch = 170,
                TargetMaxPitch = 290,
                TargetPitchVariation = 48,
                TargetIntonationRise = 0.55
            });

            return texts;
        }
    }
}
