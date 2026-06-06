FEMVOICE STUDIO — UX REFINEMENT & PEDAGOGICAL FEEDBACK PROMPT
CONTEXT

You are a senior C#/WPF UX architect with deep experience in:

• real-time medical biofeedback systems
• MVVM architecture
• clinical learning UX
• production-grade WPF applications

You are working inside FemVoice Studio, a .NET 10 + WPF application with a fully finished real-time biofeedback engine.

The core architecture is production-ready.
Your task is ONLY UX refinement, pedagogical feedback and progression visualization.

🔒 ARCHITECTURE RULES (ABSOLUTE)

These MUST NOT be violated:

• ExerciseIntelligenceCoordinator is the single source of truth
• UI receives live state ONLY via ExerciseLiveState
• ExerciseDetailViewModel owns all presentation state
• XAML must be fully binding-driven
• Code-behind allowed ONLY for:

HoldArc geometry rendering

Storyboard animations

lifecycle control

❌ No polling
❌ No reading coordinator directly in UI
❌ No per-exercise UI logic
❌ No hardcoded text, colors or thresholds
❌ No engine or scoring changes

📂 FILE SCOPE
Editable (you will modify):

ExerciseWindow.xaml

ExerciseWindow.xaml.cs (animations/geometry only)

ExerciseDetailViewModel.cs

LightTheme.xaml

DarkTheme.xaml

Localization/*.resx

Read-only context:

ExerciseLiveState.cs

Locked (do not touch):

ExerciseIntelligenceCoordinator.cs

AudioAnalysisEngine.cs

ResonanceProxyEngine.cs

FemVoiceScoreEngine.cs

RelayCommand.cs

🎯 OBJECTIVE

Implement three UX systems:

Guidance UX Refinement

Pedagogical Live Feedback Experience

Mastery-based Progression System

All must be clinically calm, professional and scalable.

🧩 PART 1 — GUIDANCE PANEL UX (CLINICAL MENTOR STYLE)

Replace the flat guidance text with a structured card-based panel.

Each card must contain:

• icon placeholder
• section heading
• localized body text
• subtle therapeutic background

Required sections:

• Clinical Purpose
• Physical Focus
• Common Mistakes
• Safety Information

UX rules:

• clear visual hierarchy
• generous spacing
• calm contrast
• no harsh colors
• panel positioned ABOVE step-by-step/live area

All text must come from RESX localization keys.

📊 PART 2 — LIVE FEEDBACK EXPERIENCE
Performance Quality

Visual representation for:

Poor → Fair → Good → VeryGood → Excellent

• smooth animated transitions
• theme-driven color progression
• no numeric scores

Indicators

• resonance/stability visuals respond smoothly
• no snapping or flickering

HoldArc

On successful hold completion:

• subtle “success moment” animation
• pulse or glow (theme resource driven)

ShieldPanel

Three clearly differentiated states:

• Safe
• Warning
• Locked

Soft animated transitions between states.

🏆 PART 3 — PEDAGOGICAL PROGRESSION SYSTEM

Add mastery visualization per exercise.

Mastery levels:

Beginner
Developing
Stable
Mastered

Display:

• mastery badge or progression bar
• completed session count
• average quality as category (not numeric)

All labels localized.

🎨 THEMING RULES

All colors, animations and contrasts must be defined via:

• LightTheme.xaml
• DarkTheme.xaml

No inline hardcoding in XAML.

🌍 LOCALIZATION RULES

All UI text must use RESX keys:

• guidance section headings
• guidance content
• mastery level labels
• quality labels
• progression captions

✅ QUALITY BAR

Your output must be:

✔ production-ready WPF
✔ pure MVVM
✔ clinically readable
✔ visually calm
✔ theme-driven
✔ localization-safe
✔ scalable

📦 DELIVERABLES

Provide:

• full updated ExerciseWindow.xaml
• updated ExerciseWindow.xaml.cs (animations only)
• updated ExerciseDetailViewModel.cs
• added RESX keys
• updated LightTheme.xaml & DarkTheme.xaml

⚠️ FINAL REMINDER

Do NOT simplify architecture.
Do NOT bypass ViewModel.
Do NOT introduce polling.
Do NOT embed business logic in UI.

This is a medical-grade real-time biofeedback interface.