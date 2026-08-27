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
    public void EverySupportedDatabaseCanUpgradeToTheCurrentCatalog(int oldVersion)
    {
        Assert.Equal(69, ExerciseDatabaseVersionPolicy.CurrentVersion);
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            oldVersion,
            ExerciseDatabaseVersionPolicy.CurrentVersion));
    }

    [Theory]
    [InlineData(13, 69)]
    [InlineData(68, 68)]
    [InlineData(69, 69)]
    [InlineData(69, 70)]
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
        var storedVersion67 = catalog
            .Where(exercise => !added.Contains(exercise.Id))
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

        Assert.Equal(450, catalog.Length);
        Assert.Equal(431, storedVersion67.Count);
        Assert.Equal(429, preserved.Count);
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
            """
            CREATE TABLE exercises_v69 (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                video TEXT NOT NULL UNIQUE,
                practice TEXT NOT NULL,
                motion_profile TEXT NOT NULL,
                score INTEGER NOT NULL,
                muscular_demand INTEGER NOT NULL
                    CHECK (muscular_demand BETWEEN 0 AND 2),
                only_feet_touch_ground INTEGER NOT NULL CHECK (only_feet_touch_ground = 1),
                shoe_agnostic INTEGER NOT NULL CHECK (shoe_agnostic = 1),
                max_space_meters INTEGER NOT NULL
                    CHECK (max_space_meters > 0 AND max_space_meters <= 2),
                equipment TEXT NOT NULL CHECK (equipment IN ('None', 'Mirror')),
                silent INTEGER NOT NULL CHECK (silent IN (0, 1)),
                exercise_mode TEXT NOT NULL CHECK (exercise_mode IN ('Repetition', 'Hold')),
                presentation TEXT NOT NULL CHECK (presentation IN ('Motion', 'Still')),
                hold_frame_percent INTEGER NOT NULL CHECK (hold_frame_percent BETWEEN 0 AND 99),
                side_sequence TEXT NOT NULL,
                direction_sequence TEXT NOT NULL,
                insect_compatibility TEXT NOT NULL,
                hard_floor_compatibility TEXT NOT NULL,
                mirror_relationship TEXT NOT NULL,
                mirror_coverage TEXT NOT NULL,
                session_movement_id INTEGER NOT NULL DEFAULT 0
                    CHECK (session_movement_id >= 0),
                CHECK (
                    (mirror_relationship = 'MirrorOnly' AND
                        equipment = 'Mirror' AND
                        mirror_coverage IN ('UpperBody', 'FullBody')) OR
                    (mirror_relationship = 'BenefitsGreatly' AND
                        equipment = 'None' AND
                        mirror_coverage IN ('UpperBody', 'FullBody')) OR
                    (mirror_relationship IN ('Unreviewed', 'Agnostic') AND
                        equipment = 'None' AND
                        mirror_coverage = 'None')),
                CHECK (
                    (exercise_mode = 'Repetition' AND hold_frame_percent = 0) OR
                    (exercise_mode = 'Hold' AND hold_frame_percent > 0))
            );
            """);

        Execute(
            connection,
            ExerciseDatabaseMigrationSql
                .CopyExistingExercisesWithNeutralCatalogMetadata);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, video, score, equipment,
                hard_floor_compatibility, mirror_relationship,
                mirror_coverage, session_movement_id
            FROM exercises_v69
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
