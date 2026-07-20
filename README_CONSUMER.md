# FemVoice Studio

FemVoice Studio is a cross-platform voice training app. It provides real-time acoustic biofeedback, structured exercises, adaptive coaching, and long-term progress tracking — all running locally on your device.

The app is built around modern clinical voice research. Rather than focusing on pitch alone, FemVoice Studio trains resonance shaping, tonal stability, intonation variation, and vocal health — the factors that most influence perceived vocal femininity.

> FemVoice Studio is a training support tool. It is not a cloud service, not a medical device, and does not replace a qualified speech-language pathologist or clinician.

---

## Disclaimer and Liability

FemVoice Studio is a **self-help training aid only**. It is **not** a substitute for a qualified voice trainer, speech-language pathologist, or other clinician, and it does not provide medical or clinical advice.

The app has **not** been approved, certified, registered, or clinically validated by any regulatory body, health authority, or professional organisation. Its exercises, measurements, scores, and guidance are built on published voice research and developed with great care, but they have **not been independently verified** and may be incomplete or inaccurate. Do not treat any output as a diagnosis or clinical assessment.

Use of FemVoice Studio is **entirely at your own risk**. The app is provided "as is", without warranties of any kind, express or implied. To the fullest extent permitted by law, the author accepts **no responsibility or liability** for any injury, vocal harm, loss, or damage arising from the use of, or reliance on, this app. If you have vocal health concerns — or before starting any new voice-training programme — consult a qualified professional.

---

## Runs On Your Devices

FemVoice Studio is built on a single shared, cross-platform interface (Avalonia), so you get the same navigation, exercises, and theme on every supported platform.

**Available now:**

- **Windows** desktops and laptops (10 or 11).
- **Android** phones and tablets.
- **Linux** (x64 or ARM64).

**In development (not ready yet):**

- **macOS** (Apple Silicon or Intel).
- **iPhone / iPad**.

The layout adapts to the screen: a full multi-column view on larger displays, and a compact layout with collapsible navigation on phones. Your training data lives on each device separately unless you move it yourself.

---

## What It Does

- Captures real-time voice input from your microphone and displays live pitch and resonance feedback.
- Analyses pitch (Hz), resonance (F1/F2/F3 formants), intonation variation, vocal weight, comfort, and consistency.
- Provides structured exercises for pitch, resonance, intonation, breathing, and practical speech.
- Tracks training sessions and scores over time with trend analysis.
- Adapts training difficulty and focus based on your recent history through SmartCoach.
- Monitors vocal health signals and prompts rest, hydration, and recovery when needed.
- Generates PDF, CSV, and JSON reports for personal review or sharing with a professional.
- Works across your devices with a layout that adapts to each screen.
- Supports light mode, dark mode, and system default themes.
- Available in 20 languages.
- Stores all data locally on each device.

---

## Who It Is For

FemVoice Studio is primarily designed for transfeminine individuals working toward a more feminine speaking voice. It can also be useful for anyone wanting a structured, self-guided voice practice tool with measurable feedback.

It is not intended to replace clinical voice therapy. Users with vocal health concerns should consult a qualified professional.

---

## Core Training Philosophy

Most voice training apps focus on raising pitch as high as possible. FemVoice Studio takes a different approach.

Pitch matters, but perceived femininity is more strongly influenced by **resonance placement**, **tonal stability**, and **intonation variation**. Chasing pitch without building resonance and comfort often leads to strain, fatigue, and unsustainable habits.

FemVoice Studio is designed to:

- Prioritise resonance shaping over pitch chasing.
- Protect vocal health throughout every session.
- Build habits that are sustainable over weeks and months.
- Adapt to each user's individual baseline and progression rate.
- Discourage pushing, pressing, or forcing the voice.

---

## Main Areas

### Dashboard
The main practice surface. Shows live pitch and resonance feedback, comfort-zone status, current SmartCoach recommendations, session controls, streaks, and quick access to all other tools.

### Exercise Guide
A structured library of practice activities organised by focus area and difficulty level. Exercises cover pitch gliding, resonance placement, intonation patterns, breath control, sentence reading, and conversation simulation. Each exercise includes step-by-step guidance, real-time feedback, and safety notes.

### SmartCoach
An adaptive coaching system that uses your training history, health signals, and progression data to recommend what to focus on each day. SmartCoach adjusts its suggestions based on recovery status, plateau detection, recent scores, and voice health indicators.

### Analysis
Detailed charts and trend views for pitch, resonance, intonation, vocal weight, comfort, and health-related signals. Includes session summaries, score history, and longitudinal trends. These tools are for training feedback and self-reflection — not clinical diagnosis.

### Resonance Analysis
A dedicated view for formant-based resonance inspection. Displays real-time F1/F2 placement, a resonance timeline, and target-area overlays. Useful for understanding resonance patterns and forward placement during practice.

### Progression
Shows how your training is developing over time. Tracks level transitions, session consistency, success rates, and whether there is enough data to make meaningful progress estimates.

### Case Review
Lets you create, review, and complete structured voice session reviews. Useful for personal reflection or for sharing selected notes with a speech therapist or clinician.

### Reports
Generates exportable summaries in PDF, CSV, or JSON format, useful for reviewing progress or sharing selected summaries with a professional.

### Settings
Covers theme, language selection, voice goals, training frequency, accessibility options (calm mode, reduced visual feedback), microphone calibration, monitoring your own voice in real time, backup and restore, and database management.

---

## Supported Languages

FemVoice Studio is fully localised and currently available in 20 languages:

🇬🇧 English · 🇳🇴 Norwegian · 🇸🇪 Swedish · 🇩🇰 Danish · 🇫🇮 Finnish  
🇫🇷 French · 🇪🇸 Spanish · 🇵🇹 Portuguese (Brazil) · 🇮🇹 Italian · 🇭🇷 Croatian  
🇩🇪 German · 🇳🇱 Dutch · 🇵🇱 Polish · 🇨🇿 Czech · 🇭🇺 Hungarian  
🇷🇴 Romanian · 🇹🇷 Turkish · 🇺🇦 Ukrainian · 🇸🇦 Arabic · 🇬🇷 Greek

The localisation system is built for easy expansion with additional languages in future releases.

---

## Data and Privacy

FemVoice Studio is local-first. All training data, session history, settings, and notes are stored on your own device. Nothing is sent to external servers.

Exports and support packages are entirely user-controlled. Avoid including personal identifiers or sensitive health information in exports unless you intend to share them.

---

## Requirements

- One of the currently available platforms: Windows 10/11, Android (5.0 / API 21 or newer), or Linux. macOS and iPhone/iPad versions are still in development and not ready yet.
- A working microphone. On phones and tablets, allow the microphone permission when the app asks for it, so live feedback works.
- A reasonably quiet practice environment.
- The packaged app build for your platform (for example the Windows installer) — it is self-contained, so no separate runtime install is needed. Building from source uses the .NET 10 SDK.

---

## Under the Hood

| Component | Details |
|---|---|
| Interface | Avalonia (.NET 10), MVVM — one shared UI across Windows, Linux, and Android |
| Audio capture | Real time via the platform backend: NAudio (Windows), ALSA (Linux), Android audio; synthetic fallback |
| Acoustic analysis | FFT-based pitch detection and formant extraction (F1/F2/F3) |
| Data | SQLite (Microsoft.Data.Sqlite), local-first |
| Reports | QuestPDF (PDF) plus CSV and JSON export |
| Charts | Custom-drawn pitch and resonance visualisations |

---

## Safety

Stop or pause immediately if you experience pain, strain, hoarseness, dizziness, or unusual discomfort. The app includes built-in safety systems that monitor vocal load and prompt rest when signals indicate strain — but these are assistive tools, not guarantees.

Use FemVoice Studio as a training aid. For clinical concerns, consult a qualified speech-language pathologist.

---

## License

To be defined.
