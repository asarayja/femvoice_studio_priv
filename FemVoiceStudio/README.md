# FemVoice Studio

Et Windows WPF-program for å trene på en mer feminin stemme gjennom sanntids pitch-analyse og strukturerte øvelser.

## 🚀 Kom i gang

### Kjør applikasjonen

Den enkleste måten å kjøre programmet på er å bruke den publishede versjonen:

```
FemVoiceStudio\publish\FemVoiceStudio.exe
```

Denne versjonen er selvstendig og krever ikke at .NET er installert.

### Alternativ: Kjør fra kildekode

Hvis du har .NET SDK installert:

```bash
cd FemVoiceStudio
dotnet run
```

### Bygg selv

```bash
# Debug versjon
cd FemVoiceStudio
dotnet build

# Release versjon
dotnet build -c Release

# Selvstendig executable (ingen .NET installert kreves)
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

### Kjør tester

```bash
# Kjør alle tester
cd FemVoiceStudio
dotnet test

# Kjør tester med detaljert output
dotnet test --verbosity normal
```

## 📋 Funksjoner

### 🎤 Lydopptak og analyse
- **Sanntids mikrofonopptak** med NAudio
- **Pitch-deteksjon** ved hjelp av YIN-algoritmen (robust for tale)
- **Intensitet-måling** for å detektere stille segmenter
- Mål-latens under 100ms

### 📊 Målparametere for feminin stemme
| Parameter | Målområde |
|-----------|-----------|
| Gjennomsnittlig pitch | 165-255 Hz |
| Primært fokusområde | 180-220 Hz |
| Pitch-variasjon | > 15 Hz standardavvik |
| Intonasjon | Stigende i spørsmål |
| F2 (resonans) | 1400-2200 Hz |

### 🧠 Smart Coach System

FemVoice Studio inkluderer et avansert Smart Coach-system som gir individualisert veiledning basert på logopedisk praksis og evidensbaserte prinsipper for stemmefeminisering.

#### FemVoiceScore-algoritme

Beregningsvektning:
- **Resonans (45%)** - Høyeste prioritet, lysere klang
- **Pitch (30%)** - Sekundær, aldri på bekostning av resonans
- **Intonasjon (15%)** - Naturlig variasjon
- **Stemmehelse (10%)** - Sikkerhetsovervåking

#### Treningsnivåer

| Nivå | Emoji | Fokus | Toleranser |
|------|-------|-------|------------|
| Nybegynner | 🟢 | Resonans først | ±20% |
| Middels | 🟡 | Presis pitch | ±10% |
| Avansert | 🔵 | Naturlig tale | ±5% |

**Nivåovergangsregler:**
- Oppgrader: >70% score i 7 av 10 siste økter
- Nedgrader: <50% score i 5 av 10 økter eller pressdeteksjon
- Minimum 14 dager mellom endringer

#### Retningsanalyse

Systemet analyserer kontinuerlig og gir anbefalinger:
- ⬆️ Øk parameter
- ⬇️ Reduser parameter
- ✅ Stabiliser/god behold

**Klinisk regel:** Resonans prioriteres ALLTID før pitch

### 📝 Øvelsestekster (Norsk)

**Nybegynner (4 tekster)**
- Hilsener
- Introduksjon  
- Dagligdagse ting
- Følelser

**Middels (4 tekster)**
- Spørsmål
- Butikken
- Planer
- Beskrivelser

**Avansert (5 tekster)**
- Eventyr
- Telefonsamtale
- Følelsesuttrykk
- Argumentasjon
- Emosjonell historie

### 💬 Tilbakemeldingssystem
Dynamisk tilbakemelding på norsk basert på:
- Gjennomsnittlig pitch (innenfor/utenfor målområde)
- Pitch-variasjon
- Intonasjonsmønster
- Konsistens

**Coach-meldinger** følger What/Why/Hvordan-struktur:
- Hva: Konkret parameter å fokusere på
- Hvorfor: Pedagogisk forklaring
- Hvordan: Spesifikk øvelse

### 🎯 Progresjonssystem
- Automatisk vanskelighetsjustering
- 5 økter med 75%+ score = nivåopprykk
- Streak-telling for daglig trening
- Statistikk over tid
- Personlige mål basert på historikk

### 📅 Kalender og statistikk
- Visuell treningskalender
- Oversikt over fullførte økter
- Gjennomsnittlig pitch og score
- Progresjon over tid

## 🏗️ Teknisk arkitektur

### Stack
- **.NET 10.0** med WPF
- **NAudio 2.2.1** - Lydbehandling
- **OxyPlot 2.1.2** - Grafer og visualisering
- **Microsoft.Data.Sqlite 8.0.0** - Databaselagring
- **CommunityToolkit.Mvvm 8.2.2** - MVVM rammeverk
- **xUnit** - Enhetstesting

### Prosjektstruktur
```
FemVoiceStudio/
├── Audio/                    # Lydbehandling
│   ├── AudioCaptureService.cs   # Mikrofonopptak
│   ├── PitchDetectionService.cs # YIN pitch-deteksjon
│   └── AudioAnalyzerService.cs  # Sanntids-analyse
├── Data/
│   └── DatabaseService.cs       # SQLite database
├── Models/                   # Data-modeller
├── Services/                # Forretningslogikk
│   ├── SmartCoachEngine.cs       # Kjernelogikk
│   ├── FemVoiceScore.cs          # Scoringsalgoritme
│   ├── LevelClassificationSystem.cs # Nivåhåndtering
│   ├── DirectionAnalyzer.cs      # Retningslogikk
│   ├── CoachMessageGenerator.cs  # Tilbakemeldinger
│   └── VoiceProfileExtensions.cs # Personalisering
├── Tests/                   # Enhetstester
├── ViewModels/              # MVVM ViewModels
├── Views/                   # WPF XAML vinduer
└── Converters/             # XAML value converters
```

### Kjernetjenester (Smart Coach)

| Tjeneste | Beskrivelse |
|----------|-------------|
| `FemVoiceScore` | Beregner vektet score (45% resonans, 30% pitch, 15% intonasjon, 10% helse) |
| `LevelClassificationSystem` | Håndterer nivåoverganger (Beginner/Intermediate/Advanced) |
| `DirectionAnalyzer` | Analyserer parametere og gir retningsanbefalinger |
| `CoachMessageGenerator` | Genererer What/Why/Hvordan-meldinger |
| `VoiceProfileExtensions` | Personalisert læring med bandit-algoritme (80% utnytte, 20% utforske) |
| `SmartCoachEngine` | Koordinerer alle tjenester |

### Database-skjema (SQLite)

Se `Resources/DatabaseSchema.sql` for komplett skjema.

**Hovedtabeller:**
- `UserSettings` - Brukerinnstillinger
- `TrainingSessions` - Treningshistorikk
- `FemVoiceScores` - Beregnede scores
- `TrainingLevels` - Nivåsporing
- `DirectionRecommendations` - Coaching-anbefalinger
- `VoiceProfiles` - Personalisert læring
- `SmartCoachGoals` - Målsporing

## ⚠️ Krav

- Windows 10/11 (x64)
- Mikrofon tilkoblet
- Windows .NET 10.0 Runtime (kun for ikke-selvstendig versjon)

## 🔧 Feilsøking

### "Ingen mikrofon funnet"
- Sjekk at mikrofonen er tilkoblet
- Sjekk at Windows har tilgang til mikrofonen i innstillinger

### Programmet starter ikke
- Sørg for at du har Windows 10/11
- Prøv å kjøre som administrator
- Sjekk at ingen annen app bruker mikrofonen

### Lav score selv om pitch er riktig
- Kalibrer mikrofonens følsomhet
- Sørg for at du snakker høyt nok
- Reduser bakgrunnsstøy

### Mikrofonkalibrering
- Gå til innstillinger og velg `Kalibrer mikrofon`.
- Trykk start, mål først stille rom, og trykk deretter selv videre til stemme/humming-målingen.
- Kalibreringen viser RMS, dBFS, signal/støy-forhold (SNR) og peak-nivå. Lav SNR betyr at stemmen ligger for nær romstøyen; peak nær 0 dBFS betyr at input kan klippe.
- Hvis appen sier at signalet er for lavt, flytt mikrofonen nærmere eller øk Windows inputnivå litt. Hvis signalet er for høyt, senk Windows inputvolum eller mikrofon-gain.

### Tester feiler
- Sjekk at alle prosjekt-referanser er korrekte
- Verifikér at database-tilkoblingen er tilgjengelig

## 📝 Lisens

Dette prosjektet er utviklet for læring og personlig utvikling.
