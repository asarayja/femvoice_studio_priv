# Research Export Guide

Research export er laget for kontrollert analyse uten unødvendig persondata.

## Standardadferd

- Export er anonymisert som standard.
- Profesjonelle notater er ekskludert som standard.
- Personlige fritekstfelt er ekskludert som standard.
- Mikrofonnavn og direkte bruker-id skal ikke eksporteres som identifiserbare verdier.

## Hva testere bør sjekke

1. Lag noen testøkter.
2. Kjør research export.
3. Åpne CSV/JSON.
4. Kontroller at filene kan leses.
5. Kontroller at navn, mikrofonnavn og fritekst ikke er med.

## Samtykke og advarsel

Research export bør bare brukes når tester forstår hva som eksporteres. Export skal være bruker-initiert.

## Ikke legg ved

Ikke legg ved profesjonelle notater, private notater eller identifiserbare data i research export med mindre det er en egen avtalt test.

