namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Ren (sideeffekt-fri) policy som avgjør OM recovery-grenen i
    /// <see cref="TargetProfileAdapter"/> skal aktiveres for en gitt målsone-kontekst.
    /// Koblingsledd mellom den persisterte kliniske gaten (<see cref="ProgressionSafetyGate"/>)
    /// og den allerede ferdig-implementerte recovery-krympingen i adapteren —
    /// adapteren endres ikke, denne policyen bestemmer kun flagget den får inn.
    ///
    /// KLINISK INVARIANT (Safety/Health &gt; Progression): recovery-flagget kan kun føre til
    /// at soner KRYMPER, aldri til at krav utvides. Derfor er begge beslutningene rene
    /// AND-er av blokkering med en ekstra anti-dobbelt-skalerings-betingelse — de kan
    /// aldri SETTE recovery der den ikke alt følger av at gaten er blokkert.
    ///
    /// Statisk og tilstandsløs, à la <see cref="PitchTargetZonePolicy"/>; ingen DI/IO.
    /// </summary>
    public static class RecoveryActivationPolicy
    {
        /// <summary>
        /// Forsidens pitch-målsone (MainViewModel): recovery er aktiv nøyaktig når den
        /// persisterte kliniske gaten er blokkert. Forsiden bygger alltid fra den
        /// USKALERTE policy-sonen (PitchTargetZonePolicy) + brukerens kalibrerte
        /// komfortsone — det finnes ingen forhånds-recovery-skalert override der, så
        /// krympingen må skje når gaten blokkerer.
        /// </summary>
        public static bool ForHomeZone(bool gateBlocked) => gateBlocked;

        /// <summary>
        /// Øvelsesvinduets måltprofil (ExerciseWindow): recovery er aktiv KUN når gaten er
        /// blokkert OG ingen persistert profil-override ble anvendt.
        ///
        /// DOBBEL-SKALERINGS-NYANSE: en anvendt override er allerede recovery-skalert av
        /// <see cref="Progression.ProgressionOrchestrator"/> (ClampToRecoveryFloor før
        /// persist). Å sette recovery på nytt i detalj-visningen ville da krympe kravene
        /// to ganger. Når INGEN override finnes brukes den uskalerte fabrikkprofilen, og
        /// krympingen MÅ skje her. Derfor: gateBlocked &amp;&amp; !overrideApplied.
        /// </summary>
        public static bool ForExerciseProfile(bool gateBlocked, bool overrideApplied)
            => gateBlocked && !overrideApplied;
    }
}
