using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Documentation for the Resonance Module - Theory and Implementation
    /// 
    /// RESONANS-MODUL TEORI
    /// =====================
    /// 
    /// 1. HVA ER RESONANS?
    /// -------------------
    /// Resonans i stemmen refererer til hvordan lydbølger forsterkes og moduleres 
    /// av vokaltraktens resonatorer. Hos cis-kvinner er resonans typisk "fremre" - 
    /// lyden kjennes og høres primært i munnen og ansiktet. Hos cis-menn er resonans 
    /// ofte "bakre" - mer i brystet og halsen.
    /// 
    /// Vokaltraktens viktigste resonatorer er:
    /// - Munnhulen (oral cavity)
    /// - Svelget (pharyngeal cavity)
    /// - Nesehulen (nasal cavity)
    /// 
    /// 2. FREMMRE STEMMEPLASSERING
    /// ---------------------------
    /// "Fremre resonans" eller "head voice" betyr at vibrasjonen kjennes i:
    /// - Leppene
    /// - Nesebroen
    /// - Tennene
    /// - Bihulene
    /// 
    /// Dette skaper en lysere, mer bærende klang med flere høyfrekvente overtoner.
    /// 
    /// Akustisk måles fremre resonans ved:
    /// - Høyere formant-frekvenser (spesielt F2)
    /// - Mindre energi i lave frekvenser
    /// - Mer energi i høyfrekvente områder
    /// 
    /// 3. HVORFOR ER DET KRITISK FOR STEMMEFEMINISERING?
    /// -------------------------------------------------
    /// Selv om pitch (frekvens) er den mest merkbare endringen, er resonans like 
    /// viktig for å skape en overbevisende feminin stemme. En høy pitch med "bakre" 
    /// resonans vil fortsatt høres maskulin ut.
    /// 
    /// Forskning viser at:
    /// - Kvinner har typisk høyere F1 og F2 enn menn
    /// - Resonans-endring kan være like viktig som pitch-endring
    /// - Kombinasjonen av riktig pitch + fremre resonans = mest effektiv feminisering
    /// 
    /// 4. PARAMETRE FOR ANALYSE
    /// ------------------------
    /// Appens analysemotor bør måle følgende for resonans-tilbakemelding:
    /// 
    /// a) Formant-frekvenser:
    ///    - F1 (første formant) - korrelerer med vokalåpning
    ///    - F2 (andre formant) - korrelerer med fremre/bakre resonans
    ///    - F2 er spesielt viktig for feminisering
    ///    
    /// b) Spectral tilt:
    ///    - Måler forholdet mellom høye og lave frekvenser
    ///    - Fremre resonans = mindre spectral tilt (mer energi høyt)
    ///    
    /// c) Harmonics-to-noise ratio (HNR):
    ///    - Indikerer stemmekvalitet
    ///    - God resonans = høyere HNR
    ///    
    /// d) Vibrasjonsmønster:
    ///    - Kan bruke accelerometer på telefonen
    ///    - Måle hvor vibrasjonen kjennes (lepper vs bryst)
    /// 
    /// 5. ØVELSER FOR RESONANS-ENDRING
    /// -------------------------------
    /// 
    /// humming (Øvelse 1, 11):
    /// - Aktiverer fremre resonatorer
    /// - Kjenn vibrasjon i lepper/nese
    /// 
    /// Nasale lyder (m, n, ng, ny):
    /// - Fører lyd til nesehulen
    /// - Skaper fremre resonans-følelse
    /// 
    /// Frontvokaler (i, e, æ):
    /// - Hever tungryggen
    /// - Åpner munnen bredere frem
    /// 
    /// Semi-occluded vocal tract (SOVT):
    /// - Straw phonation
    /// - Lip trills
    /// - Reduserer belastning, forbedrer resonans
    /// 
    /// 6. IMPLEMENTASJONSNOTER
    /// -----------------------
    /// 
    /// For å implementere resonans-analyse i appen:
    /// 
    /// 1. FFT-analyse av lydsignalet
    ///    - Extract formant-frekvenser (F1, F2)
    ///    - Bruk linear predictive coding (LPC)
    ///    
    /// 2. Spektral analyse
    ///    - Beregn spectral centroid
    ///    - Mål spectral tilt
    ///    
    /// 3. Tilbakemelding til bruker
    ///    - Visualiser F1/F2 på graf
    ///    - Gi feedback: "Mer fremre resonans!" eller "Bra!"
    ///    - Vis mål-område for F2 (typisk > 2000 Hz for feminin)
    /// 
    /// Målverdier for feminin resonans:
    /// - F1: 300-1000 Hz (høyere for åpne vokaler)
    /// - F2: 1500-2500 Hz (høyere = mer fremre)
    /// - Spectral centroid: > 2000 Hz
    /// 
    /// </summary>
    public class ResonanceModuleDocumentation
    {
        /// <summary>
        /// Get recommended formant target ranges for feminization
        /// </summary>
        public static FormantTargets GetFeminineFormantTargets()
        {
            return new FormantTargets
            {
                F1Min = 300,
                F1Max = 1000,
                F2Min = 1500,
                F2Max = 2500,
                SpectralCentroidMin = 2000,
                Description = "Målverdier for feminin resonans: høyere F2 og spektral sentralfrekvens"
            };
        }
        
        /// <summary>
        /// Get exercises specifically targeting resonance
        /// </summary>
        public static List<EnhancedExercise> GetResonanceExercises(VoiceFeminizationExerciseService service)
        {
            return service.GetExercisesByGoal(GoalCategory.Resonance);
        }
    }
    
    /// <summary>
    /// Target values for feminine resonance
    /// </summary>
    public class FormantTargets
    {
        public double F1Min { get; set; }
        public double F1Max { get; set; }
        public double F2Min { get; set; }
        public double F2Max { get; set; }
        public double SpectralCentroidMin { get; set; }
        public string Description { get; set; } = "";
    }
}