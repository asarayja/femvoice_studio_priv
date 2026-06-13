# Window Modal Behavior Checklist

Sprint G UI Addendum audit. Automated tests cover source-level behavior; visual and keyboard checks still need manual confirmation on a running app.

## Modeless Helper Windows

Name: Exercise Guide / Practice Guide  
Open path: Main window -> exercise/practice guide command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless helper window  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Calendar  
Open path: Main window -> calendar command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless helper window  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Day Details  
Open path: Calendar -> day with sessions  
Current behavior: Modeless via `DayDetailsWindow.Show()`  
Expected behavior: Modeless detail window  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented per calendar view model instance  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Yes via owner  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Statistics  
Open path: Main window -> statistics command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless helper window  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Analyzer  
Open path: Main window -> analyzer command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless tool window  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: SmartCoach  
Open path: Main window -> SmartCoach command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless helper window matching previous SmartCoach intent  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Resonance, Progression, Analysis  
Open path: Main window -> corresponding navigation commands  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless analysis/helper windows  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Settings / Innstillinger  
Open path: Main window -> settings command  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless settings window; destructive choices remain modal confirmations  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, layout updated, needs manual visual confirmation

Name: Microphone Calibration  
Open path: Settings -> microphone calibration  
Current behavior: Modeless via `MicrophoneCalibrationWindow.Show()`  
Expected behavior: Modeless helper window owned by Settings  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented per settings window instance  
Focus existing window: Implemented  
Owner set: SettingsWindow  
Closes with main app: Yes through owned Settings/MainWindow chain  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

Name: Clinician Dashboard, Coach Dashboard, Report Export, Manual Adjustments, Case Review  
Open path: Main window -> professional/research commands  
Current behavior: Modeless via `ShowOrActivateModelessWindow`  
Expected behavior: Modeless professional/helper windows  
Modal/modeless: Modeless  
Can main app still be used: Yes, needs manual confirmation  
Can user navigate elsewhere: Yes, needs manual confirmation  
Duplicate prevention: Implemented  
Focus existing window: Implemented  
Owner set: MainWindow  
Closes with main app: Implemented  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified, needs manual visual confirmation

## Intentionally Modal

Name: First-time setup / privacy consent  
Open path: App startup before MainWindow  
Current behavior: `FirstTimeSetupWindow.ShowDialog()`  
Expected behavior: Modal before app use  
Modal/modeless: Modal  
Can main app still be used: Main app not created yet  
Can user navigate elsewhere: N/A  
Duplicate prevention: N/A  
Focus existing window: N/A  
Owner set: Startup flow  
Closes with main app: N/A  
Keyboard accessible: Needs manual confirmation  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: Required onboarding/privacy choice before normal app use  
Status: Intentionally modal, needs manual visual confirmation

Name: Reset database confirmation  
Open path: Settings -> clear database  
Current behavior: `MessageBox.Show` confirmation  
Expected behavior: Modal destructive confirmation  
Modal/modeless: Modal  
Can main app still be used: No while confirming  
Can user navigate elsewhere: No while confirming  
Duplicate prevention: N/A  
Focus existing window: Platform dialog  
Owner set: Platform modal behavior  
Closes with main app: Platform dialog  
Keyboard accessible: Platform dialog, needs manual confirmation  
Dark/light readability: Platform dialog, needs manual confirmation  
Keep modal reason if modal: Destructive database reset must require an explicit decision  
Status: Intentionally modal

Name: Restore backup file picker and overwrite confirmation  
Open path: Settings -> restore backup  
Current behavior: `OpenFileDialog.ShowDialog(this)` plus `MessageBox.Show` overwrite confirmation  
Expected behavior: Modal file picker and destructive overwrite confirmation  
Modal/modeless: Modal  
Can main app still be used: No while selecting/confirming  
Can user navigate elsewhere: No while selecting/confirming  
Duplicate prevention: N/A  
Focus existing window: Platform dialog  
Owner set: SettingsWindow for file picker  
Closes with main app: Platform dialog  
Keyboard accessible: Platform dialog, needs manual confirmation  
Dark/light readability: Platform dialog, needs manual confirmation  
Keep modal reason if modal: File picker and overwrite confirmation require a decision  
Status: Intentionally modal

Name: Report export save picker / research export output picker  
Open path: Report Export -> export command  
Current behavior: `SaveFileDialog.ShowDialog()` in `ReportExportViewModel`  
Expected behavior: Modal platform save picker  
Modal/modeless: Modal  
Can main app still be used: No while choosing output path  
Can user navigate elsewhere: No while choosing output path  
Duplicate prevention: N/A  
Focus existing window: Platform dialog  
Owner set: Platform dialog  
Closes with main app: Platform dialog  
Keyboard accessible: Platform dialog, needs manual confirmation  
Dark/light readability: Platform dialog, needs manual confirmation  
Keep modal reason if modal: Save/export path must be selected before writing output  
Status: Intentionally modal

Name: Error, success, and safety MessageBox dialogs  
Open path: Analyzer, Exercise, Resonance, Settings, SmartCoach, Main navigation error paths  
Current behavior: `MessageBox.Show`  
Expected behavior: Modal acknowledgement  
Modal/modeless: Modal  
Can main app still be used: No while acknowledging  
Can user navigate elsewhere: No while acknowledging  
Duplicate prevention: N/A  
Focus existing window: Platform dialog  
Owner set: Platform modal behavior  
Closes with main app: Platform dialog  
Keyboard accessible: Platform dialog, needs manual confirmation  
Dark/light readability: Platform dialog, needs manual confirmation  
Keep modal reason if modal: Error/safety acknowledgement and operation result feedback  
Status: Intentionally modal

Name: Splash screen  
Open path: App startup  
Current behavior: `Show()` and closes after MainWindow startup  
Expected behavior: Non-interactive startup splash  
Modal/modeless: Modeless/non-interactive  
Can main app still be used: N/A during startup  
Can user navigate elsewhere: N/A  
Duplicate prevention: Startup singleton field  
Focus existing window: N/A  
Owner set: N/A  
Closes with main app: Closed after startup  
Keyboard accessible: N/A  
Dark/light readability: Needs manual confirmation  
Keep modal reason if modal: N/A  
Status: Source verified
