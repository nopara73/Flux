using Flux.Models;

namespace Flux.Services;

public sealed record WorkoutProfileCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    WorkoutModifiers Profile,
    int SelectableExerciseCount);

public sealed record WorkoutProfileLineupDeficiency(
    int Minutes,
    WorkoutModifiers Profile,
    int MaximumDistinctExerciseCount,
    int RequiredDistinctExerciseCount);

public sealed record WorkoutModifierExclusionDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    WorkoutModifiers Modifier,
    WorkoutModifiers ContextProfile,
    int ExcludedExerciseCount,
    int RequiredExcludedExerciseCount);

public static class WorkoutModifierPolicy
{
    public const int MinimumExcludedExercisesPerGroup = 5;

    private sealed record ModifierRule(
        WorkoutModifiers Flag,
        Func<Exercise, bool> IsReviewed,
        Func<Exercise, bool> IsCompatible,
        bool RequiresExclusionFloor);

    private static readonly ModifierRule[] Rules =
    [
        new(
            WorkoutModifiers.Insect,
            exercise => exercise.InsectCompatibility !=
                ExerciseInsectCompatibility.Unreviewed,
            exercise => exercise.InsectCompatibility ==
                ExerciseInsectCompatibility.Compatible,
            RequiresExclusionFloor: true),
        new(
            WorkoutModifiers.Silence,
            _ => true,
            exercise => exercise.Silent,
            RequiresExclusionFloor: false),
    ];

    private static readonly WorkoutModifiers SupportedModifierMask =
        Rules.Aggregate(
            WorkoutModifiers.None,
            (mask, rule) => mask | rule.Flag);

    private static readonly IReadOnlyList<WorkoutModifiers> Profiles =
        Array.AsReadOnly(CreateSupportedProfiles());

    public static WorkoutModifiers SupportedMask => SupportedModifierMask;

    public static IReadOnlyList<WorkoutModifiers> SupportedProfiles => Profiles;

    public static WorkoutModifiers Normalize(WorkoutModifiers modifiers)
    {
        return modifiers & SupportedModifierMask;
    }

    public static bool IsCatalogMetadataComplete(
        IEnumerable<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return exercises.All(exercise => Rules.All(rule => rule.IsReviewed(exercise)));
    }

    public static bool IsCompatible(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        WorkoutModifiers normalized = Normalize(profile);
        return Rules.All(rule =>
            !normalized.HasFlag(rule.Flag) || rule.IsCompatible(exercise));
    }

    public static bool IsSelectable(
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        return WorkoutCoveragePolicy.IsSelectable(exercise, group) &&
            IsCompatible(exercise, profile);
    }

    public static IReadOnlyList<WorkoutProfileCoverageDeficiency>
        FindCoverageDeficiencies(IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    SupportedProfiles.Select(profile => new
                    {
                        Minutes = minutes,
                        Group = group,
                        Profile = profile,
                        Count = exercises.Count(exercise =>
                            IsSelectable(exercise, group, profile)),
                    })))
            .Where(result =>
                result.Count < WorkoutCoveragePolicy.MinimumSelectableExercisesPerGroup)
            .Select(result => new WorkoutProfileCoverageDeficiency(
                result.Minutes,
                result.Group.Id,
                result.Group.DisplayName,
                result.Profile,
                result.Count))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutProfileLineupDeficiency>
        FindDistinctLineupDeficiencies(IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return ExerciseSessionService.SupportedWorkoutMinutes
            .SelectMany(minutes =>
            {
                IReadOnlyList<WorkoutGroup> groups = MassGroupingTaxonomy
                    .GetResolution(minutes > 30 ? 30 : minutes)
                    .Groups;
                return SupportedProfiles.Select(profile => new
                {
                    Minutes = minutes,
                    Profile = profile,
                    MaximumDistinctExerciseCount = GetMaximumDistinctLineupSize(
                        exercises,
                        groups,
                        profile),
                    RequiredDistinctExerciseCount = groups.Count,
                });
            })
            .Where(result =>
                result.MaximumDistinctExerciseCount <
                    result.RequiredDistinctExerciseCount)
            .Select(result => new WorkoutProfileLineupDeficiency(
                result.Minutes,
                result.Profile,
                result.MaximumDistinctExerciseCount,
                result.RequiredDistinctExerciseCount))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutModifierExclusionDeficiency>
        FindModifierExclusionDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    Rules.Where(rule => rule.RequiresExclusionFloor)
                        .SelectMany(rule => SupportedProfiles
                        .Select(profile => profile & ~rule.Flag)
                        .Distinct()
                        .Select(contextProfile => new
                        {
                            Minutes = minutes,
                            Group = group,
                            Rule = rule,
                            ContextProfile = contextProfile,
                            Count = exercises
                                .Where(exercise =>
                                    WorkoutCoveragePolicy.IsSelectable(exercise, group) &&
                                    IsCompatible(exercise, contextProfile) &&
                                    rule.IsReviewed(exercise) &&
                                    !rule.IsCompatible(exercise))
                                .Select(exercise => exercise.Id)
                                .Distinct()
                                .Count(),
                        }))))
            .Where(result =>
                result.Count < MinimumExcludedExercisesPerGroup)
            .Select(result => new WorkoutModifierExclusionDeficiency(
                result.Minutes,
                result.Group.Id,
                result.Group.DisplayName,
                result.Rule.Flag,
                result.ContextProfile,
                result.Count,
                MinimumExcludedExercisesPerGroup))
            .ToArray();
    }

    public static int GetMaximumDistinctLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(groups);

        int[][] candidateExerciseIdsByGroup = groups
            .Select(group => exercises
                .Where(exercise => IsSelectable(exercise, group, profile))
                .Select(exercise => exercise.Id)
                .Distinct()
                .ToArray())
            .OrderBy(candidateIds => candidateIds.Length)
            .ToArray();
        var assignedGroupByExerciseId = new Dictionary<int, int>();
        int matchedGroupCount = 0;

        for (int groupIndex = 0;
             groupIndex < candidateExerciseIdsByGroup.Length;
             groupIndex++)
        {
            if (TryAssignDistinctExercise(
                    groupIndex,
                    candidateExerciseIdsByGroup,
                    assignedGroupByExerciseId,
                    []))
            {
                matchedGroupCount++;
            }
        }

        return matchedGroupCount;
    }

    private static bool TryAssignDistinctExercise(
        int groupIndex,
        IReadOnlyList<int[]> candidateExerciseIdsByGroup,
        IDictionary<int, int> assignedGroupByExerciseId,
        HashSet<int> visitedExerciseIds)
    {
        foreach (int exerciseId in candidateExerciseIdsByGroup[groupIndex])
        {
            if (!visitedExerciseIds.Add(exerciseId))
            {
                continue;
            }

            if (!assignedGroupByExerciseId.TryGetValue(
                    exerciseId,
                    out int assignedGroupIndex) ||
                TryAssignDistinctExercise(
                    assignedGroupIndex,
                    candidateExerciseIdsByGroup,
                    assignedGroupByExerciseId,
                    visitedExerciseIds))
            {
                assignedGroupByExerciseId[exerciseId] = groupIndex;
                return true;
            }
        }

        return false;
    }

    private static WorkoutModifiers[] CreateSupportedProfiles()
    {
        int profileCount = 1 << Rules.Length;
        var profiles = new WorkoutModifiers[profileCount];
        for (int profileIndex = 0; profileIndex < profileCount; profileIndex++)
        {
            WorkoutModifiers profile = WorkoutModifiers.None;
            for (int ruleIndex = 0; ruleIndex < Rules.Length; ruleIndex++)
            {
                if ((profileIndex & (1 << ruleIndex)) != 0)
                {
                    profile |= Rules[ruleIndex].Flag;
                }
            }

            profiles[profileIndex] = profile;
        }

        return profiles;
    }
}
