# FEMVOICE - HydrationAdvisor

## Status 2026-06-01

Ferdig implementert og verifisert.

- `HydrationAdvisor` er trendbasert og filtrerer enkeltspikes.
- Hydration-rad er rate-limited med `MinimumSuggestionInterval` for a unnga spam.
- Advisor publiserer bare stottende hydration-events og endrer aldri `Restrict` eller `Lock`.
- `HydrationFeedbackMapper` sender rad inn i feedback-pipelinen som lavere-prioritert stotte.
- Tester dekker spike-filter, trendbasert rad, cooldown/no-spam, safety lock og feedback mapping.

## Formal

HydrationAdvisor overvaker indirekte tegn pa stemmetretthet og mulig dehydrering.

Systemet gir rolige stotteanbefalinger. Det skal aldri kontrollere okten direkte.

## Ansvarsomrade

HydrationAdvisor:

- Observerer trender
- Foreslar hydrering
- Stotter recovery

HydrationAdvisor skal aldri:

- Stoppe okten
- Sette `Restrict`
- Sette `Lock`

## Datakilder

- Resonance Drift: gradvis reduksjon i resonanskvalitet.
- Stability Variance: okende ustabilitet over tid.
- Vocal Load: akkumulert belastning gjennom okten.
- Fatigue Correlation: hydration vurderes sammen med fatigue-signaler.

## Beslutningsmodell

Raw Metrics -> Trend Engine -> Hydration Indicators -> Hydration Recommendation

## Meldingsstil

Tillatt:

- "Det kan vaere nyttig a ta noen slurker vann."
- "Litt hydrering kan hjelpe komforten."

Ikke tillatt:

- "Du er dehydrert."
- "Drikk vann na."
- "Stopp treningen."

## Integrasjon

`HealthSafetyState` -> `PausePolicy` -> `HydrationAdvisor`

Hydration er alltid sekundart til helsebeskyttelse.

## Testing

☑ Ikke trigges av enkeltspikes
☑ Basert pa trender
☑ Rate-limited
☑ Ingen spam
☑ Ingen pavirkning pa Restrict/Lock

## Verifisering

- `dotnet build FemVoiceStudio.slnx --no-restore -p:BaseOutputPath=.\bin\CodexBuild\ --verbosity minimal` - gronn, 0 warnings, 0 errors.
- `dotnet test FemVoiceStudio.Tests\FemVoiceStudio.Tests.csproj --no-build -p:BaseOutputPath=.\bin\CodexBuild\ --logger "console;verbosity=minimal"` - 281/281 tester gront.
