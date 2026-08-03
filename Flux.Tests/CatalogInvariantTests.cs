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

        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            foreach (WorkoutGroup group in MassGroupingTaxonomy.GetResolution(minutes).Groups)
            {
                int selectableCount = exercises.Count(exercise =>
                    WorkoutCoveragePolicy.IsSelectable(exercise, group));
                Assert.True(
                    selectableCount >=
                        WorkoutCoveragePolicy.MinimumSelectableExercisesPerGroup,
                    $"{group.Id} has only {selectableCount} primary-owned exercises " +
                    $"meeting the {WorkoutCoveragePolicy.MinimumCoveragePercent}% " +
                    "coverage requirement.");
            }
        }

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
            Assert.Equal(0, exercise.Score);

            if (exercise.Mode == ExerciseMode.Hold)
            {
                Assert.InRange(exercise.HoldFramePercent, 1, 99);
            }
            else
            {
                Assert.Equal(0, exercise.HoldFramePercent);
            }
        });
    }
}
