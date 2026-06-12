# Known Limitations

Dette dokumentet lister kjente begrensninger for kontrollert ekstern testing.

## Sugerørøvelse

Status: NOT TESTED, ikke FAIL

Straw exercise / sugerør-del ble ikke testet under validering fordi fysisk sugerør ikke var tilgjengelig.

Follow-up:

- Test øvelsen når fysisk sugerør er tilgjengelig.
- Bekreft at veiledning, timing, feedback og sikkerhetstekst vises riktig.
- Ikke klassifiser dette som feil før faktisk hardware/utstyrstest er gjennomført.

## Manuell WPF accessibility / visual pass

Status: trengs fortsatt

Automatiske tester dekker tekst, rapportetiketter, safe wording og enkelte UI-robusthetsregler. Manuell visuell sjekk er fortsatt nødvendig for:

- Tastaturnavigasjon.
- Fokusrekkefølge.
- Kontrast.
- Små vinduer.
- Lang norsk tekst.
- Skjermleseropplevelse.

## Eldre ikke-primære språkfiler

Status: ikke release-blocker for nb/en

Norsk og engelsk release gate er ren. Eldre ikke-primære oversettelser kan fortsatt trenge cleanup senere.

## Klinisk validering

FemVoice bruker trygge, støttende prinsipper, men dette er ikke medisinsk godkjenning. Videre gjennomgang av kvalifisert stemmefagperson anbefales før bredere klinisk bruk.

