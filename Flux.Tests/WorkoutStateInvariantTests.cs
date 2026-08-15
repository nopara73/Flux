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

        Assert.Equal(7, state.Version);
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
            CanonicalMuscleGroup.ScapularGirdle,
            score: 11);
        Exercise replacement = Exercise(
            2,
            CanonicalMuscleGroup.ShoulderAdductorsAndExtensors,
            score: 10);
        Exercise torso = Exercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise lower = Exercise(4, CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        var service = new ExerciseSessionService(
            [rejected, replacement, torso, lower],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        WorkoutGroup target = MassGroupingTaxonomy.GetGroup(
            3,
            rejected.PrimaryCanonicalGroup);
        WorkoutGroup untouched = groups.First(group => group.Id != target.Id);
        int untouchedCurrentExerciseId = state.SelectedExerciseIds[untouched.Id];

        Exercise recorded = service.RecordOutcome(state, target, keep: false);
        Exercise? unexpectedPenalty = service.FinishInterruptedWorkout(state);

        Assert.Same(rejected, recorded);
        Assert.Null(unexpectedPenalty);
        Assert.Equal(10, rejected.Score);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[target.Id]);
        Assert.Equal(untouchedCurrentExerciseId, state.SelectedExerciseIds[untouched.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
        Assert.False(state.WorkoutCompleted);
    }

    [Fact]
    public void RejectionPurgesExerciseFromEverySavedResolutionBucket()
    {
        Exercise rejected = Exercise(
            1,
            CanonicalMuscleGroup.ScapularGirdle,
            score: 11);
        Exercise replacement = Exercise(
            2,
            CanonicalMuscleGroup.ShoulderAdductorsAndExtensors,
            score: 10);
        Exercise torso = Exercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise lower = Exercise(4, CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        Exercise unrelated = Exercise(5, CanonicalMuscleGroup.CranialMuscles);
        var service = new ExerciseSessionService(
            [rejected, replacement, torso, lower, unrelated],
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
        WorkoutGroup target = MassGroupingTaxonomy.GetGroup(
            3,
            rejected.PrimaryCanonicalGroup);
        service.RecordOutcome(state, target, keep: false);
        foreach (WorkoutGroup group in groups.Where(group => group.Id != target.Id))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.FinishInterruptedWorkout(state);

        Assert.DoesNotContain(rejected.Id, state.SelectedExerciseIds.Values);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[target.Id]);
        Assert.Equal(unrelated.Id, state.SelectedExerciseIds[unrelatedGroupId]);
        Assert.All(
            rejectedGroupIds.Where(groupId => groupId != target.Id),
            groupId => Assert.False(state.SelectedExerciseIds.ContainsKey(groupId)));
    }

    [Fact]
    public void CatalogUpgradePreservesPresentKeepMarkersAndDropsMissingExercises()
    {
        Exercise present = Exercise(
            223,
            CanonicalMuscleGroup.ScapularGirdle);
        Exercise torso = Exercise(
            1001,
            CanonicalMuscleGroup.SpinalExtensors);
        Exercise lower = Exercise(
            1002,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        string savedGroupId = MassGroupingTaxonomy.GetGroup(
            3,
            present.PrimaryCanonicalGroup).Id;
        var priorState = new WorkoutState
        {
            CatalogRevision = 12,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [savedGroupId] = present.Id,
            },
            LastKeptExerciseIds = [present.Id, 999999],
        };

        string serialized = JsonSerializer.Serialize(priorState, JsonOptions);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(
                serialized,
                JsonOptions)
            ?? throw new InvalidOperationException("Workout state did not deserialize.");
        var service = new ExerciseSessionService(
            [present, torso, lower],
            new Random(1));

        service.Initialize(restored);

        Assert.Equal([present.Id], restored.LastKeptExerciseIds);
        Assert.DoesNotContain(savedGroupId, restored.SelectedExerciseIds);
        Assert.Equal(
            CatalogMigrationRules.CurrentCatalogRevision,
            restored.CatalogRevision);

        service.StartWorkout(restored, 3);

        Assert.Equal(present.Id, restored.SelectedExerciseIds[savedGroupId]);
    }

    [Fact]
    public void MissingModifierFieldsDefaultToOff()
    {
        const string json =
            """
            {
              "version": 7,
              "lastWorkoutMinutes": 10,
              "activeWorkoutMinutes": 0
            }
            """;

        WorkoutState state = JsonSerializer.Deserialize<WorkoutState>(json, JsonOptions)
            ?? throw new InvalidOperationException("Workout state did not deserialize.");

        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.ActiveWorkoutModifiers);
    }

    [Fact]
    public void ModifierPreferencePersistsWhileActiveSnapshotClearsWithWorkout()
    {
        Exercise[] exercises =
        [
            Exercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Exercise(2, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(3, CanonicalMuscleGroup.ScapularGirdle),
        ];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.Insect);
        string json = JsonSerializer.Serialize(state, JsonOptions);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(json, JsonOptions)
            ?? throw new InvalidOperationException("Workout state did not deserialize.");

        Assert.Equal(WorkoutModifiers.Insect, restored.LastWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.Insect, restored.ActiveWorkoutModifiers);

        service.FinishInterruptedWorkout(restored);

        Assert.Equal(WorkoutModifiers.Insect, restored.LastWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, restored.ActiveWorkoutModifiers);
        Assert.Equal(0, restored.ActiveWorkoutMinutes);
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
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
