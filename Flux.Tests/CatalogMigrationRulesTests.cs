using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogMigrationRulesTests
{
    [Fact]
    public void AdditiveCatalogPreservesExistingIdentityMediaAndScore()
    {
        Exercise existing = Exercise(
            7,
            "Existing movement",
            "exercise_0007.mp4",
            score: 99);
        Exercise secondExisting = Exercise(
            8,
            "Second existing movement",
            "exercise_0008.mp4",
            score: -99);
        Exercise added = Exercise(31, "Added movement", "exercise_0031.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
            [secondExisting.Id] = new(
                secondExisting.Name,
                secondExisting.Video,
                6),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [existing, secondExisting, added],
            stored);

        Assert.Equal([existing.Id, secondExisting.Id], preserved.Order());
        Assert.Equal(-4, stored[existing.Id].Score);
        Assert.Equal(6, stored[secondExisting.Id].Score);
        Assert.Equal(99, existing.Score);
        Assert.Equal(-99, secondExisting.Score);
        Assert.Equal(0, added.Score);
        Assert.DoesNotContain(added.Id, preserved);
    }

    [Fact]
    public void MigrationRejectsRemovalOrDemonstrationReplacement()
    {
        Exercise existing = Exercise(7, "Existing movement", "exercise_0007.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
        };

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([], stored));

        Exercise changedMedia = Exercise(
            existing.Id,
            existing.Name,
            "replacement.mp4");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));

        Exercise changedName = Exercise(
            existing.Id,
            "Renamed movement",
            existing.Video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedName], stored));
    }

    private static Exercise Exercise(
        int id,
        string name,
        string video,
        int score = 0)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            Video = video,
            PrimaryCanonicalGroup =
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            SecondaryCanonicalGroups = [],
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
