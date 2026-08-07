using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogInvariantTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void BundledCatalogHasStableCanonicalAssignmentsAndRequiredCoverage()
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        string json = File.ReadAllText(catalogPath);
        using JsonDocument document = JsonDocument.Parse(json);
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");

        Assert.DoesNotContain(document.RootElement.EnumerateArray(), element =>
            element.TryGetProperty("muscleGroups", out _));
        Assert.True(exercises.Length >= 300);
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Id).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Name).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Video).Distinct().Count());
        Exercise[] timedSideExercises = exercises
            .Where(exercise =>
                exercise.SideSequence != ExerciseSideSequence.Continuous)
            .ToArray();
        Assert.Equal(107, timedSideExercises.Length);
        Assert.DoesNotContain(timedSideExercises, exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Assert.Equal(8, timedDirectionExercises.Length);
        Assert.All(timedDirectionExercises, exercise =>
        {
            Assert.Equal(ExerciseSideSequence.Continuous, exercise.SideSequence);
            Assert.Equal(ExerciseMode.Repetition, exercise.Mode);
            Assert.Equal(ExercisePresentation.Motion, exercise.Presentation);
        });
        Dictionary<int, ExerciseDirectionSequence> auditedDirectionSequences = new()
        {
            [406] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [409] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [477] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [483] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [501] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [608] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [743] = ExerciseDirectionSequence.BackwardThenForward,
            [816] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
        };
        Assert.All(auditedDirectionSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key)
                    .DirectionSequence));
        int[] declaredReplacementIds = exercises
            .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
            .Select(exercise => exercise.Id)
            .Order()
            .ToArray();
        Assert.Equal(
            CatalogMigrationRules.ReplacedExerciseIds.Order(),
            declaredReplacementIds);
        Assert.All(
            exercises.Where(exercise =>
                CatalogMigrationRules.ReplacedExerciseIds.Contains(exercise.Id)),
            exercise => Assert.NotEqual(exercise.Name, exercise.RetiredName));

        Dictionary<int, ExerciseSideSequence> auditedSideSequences = new()
        {
            [58] = ExerciseSideSequence.ScreenLeftThenRight,
            [237] = ExerciseSideSequence.ScreenLeftThenRight,
            [268] = ExerciseSideSequence.ScreenLeftThenRight,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenLeftThenRight,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.ScreenRightThenLeft,
            [619] = ExerciseSideSequence.Continuous,
            [884] = ExerciseSideSequence.ScreenRightThenLeft,
            [885] = ExerciseSideSequence.ScreenRightThenLeft,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
        };
        Assert.All(auditedSideSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).SideSequence));
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            exercises.Single(exercise => exercise.Id == 118).SideSequence);
        Exercise externalRotation = exercises.Single(exercise => exercise.Id == 268);
        Assert.Equal(
            "Self-Resisted External-Rotation Isometric",
            externalRotation.Name);
        Assert.Equal(ExerciseMode.Hold, externalRotation.Mode);
        Assert.Equal(75, externalRotation.HoldFramePercent);

        string[] deficientWorkoutGroups = MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Select(group => new
            {
                Group = group,
                SelectableCount = exercises.Count(exercise =>
                    WorkoutCoveragePolicy.IsSelectable(exercise, group)),
            })
            .Where(result =>
                result.SelectableCount <
                    WorkoutCoveragePolicy.MinimumSelectableExercisesPerGroup)
            .Select(result =>
                $"{result.Group.Id}: {result.SelectableCount} primary-owned exercises " +
                $"meeting the {WorkoutCoveragePolicy.MinimumCoveragePercent}% " +
                "coverage requirement")
            .ToArray();
        Assert.True(
            deficientWorkoutGroups.Length == 0,
            string.Join(Environment.NewLine, deficientWorkoutGroups));

        Assert.All(exercises, exercise =>
        {
            Assert.InRange(exercise.Id, 1, 1000);
            Assert.True(Enum.IsDefined(exercise.PrimaryCanonicalGroup));
            Assert.Equal(
                exercise.SecondaryCanonicalGroups.Length,
                exercise.SecondaryCanonicalGroups.Distinct().Count());
            Assert.DoesNotContain(
                exercise.PrimaryCanonicalGroup,
                exercise.SecondaryCanonicalGroups);
            Assert.All(exercise.SecondaryCanonicalGroups, group =>
                Assert.True(Enum.IsDefined(group)));
            Assert.True(exercise.OnlyFeetTouchGround);
            Assert.True(exercise.ShoeAgnostic);
            Assert.InRange(exercise.MaxSpaceMeters, 1, 3);
            Assert.Equal("None", exercise.Equipment);
            Assert.True(exercise.Silent);
            Assert.False(string.IsNullOrWhiteSpace(exercise.Practice));
            Assert.False(string.IsNullOrWhiteSpace(exercise.MotionProfile));
            Assert.True(Enum.IsDefined(exercise.SideSequence));
            Assert.True(Enum.IsDefined(exercise.DirectionSequence));
            Assert.True(Enum.IsDefined(exercise.Presentation));
            Assert.Equal(0, exercise.Score);

            if (exercise.Presentation == ExercisePresentation.Still)
            {
                Assert.Equal(ExerciseMode.Hold, exercise.Mode);
            }

            if (exercise.Mode == ExerciseMode.Hold)
            {
                Assert.InRange(exercise.HoldFramePercent, 1, 99);
                Assert.True(
                    File.Exists(Path.Combine(
                        AppContext.BaseDirectory,
                        "Assets",
                        "exercise_hold_frames",
                        $"exercise_{exercise.Id:D4}.png")),
                    $"Exercise {exercise.Id} has no reviewed hold frame.");
            }
            else
            {
                Assert.Equal(0, exercise.HoldFramePercent);
            }
        });
    }
}
