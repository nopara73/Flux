using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogCompletionTests
{
    private static Exercise[] LoadCatalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        })!;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void DefaultShortWorkoutRetainsBroadTrainingWithAndWithoutLight(
        bool light,
        bool shy,
        bool insect)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] exercises = LoadCatalog();
            // A liked isolated wrist movement must never occupy the broad
            // upper-body round, including when Light prioritizes mobility.
            exercises.Single(exercise => exercise.Id == 239).Score = 100;
            var service = new ExerciseSessionService(exercises, new Random(seed));
            var state = new WorkoutState();
            WorkoutModifiers profile = state.LastWorkoutModifiers |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None) |
                (shy ? WorkoutModifiers.Shy : WorkoutModifiers.None) |
                (insect ? WorkoutModifiers.Insect : WorkoutModifiers.None);

            service.StartWorkout(state, 3, profile);

            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Equal(3, rounds.Length);
            Assert.Equal(3, rounds.Select(round =>
                WorkoutModifierPolicy.GetSessionMovementId(
                    service.GetSelectedExercise(state, round))).Distinct().Count());
            foreach (WorkoutGroup round in rounds)
            {
                Exercise selected = service.GetSelectedExercise(state, round);
                Assert.True(WorkoutCoveragePolicy.IsSelectable(selected, round));
                Assert.True(WorkoutModifierPolicy.IsCompatible(selected, profile));
                Assert.Single(selected.SequenceBlocks);
            }
            WorkoutGroup upper = rounds.Single(round => round.SelectionKey == "r3.head-neck-upper-limbs");
            Exercise upperExercise = service.GetSelectedExercise(state, upper);
            Assert.True(WorkoutCoveragePolicy.IsSelectable(upperExercise, upper));
            Assert.NotEqual(239, upperExercise.Id);
            if (light && !insect) Assert.Equal(0, upperExercise.MuscularDemand);
            foreach (WorkoutGroup round in rounds)
                service.RecordOutcome(state, round, keep: true);
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Fact]
    public void CompoundWorkoutFeedbackAndHistorySurviveReload()
    {
        Exercise[] catalog = LoadCatalog();
        var service = new ExerciseSessionService(catalog, new Random(1));
        var state = new WorkoutState { CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision };
        WorkoutModifiers profile = state.LastWorkoutModifiers | WorkoutModifiers.Insect | WorkoutModifiers.Shy;
        service.StartWorkout(state, 3, profile);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup upper = rounds.Single(round => round.SelectionKey == "r3.head-neck-upper-limbs");
        Assert.Equal(248, service.GetSelectedExercise(state, upper).Id);
        foreach (WorkoutGroup round in rounds)
        {
            service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
            Assert.True(service.KeepPendingRest(state));
            service.RecordOutcome(state, round, keep: true);
            service.ClearPendingRest(state);
        }
        Assert.True(state.WorkoutCompleted);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string hardRecovery = JsonSerializer.Serialize(state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        string meaningfulRecovery = JsonSerializer.Serialize(state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
        Dictionary<int, int> scores = catalog.ToDictionary(exercise => exercise.Id, exercise => exercise.Score);
        int version = state.Version;
        int revision = state.CatalogRevision;

        WorkoutState reloaded = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        var resumed = new ExerciseSessionService(catalog, new Random(2));
        resumed.Initialize(reloaded);
        Assert.Equal(history, JsonSerializer.Serialize(reloaded.WorkoutHistory));
        Assert.Equal(keeps, JsonSerializer.Serialize(reloaded.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(hardRecovery, JsonSerializer.Serialize(reloaded.LastHardWorkUnixMillisecondsByPrimaryMuscle));
        Assert.Equal(meaningfulRecovery, JsonSerializer.Serialize(reloaded.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle));
        Assert.All(catalog, exercise => Assert.Equal(scores[exercise.Id], exercise.Score));
        Assert.Equal(version, reloaded.Version);
        Assert.Equal(revision, reloaded.CatalogRevision);
        Assert.Contains(reloaded.WorkoutHistory.Single().Decisions, decision =>
            decision.SelectionGroupId == upper.SelectionKey &&
            decision.RootExerciseId == 248 && decision.Outcome == ExerciseOutcome.Tick);

        // Done applies the recorded decisions to preferences for the next workout.
        resumed.AcknowledgeCompletion(reloaded);
        Assert.Contains(248, reloaded.KeptExerciseRootIdsBySelectionGroupId[upper.SelectionKey]);
        string acknowledgedKeeps = JsonSerializer.Serialize(reloaded.KeptExerciseRootIdsBySelectionGroupId);
        WorkoutState acknowledged = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(reloaded))!;
        new ExerciseSessionService(catalog, new Random(3)).Initialize(acknowledged);
        Assert.Equal(acknowledgedKeeps, JsonSerializer.Serialize(acknowledged.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(history, JsonSerializer.Serialize(acknowledged.WorkoutHistory));
        Assert.Equal(hardRecovery, JsonSerializer.Serialize(acknowledged.LastHardWorkUnixMillisecondsByPrimaryMuscle));
        Assert.Equal(meaningfulRecovery, JsonSerializer.Serialize(acknowledged.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle));
    }

    [Fact]
    public void PlantedTeacupAdmissionDoesNotResetExistingCatalogFeedback()
    {
        Exercise[] catalog = LoadCatalog();
        Dictionary<int, StoredExerciseSnapshot> stored = catalog
            .Where(exercise => exercise.Id != 1027)
            .ToDictionary(exercise => exercise.Id, exercise =>
                new StoredExerciseSnapshot(exercise.Name, exercise.Video,
                    Score: exercise.Id % 41 - 20));
        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog, stored);
        Assert.Equal(stored.Keys.ToHashSet(), preserved);
        Assert.DoesNotContain(1027, preserved);

        var state = new WorkoutState
        {
            CatalogRevision = 73,
            SelectedExerciseIds = new() { ["r30.rotator-cuff"] = 1026 },
            KeptExerciseRootIdsBySelectionGroupId = new()
                { ["r30.rotator-cuff"] = [1026] },
            LastKeptExerciseIds = [1026],
            LastHardWorkUnixMillisecondsByPrimaryMuscle = new()
                { [nameof(CanonicalMuscleGroup.RotatorCuff)] = 123456 },
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(
            state, catalog.ToDictionary(exercise => exercise.Id)));
        Assert.Equal(1026, state.SelectedExerciseIds["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 },
            state.KeptExerciseRootIdsBySelectionGroupId["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 }, state.LastKeptExerciseIds);
        Assert.Equal(123456,
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle[nameof(CanonicalMuscleGroup.RotatorCuff)]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }
}
