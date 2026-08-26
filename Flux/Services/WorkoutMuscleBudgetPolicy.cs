using Flux.Models;

namespace Flux.Services;

public static class WorkoutMuscleBudgetPolicy
{
    public const int MaximumLoadHalfUnits = 10;

    public const int ModeratePrimaryLoadHalfUnits = 1;

    public const int HardPrimaryLoadHalfUnits = 2;

    public const int HardSecondaryLoadHalfUnits = 1;

    public const int ScoreHalfUnitsPerVote = 2;

    public const int MaximumRebalancePasses = 12;

    public static IReadOnlyDictionary<CanonicalMuscleGroup, int> CalculateLoadHalfUnits(
        IEnumerable<Exercise> scheduledExercises)
    {
        ArgumentNullException.ThrowIfNull(scheduledExercises);

        var result = new Dictionary<CanonicalMuscleGroup, int>();
        foreach (Exercise exercise in scheduledExercises)
        {
            ArgumentNullException.ThrowIfNull(exercise);
            int primaryLoadHalfUnits = GetPrimaryLoadHalfUnits(exercise);
            if (primaryLoadHalfUnits > 0)
            {
                result[exercise.PrimaryCanonicalGroup] =
                    result.GetValueOrDefault(exercise.PrimaryCanonicalGroup) +
                    primaryLoadHalfUnits;
            }

            int secondaryLoadHalfUnits = GetSecondaryLoadHalfUnits(exercise);
            if (secondaryLoadHalfUnits > 0)
            {
                foreach (CanonicalMuscleGroup secondary in
                         exercise.SecondaryCanonicalGroups.Distinct())
                {
                    result[secondary] = result.GetValueOrDefault(secondary) +
                        secondaryLoadHalfUnits;
                }
            }
        }

        return result;
    }

    public static int GetPrimaryLoadHalfUnits(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return exercise.MuscularDemand switch
        {
            Exercise.MinimumMuscularDemand => 0,
            Exercise.ModerateMuscularDemand => ModeratePrimaryLoadHalfUnits,
            Exercise.MaximumMuscularDemand => HardPrimaryLoadHalfUnits,
            _ => throw new ArgumentOutOfRangeException(
                nameof(exercise.MuscularDemand),
                exercise.MuscularDemand,
                "Muscular demand must be between 0 and 2."),
        };
    }

    public static int GetSecondaryLoadHalfUnits(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return exercise.MuscularDemand switch
        {
            Exercise.MinimumMuscularDemand => 0,
            Exercise.ModerateMuscularDemand => 0,
            Exercise.MaximumMuscularDemand => HardSecondaryLoadHalfUnits,
            _ => throw new ArgumentOutOfRangeException(
                nameof(exercise.MuscularDemand),
                exercise.MuscularDemand,
                "Muscular demand must be between 0 and 2."),
        };
    }

    public static int GetTemporaryDownvoteHalfUnits(
        IReadOnlyDictionary<CanonicalMuscleGroup, int> loadHalfUnits,
        IEnumerable<CanonicalMuscleGroup> candidateMuscleGroups)
    {
        ArgumentNullException.ThrowIfNull(loadHalfUnits);
        ArgumentNullException.ThrowIfNull(candidateMuscleGroups);

        return candidateMuscleGroups
            .Distinct()
            .Sum(group => Math.Max(
                0,
                loadHalfUnits.GetValueOrDefault(group) - MaximumLoadHalfUnits));
    }

    public static int GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
        IReadOnlyDictionary<CanonicalMuscleGroup, int> existingLoadHalfUnits,
        Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(existingLoadHalfUnits);
        ArgumentNullException.ThrowIfNull(exercise);

        var addedLoad = new Dictionary<CanonicalMuscleGroup, int>();
        int primaryLoadHalfUnits = GetPrimaryLoadHalfUnits(exercise);
        if (primaryLoadHalfUnits > 0)
        {
            addedLoad[exercise.PrimaryCanonicalGroup] = primaryLoadHalfUnits;
        }

        int secondaryLoadHalfUnits = GetSecondaryLoadHalfUnits(exercise);
        if (secondaryLoadHalfUnits > 0)
        {
            foreach (CanonicalMuscleGroup secondary in
                     exercise.SecondaryCanonicalGroups.Distinct())
            {
                addedLoad[secondary] = addedLoad.GetValueOrDefault(secondary) +
                    secondaryLoadHalfUnits;
            }
        }

        return addedLoad.Sum(entry => Math.Max(
            0,
            existingLoadHalfUnits.GetValueOrDefault(entry.Key) + entry.Value -
                MaximumLoadHalfUnits));
    }

    public static long GetAdjustedScoreHalfUnits(
        int savedScore,
        int temporaryDownvoteHalfUnits) =>
        (long)savedScore * ScoreHalfUnitsPerVote - temporaryDownvoteHalfUnits;
}
