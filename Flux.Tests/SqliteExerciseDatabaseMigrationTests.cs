using Flux.Data;
using Microsoft.Data.Sqlite;

namespace Flux.Tests;

public sealed class SqliteExerciseDatabaseMigrationTests
{
    [Fact]
    public void Version60MirrorOnlyRowCanBeCopiedIntoVersion61Schema()
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
            CREATE TABLE exercises_v60 (
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
                direction_partner_exercise_id INTEGER NOT NULL
                    CHECK (direction_partner_exercise_id >= 0),
                insect_compatibility TEXT NOT NULL,
                mirror_relationship TEXT NOT NULL,
                mirror_coverage TEXT NOT NULL,
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
                .CopyExistingExercisesWithNeutralMirrorMetadata);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, video, score, equipment,
                mirror_relationship, mirror_coverage
            FROM exercises_v60
            WHERE id = 528
            """;
        using SqliteDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Mirror Posture Check", reader.GetString(0));
        Assert.Equal("exercise_0528.mp4", reader.GetString(1));
        Assert.Equal(7, reader.GetInt32(2));
        Assert.Equal("None", reader.GetString(3));
        Assert.Equal("Unreviewed", reader.GetString(4));
        Assert.Equal("None", reader.GetString(5));
        Assert.False(reader.Read());
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
