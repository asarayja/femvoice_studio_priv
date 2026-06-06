using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ExerciseSessionTimerStateTests
    {
        [Fact]
        public void Tick_WhenRunning_IncrementsLocalTimerWithoutViewModelFeedback()
        {
            var timer = new ExerciseSessionTimerState();

            var first = timer.Tick(isRunning: true, viewModelElapsedSeconds: 0);
            var second = timer.Tick(isRunning: true, viewModelElapsedSeconds: 0);

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.Equal(2, timer.LocalElapsedSeconds);
        }

        [Fact]
        public void Tick_UsesViewModelElapsedSeconds_WhenViewModelIsAhead()
        {
            var timer = new ExerciseSessionTimerState();

            timer.Tick(isRunning: true, viewModelElapsedSeconds: 0);
            var displaySeconds = timer.Tick(isRunning: true, viewModelElapsedSeconds: 8);

            Assert.Equal(8, displaySeconds);
            Assert.Equal(2, timer.LocalElapsedSeconds);
        }

        [Fact]
        public void Tick_WhenNotRunning_DoesNotIncrementLocalTimer()
        {
            var timer = new ExerciseSessionTimerState();

            var displaySeconds = timer.Tick(isRunning: false, viewModelElapsedSeconds: 0);

            Assert.Equal(0, displaySeconds);
            Assert.Equal(0, timer.LocalElapsedSeconds);
        }

        [Fact]
        public void Reset_ClearsLocalTimerBetweenExerciseSessions()
        {
            var timer = new ExerciseSessionTimerState();
            timer.Tick(isRunning: true, viewModelElapsedSeconds: 0);
            timer.Tick(isRunning: true, viewModelElapsedSeconds: 0);

            timer.Reset();

            Assert.Equal(0, timer.LocalElapsedSeconds);
            Assert.Equal(0, timer.GetDisplaySeconds(viewModelElapsedSeconds: 0));
        }
    }
}
