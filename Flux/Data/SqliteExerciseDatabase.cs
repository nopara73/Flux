using System.Globalization;
using System.Text.Json;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using Flux.Models;
using Flux.Services;

namespace Flux.Data;

public sealed class SqliteExerciseDatabase : SQLiteOpenHelper, IExerciseDatabase
{
    private const string DatabaseFileName = "flux_exercises.db";
    private const int DatabaseVersion = 30;
    private const string ExerciseTable = "exercises";
    private const string CanonicalGroupTable = "canonical_muscle_groups";
    private const string ExerciseCanonicalGroupTable = "exercise_canonical_groups";
    private const string WorkoutBucketTable = "workout_buckets";
    private const string RollupTable = "canonical_group_rollups";
    private const string CatalogAsset = "exercises.json";
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
        "presentation",
        "hold_frame_percent",
        "side_sequence",
        "direction_sequence",
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

        CreateExerciseSchema(database);
        CreateMassGroupingSchema(database);
        InsertTaxonomy(database);
        InsertCatalog(database, ReadAndValidateBundledCatalog());
    }

    public override void OnUpgrade(SQLiteDatabase? database, int oldVersion, int newVersion)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (oldVersion is not (14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29) ||
            newVersion != DatabaseVersion)
        {
            throw new NotSupportedException(
                $"No non-destructive exercise database migration exists from " +
                $"{oldVersion} to {newVersion}.");
        }

        Exercise[] catalog = ReadAndValidateBundledCatalog();
        Dictionary<int, StoredExerciseSnapshot> existingExercises =
            ReadExistingExercises(database);
        IReadOnlySet<int> preservedExerciseIds =
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, existingExercises);
        Dictionary<int, StoredExerciseSnapshot> preservedExercises = existingExercises
            .Where(entry => preservedExerciseIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        database.BeginTransaction();
        try
        {
            if (oldVersion < 18)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN side_sequence TEXT NOT NULL " +
                    "DEFAULT 'Continuous' CHECK (side_sequence IN " +
                    "('Continuous', 'ScreenLeftThenRight', 'ScreenRightThenLeft'))");
            }
            if (oldVersion < 20)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN presentation TEXT NOT NULL " +
                    "DEFAULT 'Motion' CHECK (presentation IN ('Motion', 'Still'))");
            }
            if (oldVersion < 21)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN direction_sequence TEXT NOT NULL " +
                    "DEFAULT 'None' CHECK (direction_sequence IN " +
                    "('None', 'ForwardThenBackward', 'BackwardThenForward', " +
                    "'ClockwiseThenCounterclockwise', " +
                    "'CounterclockwiseThenClockwise', 'InwardThenOutward', " +
                    "'OutwardThenInward'))");
            }
            CreateMassGroupingSchema(database);
            ClearMassGroupingReferenceData(database);
            DeleteReplacedExercises(database, existingExercises.Keys, preservedExerciseIds);
            InsertTaxonomy(database);
            SynchronizeCatalog(database, catalog, preservedExercises);
            ValidatePreservedExercises(database, preservedExercises, catalog);
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

    private static void CreateExerciseSchema(SQLiteDatabase database)
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
                presentation TEXT NOT NULL DEFAULT 'Motion'
                    CHECK (presentation IN ('Motion', 'Still')),
                hold_frame_percent INTEGER NOT NULL DEFAULT 0
                    CHECK (hold_frame_percent >= 0 AND hold_frame_percent <= 99),
                side_sequence TEXT NOT NULL DEFAULT 'Continuous'
                    CHECK (side_sequence IN (
                        'Continuous',
                        'ScreenLeftThenRight',
                        'ScreenRightThenLeft')),
                direction_sequence TEXT NOT NULL DEFAULT 'None'
                    CHECK (direction_sequence IN (
                        'None',
                        'ForwardThenBackward',
                        'BackwardThenForward',
                        'ClockwiseThenCounterclockwise',
                        'CounterclockwiseThenClockwise',
                        'InwardThenOutward',
                        'OutwardThenInward')),
                CHECK (
                    (exercise_mode = 'Repetition' AND hold_frame_percent = 0) OR
                    (exercise_mode = 'Hold' AND hold_frame_percent > 0))
            )
            """);
        database.ExecSQL(
            "CREATE INDEX index_exercises_score ON exercises (score DESC)");
    }

    private static void CreateMassGroupingSchema(SQLiteDatabase database)
    {
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS canonical_muscle_groups (
                id INTEGER NOT NULL PRIMARY KEY,
                stable_key TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                mass_order INTEGER NOT NULL UNIQUE
            )
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS exercise_canonical_groups (
                exercise_id INTEGER NOT NULL,
                canonical_group_id INTEGER NOT NULL,
                assignment_role INTEGER NOT NULL
                    CHECK (assignment_role IN (1, 2)),
                PRIMARY KEY (exercise_id, canonical_group_id),
                FOREIGN KEY (exercise_id) REFERENCES exercises(id) ON DELETE CASCADE,
                FOREIGN KEY (canonical_group_id)
                    REFERENCES canonical_muscle_groups(id) ON DELETE RESTRICT
            )
            """);
        database.ExecSQL(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS one_primary_group_per_exercise
            ON exercise_canonical_groups (exercise_id)
            WHERE assignment_role = 1
            """);
        database.ExecSQL(
            """
            CREATE INDEX IF NOT EXISTS index_exercise_canonical_groups_group
            ON exercise_canonical_groups
                (canonical_group_id, assignment_role, exercise_id)
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS workout_buckets (
                stable_key TEXT NOT NULL PRIMARY KEY,
                resolution_minutes INTEGER NOT NULL,
                schedule_order INTEGER NOT NULL,
                display_name TEXT NOT NULL,
                UNIQUE (resolution_minutes, schedule_order)
            )
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS canonical_group_rollups (
                resolution_minutes INTEGER NOT NULL,
                canonical_group_id INTEGER NOT NULL,
                bucket_key TEXT NOT NULL,
                PRIMARY KEY (resolution_minutes, canonical_group_id),
                FOREIGN KEY (canonical_group_id)
                    REFERENCES canonical_muscle_groups(id) ON DELETE CASCADE,
                FOREIGN KEY (bucket_key)
                    REFERENCES workout_buckets(stable_key) ON DELETE CASCADE
            )
            """);
    }

    private static void ClearMassGroupingReferenceData(SQLiteDatabase database)
    {
        database.Delete(ExerciseCanonicalGroupTable, null, null);
        database.Delete(RollupTable, null, null);
        database.Delete(WorkoutBucketTable, null, null);
        database.Delete(CanonicalGroupTable, null, null);
    }

    private static void DeleteReplacedExercises(
        SQLiteDatabase database,
        IEnumerable<int> existingExerciseIds,
        IReadOnlySet<int> preservedExerciseIds)
    {
        foreach (int exerciseId in existingExerciseIds.Where(
            exerciseId => !preservedExerciseIds.Contains(exerciseId)))
        {
            int deleted = database.Delete(
                ExerciseTable,
                "id = ?",
                [exerciseId.ToString(CultureInfo.InvariantCulture)]);
            if (deleted != 1)
            {
                throw new InvalidOperationException(
                    $"Could not retire replaced exercise {exerciseId}.");
            }
        }
    }

    private static void InsertTaxonomy(SQLiteDatabase database)
    {
        foreach (CanonicalMuscleGroup group in Enum.GetValues<CanonicalMuscleGroup>())
        {
            using var values = new ContentValues();
            values.Put("id", (int)group);
            values.Put("stable_key", group.ToString());
            values.Put("display_name", MassGroupingTaxonomy.GetCanonicalDisplayName(group));
            values.Put("mass_order", (int)group);
            database.InsertOrThrow(CanonicalGroupTable, null, values);
        }

        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            WorkoutResolution resolution = MassGroupingTaxonomy.GetResolution(minutes);
            foreach (WorkoutGroup group in resolution.Groups)
            {
                using (var bucketValues = new ContentValues())
                {
                    bucketValues.Put("stable_key", group.Id);
                    bucketValues.Put("resolution_minutes", minutes);
                    bucketValues.Put("schedule_order", group.Order);
                    bucketValues.Put("display_name", group.DisplayName);
                    database.InsertOrThrow(WorkoutBucketTable, null, bucketValues);
                }

                foreach (CanonicalMuscleGroup canonicalGroup in group.CanonicalGroups)
                {
                    using var rollupValues = new ContentValues();
                    rollupValues.Put("resolution_minutes", minutes);
                    rollupValues.Put("canonical_group_id", (int)canonicalGroup);
                    rollupValues.Put("bucket_key", group.Id);
                    database.InsertOrThrow(RollupTable, null, rollupValues);
                }
            }
        }
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
        IReadOnlyCollection<Exercise> catalog)
    {
        foreach (Exercise exercise in catalog)
        {
            InsertExercise(database, exercise, exercise.Score);
            InsertCanonicalAssignments(database, exercise);
        }
    }

    private static void SynchronizeCatalog(
        SQLiteDatabase database,
        IReadOnlyCollection<Exercise> catalog,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> existingExercises)
    {
        foreach (Exercise exercise in catalog)
        {
            if (existingExercises.ContainsKey(exercise.Id))
            {
                UpdateExerciseMetadata(database, exercise);
            }
            else
            {
                InsertExercise(database, exercise, exercise.Score);
            }

            InsertCanonicalAssignments(database, exercise);
        }
    }

    private static void InsertExercise(
        SQLiteDatabase database,
        Exercise exercise,
        int score)
    {
        using ContentValues values = CreateExerciseValues(exercise, includeId: true);
        values.Put("score", score);
        database.InsertOrThrow(ExerciseTable, null, values);
    }

    private static void UpdateExerciseMetadata(
        SQLiteDatabase database,
        Exercise exercise)
    {
        using ContentValues values = CreateExerciseValues(
            exercise,
            includeId: false,
            includeIdentity: false);
        values.Put("name", exercise.Name);
        int updated = database.Update(
            ExerciseTable,
            values,
            "id = ?",
            [exercise.Id.ToString(CultureInfo.InvariantCulture)]);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Could not migrate exercise {exercise.Id} without replacing it.");
        }
    }

    private static ContentValues CreateExerciseValues(
        Exercise exercise,
        bool includeId,
        bool includeIdentity = true)
    {
        var values = new ContentValues();
        if (includeId)
        {
            values.Put("id", exercise.Id);
        }

        if (includeIdentity)
        {
            values.Put("name", exercise.Name);
            values.Put("video", exercise.Video);
        }
        values.Put("practice", exercise.Practice);
        values.Put("motion_profile", exercise.MotionProfile);
        values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
        values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
        values.Put("max_space_meters", exercise.MaxSpaceMeters);
        values.Put("equipment", exercise.Equipment);
        values.Put("silent", exercise.Silent ? 1 : 0);
        values.Put("exercise_mode", exercise.Mode.ToString());
        values.Put("presentation", exercise.Presentation.ToString());
        values.Put("hold_frame_percent", exercise.HoldFramePercent);
        values.Put("side_sequence", exercise.SideSequence.ToString());
        values.Put("direction_sequence", exercise.DirectionSequence.ToString());
        return values;
    }

    private static void InsertCanonicalAssignments(
        SQLiteDatabase database,
        Exercise exercise)
    {
        InsertCanonicalAssignment(
            database,
            exercise.Id,
            exercise.PrimaryCanonicalGroup,
            assignmentRole: 1);
        foreach (CanonicalMuscleGroup group in exercise.SecondaryCanonicalGroups)
        {
            InsertCanonicalAssignment(
                database,
                exercise.Id,
                group,
                assignmentRole: 2);
        }
    }

    private static void InsertCanonicalAssignment(
        SQLiteDatabase database,
        int exerciseId,
        CanonicalMuscleGroup group,
        int assignmentRole)
    {
        using var values = new ContentValues();
        values.Put("exercise_id", exerciseId);
        values.Put("canonical_group_id", (int)group);
        values.Put("assignment_role", assignmentRole);
        database.InsertOrThrow(ExerciseCanonicalGroupTable, null, values);
    }

    private static Dictionary<int, StoredExerciseSnapshot> ReadExistingExercises(
        SQLiteDatabase database)
    {
        var existingExercises = new Dictionary<int, StoredExerciseSnapshot>();
        using ICursor? cursor = database.Query(
            ExerciseTable,
            ["id", "name", "video", "score"],
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
            existingExercises[cursor.GetInt(0)] = new StoredExerciseSnapshot(
                cursor.GetString(1)
                    ?? throw new InvalidOperationException(
                        "An existing exercise has no name."),
                cursor.GetString(2)
                    ?? throw new InvalidOperationException(
                        "An existing exercise has no demonstration."),
                cursor.GetInt(3));
        }

        return existingExercises;
    }

    private static void ValidatePreservedExercises(
        SQLiteDatabase database,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> before,
        IReadOnlyCollection<Exercise> bundledCatalog)
    {
        Dictionary<int, StoredExerciseSnapshot> after = ReadExistingExercises(database);
        IReadOnlyDictionary<int, Exercise> bundledById = bundledCatalog.ToDictionary(
            exercise => exercise.Id);
        foreach ((int exerciseId, StoredExerciseSnapshot previous) in before)
        {
            if (!after.TryGetValue(exerciseId, out StoredExerciseSnapshot? current) ||
                current.Name != bundledById[exerciseId].Name ||
                current.Video != previous.Video ||
                current.Score != previous.Score ||
                string.IsNullOrWhiteSpace(current.Video))
            {
                throw new InvalidOperationException(
                    $"Exercise {exerciseId} lost its score or demonstration during migration.");
            }
        }
    }

    private IReadOnlyList<Exercise> LoadExercises()
    {
        SQLiteDatabase database = ReadableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        Dictionary<int, CanonicalAssignments> assignmentsByExerciseId =
            LoadCanonicalAssignments(database);
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
            if (!assignmentsByExerciseId.TryGetValue(
                    id,
                    out CanonicalAssignments? assignments) ||
                assignments.Primary is null)
            {
                throw new InvalidOperationException(
                    $"Exercise {id} has no primary canonical assignment.");
            }

            exercises.Add(new Exercise
            {
                Id = id,
                Name = cursor.GetString(1)
                    ?? throw new InvalidOperationException("An exercise has no name."),
                Video = cursor.GetString(2)
                    ?? throw new InvalidOperationException("An exercise has no video."),
                PrimaryCanonicalGroup = assignments.Primary.Value,
                SecondaryCanonicalGroups = assignments.Secondary.ToArray(),
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
                Presentation = Enum.Parse<ExercisePresentation>(cursor.GetString(12)
                    ?? throw new InvalidOperationException(
                        "An exercise has no presentation.")),
                HoldFramePercent = cursor.GetInt(13),
                SideSequence = Enum.Parse<ExerciseSideSequence>(cursor.GetString(14)
                    ?? throw new InvalidOperationException(
                        "An exercise has no side sequence.")),
                DirectionSequence = Enum.Parse<ExerciseDirectionSequence>(
                    cursor.GetString(15)
                        ?? throw new InvalidOperationException(
                            "An exercise has no direction sequence.")),
            });
        }

        ValidateCatalog(exercises, requireInitialScores: false);
        return exercises.AsReadOnly();
    }

    private static Dictionary<int, CanonicalAssignments> LoadCanonicalAssignments(
        SQLiteDatabase database)
    {
        var assignmentsByExerciseId = new Dictionary<int, CanonicalAssignments>();
        using ICursor? cursor = database.Query(
            ExerciseCanonicalGroupTable,
            ["exercise_id", "canonical_group_id", "assignment_role"],
            null,
            null,
            null,
            null,
            "exercise_id ASC, assignment_role ASC, canonical_group_id ASC");

        if (cursor is null)
        {
            throw new InvalidOperationException(
                "Unable to read canonical exercise assignments.");
        }

        while (cursor.MoveToNext())
        {
            int exerciseId = cursor.GetInt(0);
            var group = (CanonicalMuscleGroup)cursor.GetInt(1);
            int role = cursor.GetInt(2);
            if (!assignmentsByExerciseId.TryGetValue(
                    exerciseId,
                    out CanonicalAssignments? assignments))
            {
                assignments = new CanonicalAssignments();
                assignmentsByExerciseId.Add(exerciseId, assignments);
            }

            if (role == 1)
            {
                if (assignments.Primary is not null)
                {
                    throw new InvalidOperationException(
                        $"Exercise {exerciseId} has multiple primary assignments.");
                }

                assignments.Primary = group;
            }
            else
            {
                assignments.Secondary.Add(group);
            }
        }

        return assignmentsByExerciseId;
    }

    private static void ValidateCatalog(
        IReadOnlyCollection<Exercise> exercises,
        bool requireInitialScores)
    {
        bool hasUndersizedWorkoutGroup = MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Any(group => exercises.Count(exercise =>
                    WorkoutCoveragePolicy.IsSelectable(exercise, group)) <
                WorkoutCoveragePolicy.MinimumSelectableExercisesPerGroup);
        bool violatesRequirements = exercises.Any(exercise =>
            !Enum.IsDefined(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Distinct().Count() !=
                exercise.SecondaryCanonicalGroups.Length ||
            exercise.SecondaryCanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Any(group => !Enum.IsDefined(group)) ||
            !exercise.OnlyFeetTouchGround ||
            !exercise.ShoeAgnostic ||
            exercise.MaxSpaceMeters is <= 0 or > 3 ||
            exercise.Equipment != "None" ||
            !exercise.Silent ||
            string.IsNullOrWhiteSpace(exercise.Practice) ||
            string.IsNullOrWhiteSpace(exercise.MotionProfile) ||
            !Enum.IsDefined(exercise.Mode) ||
            !Enum.IsDefined(exercise.Presentation) ||
            !Enum.IsDefined(exercise.SideSequence) ||
            !Enum.IsDefined(exercise.DirectionSequence) ||
            (exercise.SideSequence != ExerciseSideSequence.Continuous &&
                exercise.DirectionSequence != ExerciseDirectionSequence.None) ||
            (exercise.DirectionSequence != ExerciseDirectionSequence.None &&
                (exercise.Mode != ExerciseMode.Repetition ||
                    exercise.Presentation != ExercisePresentation.Motion)) ||
            (exercise.Presentation == ExercisePresentation.Still &&
                exercise.Mode != ExerciseMode.Hold) ||
            (exercise.Mode == ExerciseMode.Repetition && exercise.HoldFramePercent != 0) ||
            (exercise.Mode == ExerciseMode.Hold &&
                exercise.HoldFramePercent is <= 0 or > 99));
        bool hasInvalidInitialScore =
            requireInitialScores && exercises.Any(exercise => exercise.Score != 0);
        bool hasInvalidReplacementMetadata = false;
        if (requireInitialScores)
        {
            int[] declaredReplacementIds = exercises
                .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
                .Select(exercise => exercise.Id)
                .Order()
                .ToArray();
            hasInvalidReplacementMetadata =
                !declaredReplacementIds.SequenceEqual(
                    CatalogMigrationRules.ReplacedExerciseIds.Order()) ||
                exercises.Any(exercise =>
                    CatalogMigrationRules.ReplacedExerciseIds.Contains(exercise.Id) !=
                        !string.IsNullOrWhiteSpace(exercise.RetiredName) ||
                    (!string.IsNullOrWhiteSpace(exercise.RetiredName) &&
                        string.Equals(
                            exercise.RetiredName,
                            exercise.Name,
                            StringComparison.Ordinal)));
        }

        if (hasUndersizedWorkoutGroup ||
            exercises.Select(exercise => exercise.Id).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Name).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Video).Distinct().Count() != exercises.Count ||
            violatesRequirements ||
            hasInvalidInitialScore ||
            hasInvalidReplacementMetadata)
        {
            throw new InvalidOperationException(
                "The bundled exercise catalog does not satisfy its required invariants.");
        }
    }

    private sealed class CanonicalAssignments
    {
        public CanonicalMuscleGroup? Primary { get; set; }

        public List<CanonicalMuscleGroup> Secondary { get; } = [];
    }
}
