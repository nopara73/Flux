using Flux.Models;

namespace Flux.Services;

public static class WorkoutCoveragePolicy
{
    public const int MinimumCoveragePercent = 50;
    public const int MinimumSelectableExercisesPerGroup = 10;

    public static int GetCanonicalCoverage(
        Exercise exercise,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return group.CanonicalGroups.Count(exercise.Trains);
    }

    public static int GetRequiredCanonicalCoverage(WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.CanonicalGroups.Count == 0)
        {
            throw new ArgumentException(
                "A workout group must contain at least one canonical group.",
                nameof(group));
        }

        return (group.CanonicalGroups.Count * MinimumCoveragePercent + 99) / 100;
    }

    public static bool IsSelectable(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) &&
            GetCanonicalCoverage(exercise, group) >=
                GetRequiredCanonicalCoverage(group);
    }
}
