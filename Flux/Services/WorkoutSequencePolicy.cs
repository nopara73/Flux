using Flux.Models;

namespace Flux.Services;

public static class WorkoutSequencePolicy
{
    public static Exercise[] GetMembers(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);

        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        var members = new List<Exercise>();
        var seenIds = new HashSet<int>();
        foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
        {
            if (!exercisesById.TryGetValue(block.ExerciseId, out Exercise? member))
            {
                return [];
            }
            if (seenIds.Add(member.Id))
            {
                members.Add(member);
            }
        }

        return members.ToArray();
    }

    public static WorkoutGroup[] GetPrimaryCoverageGroups(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);
        ArgumentNullException.ThrowIfNull(groups);

        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        var coveredGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
        {
            if (!exercisesById.TryGetValue(block.ExerciseId, out Exercise? member))
            {
                return [];
            }

            WorkoutGroup[] primaryGroups = groups
                .Where(group => group.CanonicalGroups.Contains(
                    member.PrimaryCanonicalGroup))
                .ToArray();
            if (primaryGroups.Length != 1)
            {
                return [];
            }
            coveredGroupIds.Add(primaryGroups[0].Id);
        }

        return groups
            .Where(group => coveredGroupIds.Contains(group.Id))
            .OrderBy(group => group.Order)
            .ToArray();
    }

    public static int GetCanonicalCoverage(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Exercise[] members = GetMembers(root, exercisesById);
        return members.Select(member => member.PrimaryCanonicalGroup)
            .Distinct().Count(group.CanonicalGroups.Contains);
    }

    public static WorkoutGroup[][] GetPlacementOptions(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);
        ArgumentNullException.ThrowIfNull(groups);
        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        WorkoutGroup[] primaryGroups = GetPrimaryCoverageGroups(
            root,
            exercisesById,
            groups);
        // One atomic sequence owns exactly the slots trained by its members'
        // primaries. Secondary claims never create an alternative identity or slot.
        return primaryGroups.Length > 0 ? [primaryGroups] : [];
    }
}
