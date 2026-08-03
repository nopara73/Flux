using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogMigrationRulesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

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

    [Fact]
    public void VersionFifteenInventoryReconcilesAdditivelyIntoBundledCatalog()
    {
        int[] versionSixteenIds = [400, 401, 402, 403, 404, 405, 406];
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Assert.All(versionSixteenIds, id =>
            Assert.Contains(bundled, exercise => exercise.Id == id));

        Dictionary<int, StoredExerciseSnapshot> versionFifteen = bundled
            .Where(exercise => !versionSixteenIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    -exercise.Id));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            bundled,
            versionFifteen);

        Assert.Equal(versionFifteen.Keys.Order(), preserved.Order());
        Assert.All(versionFifteen, entry =>
            Assert.Equal(-entry.Key, entry.Value.Score));
        Assert.DoesNotContain(versionSixteenIds, preserved.Contains);
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
