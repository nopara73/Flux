using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutStateInvariantTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

    [Fact]
    public void SerializedVersionFourUnsupportedDurationMigratesToNearestResolution()
    {
        const string json =
            """
            {
              "version": 4,
              "lastWorkoutMinutes": 6,
              "activeWorkoutMinutes": 0
            }
            """;
        LegacyWorkoutState legacy = JsonSerializer.Deserialize<LegacyWorkoutState>(
                json,
                JsonOptions)
            ?? throw new InvalidOperationException("Legacy state did not deserialize.");
        WorkoutState state = LegacyWorkoutStateMigration.Migrate(legacy);
        var service = new ExerciseSessionService([], new Random(1));

        service.Initialize(state);

        Assert.Equal(5, state.Version);
        Assert.Equal(7, state.LastWorkoutMinutes);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InvalidActiveWorkoutResetsTransientStateButPreservesSavedHistory()
    {
        Exercise selected = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        var service = new ExerciseSessionService([selected], new Random(1));
        string savedGroupId = MassGroupingTaxonomy.GetGroup(
            3,
            selected.PrimaryCanonicalGroup).Id;
        var state = new WorkoutState
        {
            Version = 5,
            LastWorkoutMinutes = 4,
            ActiveWorkoutMinutes = 4,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [savedGroupId] = selected.Id,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [savedGroupId] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = savedGroupId,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = selected.Id,
            PendingScoreValue = -3,
            WorkoutCompleted = true,
            CompletionAcknowledged = true,
        };

        service.Initialize(state);

        Assert.Equal(5, state.LastWorkoutMinutes);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Equal(selected.Id, state.SelectedExerciseIds[savedGroupId]);
        Assert.Equal(selected.Id, state.PendingScoreExerciseId);
        Assert.Equal(-3, state.PendingScoreValue);
        Assert.Empty(state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.False(state.WorkoutCompleted);
        Assert.False(state.CompletionAcknowledged);
    }

    [Fact]
    public void AbruptCloseDuringExerciseKeepsRecordedDecisionsWithoutPenalizingCurrentRound()
    {
        Exercise rejected = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            score: 11);
        Exercise replacement = Exercise(
            2,
            CanonicalMuscleGroup.LateralKneeExtensors,
            score: 10);
        Exercise torso = Exercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = Exercise(4, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [rejected, replacement, torso, upper],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int untouchedCurrentExerciseId = state.SelectedExerciseIds[groups[1].Id];

        Exercise recorded = service.RecordOutcome(state, groups[0], keep: false);
        Exercise? unexpectedPenalty = service.FinishInterruptedWorkout(state);

        Assert.Same(rejected, recorded);
        Assert.Null(unexpectedPenalty);
        Assert.Equal(10, rejected.Score);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(untouchedCurrentExerciseId, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
        Assert.False(state.WorkoutCompleted);
    }

    [Fact]
    public void RejectionPurgesExerciseFromEverySavedResolutionBucket()
    {
        Exercise rejected = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            score: 11);
        Exercise replacement = Exercise(
            2,
            CanonicalMuscleGroup.LateralKneeExtensors,
            score: 10);
        Exercise torso = Exercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = Exercise(4, CanonicalMuscleGroup.ScapularGirdle);
        Exercise unrelated = Exercise(5, CanonicalMuscleGroup.CranialMuscles);
        var service = new ExerciseSessionService(
            [rejected, replacement, torso, upper, unrelated],
            new Random(1));
        var state = new WorkoutState();
        string[] rejectedGroupIds = MassGroupingTaxonomy.SupportedMinutes
            .Select(minutes => MassGroupingTaxonomy.GetGroup(
                minutes,
                rejected.PrimaryCanonicalGroup).Id)
            .ToArray();
        foreach (string groupId in rejectedGroupIds)
        {
            state.SelectedExerciseIds[groupId] = rejected.Id;
        }

        string unrelatedGroupId = MassGroupingTaxonomy.GetGroup(
            30,
            unrelated.PrimaryCanonicalGroup).Id;
        state.SelectedExerciseIds[unrelatedGroupId] = unrelated.Id;

        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        service.RecordOutcome(state, groups[0], keep: false);
        service.RecordOutcome(state, groups[1], keep: true);
        service.RecordOutcome(state, groups[2], keep: true);
        service.FinishInterruptedWorkout(state);

        Assert.DoesNotContain(rejected.Id, state.SelectedExerciseIds.Values);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(unrelated.Id, state.SelectedExerciseIds[unrelatedGroupId]);
        Assert.All(
            rejectedGroupIds.Where(groupId => groupId != groups[0].Id),
            groupId => Assert.False(state.SelectedExerciseIds.ContainsKey(groupId)));
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = Enum.GetValues<CanonicalMuscleGroup>()
                .Where(group => group != primary)
                .ToArray(),
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            HoldFramePercent = 0,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
