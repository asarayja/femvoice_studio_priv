using System;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Voice Activity Detection basert på energi og spektral egenskaper
    /// </summary>
    public class VoiceActivityDetector
    {
        private readonly int _sampleRate;
        private readonly RollingStatistics _energyHistory;
        
        private double _energyThreshold;
        private const double SpectralCentroidThreshold = 2000;
        private const int MinSpeechFrames = 3;
        private const int MinSilenceFrames = 10;
        
        private int _consecutiveSpeechFrames;
        private int _consecutiveSilenceFrames;
        private bool _wasSpeaking;
        
        public bool IsSpeaking { get; private set; }
        public double CurrentEnergy { get; private set; }
        
        public VoiceActivityDetector(int sampleRate = 44100)
        {
            _sampleRate = sampleRate;
            _energyHistory = new RollingStatistics(50);
            _energyThreshold = 0.02;
        }
        
        public void Calibrate(float[] noiseSamples)
        {
            double noiseEnergy = CalculateEnergy(noiseSamples);
            _energyHistory.Clear();
            for (int i = 0; i < 30; i++)
                _energyHistory.Add(noiseEnergy);
            
            _energyThreshold = _energyHistory.Mean * 2.5;
        }
        
        public bool Detect(float[] samples)
        {
            CurrentEnergy = CalculateEnergy(samples);
            _energyHistory.Add(CurrentEnergy);
            
            bool energyBased = CurrentEnergy > _energyThreshold;
            
            if (energyBased)
            {
                _consecutiveSpeechFrames++;
                _consecutiveSilenceFrames = 0;
                
                if (_consecutiveSpeechFrames >= MinSpeechFrames)
                {
                    _wasSpeaking = IsSpeaking;
                    IsSpeaking = true;
                }
            }
            else
            {
                _consecutiveSilenceFrames++;
                _consecutiveSpeechFrames = 0;
                
                if (_consecutiveSilenceFrames >= MinSilenceFrames)
                {
                    _wasSpeaking = IsSpeaking;
                    IsSpeaking = false;
                }
            }
            
            return IsSpeaking;
        }
        
        private static double CalculateEnergy(float[] samples)
        {
            if (samples.Length == 0) return 0;
            double sum = 0;
            foreach (var s in samples) sum += s * s;
            return Math.Sqrt(sum / samples.Length);
        }
        
        public void SetSensitivity(double sensitivity)
        {
            _energyThreshold = _energyHistory.Mean * (1.5 + sensitivity * 2.0);
        }
        
        public void Reset()
        {
            _consecutiveSpeechFrames = 0;
            _consecutiveSilenceFrames = 0;
            IsSpeaking = false;
            _wasSpeaking = false;
        }
    }
}
