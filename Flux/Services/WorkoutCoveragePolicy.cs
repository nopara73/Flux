using Flux.Models;

namespace Flux.Services;

public static class WorkoutCoveragePolicy
{
    public static int GetCanonicalCoverage(
        Exercise exercise,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return IsPrimaryForGroup(exercise, group) ? 1 : 0;
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

        return 1;
    }

    public static bool IsSelectable(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return IsPrimaryForGroup(exercise, group);
    }

    public static bool IsPrimaryForGroup(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup);
    }
}
