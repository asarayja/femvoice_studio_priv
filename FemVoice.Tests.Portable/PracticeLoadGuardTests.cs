using System;
using FemVoiceStudio.Services.VoiceLoad;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The rest guidance must be driven by the clock, never by guesswork: no advice early, "soon" after a real stretch
    /// of practice, "now" after a long one, and a real rest must count. Simulated time only (no sleeping).
    /// </summary>
    public class PracticeLoadGuardTests
    {
        private const double TickSeconds = 1024.0 / 44100.0;   // one capture frame

        /// <summary>Practise for <paramref name="minutes"/> at the given voiced fraction; returns the last advice and
        /// reports the first minute at which each level appeared.</summary>
        private static RestAdvice Practise(PracticeLoadGuard guard, ref DateTime now, double minutes, double voicedFraction,
            bool aboveTarget, out double? firstSoonMinute, out double? firstNowMinute)
        {
            firstSoonMinute = firstNowMinute = null;
            var start = now;
            guard.BeginSession(now);
            RestAdvice advice = default;
            int ticks = (int)(minutes * 60 / TickSeconds);
            for (int i = 0; i < ticks; i++)
            {
                now = now.AddSeconds(TickSeconds);
                // Phrase-like pattern: voiced for the first part of every 5-second window.
                bool voiced = ((now - start).TotalSeconds % 5.0) < 5.0 * voicedFraction;
                advice = guard.Observe(now, (int)(now - start).TotalSeconds, voiced ? 185 : 0, voiced ? 0.08 : 0, voiced && aboveTarget);
                double minute = (now - start).TotalMinutes;
                if (advice.Level >= RestAdviceLevel.Soon) firstSoonMinute ??= minute;
                if (advice.Level == RestAdviceLevel.Now) firstNowMinute ??= minute;
            }
            return advice;
        }

        [Fact]
        public void ShortPractice_GivesNoAdvice()
        {
            var guard = new PracticeLoadGuard();
            var now = new DateTime(2026, 1, 5, 18, 0, 0);
            var advice = Practise(guard, ref now, minutes: 5, voicedFraction: 0.6, aboveTarget: false, out var soon, out var nowAt);
            Assert.Equal(RestAdviceLevel.None, advice.Level);
            Assert.Null(soon);
            Assert.Null(nowAt);
        }

        [Fact]
        public void ContinuousPractice_SuggestsRestSoon_ThenNow_InThatOrder()
        {
            var guard = new PracticeLoadGuard();
            var now = new DateTime(2026, 1, 5, 18, 0, 0);
            var advice = Practise(guard, ref now, minutes: 40, voicedFraction: 0.6, aboveTarget: false, out var soon, out var nowAt);

            Assert.NotNull(soon);
            Assert.InRange(soon!.Value, 8.0, 20.0);          // "a while", not after two minutes and not after an hour
            Assert.NotNull(nowAt);
            Assert.True(nowAt!.Value > soon.Value);           // escalates, never jumps straight to "now"
            Assert.InRange(nowAt.Value, 20.0, 35.0);          // ≈15 min of actual voicing at a 60 % voiced fraction
            Assert.Equal(RestAdviceLevel.Now, advice.Level);  // and it stays until the user actually rests
        }

        [Fact]
        public void GoingAboveTheTargetZone_BringsTheAdviceEarlier()
        {
            var a = new PracticeLoadGuard();
            var b = new PracticeLoadGuard();
            var t1 = new DateTime(2026, 1, 5, 18, 0, 0);
            var t2 = t1;
            Practise(a, ref t1, 25, 0.6, aboveTarget: false, out var soonInZone, out _);
            Practise(b, ref t2, 25, 0.6, aboveTarget: true, out var soonAbove, out _);
            Assert.NotNull(soonInZone);
            Assert.NotNull(soonAbove);
            Assert.True(soonAbove!.Value < soonInZone!.Value);
        }

        [Fact]
        public void ARealRest_Counts_ButBackToBackExercisesDoNot()
        {
            var now = new DateTime(2026, 1, 5, 18, 0, 0);

            // Three 6-minute exercises with only 20-second gaps: the sitting accumulates → advice appears, although
            // no single exercise is long enough to trigger it on its own.
            var busy = new PracticeLoadGuard();
            RestAdvice last = default;
            for (int i = 0; i < 3; i++)
            {
                last = Practise(busy, ref now, 6, 0.6, false, out _, out _);
                now = now.AddSeconds(20);
            }
            Assert.True(last.Level >= RestAdviceLevel.Soon);

            // The same three exercises with a real rest in between: "time since rest" starts over each time.
            var rested = new PracticeLoadGuard();
            var t = new DateTime(2026, 1, 5, 18, 0, 0);
            RestAdvice lastRested = default;
            for (int i = 0; i < 3; i++)
            {
                lastRested = Practise(rested, ref t, 6, 0.6, false, out _, out _);
                t = t.Add(PracticeLoadGuard.RestGap).AddSeconds(30);
            }
            Assert.Equal(RestAdviceLevel.None, lastRested.Level);
            Assert.True(lastRested.SecondsSinceRest < 6.5 * 60);
        }

        [Fact]
        public void ALongBreak_StartsAFreshSitting()
        {
            var guard = new PracticeLoadGuard();
            var now = new DateTime(2026, 1, 5, 18, 0, 0);
            var tired = Practise(guard, ref now, 40, 0.6, false, out _, out _);
            Assert.Equal(RestAdviceLevel.Now, tired.Level);

            now = now.Add(PracticeLoadGuard.NewSittingGap).AddMinutes(1);
            var fresh = Practise(guard, ref now, 3, 0.6, false, out _, out _);
            Assert.Equal(RestAdviceLevel.None, fresh.Level);
            Assert.True(fresh.VoicedSeconds < 3 * 60);        // the earlier 40 minutes are gone
        }

        [Fact]
        public void Silence_NeverProducesAdvice()
        {
            var guard = new PracticeLoadGuard();
            var now = new DateTime(2026, 1, 5, 18, 0, 0);
            var advice = Practise(guard, ref now, 30, voicedFraction: 0.0, aboveTarget: false, out var soon, out _);
            Assert.Equal(RestAdviceLevel.None, advice.Level);   // an open microphone is not practice
            Assert.Null(soon);
            Assert.Equal(0, advice.VoicedSeconds, 3);
        }
    }
}
