using System.Windows;
using System.Runtime.CompilerServices;

// Restored during the Linux portable-core split: this grant previously lived in AudioCaptureService.cs,
// which moved to the FemVoice.Audio.Windows assembly. The Windows test project (FemVoiceStudio.Tests)
// accesses internal members of THIS WPF assembly (e.g. SmartCoachViewModel.BuildRecommendedExerciseHint),
// so the InternalsVisibleTo grant must originate from this assembly.
[assembly: InternalsVisibleTo("FemVoiceStudio.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
