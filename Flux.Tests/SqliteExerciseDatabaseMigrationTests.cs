using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Data;
using Flux.Models;
using Flux.Services;
using Microsoft.Data.Sqlite;

namespace Flux.Tests;

public sealed class SqliteExerciseDatabaseMigrationTests
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(14)]
    [InlineData(66)]
    [InlineData(67)]
    [InlineData(68)]
    [InlineData(69)]
    [InlineData(70)]
    public void EverySupportedDatabaseCanUpgradeToTheCurrentCatalog(int oldVersion)
    {
        Assert.Equal(71, ExerciseDatabaseVersionPolicy.CurrentVersion);
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            oldVersion,
            ExerciseDatabaseVersionPolicy.CurrentVersion));
    }

    [Theory]
    [InlineData(13, 71)]
    [InlineData(68, 68)]
    [InlineData(69, 69)]
    [InlineData(70, 70)]
    [InlineData(71, 71)]
    [InlineData(71, 72)]
    public void UnsupportedDatabaseTransitionsRemainRejected(
        int oldVersion,
        int newVersion)
    {
        Assert.False(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            oldVersion,
            newVersion));
    }

    [Fact]
    public void Version67UpgradeAddsSequenceRecordsAndPreservesEveryUnchangedScore()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedExerciseIds =
        [
            529, 530, 531, 532, 533, 534, 535, 536, 537,
            538, 539, 540, 541, 542, 543, 545, 546, 547, 548,
        ];
        HashSet<int> added = addedExerciseIds.ToHashSet();
        HashSet<int> laterHardFloorCoverageIds = new int[]
        {
            549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
            559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        }.ToHashSet();
        var storedVersion67 = catalog
            .Where(exercise =>
                !added.Contains(exercise.Id) &&
                !laterHardFloorCoverageIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Id switch
                    {
                        520 => "Silent Vowel-Shape Sequence",
                        521 => "Smile-to-Neutral Transitions",
                        _ => exercise.Name,
                    },
                    exercise.Video,
                    Score: exercise.Id % 17 - 8));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion67);

        Assert.Equal(475, catalog.Length);
        Assert.Equal(430, storedVersion67.Count);
        Assert.Equal(428, preserved.Count);
        Assert.DoesNotContain(520, preserved);
        Assert.DoesNotContain(521, preserved);
        Assert.Equal(
            storedVersion67.Keys.Except([520, 521]).Order(),
            preserved.Order());
        Assert.All(storedVersion67, entry =>
            Assert.Equal(entry.Key % 17 - 8, entry.Value.Score));
        Assert.Equal(5, catalog.Count(exercise =>
            added.Contains(exercise.Id) &&
            exercise.MuscularDemand == Exercise.MaximumMuscularDemand));
        Assert.All(addedExerciseIds, exerciseId =>
            Assert.Contains(catalog, root => root.SequenceBlocks.Any(block =>
                block.ExerciseId == exerciseId)));
    }

    [Fact]
    public void Version69UpgradeAddsHardFloorCoverageRecordsAndPreservesUnchangedScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedExerciseIds =
        [
            549, 550, 551, 552, 554, 555, 556, 557,
            560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        ];
        HashSet<int> added = addedExerciseIds.ToHashSet();
        Dictionary<int, StoredExerciseSnapshot> storedVersion69 = catalog
            .Where(exercise => !added.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 19 - 9));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion69);

        Assert.Equal(475, catalog.Length);
        Assert.Equal(449, storedVersion69.Count);
        Assert.Equal(storedVersion69.Keys.Order(), preserved.Order());
        Assert.All(storedVersion69, entry =>
            Assert.Equal(entry.Key % 19 - 9, entry.Value.Score));
        Assert.All(addedExerciseIds, exerciseId =>
        {
            Exercise addedExercise = Assert.Single(
                catalog,
                exercise => exercise.Id == exerciseId);
            Assert.Equal(
                exerciseId is 556 or 565
                    ? ExerciseHardFloorCompatibility.Compatible
                    : ExerciseHardFloorCompatibility.Incompatible,
                addedExercise.HardFloorCompatibility);
            Assert.DoesNotContain(exerciseId, preserved);
        });
    }

    [Fact]
    public void PublishedVersion68CatalogUpgradesWithoutRejectingChangedHardFloorExercises()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedAfterVersion68 =
        [
            547, 548,
            549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
            559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        ];
        var publishedVersion68Names = new Dictionary<int, string>
        {
            [439] = "Pogo Bounces with Fixed-Gaze Head Turns",
            [442] = "Pogo Bounces with Fixed-Gaze Head Nods",
            [444] = "Pogo Bounces with Fixed-Gaze Head Tilts",
            [478] = "Eye-Tracking Rotational Jumps",
        };
        HashSet<int> added = addedAfterVersion68.ToHashSet();
        Dictionary<int, StoredExerciseSnapshot> storedVersion68 = catalog
            .Where(exercise => !added.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    publishedVersion68Names.GetValueOrDefault(
                        exercise.Id,
                        exercise.Name),
                    exercise.Video,
                    Score: exercise.Id % 19 - 9));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion68);

        Assert.Equal(447, storedVersion68.Count);
        Assert.Equal(443, preserved.Count);
        Assert.Equal(
            storedVersion68.Keys
                .Except(publishedVersion68Names.Keys)
                .Order(),
            preserved.Order());
        Assert.All(storedVersion68, entry =>
            Assert.Equal(entry.Key % 19 - 9, entry.Value.Score));
    }

    [Fact]
    public void LegacyMirrorOnlyRowCanBeCopiedIntoCurrentSchema()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(
            connection,
            """
            CREATE TABLE exercises (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                video TEXT NOT NULL UNIQUE,
                practice TEXT NOT NULL,
                motion_profile TEXT NOT NULL,
                score INTEGER NOT NULL,
                muscular_demand INTEGER NOT NULL,
                only_feet_touch_ground INTEGER NOT NULL,
                shoe_agnostic INTEGER NOT NULL,
                max_space_meters INTEGER NOT NULL,
                equipment TEXT NOT NULL,
                silent INTEGER NOT NULL,
                exercise_mode TEXT NOT NULL,
                presentation TEXT NOT NULL,
                hold_frame_percent INTEGER NOT NULL,
                side_sequence TEXT NOT NULL,
                direction_sequence TEXT NOT NULL,
                direction_partner_exercise_id INTEGER NOT NULL,
                insect_compatibility TEXT NOT NULL,
                mirror_relationship TEXT NOT NULL,
                mirror_coverage TEXT NOT NULL
            );

            INSERT INTO exercises VALUES (
                528,
                'Mirror Posture Check',
                'exercise_0528.mp4',
                'Posture',
                'Static',
                7,
                1,
                1,
                1,
                2,
                'Mirror',
                1,
                'Repetition',
                'Motion',
                0,
                'Continuous',
                'None',
                0,
                'Compatible',
                'MirrorOnly',
                'FullBody');
            """);

        Execute(
            connection,
            ExerciseDatabaseMigrationSql.DropRebuiltExerciseTableIfPresent);
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);

        Execute(
            connection,
            ExerciseDatabaseMigrationSql
                .CopyExistingExercisesWithNeutralCatalogMetadata);
        Execute(connection, "DROP TABLE exercises");
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.RenameRebuiltExerciseTable);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, video, score, equipment,
                hard_floor_compatibility, mirror_relationship,
                mirror_coverage, session_movement_id
            FROM exercises
            WHERE id = 528
            """;
        using SqliteDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Mirror Posture Check", reader.GetString(0));
        Assert.Equal("exercise_0528.mp4", reader.GetString(1));
        Assert.Equal(7, reader.GetInt32(2));
        Assert.Equal("None", reader.GetString(3));
        Assert.Equal("Unreviewed", reader.GetString(4));
        Assert.Equal("Unreviewed", reader.GetString(5));
        Assert.Equal("None", reader.GetString(6));
        Assert.Equal(0, reader.GetInt32(7));
        Assert.False(reader.Read());
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
