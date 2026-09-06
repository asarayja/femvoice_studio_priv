using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The "Start here" path must be a coherent, sequential curriculum over REAL catalog ids: every id exists, stages
    /// advance only when complete, the next exercise is the first not-done one in path order, and the path finishes.
    /// </summary>
    public class BeginnerPathTests
    {
        private static Dictionary<int, int> Counts(params (int id, int n)[] items)
            => items.ToDictionary(i => i.id, i => i.n);

        [Fact]
        public void EveryPathExerciseExistsInTheCatalog_AndIsUnique()
        {
            var catalogIds = new VoiceFeminizationExerciseService().GetAllEnhancedExercises().Select(e => e.Id).ToHashSet();
            var pathIds = BeginnerPath.AllExerciseIds.ToList();
            Assert.All(pathIds, id => Assert.Contains(id, catalogIds));
            Assert.Equal(pathIds.Count, pathIds.Distinct().Count());
            Assert.Equal(3, BeginnerPath.Stages.Count);
        }

        [Fact]
        public void FreshUser_StartsAtStage1_FirstExercise()
        {
            var s = BeginnerPath.Evaluate(new Dictionary<int, int>());
            Assert.False(s.IsComplete);
            Assert.Equal(1, s.StageNumber);
            Assert.Equal("fundamentals", s.StageKey);
            Assert.Equal(BeginnerPath.Stages[0].ExerciseIds[0], s.NextExerciseId);
            Assert.Equal(0, s.StageDone);
            Assert.Equal(0, s.PercentOverall);
        }

        [Fact]
        public void OneCompletion_IsNotDone_UntilRequiredCount()
        {
            int first = BeginnerPath.Stages[0].ExerciseIds[0];
            var notYet = BeginnerPath.Evaluate(Counts((first, BeginnerPath.RequiredPerExercise - 1)));
            Assert.Equal(first, notYet.NextExerciseId);   // still the same next exercise
            var done = BeginnerPath.Evaluate(Counts((first, BeginnerPath.RequiredPerExercise)));
            Assert.Equal(BeginnerPath.Stages[0].ExerciseIds[1], done.NextExerciseId);
            Assert.Equal(1, done.StageDone);
        }

        [Fact]
        public void NextExercise_IsFirstNotDone_InPathOrder_EvenIfLaterOnesAreDone()
        {
            // Done the 2nd and 3rd of stage 1 but not the 1st → next is still the 1st.
            var ids = BeginnerPath.Stages[0].ExerciseIds;
            var s = BeginnerPath.Evaluate(Counts((ids[1], 5), (ids[2], 5)));
            Assert.Equal(ids[0], s.NextExerciseId);
            Assert.Equal(2, s.StageDone);
        }

        [Fact]
        public void CompletingStage1_AdvancesToStage2()
        {
            var counts = BeginnerPath.Stages[0].ExerciseIds.ToDictionary(id => id, _ => BeginnerPath.RequiredPerExercise);
            var s = BeginnerPath.Evaluate(counts);
            Assert.Equal(2, s.StageNumber);
            Assert.Equal("resonance", s.StageKey);
            Assert.Equal(BeginnerPath.Stages[1].ExerciseIds[0], s.NextExerciseId);
            Assert.Equal(0, s.StageDone);
        }

        [Fact]
        public void AllDone_IsComplete_With100Percent_AndNoNext()
        {
            var counts = BeginnerPath.AllExerciseIds.ToDictionary(id => id, _ => 10);
            var s = BeginnerPath.Evaluate(counts);
            Assert.True(s.IsComplete);
            Assert.Equal(0, s.NextExerciseId);
            Assert.Equal(100, s.PercentOverall);
            Assert.Equal(s.OverallTotal, s.OverallDone);
        }

        [Fact]
        public void Percent_IsMonotonic_AsExercisesComplete()
        {
            var counts = new Dictionary<int, int>();
            int prev = -1;
            foreach (var id in BeginnerPath.AllExerciseIds)
            {
                counts[id] = BeginnerPath.RequiredPerExercise;
                int p = BeginnerPath.Evaluate(counts).PercentOverall;
                Assert.True(p > prev, $"percent did not rise after completing {id}");
                prev = p;
            }
            Assert.Equal(100, prev);
        }
    }
}
