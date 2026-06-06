using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FemVoiceStudio.Subsystems.Audio;
using FemVoiceStudio.Subsystems.Analysis;
using FemVoiceStudio.Subsystems.Data;
using FemVoiceStudio.Subsystems.Progression;
using FemVoiceStudio.Subsystems.SmartCoach;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing common functionality
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Base class for ViewModels that use subsystems via DI
    /// </summary>
    public abstract class SubsystemViewModelBase : ViewModelBase
    {
        protected readonly IProgressionSubsystem ProgressionSubsystem;
        protected readonly IAudioSubsystem AudioSubsystem;
        protected readonly IAnalysisSubsystem AnalysisSubsystem;
        protected readonly ISmartCoachSubsystem SmartCoachSubsystem;
        protected readonly IDataSubsystem DataSubsystem;

        protected SubsystemViewModelBase(
            IProgressionSubsystem progressionSubsystem,
            IAudioSubsystem audioSubsystem,
            IAnalysisSubsystem analysisSubsystem,
            ISmartCoachSubsystem smartCoachSubsystem,
            IDataSubsystem dataSubsystem)
        {
            ProgressionSubsystem = progressionSubsystem ?? throw new ArgumentNullException(nameof(progressionSubsystem));
            AudioSubsystem = audioSubsystem ?? throw new ArgumentNullException(nameof(audioSubsystem));
            AnalysisSubsystem = analysisSubsystem ?? throw new ArgumentNullException(nameof(analysisSubsystem));
            SmartCoachSubsystem = smartCoachSubsystem ?? throw new ArgumentNullException(nameof(smartCoachSubsystem));
            DataSubsystem = dataSubsystem ?? throw new ArgumentNullException(nameof(dataSubsystem));

            // Subscribe to audio events
            AudioSubsystem.AudioSampleAvailable += OnAudioSampleAvailable;
        }

        protected virtual void OnAudioSampleAvailable(object? sender, AudioSampleEventArgs e)
        {
            // Override in derived classes to handle audio data
        }

        public virtual void Cleanup()
        {
            AudioSubsystem.AudioSampleAvailable -= OnAudioSampleAvailable;
        }
    }
}
