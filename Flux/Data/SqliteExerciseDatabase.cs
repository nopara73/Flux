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
    private const int DatabaseVersion = 7;
    private const string TableName = "exercises";
    private const string CatalogAsset = "exercises.json";
    private const int ExpectedExerciseCount = 1000;
    private const int ExpectedExercisesPerRegion = 100;

    private static readonly string[] Columns =
    [
        "id",
        "name",
        "video",
        "dominant_region",
        "practice",
        "motion_profile",
        "score",
        "only_feet_touch_ground",
        "shoe_agnostic",
        "max_space_meters",
        "equipment",
        "silent",
        "exercise_mode",
        "hold_frame_percent",
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
                video TEXT NOT NULL UNIQUE,
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
                    CHECK (silent = 1),
                exercise_mode TEXT NOT NULL DEFAULT 'Repetition'
                    CHECK (exercise_mode IN ('Repetition', 'Hold')),
                hold_frame_percent INTEGER NOT NULL DEFAULT 0
                    CHECK (hold_frame_percent >= 0 AND hold_frame_percent <= 99),
                CHECK (
                    (exercise_mode = 'Repetition' AND hold_frame_percent = 0) OR
                    (exercise_mode = 'Hold' AND hold_frame_percent > 0))
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

        bool catalogRefreshRequired = false;

        if (oldVersion < 4)
        {
            database.ExecSQL(
                "ALTER TABLE exercises ADD COLUMN exercise_mode TEXT NOT NULL " +
                "DEFAULT 'Repetition' CHECK (exercise_mode IN ('Repetition', 'Hold'))");
            database.ExecSQL(
                "ALTER TABLE exercises ADD COLUMN hold_frame_percent INTEGER NOT NULL " +
                "DEFAULT 0 CHECK (hold_frame_percent >= 0 AND hold_frame_percent <= 99)");
            catalogRefreshRequired = true;
        }

        if (oldVersion < 5)
        {
            // Older installations retain their legacy `gif` column. Keeping it is
            // harmless and avoids rebuilding the table (and risking user scores).
            database.ExecSQL(
                "ALTER TABLE exercises ADD COLUMN video TEXT NOT NULL DEFAULT ''");
            catalogRefreshRequired = true;
        }

        if (oldVersion < 6)
        {
            // Refresh reviewed demonstrations, names, and hold metadata while
            // retaining the score keyed by each stable exercise ID.
            catalogRefreshRequired = true;
        }

        if (oldVersion < 7)
        {
            // Refresh the completed shoulder demonstrations and revised names
            // while retaining the score keyed by each stable exercise ID.
            catalogRefreshRequired = true;
        }

        if (oldVersion < 3)
        {
            catalogRefreshRequired = true;
        }

        if (catalogRefreshRequired)
        {
            RefreshCatalogPreservingScores(database);
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
        Exercise[] catalog = ReadBundledCatalog();

        ValidateCatalog(catalog, requireInitialScores: true);

        foreach (Exercise exercise in catalog)
        {
            using var values = new ContentValues();
            values.Put("id", exercise.Id);
            values.Put("name", exercise.Name);
            values.Put("video", exercise.Video);
            values.Put("dominant_region", exercise.DominantRegion.ToString());
            values.Put("practice", exercise.Practice);
            values.Put("motion_profile", exercise.MotionProfile);
            values.Put("score", exercise.Score);
            values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
            values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
            values.Put("max_space_meters", exercise.MaxSpaceMeters);
            values.Put("equipment", exercise.Equipment);
            values.Put("silent", exercise.Silent ? 1 : 0);
            values.Put("exercise_mode", exercise.Mode.ToString());
            values.Put("hold_frame_percent", exercise.HoldFramePercent);
            database.InsertOrThrow(TableName, null, values);
        }
    }

    private void RefreshCatalogPreservingScores(SQLiteDatabase database)
    {
        Exercise[] catalog = ReadBundledCatalog();
        ValidateCatalog(catalog, requireInitialScores: true);

        database.BeginTransaction();
        try
        {
            // Release the UNIQUE name values before applying renamed records.
            database.ExecSQL(
                "UPDATE exercises SET name = '__flux_catalog_v7_' || id");

            foreach (Exercise exercise in catalog)
            {
                using var values = new ContentValues();
                values.Put("name", exercise.Name);
                values.Put("video", exercise.Video);
                values.Put("dominant_region", exercise.DominantRegion.ToString());
                values.Put("practice", exercise.Practice);
                values.Put("motion_profile", exercise.MotionProfile);
                values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
                values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
                values.Put("max_space_meters", exercise.MaxSpaceMeters);
                values.Put("equipment", exercise.Equipment);
                values.Put("silent", exercise.Silent ? 1 : 0);
                values.Put("exercise_mode", exercise.Mode.ToString());
                values.Put("hold_frame_percent", exercise.HoldFramePercent);

                int updatedRows = database.Update(
                    TableName,
                    values,
                    "id = ?",
                    [exercise.Id.ToString(CultureInfo.InvariantCulture)]);
                if (updatedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Could not refresh catalog exercise {exercise.Id}.");
                }
            }

            database.SetTransactionSuccessful();
        }
        finally
        {
            database.EndTransaction();
        }
    }

    private Exercise[] ReadBundledCatalog()
    {
        using Stream stream = _context.Assets!.Open(CatalogAsset);
        return JsonSerializer.Deserialize(
                stream,
                ExerciseCatalogJsonContext.Default.ExerciseArray)
            ?? throw new InvalidOperationException("The exercise catalog is empty.");
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
                Video = cursor.GetString(2)
                    ?? throw new InvalidOperationException("An exercise has no video."),
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
                Mode = Enum.Parse<ExerciseMode>(cursor.GetString(12)
                    ?? throw new InvalidOperationException("An exercise has no mode.")),
                HoldFramePercent = cursor.GetInt(13),
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
            string.IsNullOrWhiteSpace(exercise.MotionProfile) ||
            !Enum.IsDefined(exercise.Mode) ||
            (exercise.Mode == ExerciseMode.Repetition && exercise.HoldFramePercent != 0) ||
            (exercise.Mode == ExerciseMode.Hold &&
                exercise.HoldFramePercent is <= 0 or > 99));
        bool hasInvalidInitialScore =
            requireInitialScores && exercises.Any(exercise => exercise.Score != 0);

        if (hasInvalidRegionCount ||
            exercises.Select(exercise => exercise.Id).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Name).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Video).Distinct().Count() != exercises.Count ||
            violatesRequirements ||
            hasInvalidInitialScore)
        {
            throw new InvalidOperationException(
                "The bundled exercise catalog does not satisfy its required invariants.");
        }
    }
}
