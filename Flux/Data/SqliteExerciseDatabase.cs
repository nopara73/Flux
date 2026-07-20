using System.Globalization;
using System.Text.Json;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using Flux.Models;

namespace Flux.Data;

public sealed class SqliteExerciseDatabase : SQLiteOpenHelper, IExerciseDatabase
{
    private const string DatabaseFileName = "flux_exercises.db";
    private const int DatabaseVersion = 2;
    private const string TableName = "exercises";
    private const string CatalogAsset = "exercises.json";
    private const int ExpectedExerciseCount = 1000;
    private const int ExpectedExercisesPerRegion = 100;

    private static readonly string[] Columns =
    [
        "id",
        "name",
        "gif",
        "dominant_region",
        "practice",
        "motion_profile",
        "score",
        "only_feet_touch_ground",
        "shoe_agnostic",
        "max_space_meters",
        "equipment",
        "silent",
    ];

    private readonly Context _context;
    private IReadOnlyList<Exercise>? _exercises;

    public SqliteExerciseDatabase(Context context)
        : base(context, DatabaseFileName, null, DatabaseVersion)
    {
        _context = context.ApplicationContext ?? context;
    }

    public IReadOnlyList<Exercise> Exercises =>
        _exercises ??= LoadExercises();

    public override void OnCreate(SQLiteDatabase? database)
    {
        ArgumentNullException.ThrowIfNull(database);

        database.ExecSQL(
            """
            CREATE TABLE exercises (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                gif TEXT NOT NULL UNIQUE,
                dominant_region TEXT NOT NULL CHECK (dominant_region IN (
                    'FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
                    'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')),
                practice TEXT NOT NULL,
                motion_profile TEXT NOT NULL,
                score INTEGER NOT NULL DEFAULT 0,
                only_feet_touch_ground INTEGER NOT NULL DEFAULT 1
                    CHECK (only_feet_touch_ground = 1),
                shoe_agnostic INTEGER NOT NULL DEFAULT 1
                    CHECK (shoe_agnostic = 1),
                max_space_meters INTEGER NOT NULL DEFAULT 3
                    CHECK (max_space_meters > 0 AND max_space_meters <= 3),
                equipment TEXT NOT NULL DEFAULT 'None'
                    CHECK (equipment = 'None'),
                silent INTEGER NOT NULL DEFAULT 1
                    CHECK (silent = 1)
            )
            """);
        database.ExecSQL(
            "CREATE INDEX index_exercises_region_score " +
            "ON exercises (dominant_region, score DESC)");

        Seed(database);
    }

    public override void OnUpgrade(SQLiteDatabase? database, int oldVersion, int newVersion)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (oldVersion < 2 && newVersion >= 2)
        {
            database.ExecSQL("DROP TABLE IF EXISTS exercises");
            OnCreate(database);
            return;
        }

        throw new NotSupportedException(
            $"No exercise database migration exists from {oldVersion} to {newVersion}.");
    }

    public void UpdateScore(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        using var values = new ContentValues();
        values.Put("score", exercise.Score);
        SQLiteDatabase database = WritableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        int updatedRows = database.Update(
            TableName,
            values,
            "id = ?",
            [exercise.Id.ToString(CultureInfo.InvariantCulture)]);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Could not persist the score for exercise {exercise.Id}.");
        }
    }

    private void Seed(SQLiteDatabase database)
    {
        using Stream stream = _context.Assets!.Open(CatalogAsset);
        Exercise[] catalog = JsonSerializer.Deserialize(
                stream,
                ExerciseCatalogJsonContext.Default.ExerciseArray)
            ?? throw new InvalidOperationException("The exercise catalog is empty.");

        ValidateCatalog(catalog, requireInitialScores: true);

        foreach (Exercise exercise in catalog)
        {
            using var values = new ContentValues();
            values.Put("id", exercise.Id);
            values.Put("name", exercise.Name);
            values.Put("gif", exercise.Gif);
            values.Put("dominant_region", exercise.DominantRegion.ToString());
            values.Put("practice", exercise.Practice);
            values.Put("motion_profile", exercise.MotionProfile);
            values.Put("score", exercise.Score);
            values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
            values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
            values.Put("max_space_meters", exercise.MaxSpaceMeters);
            values.Put("equipment", exercise.Equipment);
            values.Put("silent", exercise.Silent ? 1 : 0);
            database.InsertOrThrow(TableName, null, values);
        }
    }

    private IReadOnlyList<Exercise> LoadExercises()
    {
        var exercises = new List<Exercise>(ExpectedExerciseCount);
        SQLiteDatabase database = ReadableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        using ICursor? cursor = database.Query(
            TableName,
            Columns,
            null,
            null,
            null,
            null,
            "id ASC");

        if (cursor is null)
        {
            throw new InvalidOperationException("Unable to read the exercise database.");
        }

        while (cursor.MoveToNext())
        {
            string regionName = cursor.GetString(3)
                ?? throw new InvalidOperationException("An exercise has no region.");

            exercises.Add(new Exercise
            {
                Id = cursor.GetInt(0),
                Name = cursor.GetString(1)
                    ?? throw new InvalidOperationException("An exercise has no name."),
                Gif = cursor.GetString(2)
                    ?? throw new InvalidOperationException("An exercise has no GIF."),
                DominantRegion = Enum.Parse<DominantRegion>(regionName),
                Practice = cursor.GetString(4)
                    ?? throw new InvalidOperationException("An exercise has no practice."),
                MotionProfile = cursor.GetString(5)
                    ?? throw new InvalidOperationException("An exercise has no motion profile."),
                Score = cursor.GetInt(6),
                OnlyFeetTouchGround = cursor.GetInt(7) == 1,
                ShoeAgnostic = cursor.GetInt(8) == 1,
                MaxSpaceMeters = cursor.GetInt(9),
                Equipment = cursor.GetString(10)
                    ?? throw new InvalidOperationException("An exercise has no equipment value."),
                Silent = cursor.GetInt(11) == 1,
            });
        }

        ValidateCatalog(exercises, requireInitialScores: false);
        return exercises.AsReadOnly();
    }

    private static void ValidateCatalog(
        IReadOnlyCollection<Exercise> exercises,
        bool requireInitialScores)
    {
        if (exercises.Count != ExpectedExerciseCount)
        {
            throw new InvalidOperationException(
                $"The catalog must contain exactly {ExpectedExerciseCount} exercises.");
        }

        bool hasInvalidRegionCount = exercises
            .GroupBy(exercise => exercise.DominantRegion)
            .Any(group => group.Count() != ExpectedExercisesPerRegion);
        bool violatesRequirements = exercises.Any(exercise =>
            !exercise.OnlyFeetTouchGround ||
            !exercise.ShoeAgnostic ||
            exercise.MaxSpaceMeters is <= 0 or > 3 ||
            exercise.Equipment != "None" ||
            !exercise.Silent ||
            string.IsNullOrWhiteSpace(exercise.Practice) ||
            string.IsNullOrWhiteSpace(exercise.MotionProfile));
        bool hasInvalidInitialScore =
            requireInitialScores && exercises.Any(exercise => exercise.Score != 0);

        if (hasInvalidRegionCount ||
            exercises.Select(exercise => exercise.Id).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Name).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Gif).Distinct().Count() != exercises.Count ||
            violatesRequirements ||
            hasInvalidInitialScore)
        {
            throw new InvalidOperationException(
                "The bundled exercise catalog does not satisfy its required invariants.");
        }
    }
}
