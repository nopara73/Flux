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
    private const int DatabaseVersion = 14;
    private const string ExerciseTable = "exercises";
    private const string MuscleGroupTable = "exercise_muscle_groups";
    private const string CatalogAsset = "exercises.json";
    private const int MinimumExercisesPerMuscleGroup = 10;

    private static readonly string[] ExerciseColumns =
    [
        "id",
        "name",
        "video",
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

    public override void OnConfigure(SQLiteDatabase? database)
    {
        base.OnConfigure(database);
        database?.SetForeignKeyConstraintsEnabled(true);
    }

    public override void OnCreate(SQLiteDatabase? database)
    {
        ArgumentNullException.ThrowIfNull(database);

        CreateSchema(database);
        InsertCatalog(database, ReadAndValidateBundledCatalog());
    }

    public override void OnUpgrade(SQLiteDatabase? database, int oldVersion, int newVersion)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (newVersion != DatabaseVersion || oldVersion >= DatabaseVersion)
        {
            throw new NotSupportedException(
                $"No exercise database migration exists from {oldVersion} to {newVersion}.");
        }

        Exercise[] catalog = ReadAndValidateBundledCatalog();
        Dictionary<int, ExistingExercise> existingExercises =
            ReadExistingExerciseIdentities(database);

        database.BeginTransaction();
        try
        {
            database.ExecSQL($"DROP TABLE IF EXISTS {MuscleGroupTable}");
            database.ExecSQL($"DROP TABLE IF EXISTS {ExerciseTable}");
            CreateSchema(database);
            InsertCatalog(database, catalog, existingExercises);
            database.SetTransactionSuccessful();
        }
        finally
        {
            database.EndTransaction();
        }
    }

    public void UpdateScore(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        using var values = new ContentValues();
        values.Put("score", exercise.Score);
        SQLiteDatabase database = WritableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        int updatedRows = database.Update(
            ExerciseTable,
            values,
            "id = ?",
            [exercise.Id.ToString(CultureInfo.InvariantCulture)]);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Could not persist the score for exercise {exercise.Id}.");
        }
    }

    private static void CreateSchema(SQLiteDatabase database)
    {
        database.ExecSQL(
            """
            CREATE TABLE exercises (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                video TEXT NOT NULL UNIQUE,
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
            """
            CREATE TABLE exercise_muscle_groups (
                exercise_id INTEGER NOT NULL,
                muscle_group TEXT NOT NULL CHECK (muscle_group IN (
                    'Glutes', 'Core', 'Quadriceps', 'Hamstrings', 'UpperBack',
                    'Shoulders', 'Chest', 'LowerBack', 'Calves', 'HipFlexors',
                    'Adductors', 'Abductors', 'MidBack', 'Trapezius', 'Forearms',
                    'Triceps', 'Biceps', 'RotatorCuff', 'Neck', 'Shins')),
                PRIMARY KEY (exercise_id, muscle_group),
                FOREIGN KEY (exercise_id) REFERENCES exercises(id) ON DELETE CASCADE
            )
            """);
        database.ExecSQL(
            "CREATE INDEX index_exercises_score ON exercises (score DESC)");
        database.ExecSQL(
            "CREATE INDEX index_exercise_muscle_groups_group " +
            "ON exercise_muscle_groups (muscle_group, exercise_id)");
    }

    private Exercise[] ReadAndValidateBundledCatalog()
    {
        using Stream stream = _context.Assets!.Open(CatalogAsset);
        Exercise[] catalog = JsonSerializer.Deserialize(
                stream,
                ExerciseCatalogJsonContext.Default.ExerciseArray)
            ?? throw new InvalidOperationException("The exercise catalog is empty.");
        ValidateCatalog(catalog, requireInitialScores: true);
        return catalog;
    }

    private static void InsertCatalog(
        SQLiteDatabase database,
        IReadOnlyCollection<Exercise> catalog,
        IReadOnlyDictionary<int, ExistingExercise>? existingExercises = null)
    {
        foreach (Exercise exercise in catalog)
        {
            int score = existingExercises is not null &&
                existingExercises.TryGetValue(exercise.Id, out ExistingExercise? existing) &&
                existing is not null &&
                string.Equals(existing.Name, exercise.Name, StringComparison.Ordinal)
                    ? existing.Score
                    : exercise.Score;

            using (var values = new ContentValues())
            {
                values.Put("id", exercise.Id);
                values.Put("name", exercise.Name);
                values.Put("video", exercise.Video);
                values.Put("practice", exercise.Practice);
                values.Put("motion_profile", exercise.MotionProfile);
                values.Put("score", score);
                values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
                values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
                values.Put("max_space_meters", exercise.MaxSpaceMeters);
                values.Put("equipment", exercise.Equipment);
                values.Put("silent", exercise.Silent ? 1 : 0);
                values.Put("exercise_mode", exercise.Mode.ToString());
                values.Put("hold_frame_percent", exercise.HoldFramePercent);
                database.InsertOrThrow(ExerciseTable, null, values);
            }

            foreach (MuscleGroup muscleGroup in exercise.MuscleGroups)
            {
                using var values = new ContentValues();
                values.Put("exercise_id", exercise.Id);
                values.Put("muscle_group", muscleGroup.ToString());
                database.InsertOrThrow(MuscleGroupTable, null, values);
            }
        }
    }

    private static Dictionary<int, ExistingExercise> ReadExistingExerciseIdentities(
        SQLiteDatabase database)
    {
        var existingExercises = new Dictionary<int, ExistingExercise>();
        using ICursor? cursor = database.Query(
            ExerciseTable,
            ["id", "name", "score"],
            null,
            null,
            null,
            null,
            null);

        if (cursor is null)
        {
            throw new InvalidOperationException(
                "Unable to read existing exercise identities and scores.");
        }

        while (cursor.MoveToNext())
        {
            existingExercises[cursor.GetInt(0)] = new ExistingExercise(
                cursor.GetString(1)
                    ?? throw new InvalidOperationException(
                        "An existing exercise has no name."),
                cursor.GetInt(2));
        }

        return existingExercises;
    }

    private IReadOnlyList<Exercise> LoadExercises()
    {
        SQLiteDatabase database = ReadableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        Dictionary<int, List<MuscleGroup>> groupsByExerciseId =
            LoadMuscleGroups(database);
        var exercises = new List<Exercise>();

        using ICursor? cursor = database.Query(
            ExerciseTable,
            ExerciseColumns,
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
            int id = cursor.GetInt(0);
            if (!groupsByExerciseId.TryGetValue(id, out List<MuscleGroup>? muscleGroups))
            {
                throw new InvalidOperationException(
                    $"Exercise {id} has no muscle-group assignment.");
            }

            exercises.Add(new Exercise
            {
                Id = id,
                Name = cursor.GetString(1)
                    ?? throw new InvalidOperationException("An exercise has no name."),
                Video = cursor.GetString(2)
                    ?? throw new InvalidOperationException("An exercise has no video."),
                MuscleGroups = muscleGroups.ToArray(),
                Practice = cursor.GetString(3)
                    ?? throw new InvalidOperationException("An exercise has no practice."),
                MotionProfile = cursor.GetString(4)
                    ?? throw new InvalidOperationException("An exercise has no motion profile."),
                Score = cursor.GetInt(5),
                OnlyFeetTouchGround = cursor.GetInt(6) == 1,
                ShoeAgnostic = cursor.GetInt(7) == 1,
                MaxSpaceMeters = cursor.GetInt(8),
                Equipment = cursor.GetString(9)
                    ?? throw new InvalidOperationException("An exercise has no equipment value."),
                Silent = cursor.GetInt(10) == 1,
                Mode = Enum.Parse<ExerciseMode>(cursor.GetString(11)
                    ?? throw new InvalidOperationException("An exercise has no mode.")),
                HoldFramePercent = cursor.GetInt(12),
            });
        }

        ValidateCatalog(exercises, requireInitialScores: false);
        return exercises.AsReadOnly();
    }

    private static Dictionary<int, List<MuscleGroup>> LoadMuscleGroups(
        SQLiteDatabase database)
    {
        var groupsByExerciseId = new Dictionary<int, List<MuscleGroup>>();
        using ICursor? cursor = database.Query(
            MuscleGroupTable,
            ["exercise_id", "muscle_group"],
            null,
            null,
            null,
            null,
            "exercise_id ASC, muscle_group ASC");

        if (cursor is null)
        {
            throw new InvalidOperationException("Unable to read muscle-group assignments.");
        }

        while (cursor.MoveToNext())
        {
            int exerciseId = cursor.GetInt(0);
            string groupName = cursor.GetString(1)
                ?? throw new InvalidOperationException(
                    "A muscle-group assignment has no group name.");
            MuscleGroup muscleGroup = Enum.Parse<MuscleGroup>(groupName);

            if (!groupsByExerciseId.TryGetValue(
                    exerciseId,
                    out List<MuscleGroup>? muscleGroups))
            {
                muscleGroups = [];
                groupsByExerciseId.Add(exerciseId, muscleGroups);
            }

            muscleGroups.Add(muscleGroup);
        }

        return groupsByExerciseId;
    }

    private static void ValidateCatalog(
        IReadOnlyCollection<Exercise> exercises,
        bool requireInitialScores)
    {
        bool hasInvalidMuscleGroupCount = Enum
            .GetValues<MuscleGroup>()
            .Any(muscleGroup => exercises.Count(exercise =>
                exercise.MuscleGroups.Contains(muscleGroup)) <
                    MinimumExercisesPerMuscleGroup);
        bool violatesRequirements = exercises.Any(exercise =>
            exercise.MuscleGroups.Length == 0 ||
            exercise.MuscleGroups.Distinct().Count() != exercise.MuscleGroups.Length ||
            exercise.MuscleGroups.Any(muscleGroup => !Enum.IsDefined(muscleGroup)) ||
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

        if (hasInvalidMuscleGroupCount ||
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

    private sealed record ExistingExercise(string Name, int Score);
}
