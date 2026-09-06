using System;
using System.Collections.Generic;
using System.Linq;

namespace FemVoiceStudio.Services
{
    /// <summary>One stage of the "Start here" path: an ordered set of catalog exercise ids to work through.</summary>
    public sealed record BeginnerPathStage(int Number, string Key, IReadOnlyList<int> ExerciseIds);

    /// <summary>Where the user is on the path right now.</summary>
    public readonly record struct BeginnerPathStatus(
        bool IsComplete,
        int StageNumber,          // 1-based; when complete, the last stage
        string StageKey,          // stable token for localization ("fundamentals" / "resonance" / "integration")
        int NextExerciseId,       // 0 when complete
        int StageDone,            // exercises done in the current stage
        int StageTotal,
        int OverallDone,
        int OverallTotal,
        int PercentOverall);

    /// <summary>
    /// A STATIC, SEQUENTIAL "Start here" curriculum for someone beginning voice feminization — the answer to
    /// "what do I practise today, in what order?". Three stages built from the existing 17-exercise catalog
    /// (<see cref="VoiceFeminizationExerciseService"/>), each exercise counted "done" once it has been completed
    /// <see cref="RequiredPerExercise"/> times. Pure and deterministic (no I/O), so it is fully unit-testable.
    ///
    /// This deliberately COMPLEMENTS SmartCoach rather than duplicating it: SmartCoach is adaptive and daily (one
    /// health-gated recommendation per day from recent sessions); this path is a fixed beginner sequence that the
    /// dashboard shows as "Steg X av 3 · Neste: …" until it is complete, after which the card steps aside.
    ///
    /// Stage rationale (order matters — foundations before resonance before integration):
    ///   1 fundamentals — breath/SOVT + gentle forward-resonance basics: 14 straw phonation, 1 humming, 2 vowels.
    ///   2 resonance    — the lever that most changes perceived gender: 17 big/small dog contrast, 11 forward
    ///                    resonance shift, 12 starter-pitch memorisation, 3 glide up.
    ///   3 integration  — carrying it into speech: 5 consistency, 7 question melody, 8 statement melody, 9 phrases.
    /// </summary>
    public static class BeginnerPath
    {
        /// <summary>Completed sessions of an exercise needed before the path counts it as done.</summary>
        public const int RequiredPerExercise = 2;

        public static readonly IReadOnlyList<BeginnerPathStage> Stages = new[]
        {
            new BeginnerPathStage(1, "fundamentals", new[] { 14, 1, 2 }),
            new BeginnerPathStage(2, "resonance",    new[] { 17, 11, 12, 3 }),
            new BeginnerPathStage(3, "integration",  new[] { 5, 7, 8, 9 }),
        };

        /// <summary>All exercise ids on the path, in path order.</summary>
        public static IEnumerable<int> AllExerciseIds => Stages.SelectMany(s => s.ExerciseIds);

        /// <summary>
        /// Evaluate the user's position from per-exercise completed-session counts (keyed by catalog exercise id;
        /// missing ids count as 0). The current stage is the first stage with an exercise not yet done; the next
        /// exercise is the first not-done exercise in that stage, in path order.
        /// </summary>
        public static BeginnerPathStatus Evaluate(IReadOnlyDictionary<int, int> completedCounts)
        {
            var counts = completedCounts ?? new Dictionary<int, int>();
            bool Done(int id) => counts.TryGetValue(id, out var n) && n >= RequiredPerExercise;

            int overallTotal = Stages.Sum(s => s.ExerciseIds.Count);
            int overallDone = Stages.Sum(s => s.ExerciseIds.Count(Done));
            int percent = overallTotal == 0 ? 100 : (int)Math.Round(100.0 * overallDone / overallTotal);

            foreach (var stage in Stages)
            {
                int stageDone = stage.ExerciseIds.Count(Done);
                if (stageDone == stage.ExerciseIds.Count) continue;   // stage complete → move on
                int next = stage.ExerciseIds.First(id => !Done(id));
                return new BeginnerPathStatus(false, stage.Number, stage.Key, next,
                    stageDone, stage.ExerciseIds.Count, overallDone, overallTotal, percent);
            }

            var last = Stages[Stages.Count - 1];
            return new BeginnerPathStatus(true, last.Number, last.Key, 0,
                last.ExerciseIds.Count, last.ExerciseIds.Count, overallDone, overallTotal, 100);
        }
    }
}
