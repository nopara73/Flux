using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutRecoveryPolicyTests
{
    [Fact]
    public void PreviousDayFilterRequiresKeepDateAndHardDemandTogether()
    {
        Exercise yesterdayHard = Exercise(1, muscularDemand: 2);
        Exercise yesterdayModerate = Exercise(2, muscularDemand: 1);
        Exercise yesterdayHardButNotKept = Exercise(3, muscularDemand: 2);
        Exercise olderHard = Exercise(4, muscularDemand: 2);
        Exercise todayHard = Exercise(5, muscularDemand: 2);
        Exercise[] exercises =
            [yesterdayHard, yesterdayModerate, yesterdayHardButNotKept, olderHard, todayHard];
        var keptExerciseIds = new HashSet<int>
        {
            yesterdayHard.Id,
            yesterdayModerate.Id,
            olderHard.Id,
            todayHard.Id,
        };
        var lastKeptDates = new Dictionary<int, string>
        {
            [yesterdayHard.Id] = "2026-08-19",
            [yesterdayModerate.Id] = "2026-08-19",
            [yesterdayHardButNotKept.Id] = "2026-08-19",
            [olderHard.Id] = "2026-08-18",
            [todayHard.Id] = "2026-08-20",
        };

        HashSet<int> excluded =
            WorkoutRecoveryPolicy.GetPreviousDayHardKeptExerciseIds(
                keptExerciseIds,
                lastKeptDates,
                exercises.ToDictionary(exercise => exercise.Id),
                new DateOnly(2026, 8, 20));

        Assert.Equal([yesterdayHard.Id], excluded);
    }

    [Theory]
    [InlineData("2024-02-29", true)]
    [InlineData("2026-02-29", false)]
    [InlineData("2026-8-20", false)]
    [InlineData("", false)]
    public void LocalDateKeysUseOneStrictCalendarFormat(
        string value,
        bool expected)
    {
        Assert.Equal(expected, WorkoutRecoveryPolicy.IsValidLocalDateKey(value));
    }

    private static Exercise Exercise(int id, int muscularDemand) =>
        new()
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_videos/exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = CanonicalMuscleGroup.GlutealExtensors,
            SecondaryCanonicalGroups = [],
            Practice = "Test",
            MotionProfile = "Test",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            InsectCompatibility = ExerciseInsectCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            MuscularDemand = muscularDemand,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = "None",
            Silent = true,
        };
}
