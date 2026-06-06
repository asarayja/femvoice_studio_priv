using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Data;

namespace FemVoiceStudio.Views
{
    public partial class MicrophoneCalibrationWindow : Window
    {
        private const int PhaseSeconds = 3;
        private readonly object _samplesLock = new();
        private readonly List<float> _currentSamples = new();
        private float[] _backgroundSamples = Array.Empty<float>();
        private float[] _voiceSamples = Array.Empty<float>();
        private CalibrationStep _step = CalibrationStep.NotStarted;
        private AudioCaptureService? _capture;
        private DateTime _lastLevelUpdateUtc = DateTime.MinValue;

        public MicrophoneCalibrationWindow()
        {
            InitializeComponent();
        }

        private async void OnStartCalibration(object sender, RoutedEventArgs e)
        {
            StopCapture();
            _backgroundSamples = Array.Empty<float>();
            _voiceSamples = Array.Empty<float>();
            ResultText.Text = "";
            LevelText.Text = "";
            Progress.Value = 0;
            NextButton.IsEnabled = false;
            StartButton.IsEnabled = false;

            try
            {
                _capture = new AudioCaptureService();
                _capture.ApplyInputProcessing = false;
                _capture.HearOwnVoice = GetHearOwnVoiceSetting();
                _capture.AudioDataAvailable += OnAudioDataAvailable;
                _capture.ErrorOccurred += OnAudioError;
                _capture.InitializeLowLatency();

                DeviceText.Text = string.Format(Loc.Get("MicCalibration_DeviceFormat"), _capture.InputDeviceName);
                _step = CalibrationStep.SilenceReady;
                StartButton.Content = Loc.Get("MicCalibration_Restart");

                _backgroundSamples = await CapturePhaseAsync("MicCalibration_SilenceRecording");
                if (_backgroundSamples.Length == 0)
                {
                    ResultText.Text = Loc.Get("MicCalibration_NoSamples");
                    _step = CalibrationStep.SilenceReady;
                    NextButton.Content = Loc.Get("MicCalibration_RecordSilence");
                    NextButton.IsEnabled = true;
                    return;
                }

                _step = CalibrationStep.VoiceReady;
                Progress.Value = 0;
                LevelText.Text = FormatPhaseSummary(Loc.Get("MicCalibration_NoiseLabel"), _backgroundSamples);
                InstructionText.Text = Loc.Get("MicCalibration_VoiceInstruction");
                ResultText.Text = Loc.Get("MicCalibration_SilenceCaptured");
                NextButton.Content = Loc.Get("MicCalibration_RecordVoice");
                NextButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ResultText.Text = string.Format(Loc.Get("MicCalibration_FailedFormat"), ex.Message);
                StopCapture();
            }
            finally
            {
                StartButton.IsEnabled = true;
            }
        }

        private async void OnNextCalibrationStep(object sender, RoutedEventArgs e)
        {
            if (_capture == null)
                return;

            NextButton.IsEnabled = false;
            StartButton.IsEnabled = false;

            try
            {
                if (_step == CalibrationStep.SilenceReady)
                {
                    _backgroundSamples = await CapturePhaseAsync("MicCalibration_SilenceRecording");
                    if (_backgroundSamples.Length == 0)
                    {
                        ResultText.Text = Loc.Get("MicCalibration_NoSamples");
                        _step = CalibrationStep.SilenceReady;
                        return;
                    }

                    _step = CalibrationStep.VoiceReady;
                    Progress.Value = 0;
                    LevelText.Text = FormatPhaseSummary(Loc.Get("MicCalibration_NoiseLabel"), _backgroundSamples);
                    InstructionText.Text = Loc.Get("MicCalibration_VoiceInstruction");
                    ResultText.Text = Loc.Get("MicCalibration_SilenceCaptured");
                    NextButton.Content = Loc.Get("MicCalibration_RecordVoice");
                    return;
                }

                if (_step == CalibrationStep.VoiceReady)
                {
                    _voiceSamples = await CapturePhaseAsync("MicCalibration_VoiceRecording");

                    if (_voiceSamples.Length == 0)
                    {
                        ResultText.Text = Loc.Get("MicCalibration_NoSamples");
                        _step = CalibrationStep.VoiceReady;
                        return;
                    }

                    FinishCalibration();
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = string.Format(Loc.Get("MicCalibration_FailedFormat"), ex.Message);
            }
            finally
            {
                StartButton.IsEnabled = true;
                NextButton.IsEnabled = _step is CalibrationStep.SilenceReady or CalibrationStep.VoiceReady;
            }
        }

        private void FinishCalibration()
        {
            if (_capture == null)
                return;

            var calibration = new MicrophoneCalibrationService();
            var quality = calibration.AssessCalibrationQuality(_backgroundSamples, _voiceSamples);
            if (!quality.IsUsable)
            {
                ResultText.Text = Loc.Get(GetQualityMessageKey(quality.Status))
                    + Environment.NewLine
                    + FormatQuality(quality)
                    + FormatCompatibility(quality.CompatibilityFlags);
                _step = CalibrationStep.VoiceReady;
                Progress.Value = 0;
                NextButton.Content = Loc.Get("MicCalibration_RecordVoice");
                return;
            }

            var profile = calibration.BuildAdaptiveProfile(_capture.InputDeviceName, _backgroundSamples, _voiceSamples);
            calibration.Save(profile);

            _step = CalibrationStep.Completed;
            StopCapture();
            Progress.Value = 100;
            NextButton.IsEnabled = false;
            LevelText.Text = FormatQuality(quality);
            InstructionText.Text = Loc.Get("MicCalibration_Complete");
            ResultText.Text = string.Format(
                Loc.Get("MicCalibration_SavedFormat"),
                profile.NoiseFloorRms,
                profile.SpeechRms,
                profile.NoiseGateThreshold,
                profile.VoicedRmsThreshold,
                profile.CalibrationCount,
                profile.SignalToNoiseDb,
                profile.PeakDbFs)
                + FormatCompatibility(profile.CompatibilityFlags);
        }

        private async Task<float[]> CapturePhaseAsync(string instructionKey)
        {
            lock (_samplesLock)
            {
                _currentSamples.Clear();
            }

            InstructionText.Text = Loc.Get(instructionKey);
            ResultText.Text = "";
            Progress.Value = 0;

            _capture?.StartRecording();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(PhaseSeconds))
            {
                Progress.Value = Math.Min(100, stopwatch.Elapsed.TotalSeconds / PhaseSeconds * 100);
                await Task.Delay(50);
            }

            _capture?.StopRecording();
            Progress.Value = 100;

            lock (_samplesLock)
            {
                return _currentSamples.ToArray();
            }
        }

        private void OnAudioDataAvailable(object? sender, float[] samples)
        {
            lock (_samplesLock)
            {
                _currentSamples.AddRange(samples);
            }

            var now = DateTime.UtcNow;
            if ((now - _lastLevelUpdateUtc).TotalMilliseconds < 150)
                return;

            _lastLevelUpdateUtc = now;
            var rms = MicrophoneCalibrationService.CalculateRms(samples);
            var peak = MicrophoneCalibrationService.CalculatePeak(samples);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LevelText.Text = string.Format(
                    Loc.Get("MicCalibration_LiveLevelFormat"),
                    rms,
                    MicrophoneCalibrationService.CalculateDbFs(rms),
                    MicrophoneCalibrationService.CalculateDbFs(peak));
            }));
        }

        private void OnAudioError(object? sender, string message)
        {
            Dispatcher.BeginInvoke(new Action(() => ResultText.Text = message));
        }

        private static bool GetHearOwnVoiceSetting()
        {
            try
            {
                return new DatabaseService().GetUserSettings().HearOwnVoice;
            }
            catch
            {
                return false;
            }
        }

        private void StopCapture()
        {
            if (_capture == null)
                return;

            _capture.AudioDataAvailable -= OnAudioDataAvailable;
            _capture.ErrorOccurred -= OnAudioError;
            _capture.Dispose();
            _capture = null;
        }

        private static string GetQualityMessageKey(CalibrationQualityStatus status)
            => status switch
            {
                CalibrationQualityStatus.NoSamples => "MicCalibration_NoSamples",
                CalibrationQualityStatus.TooLoud => "MicCalibration_TooLoud",
                CalibrationQualityStatus.TooQuiet => "MicCalibration_TooQuiet",
                CalibrationQualityStatus.TooCloseToNoise => "MicCalibration_TooCloseToNoise",
                _ => "MicCalibration_TooQuiet"
            };

        private static string FormatPhaseSummary(string label, float[] samples)
        {
            var rms = MicrophoneCalibrationService.CalculateRms(samples);
            return string.Format(
                "{0}: RMS {1:F4} ({2:F1} dBFS)",
                label,
                rms,
                MicrophoneCalibrationService.CalculateDbFs(rms));
        }

        private static string FormatQuality(CalibrationQualityReport quality)
            => string.Format(
                Loc.Get("MicCalibration_QualityFormat"),
                quality.NoiseFloorRms,
                quality.SpeechRms,
                quality.SignalToNoiseDb,
                quality.PeakDbFs);

        private static string FormatCompatibility(MicrophoneCompatibilityFlags flags)
        {
            if (flags == MicrophoneCompatibilityFlags.None)
                return "";

            var messages = new List<string>();
            if (flags.HasFlag(MicrophoneCompatibilityFlags.LowOutput))
                messages.Add(Loc.Get("MicCalibration_FlagLowOutput"));
            if (flags.HasFlag(MicrophoneCompatibilityFlags.HighNoiseFloor))
                messages.Add(Loc.Get("MicCalibration_FlagHighNoise"));
            if (flags.HasFlag(MicrophoneCompatibilityFlags.ClippingRisk))
                messages.Add(Loc.Get("MicCalibration_FlagClipping"));
            if (flags.HasFlag(MicrophoneCompatibilityFlags.PossibleNoiseGate))
                messages.Add(Loc.Get("MicCalibration_FlagNoiseGate"));
            if (flags.HasFlag(MicrophoneCompatibilityFlags.PossibleAgcOrCompression))
                messages.Add(Loc.Get("MicCalibration_FlagProcessing"));

            return Environment.NewLine
                + Loc.Get("MicCalibration_CompatibilityHeader")
                + Environment.NewLine
                + string.Join(Environment.NewLine, messages);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            StopCapture();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCapture();
            base.OnClosed(e);
        }

        private enum CalibrationStep
        {
            NotStarted,
            SilenceReady,
            VoiceReady,
            Completed
        }
    }
}
