using Flux.Models;

namespace Flux.Data;

public sealed class FakeExerciseDatabase : IExerciseDatabase
{
    public const string SharedGif = "exercise_placeholder.gif";

    private const int ExercisesPerRegion = 10;
    private const int TotalExercises = 100;

    private static readonly IReadOnlyDictionary<DominantRegion, string[]> ExerciseNames =
        new Dictionary<DominantRegion, string[]>
        {
            [DominantRegion.FEET] =
            [
                "Toe Tap Flow",
                "Heel Rock",
                "Arch Lift",
                "Toe Fan",
                "Foot Alphabet",
                "Ball Press",
                "Sole Roll",
                "Instep Pulse",
                "Heel-to-Toe Shift",
                "Single-Foot Balance",
            ],
            [DominantRegion.LEGS] =
            [
                "Air Squat",
                "Reverse Lunge",
                "Side Lunge",
                "Wall Sit",
                "Calf Raise",
                "Standing Hamstring Curl",
                "Knee Drive",
                "Split Squat",
                "Leg Extension",
                "Skater Step",
            ],
            [DominantRegion.HANDS] =
            [
                "Finger Fan",
                "Fist Clench",
                "Thumb Opposition",
                "Wrist-to-Finger Wave",
                "Grip Pulse",
                "Finger Taps",
                "Prayer Press",
                "Pinch Hold",
                "Palm Lift",
                "Knuckle Roll",
            ],
            [DominantRegion.ARMS] =
            [
                "Biceps Curl",
                "Triceps Kickback",
                "Arm Circles",
                "Hammer Curl",
                "Overhead Triceps Reach",
                "Front Arm Raise",
                "Lateral Arm Raise",
                "Boxer Punch",
                "Arm Sweep",
                "Isometric Curl Hold",
            ],
            [DominantRegion.HEAD] =
            [
                "Chin Tuck",
                "Slow Head Turn",
                "Neck Side Tilt",
                "Neck Nod",
                "Jaw Release",
                "Eye Tracking Sweep",
                "Head Circle",
                "Temple Press",
                "Forehead Press",
                "Crown Reach",
            ],
            [DominantRegion.SHOULDERS] =
            [
                "Shoulder Roll",
                "Scapular Squeeze",
                "Overhead Press",
                "Shoulder Shrug",
                "Wall Angel",
                "Y Raise",
                "External Rotation",
                "Cross-Body Reach",
                "Shoulder Tap",
                "Reverse Fly",
            ],
            [DominantRegion.HIPS] =
            [
                "Hip Circle",
                "Glute Bridge",
                "Fire Hydrant",
                "Standing Hip Abduction",
                "Hip Hinge",
                "Clamshell",
                "Donkey Kick",
                "Frog Pump",
                "Lateral Band Walk",
                "Hip Flexor March",
            ],
            [DominantRegion.CHEST] =
            [
                "Push-Up",
                "Knee Push-Up",
                "Chest Fly",
                "Incline Push-Up",
                "Wide Push-Up",
                "Floor Press",
                "Svend Press",
                "Chest Squeeze",
                "Decline Push-Up",
                "Isometric Wall Press",
            ],
            [DominantRegion.BACK] =
            [
                "Bird Dog",
                "Superman Lift",
                "Bent-Over Row",
                "Lat Pulldown",
                "Good Morning",
                "Reverse Snow Angel",
                "Prone Cobra",
                "Renegade Row",
                "Back Extension",
                "Dead Hang",
            ],
            [DominantRegion.CORE] =
            [
                "Forearm Plank",
                "Side Plank",
                "Dead Bug",
                "Bicycle Crunch",
                "Mountain Climber",
                "Russian Twist",
                "Hollow Hold",
                "Heel Touch",
                "Bear Plank",
                "Seated Knee Tuck",
            ],
        };

    public FakeExerciseDatabase()
    {
        Exercises = CreateExercises();
    }

    public IReadOnlyList<Exercise> Exercises { get; }

    private static IReadOnlyList<Exercise> CreateExercises()
    {
        var exercises = new List<Exercise>(TotalExercises);

        foreach (DominantRegion region in Enum.GetValues<DominantRegion>())
        {
            if (!ExerciseNames.TryGetValue(region, out string[]? names) ||
                names.Length != ExercisesPerRegion)
            {
                throw new InvalidOperationException(
                    $"{region} must contain exactly {ExercisesPerRegion} fake exercises.");
            }

            exercises.AddRange(names.Select(name => new Exercise
            {
                Name = name,
                Gif = SharedGif,
                DominantRegion = region,
                Score = 0,
            }));
        }

        Validate(exercises);
        return exercises.AsReadOnly();
    }

    private static void Validate(IReadOnlyCollection<Exercise> exercises)
    {
        if (exercises.Count != TotalExercises)
        {
            throw new InvalidOperationException(
                $"The fake database must contain exactly {TotalExercises} exercises.");
        }

        if (exercises.Any(exercise => exercise.Score != 0))
        {
            throw new InvalidOperationException("Every fake exercise must start at score 0.");
        }

        if (exercises.Select(exercise => exercise.Name).Distinct().Count() != TotalExercises)
        {
            throw new InvalidOperationException("Every fake exercise name must be unique.");
        }

        bool hasInvalidRegionCount = exercises
            .GroupBy(exercise => exercise.DominantRegion)
            .Any(group => group.Count() != ExercisesPerRegion);

        if (hasInvalidRegionCount)
        {
            throw new InvalidOperationException(
                $"Every dominant region must contain exactly {ExercisesPerRegion} exercises.");
        }

        if (exercises.Any(exercise => exercise.Gif != SharedGif))
        {
            throw new InvalidOperationException("Every fake exercise must use the shared GIF.");
        }
    }
}
