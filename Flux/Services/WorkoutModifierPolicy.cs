using Flux.Models;

namespace Flux.Services;

public sealed record WorkoutModifierPairCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    WorkoutModifiers FirstModifier,
    bool FirstModifierEnabled,
    WorkoutModifiers SecondModifier,
    bool SecondModifierEnabled,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutProfileLineupDeficiency(
    int Minutes,
    WorkoutModifiers Profile,
    int MaximumDistinctExerciseCount,
    int RequiredDistinctExerciseCount);

public sealed record WorkoutModifierMaterialityDeficiency(
    WorkoutModifiers Modifier,
    WorkoutModifiers ContextProfile,
    int RelaxedExerciseCount,
    int ConstrainedExerciseCount,
    int ReleasedExerciseCount,
    int RequiredReleasedExerciseCount,
    int AffectedBucketCount,
    int RequiredAffectedBucketCount);

public static class WorkoutModifierPolicy
{
    public const int MinimumExercisesPerPairStatePerGroup = 5;
    public const int MinimumReleasedExercises = 5;
    public const int MinimumReleasedExercisePercent = 5;
    public const int MinimumAffectedBucketPercent = 10;

    private const int MaterialityResolutionMinutes = 30;

    private sealed record ModifierRule(
        WorkoutModifiers Flag,
        Func<Exercise, bool> IsReviewed,
        Func<Exercise, bool> IsCompatible);

    private static readonly ModifierRule[] Rules =
    [
        new(
            WorkoutModifiers.Insect,
            exercise => exercise.InsectCompatibility !=
                ExerciseInsectCompatibility.Unreviewed,
            exercise => exercise.InsectCompatibility ==
                ExerciseInsectCompatibility.Compatible),
        new(
            WorkoutModifiers.Silence,
            _ => true,
            exercise => exercise.Silent),
    ];

    private static readonly WorkoutModifiers SupportedModifierMask =
        Rules.Aggregate(
            WorkoutModifiers.None,
            (mask, rule) => mask | rule.Flag);

    private static readonly IReadOnlyList<WorkoutModifiers> ProfilesForValidation =
        Array.AsReadOnly(CreatePairwiseValidationProfiles());

    public static WorkoutModifiers SupportedMask => SupportedModifierMask;

    public static IReadOnlyList<WorkoutModifiers> ValidationProfiles =>
        ProfilesForValidation;

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

    public static IReadOnlyList<WorkoutModifierPairCoverageDeficiency>
        FindPairwiseCoverageDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    GetModifierRulePairs().SelectMany(pair =>
                        GetBooleanStates().SelectMany(firstEnabled =>
                            GetBooleanStates().Select(secondEnabled =>
                            {
                                WorkoutModifiers profile =
                                    (firstEnabled
                                        ? pair.First.Flag
                                        : WorkoutModifiers.None) |
                                    (secondEnabled
                                        ? pair.Second.Flag
                                        : WorkoutModifiers.None);
                                return new
                                {
                                    Minutes = minutes,
                                    Group = group,
                                    FirstRule = pair.First,
                                    FirstEnabled = firstEnabled,
                                    SecondRule = pair.Second,
                                    SecondEnabled = secondEnabled,
                                    Count = exercises
                                        .Where(exercise =>
                                            Rules.All(rule =>
                                                rule.IsReviewed(exercise)) &&
                                            IsSelectable(
                                                exercise,
                                                group,
                                                profile))
                                        .Select(exercise => exercise.Id)
                                        .Distinct()
                                        .Count(),
                                };
                            })))))
            .Where(result => result.Count < MinimumExercisesPerPairStatePerGroup)
            .Select(result => new WorkoutModifierPairCoverageDeficiency(
                result.Minutes,
                result.Group.Id,
                result.Group.DisplayName,
                result.FirstRule.Flag,
                result.FirstEnabled,
                result.SecondRule.Flag,
                result.SecondEnabled,
                result.Count,
                MinimumExercisesPerPairStatePerGroup))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutModifierMaterialityDeficiency>
        FindMaterialityDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyList<WorkoutGroup> buckets = MassGroupingTaxonomy
            .GetResolution(MaterialityResolutionMinutes)
            .Groups;
        Exercise[] reviewedExercises = exercises
            .Where(exercise => Rules.All(rule => rule.IsReviewed(exercise)))
            .ToArray();

        return GetMaterialityEdges()
            .Select(edge =>
            {
                WorkoutModifiers contextProfile = Normalize(edge.ContextProfile);
                WorkoutModifiers constrainedProfile =
                    contextProfile | edge.Rule.Flag;
                HashSet<int> relaxedExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    contextProfile);
                HashSet<int> constrainedExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    constrainedProfile);
                int releasedExerciseCount = relaxedExerciseIds
                    .Except(constrainedExerciseIds)
                    .Count();
                int requiredReleasedExerciseCount = Math.Max(
                    MinimumReleasedExercises,
                    GetPercentageFloor(
                        relaxedExerciseIds.Count,
                        MinimumReleasedExercisePercent));
                int affectedBucketCount = buckets.Count(bucket =>
                    reviewedExercises.Any(exercise =>
                        IsSelectable(exercise, bucket, contextProfile) &&
                        !IsSelectable(exercise, bucket, constrainedProfile)));
                int requiredAffectedBucketCount = GetPercentageFloor(
                    buckets.Count,
                    MinimumAffectedBucketPercent);

                return new WorkoutModifierMaterialityDeficiency(
                    edge.Rule.Flag,
                    contextProfile,
                    relaxedExerciseIds.Count,
                    constrainedExerciseIds.Count,
                    releasedExerciseCount,
                    requiredReleasedExerciseCount,
                    affectedBucketCount,
                    requiredAffectedBucketCount);
            })
            .Where(deficiency =>
                deficiency.ReleasedExerciseCount <
                    deficiency.RequiredReleasedExerciseCount ||
                deficiency.AffectedBucketCount <
                    deficiency.RequiredAffectedBucketCount)
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
                return ValidationProfiles.Select(profile => new
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

    private static WorkoutModifiers[] CreatePairwiseValidationProfiles()
    {
        var profiles = new List<WorkoutModifiers> { WorkoutModifiers.None };
        profiles.AddRange(Rules.Select(rule => rule.Flag));
        profiles.AddRange(GetModifierRulePairs().Select(pair =>
            pair.First.Flag | pair.Second.Flag));
        return profiles.Distinct().ToArray();
    }

    private static IEnumerable<(ModifierRule First, ModifierRule Second)>
        GetModifierRulePairs()
    {
        for (int firstIndex = 0; firstIndex < Rules.Length - 1; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1;
                 secondIndex < Rules.Length;
                 secondIndex++)
            {
                yield return (Rules[firstIndex], Rules[secondIndex]);
            }
        }
    }

    private static IEnumerable<(ModifierRule Rule, WorkoutModifiers ContextProfile)>
        GetMaterialityEdges()
    {
        foreach (ModifierRule rule in Rules)
        {
            yield return (rule, WorkoutModifiers.None);
        }

        foreach ((ModifierRule First, ModifierRule Second) pair in
                 GetModifierRulePairs())
        {
            yield return (pair.First, pair.Second.Flag);
            yield return (pair.Second, pair.First.Flag);
        }
    }

    private static HashSet<int> GetSelectableExerciseIds(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> buckets,
        WorkoutModifiers profile)
    {
        return exercises
            .Where(exercise => buckets.Any(bucket =>
                IsSelectable(exercise, bucket, profile)))
            .Select(exercise => exercise.Id)
            .ToHashSet();
    }

    private static int GetPercentageFloor(int count, int percent)
    {
        return (int)Math.Ceiling(count * percent / 100d);
    }

    private static IEnumerable<bool> GetBooleanStates()
    {
        yield return false;
        yield return true;
    }
}
