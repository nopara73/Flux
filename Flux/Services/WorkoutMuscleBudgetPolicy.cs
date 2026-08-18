using Flux.Models;

namespace Flux.Services;

public static class WorkoutMuscleBudgetPolicy
{
    public const int MaximumLoadHalfUnits = 10;

    public const int PrimaryLoadHalfUnits = 2;

    public const int SecondaryLoadHalfUnits = 1;

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
            result[exercise.PrimaryCanonicalGroup] =
                result.GetValueOrDefault(exercise.PrimaryCanonicalGroup) +
                PrimaryLoadHalfUnits;
            foreach (CanonicalMuscleGroup secondary in
                     exercise.SecondaryCanonicalGroups.Distinct())
            {
                result[secondary] = result.GetValueOrDefault(secondary) +
                    SecondaryLoadHalfUnits;
            }
        }

        return result;
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

        var addedLoad = new Dictionary<CanonicalMuscleGroup, int>
        {
            [exercise.PrimaryCanonicalGroup] = PrimaryLoadHalfUnits,
        };
        foreach (CanonicalMuscleGroup secondary in
                 exercise.SecondaryCanonicalGroups.Distinct())
        {
            addedLoad[secondary] = addedLoad.GetValueOrDefault(secondary) +
                SecondaryLoadHalfUnits;
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
