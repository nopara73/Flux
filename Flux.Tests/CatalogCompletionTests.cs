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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DefaultShortWorkoutRetainsBroadTrainingWithAndWithoutLight(
        bool light,
        bool shy)
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
                (shy ? WorkoutModifiers.Shy : WorkoutModifiers.None);

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
            Assert.True(WorkoutCoveragePolicy.GetCanonicalCoverage(upperExercise, upper) >= 6);
            Assert.NotEqual(239, upperExercise.Id);
            if (light) Assert.Equal(0, upperExercise.MuscularDemand);
            foreach (WorkoutGroup round in rounds)
                service.RecordOutcome(state, round, keep: true);
            Assert.True(state.WorkoutCompleted);
        }
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
