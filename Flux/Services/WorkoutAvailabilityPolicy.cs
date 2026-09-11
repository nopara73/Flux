using System.Numerics;
using Flux.Models;

namespace Flux.Services;

public sealed record WorkoutRegionAvailability(string Name, int AvailableTargets, int TotalTargets, bool Included = false);

public sealed class WorkoutUnavailableException(WorkoutAvailability availability)
    : InvalidOperationException(availability.CanStart ? "Limited workout scope needs confirmation." : "No complete workout fits this setup and duration.")
{
    public WorkoutAvailability Availability { get; } = availability;
}

public sealed record WorkoutAvailability(
    int Minutes,
    int ResolutionMinutes,
    IReadOnlyList<WorkoutGroup> Groups,
    IReadOnlyList<string> MissingGroups,
    IReadOnlyList<WorkoutRegionAvailability> Regions,
    bool RequiresAcceptance)
{
    public bool CanStart => Groups.Count > 0;
}

public static class WorkoutAvailabilityPolicy
{
    public static IReadOnlyList<string> FindCatalogViolations(IReadOnlyList<Exercise> exercises)
    {
        var violations = new List<string>();
        if (!WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises)) violations.Add("Unreviewed modifier metadata");
        foreach (CanonicalMuscleGroup primary in Enum.GetValues<CanonicalMuscleGroup>())
        {
            if (!exercises.Any(exercise => exercise.PrimaryCanonicalGroup == primary))
                violations.Add($"No primary movement for {primary}");
        }
        foreach (Exercise exercise in exercises)
        {
            if (!exercise.OnlyFeetTouchGround || !exercise.ShoeAgnostic || exercise.MaxSpaceMeters > 2 ||
                exercise.Equipment is not ("None" or "Mirror"))
                violations.Add($"Exercise {exercise.Id} violates the physical admission contract");
        }
        return violations;
    }

    public static WorkoutAvailability Evaluate(
        IReadOnlyList<Exercise> exercises, int minutes, WorkoutModifiers modifiers)
    {
        if (!ExerciseSessionService.SupportedWorkoutMinutes.Contains(minutes))
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }
        var byId = exercises.ToDictionary(exercise => exercise.Id);
        Exercise[] roots = exercises.Where(root => root.SequenceBlocks.Length > 0 &&
            root.SequenceBlocks.Length <= minutes &&
            WorkoutSequencePolicy.GetMembers(root, byId) is { Length: > 0 } members &&
            members.All(member => WorkoutModifierPolicy.IsCompatible(member, modifiers)))
            .ToArray();
        var primaries = roots.SelectMany(root => WorkoutSequencePolicy.GetMembers(root, byId))
            .Select(member => member.PrimaryCanonicalGroup).ToHashSet();
        WorkoutGroup[] broadGroups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        var regions = broadGroups.Select(group => new WorkoutRegionAvailability(
            group.DisplayName, group.CanonicalGroups.Count(primaries.Contains),
            group.CanonicalGroups.Count)).ToArray();
        int requestedResolution = Math.Min(minutes, 30);
        WorkoutAvailability Result(int resolution, WorkoutGroup[] groups, string[] missing, bool limited) =>
            new(minutes, resolution, groups,
                MassGroupingTaxonomy.GetResolution(30).Groups.Where(group =>
                    !group.CanonicalGroups.Any(primaries.Contains) ||
                    !groups.Any(included => included.CanonicalGroups.Any(group.CanonicalGroups.Contains)))
                    .Select(group => group.DisplayName).ToArray(), regions.Select((region, index) => region with
            {
                Included = groups.Any(group => group.CanonicalGroups.Any(broadGroups[index].CanonicalGroups.Contains)),
            }).ToArray(), limited);
        foreach (int resolution in MassGroupingTaxonomy.SupportedMinutes
                     .Where(value => value <= requestedResolution).OrderDescending())
        {
            WorkoutGroup[] requested = MassGroupingTaxonomy.GetResolution(resolution).Groups.ToArray();
            WorkoutGroup[] available = requested.Where(group =>
                roots.Any(root => WorkoutSequencePolicy.GetPlacementOptions(root, byId, requested)
                    .Any(option => option.Any(covered => covered.Id == group.Id)))).ToArray();
            if (available.Length > 0 && CanFill(roots, byId, available, minutes))
            {
                return Result(resolution, available,
                    requested.Except(available).Select(group => group.DisplayName).ToArray(),
                    resolution != requestedResolution || available.Length != requested.Length);
            }
        }
        // Only the three body regions are combined here: at most seven subsets,
        // independent of modifier count. Never split a sequence to fill a slot.
        foreach (int mask in Enumerable.Range(1, 7)
                     .OrderByDescending(value => BitOperations.PopCount((uint)value)))
        {
            WorkoutGroup[] groups = broadGroups.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            if (CanFill(roots, byId, groups, minutes))
            {
                return Result(3, groups,
                    broadGroups.Except(groups).Select(group => group.DisplayName).ToArray(), true);
            }
        }
        return Result(requestedResolution, [], broadGroups.Select(group => group.DisplayName).ToArray(), true);
    }

    private static bool CanFill(Exercise[] roots, IReadOnlyDictionary<int, Exercise> byId,
        WorkoutGroup[] groups, int minutes)
    {
        var candidates = new List<AtomicSequenceCandidate>();
        foreach (Exercise root in roots)
        {
            foreach (WorkoutGroup[] option in WorkoutSequencePolicy.GetPlacementOptions(root, byId, groups))
            {
                ulong mask = 0;
                var utilities = new BigInteger[groups.Length];
                foreach (WorkoutGroup group in option)
                {
                    int index = Array.IndexOf(groups, group);
                    mask |= 1UL << index;
                    utilities[index] = BigInteger.One;
                }
                candidates.Add(new(root.Id, WorkoutModifierPolicy.GetSessionMovementId(root), mask,
                    root.SequenceBlocks.Length, utilities, root.Id));
            }
        }
        return candidates.Count > 0 && AtomicSequenceLineupSolver.Solve(groups.Length, minutes, candidates) is not null;
    }
}
