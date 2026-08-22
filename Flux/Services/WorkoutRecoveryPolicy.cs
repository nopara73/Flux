using Flux.Models;

namespace Flux.Services;

public enum HardExerciseRotationStatus
{
    RecoveringHard,
    Neutral,
    FreshHard,
}

public static class WorkoutRecoveryPolicy
{
    public const int HardMuscularDemand = Exercise.MaximumMuscularDemand;

    public const long HardRecoveryWindowMilliseconds =
        36L * 60L * 60L * 1000L;

    public static bool IsHardExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.MuscularDemand == HardMuscularDemand;
    }

    public static long GetLastHardWorkUnixMilliseconds(
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle)
    {
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        return lastHardWorkByPrimaryMuscle.GetValueOrDefault(
            primaryMuscle.ToString());
    }

    public static bool IsPrimaryMuscleRecovering(
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle,
        long nowUnixMilliseconds)
    {
        long lastHardWorkUnixMilliseconds = GetLastHardWorkUnixMilliseconds(
            lastHardWorkByPrimaryMuscle,
            primaryMuscle);
        if (lastHardWorkUnixMilliseconds <= 0)
        {
            return false;
        }

        long elapsedMilliseconds = nowUnixMilliseconds -
            lastHardWorkUnixMilliseconds;
        return elapsedMilliseconds < HardRecoveryWindowMilliseconds;
    }

    public static HardExerciseRotationStatus GetRotationStatus(
        Exercise exercise,
        WorkoutGroup group,
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);

        if (!IsHardExercise(exercise))
        {
            return HardExerciseRotationStatus.Neutral;
        }
        if (IsPrimaryMuscleRecovering(
                lastHardWorkByPrimaryMuscle,
                exercise.PrimaryCanonicalGroup,
                nowUnixMilliseconds))
        {
            return HardExerciseRotationStatus.RecoveringHard;
        }

        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup)
            ? HardExerciseRotationStatus.FreshHard
            : HardExerciseRotationStatus.Neutral;
    }

    public static void RecordCompletedHardExercise(
        IDictionary<string, long> lastHardWorkByPrimaryMuscle,
        Exercise exercise,
        long completedAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(exercise);

        if (!IsHardExercise(exercise) || completedAtUnixMilliseconds <= 0)
        {
            return;
        }

        string primaryMuscle = exercise.PrimaryCanonicalGroup.ToString();
        _ = lastHardWorkByPrimaryMuscle.TryGetValue(
            primaryMuscle,
            out long previousCompletion);
        lastHardWorkByPrimaryMuscle[primaryMuscle] = Math.Max(
            completedAtUnixMilliseconds,
            previousCompletion);
    }
}
