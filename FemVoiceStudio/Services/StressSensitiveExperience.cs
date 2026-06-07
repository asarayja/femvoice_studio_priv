using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Tilgjengelighets-tjeneste som demper presentasjonslaget for brukere som har
    /// slått på <see cref="UserVoiceProfile.StressSensitiveMode"/> og/eller
    /// <see cref="UserVoiceProfile.ReducedVisualFeedback"/>.
    ///
    /// KLINISK PRINSIPP (ufravikelig): dempingen endrer KUN HVORDAN sikkerhets- og
    /// helseinformasjon formidles — aldri OM den formidles. En lås/strain-tilstand
    /// vises fortsatt; den vises bare med varme farger (gul) i stedet for rødt og med
    /// rolig formulering (Suggestion-severity i stedet for Warning). Safety-INNHOLDET
    /// beholdes alltid. Dette respekterer hierarkiet Safety &gt; Health &gt; ... &gt; UI:
    /// UI-laget gjøres mildere, men informasjonen over det i hierarkiet består.
    ///
    /// Tjenesten er en DI-singleton. Profilen lastes lazy ved første bruk og caches;
    /// <see cref="Refresh"/> tvinger ny lasting etter at Settings er lagret. Når
    /// databasen mangler (tester/design) faller alt trygt tilbake til "av" — dvs.
    /// uendret oppførsel — og kaster aldri.
    /// </summary>
    public sealed class StressSensitiveExperience
    {
        private readonly IDatabaseService? _database;
        private readonly object _gate = new();

        private bool _loaded;
        private bool _isStressSensitive;
        private bool _isReducedVisual;

        public StressSensitiveExperience(IDatabaseService? database)
        {
            // Null-database tillates bevisst: tjenesten skal aldri krasje i kontekster
            // uten DI (tester, design-tid). Den oppfører seg da som "alt av".
            _database = database;
        }

        /// <summary>Roligere, mindre pågående presentasjon når brukerprofilen ber om det.</summary>
        public bool IsStressSensitive
        {
            get { EnsureLoaded(); return _isStressSensitive; }
        }

        /// <summary>Færre samtidige visuelle signaler når brukerprofilen ber om det.</summary>
        public bool IsReducedVisual
        {
            get { EnsureLoaded(); return _isReducedVisual; }
        }

        /// <summary>
        /// Tvinger ny lasting av profilen ved neste bruk. Kalles etter at Settings er
        /// lagret slik at endrede flagg slår igjennom uten omstart. Idempotent og trygg.
        /// </summary>
        public void Refresh()
        {
            lock (_gate)
            {
                _loaded = false;
            }
        }

        private void EnsureLoaded()
        {
            lock (_gate)
            {
                if (_loaded)
                    return;

                // Default = av (uendret oppførsel) hvis databasen mangler eller feiler,
                // eller hvis ingen profil er lagret ennå. Aldri kast herfra.
                var stress = false;
                var reduced = false;

                if (_database != null)
                {
                    try
                    {
                        var profile = _database.GetUserVoiceProfile();
                        if (profile != null)
                        {
                            stress = profile.StressSensitiveMode;
                            reduced = profile.ReducedVisualFeedback;
                        }
                    }
                    catch
                    {
                        // Trygt fall tilbake til "av" — tilgjengelighetsdemping skal aldri
                        // hindre at appen fungerer.
                        stress = false;
                        reduced = false;
                    }
                }

                _isStressSensitive = stress;
                _isReducedVisual = reduced;
                _loaded = true;
            }
        }

        /// <summary>
        /// Demper en brush-nøkkel når StressSensitiveMode er på: røde alarm-farger
        /// erstattes av varme advarselsfarger. Tilstanden FORMIDLES fortsatt — bare
        /// roligere. Ukjente nøkler og avslått modus gir nøkkelen uendret tilbake.
        /// </summary>
        public string SoftenBrushKey(string brushKey)
        {
            if (string.IsNullOrEmpty(brushKey) || !IsStressSensitive)
                return brushKey;

            return brushKey switch
            {
                "ErrorBrush"        => "WarningBrush",
                "QualityBrush_Poor" => "QualityBrush_Fair",
                _                   => brushKey
            };
        }

        /// <summary>
        /// Demper meldings-severity når StressSensitiveMode er på: Warning blir
        /// Suggestion (innholdet beholdes — bare den visuelle prominensen dempes).
        /// Info og Suggestion er allerede rolige og passerer uendret.
        /// </summary>
        public MessageSeverity SoftenSeverity(MessageSeverity severity)
        {
            if (!IsStressSensitive)
                return severity;

            return severity == MessageSeverity.Warning
                ? MessageSeverity.Suggestion
                : severity;
        }
    }
}
