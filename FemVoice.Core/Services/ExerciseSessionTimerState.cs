using System;

namespace FemVoiceStudio.Services
{
    public sealed class ExerciseSessionTimerState
    {
        public int LocalElapsedSeconds { get; private set; }

        public int Tick(bool isRunning, int viewModelElapsedSeconds)
        {
            if (isRunning)
                LocalElapsedSeconds++;

            return GetDisplaySeconds(viewModelElapsedSeconds);
        }

        public int GetDisplaySeconds(int viewModelElapsedSeconds)
            => Math.Max(LocalElapsedSeconds, Math.Max(0, viewModelElapsedSeconds));

        public void Reset()
            => LocalElapsedSeconds = 0;
    }
}
