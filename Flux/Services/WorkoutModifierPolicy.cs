using System.Numerics;
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
    MirrorEquipment MirrorEquipment,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutHardFloorCategoryCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    ExerciseHardFloorCompatibility HardFloorCompatibility,
    WorkoutModifiers PartnerModifier,
    bool PartnerModifierEnabled,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutMirrorCategoryDeficiency(
    ExerciseMirrorRelationship Relationship,
    ExerciseMirrorCoverage MinimumCoverage,
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
    WorkoutModifiers ModifiedProfile,
    int BaselineExerciseCount,
    int ModifiedExerciseCount,
    int MaterialExerciseCount,
    int RequiredMaterialExerciseCount,
    int AffectedBucketCount,
    int RequiredAffectedBucketCount);

public static class WorkoutModifierPolicy
{
    public const int MinimumExercisesPerPairStatePerGroup = 5;
    public const int MinimumExercisesPerMirrorCategory = 5;
    public const int MinimumMaterialExercises = 5;
    public const int MinimumMaterialExercisePercent = 5;
    public const int MinimumAffectedBucketPercent = 10;

    private const int MaterialityResolutionMinutes = 30;

    private sealed record ModifierRule(
        WorkoutModifiers Flag,
        Func<Exercise, bool> IsReviewed,
        Func<Exercise, WorkoutModifiers, bool> IsCompatibleForProfile);

    private static readonly ModifierRule[] Rules =
    [
        new(
            WorkoutModifiers.HardFloor,
            exercise => exercise.HardFloorCompatibility !=
                ExerciseHardFloorCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.HardFloor) ||
                exercise.HardFloorCompatibility ==
                    ExerciseHardFloorCompatibility.Compatible),
        new(
            WorkoutModifiers.Insect,
            exercise => exercise.InsectCompatibility !=
                ExerciseInsectCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Insect) ||
                exercise.InsectCompatibility ==
                    ExerciseInsectCompatibility.Compatible),
        new(
            WorkoutModifiers.Silence,
            _ => true,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Silence) || exercise.Silent),
        new(
            WorkoutModifiers.Mirror,
            IsMirrorMetadataReviewed,
            IsMirrorCompatible),
    ];

    private static readonly WorkoutModifiers SupportedModifierMask =
        Rules.Aggregate(
            WorkoutModifiers.TallMirror,
            (mask, rule) => mask | rule.Flag);

    private static readonly IReadOnlyList<WorkoutModifiers> ProfilesForValidation =
        Array.AsReadOnly(CreatePairwiseValidationProfiles());

    public static WorkoutModifiers SupportedMask => SupportedModifierMask;

    public static IReadOnlyList<WorkoutModifiers> ValidationProfiles =>
        ProfilesForValidation;

    public static int GetSessionMovementId(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.SessionMovementId > 0
            ? exercise.SessionMovementId
            : exercise.Id;
    }

    public static WorkoutModifiers Normalize(WorkoutModifiers modifiers)
    {
        WorkoutModifiers normalized = modifiers & SupportedModifierMask;
        if (!normalized.HasFlag(WorkoutModifiers.Mirror))
        {
            normalized &= ~WorkoutModifiers.TallMirror;
        }

        return normalized;
    }

    public static MirrorEquipment GetMirrorEquipment(WorkoutModifiers profile)
    {
        WorkoutModifiers normalized = Normalize(profile);
        if (!normalized.HasFlag(WorkoutModifiers.Mirror))
        {
            return MirrorEquipment.None;
        }

        return normalized.HasFlag(WorkoutModifiers.TallMirror)
            ? MirrorEquipment.Tall
            : MirrorEquipment.Compact;
    }

    public static WorkoutModifiers WithMirrorEquipment(
        WorkoutModifiers profile,
        MirrorEquipment equipment)
    {
        if (!Enum.IsDefined(equipment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null);
        }

        WorkoutModifiers withoutMirror = Normalize(profile) &
            ~(WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror);
        return equipment switch
        {
            MirrorEquipment.None => withoutMirror,
            MirrorEquipment.Compact => withoutMirror | WorkoutModifiers.Mirror,
            MirrorEquipment.Tall => withoutMirror |
                WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };
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
            rule.IsCompatibleForProfile(exercise, normalized));
    }

    public static bool IsMirrorRelevant(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.MirrorRelationship is
            ExerciseMirrorRelationship.MirrorOnly or
            ExerciseMirrorRelationship.BenefitsGreatly;
    }

    public static bool IsMirrorPreferred(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        MirrorEquipment equipment = GetMirrorEquipment(profile);
        if (equipment == MirrorEquipment.None)
        {
            return false;
        }

        return exercise.MirrorRelationship switch
        {
            ExerciseMirrorRelationship.MirrorOnly =>
                IsMirrorCompatible(exercise, Normalize(profile)),
            ExerciseMirrorRelationship.BenefitsGreatly =>
                exercise.MinimumMirrorCoverage ==
                    ExerciseMirrorCoverage.UpperBody ||
                equipment == MirrorEquipment.Tall,
            _ => false,
        };
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
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    GetModifierRulePairs().SelectMany(pair =>
                        GetRuleStateProfiles(pair.First).SelectMany(firstState =>
                            GetRuleStateProfiles(pair.Second).Select(secondState =>
                            {
                                WorkoutModifiers profile = Normalize(
                                    firstState | secondState);
                                bool requiresMirrorRelevance =
                                    GetMirrorEquipment(profile) !=
                                        MirrorEquipment.None;
                                return new
                                {
                                    Minutes = minutes,
                                    Group = group,
                                    FirstRule = pair.First,
                                    FirstEnabled = firstState !=
                                        WorkoutModifiers.None,
                                    SecondRule = pair.Second,
                                    SecondEnabled = secondState !=
                                        WorkoutModifiers.None,
                                    MirrorEquipment = GetMirrorEquipment(profile),
                                    Count = exercises
                                        .Where(exercise =>
                                            Rules.All(rule =>
                                                rule.IsReviewed(exercise)) &&
                                            IsSequenceUnitEligible(
                                                exercise,
                                                exercisesById,
                                                group,
                                                profile) &&
                                            (!requiresMirrorRelevance ||
                                                IsMirrorRelevant(exercise)))
                                        .Select(GetSessionMovementId)
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
                result.MirrorEquipment,
                result.Count,
                MinimumExercisesPerPairStatePerGroup))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutHardFloorCategoryCoverageDeficiency>
        FindHardFloorCategoryCoverageDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        ExerciseHardFloorCompatibility[] requiredCategories =
        [
            ExerciseHardFloorCompatibility.Compatible,
            ExerciseHardFloorCompatibility.Incompatible,
        ];
        (WorkoutModifiers Modifier, bool Enabled)[] partnerStates =
        [
            (WorkoutModifiers.Insect, false),
            (WorkoutModifiers.Insect, true),
            (WorkoutModifiers.Silence, false),
            (WorkoutModifiers.Silence, true),
            (WorkoutModifiers.Mirror, false),
        ];

        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    requiredCategories.SelectMany(category =>
                        partnerStates.Select(partnerState =>
                        {
                            WorkoutModifiers profile = category ==
                                ExerciseHardFloorCompatibility.Compatible
                                    ? WorkoutModifiers.HardFloor
                                    : WorkoutModifiers.None;
                            if (partnerState.Enabled)
                            {
                                profile |= partnerState.Modifier;
                            }
                            profile = Normalize(profile);

                            int matchingExerciseCount = exercises
                                .Where(exercise =>
                                    exercise.HardFloorCompatibility == category &&
                                    IsSequenceHardFloorCategory(
                                        exercise,
                                        exercisesById,
                                        category) &&
                                    IsSequenceUnitEligible(
                                        exercise,
                                        exercisesById,
                                        group,
                                        profile))
                                .Select(GetSessionMovementId)
                                .Distinct()
                                .Count();
                            return new WorkoutHardFloorCategoryCoverageDeficiency(
                                minutes,
                                group.Id,
                                group.DisplayName,
                                category,
                                partnerState.Modifier,
                                partnerState.Enabled,
                                matchingExerciseCount,
                                MinimumExercisesPerPairStatePerGroup);
                        }))))
            .Where(deficiency =>
                deficiency.MatchingExerciseCount <
                    deficiency.RequiredExerciseCount)
            .ToArray();
    }

    public static IReadOnlyList<WorkoutMirrorCategoryDeficiency>
        FindMirrorCategoryDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        (ExerciseMirrorRelationship Relationship,
            ExerciseMirrorCoverage Coverage)[] requiredCategories =
        [
            (ExerciseMirrorRelationship.MirrorOnly,
                ExerciseMirrorCoverage.UpperBody),
            (ExerciseMirrorRelationship.MirrorOnly,
                ExerciseMirrorCoverage.FullBody),
            (ExerciseMirrorRelationship.BenefitsGreatly,
                ExerciseMirrorCoverage.UpperBody),
            (ExerciseMirrorRelationship.BenefitsGreatly,
                ExerciseMirrorCoverage.FullBody),
            (ExerciseMirrorRelationship.Agnostic,
                ExerciseMirrorCoverage.None),
        ];

        return requiredCategories
            .Select(category => new WorkoutMirrorCategoryDeficiency(
                category.Relationship,
                category.Coverage,
                exercises
                    .Where(exercise =>
                        IsMirrorMetadataReviewed(exercise) &&
                        exercise.MirrorRelationship == category.Relationship &&
                        exercise.MinimumMirrorCoverage == category.Coverage)
                    .Select(GetSessionMovementId)
                    .Distinct()
                    .Count(),
                MinimumExercisesPerMirrorCategory))
            .Where(deficiency =>
                deficiency.MatchingExerciseCount <
                    deficiency.RequiredExerciseCount)
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
        IReadOnlyDictionary<int, Exercise> reviewedExercisesById =
            reviewedExercises.ToDictionary(exercise => exercise.Id);

        return GetMaterialityEdges()
            .Select(edge =>
            {
                WorkoutModifiers contextProfile = Normalize(edge.ContextProfile);
                WorkoutModifiers modifiedProfile = Normalize(
                    contextProfile | edge.EnabledStateProfile);
                HashSet<int> baselineExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    contextProfile);
                HashSet<int> modifiedExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    modifiedProfile);
                bool isMirror = edge.Rule.Flag == WorkoutModifiers.Mirror;
                HashSet<int> materialExerciseIds = isMirror
                    ? reviewedExercises
                        .Where(exercise =>
                            IsMirrorPreferred(exercise, modifiedProfile) &&
                            buckets.Any(bucket => IsSequenceUnitEligible(
                                exercise,
                                reviewedExercisesById,
                                bucket,
                                modifiedProfile)))
                        .Select(GetSessionMovementId)
                        .ToHashSet()
                    : baselineExerciseIds
                        .Except(modifiedExerciseIds)
                        .ToHashSet();
                int requiredMaterialExerciseCount = Math.Max(
                    MinimumMaterialExercises,
                    GetPercentageFloor(
                        isMirror
                            ? modifiedExerciseIds.Count
                            : baselineExerciseIds.Count,
                        MinimumMaterialExercisePercent));
                int affectedBucketCount = buckets.Count(bucket => isMirror
                    ? reviewedExercises.Any(exercise =>
                        IsMirrorPreferred(exercise, modifiedProfile) &&
                        IsSequenceUnitEligible(
                            exercise,
                            reviewedExercisesById,
                            bucket,
                            modifiedProfile))
                    : GetSelectableExerciseIds(
                            reviewedExercises,
                            [bucket],
                            contextProfile)
                        .Except(GetSelectableExerciseIds(
                            reviewedExercises,
                            [bucket],
                            modifiedProfile))
                        .Any());
                int requiredAffectedBucketCount = GetPercentageFloor(
                    buckets.Count,
                    MinimumAffectedBucketPercent);

                return new WorkoutModifierMaterialityDeficiency(
                    edge.Rule.Flag,
                    contextProfile,
                    modifiedProfile,
                    baselineExerciseIds.Count,
                    modifiedExerciseIds.Count,
                    materialExerciseIds.Count,
                    requiredMaterialExerciseCount,
                    affectedBucketCount,
                    requiredAffectedBucketCount);
            })
            .Where(deficiency =>
                deficiency.MaterialExerciseCount <
                    deficiency.RequiredMaterialExerciseCount ||
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
                        profile,
                        minutes),
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
        WorkoutModifiers profile,
        int? workoutMinutes = null)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(groups);
        int availableBlocks = workoutMinutes ?? groups.Count;
        if (availableBlocks < groups.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(workoutMinutes));
        }

        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        int oneBlockLineupSize = GetMaximumOneBlockLineupSize(
            exercises,
            exercisesById,
            groups,
            profile);
        if (oneBlockLineupSize == groups.Count)
        {
            return groups.Count;
        }

        var groupIndexById = groups
            .Select((group, index) => (group.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index);
        var candidates = new List<AtomicSequenceCandidate>();
        foreach (Exercise exercise in exercises.Where(exercise =>
                     IsSequenceCompatible(exercise, exercisesById, profile)))
        {
            foreach (WorkoutGroup[] placement in
                     WorkoutSequencePolicy.GetPlacementOptions(
                         exercise,
                         exercisesById,
                         groups))
            {
                if (exercise.SequenceBlocks.Length +
                        (groups.Count - placement.Length) > availableBlocks)
                {
                    continue;
                }
                ulong coverageMask = placement.Aggregate(
                    0UL,
                    (mask, group) => mask | 1UL << groupIndexById[group.Id]);
                var utilities = new BigInteger[groups.Count];
                foreach (WorkoutGroup coveredGroup in placement)
                {
                    utilities[groupIndexById[coveredGroup.Id]] = BigInteger.One;
                }
                candidates.Add(new AtomicSequenceCandidate(
                    exercise.Id,
                    GetSessionMovementId(exercise),
                    coverageMask,
                    exercise.SequenceBlocks.Length,
                    utilities,
                    exercise.Id));
            }
        }

        // Empty one-block placements make the solver return the maximum number
        // of real muscle slots that can be filled atomically, rather than only
        // answering whether a complete lineup exists.
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var utilities = new BigInteger[groups.Count];
            candidates.Add(new AtomicSequenceCandidate(
                int.MinValue + groupIndex,
                int.MinValue + groupIndex,
                1UL << groupIndex,
                1,
                utilities,
                int.MaxValue - groupIndex));
        }

        AtomicSequenceLineup lineup = AtomicSequenceLineupSolver.Solve(
            groups.Count,
            availableBlocks,
            candidates) ?? throw new InvalidOperationException(
                "Atomic lineup validation could not place empty muscle slots.");
        return lineup.ExerciseIdByGroupIndex.Values.Count(exerciseId =>
            exerciseId > 0);
    }

    private static int GetMaximumOneBlockLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile)
    {
        int[][] candidateMovementIdsByGroup = groups
            .Select(group => exercises
                .Where(exercise =>
                    exercise.SequenceBlocks.Length == 1 &&
                    IsSequenceCompatible(exercise, exercisesById, profile) &&
                    WorkoutSequencePolicy.GetPlacementOptions(
                        exercise,
                        exercisesById,
                        groups).Any(option =>
                            option.Length == 1 && option[0].Id == group.Id))
                .Select(GetSessionMovementId)
                .Distinct()
                .ToArray())
            .OrderBy(candidateIds => candidateIds.Length)
            .ToArray();
        var assignedGroupByMovementId = new Dictionary<int, int>();
        int matchedGroupCount = 0;
        for (int groupIndex = 0;
             groupIndex < candidateMovementIdsByGroup.Length;
             groupIndex++)
        {
            if (TryAssignDistinctOneBlockMovement(
                    groupIndex,
                    candidateMovementIdsByGroup,
                    assignedGroupByMovementId,
                    []))
            {
                matchedGroupCount++;
            }
        }
        return matchedGroupCount;
    }

    private static bool TryAssignDistinctOneBlockMovement(
        int groupIndex,
        IReadOnlyList<int[]> candidateMovementIdsByGroup,
        IDictionary<int, int> assignedGroupByMovementId,
        HashSet<int> visitedMovementIds)
    {
        foreach (int movementId in candidateMovementIdsByGroup[groupIndex])
        {
            if (!visitedMovementIds.Add(movementId))
            {
                continue;
            }
            if (!assignedGroupByMovementId.TryGetValue(
                    movementId,
                    out int assignedGroupIndex) ||
                TryAssignDistinctOneBlockMovement(
                    assignedGroupIndex,
                    candidateMovementIdsByGroup,
                    assignedGroupByMovementId,
                    visitedMovementIds))
            {
                assignedGroupByMovementId[movementId] = groupIndex;
                return true;
            }
        }
        return false;
    }

    private static bool IsSequenceCompatible(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutModifiers profile)
    {
        if (exercise.SequenceBlocks.Length == 0)
        {
            return false;
        }

        return exercise.SequenceBlocks
            .Select(block => exercisesById.GetValueOrDefault(block.ExerciseId))
            .Where(member => member is not null)
            .DistinctBy(member => member!.Id)
            .Count() == exercise.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Count() &&
            exercise.SequenceBlocks.All(block =>
                exercisesById.TryGetValue(block.ExerciseId, out Exercise? member) &&
                Rules.All(rule => rule.IsReviewed(member)) &&
                IsCompatible(member, profile));
    }

    private static bool IsSequenceUnitEligible(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        if (exercise.SequenceBlocks.Length == 0 ||
            WorkoutSequencePolicy.GetCanonicalCoverage(
                exercise,
                exercisesById,
                group) < WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group))
        {
            return false;
        }

        return IsSequenceCompatible(exercise, exercisesById, profile);
    }

    private static bool IsSequenceHardFloorCategory(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        ExerciseHardFloorCompatibility category)
    {
        return exercise.SequenceBlocks.Length > 0 &&
            exercise.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .All(exerciseId =>
                    exercisesById.TryGetValue(exerciseId, out Exercise? member) &&
                    member.HardFloorCompatibility == category);
    }

    private static WorkoutModifiers[] CreatePairwiseValidationProfiles()
    {
        var profiles = new List<WorkoutModifiers> { WorkoutModifiers.None };
        profiles.AddRange(Rules.SelectMany(GetRuleStateProfiles));
        profiles.AddRange(GetModifierRulePairs().SelectMany(pair =>
            GetRuleStateProfiles(pair.First)
                .SelectMany(firstState => GetRuleStateProfiles(pair.Second)
                    .Select(secondState => Normalize(firstState | secondState)))));
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

    private static bool IsMirrorMetadataReviewed(Exercise exercise)
    {
        return exercise.MirrorRelationship switch
        {
            ExerciseMirrorRelationship.MirrorOnly =>
                string.Equals(
                    exercise.Equipment,
                    "Mirror",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage is
                    ExerciseMirrorCoverage.UpperBody or
                    ExerciseMirrorCoverage.FullBody,
            ExerciseMirrorRelationship.BenefitsGreatly =>
                string.Equals(
                    exercise.Equipment,
                    "None",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage is
                    ExerciseMirrorCoverage.UpperBody or
                    ExerciseMirrorCoverage.FullBody,
            ExerciseMirrorRelationship.Agnostic =>
                string.Equals(
                    exercise.Equipment,
                    "None",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None,
            _ => false,
        };
    }

    private static bool IsMirrorCompatible(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        if (exercise.MirrorRelationship != ExerciseMirrorRelationship.MirrorOnly)
        {
            return true;
        }

        return GetMirrorEquipment(profile) switch
        {
            MirrorEquipment.None => false,
            MirrorEquipment.Compact =>
                exercise.MinimumMirrorCoverage ==
                    ExerciseMirrorCoverage.UpperBody,
            MirrorEquipment.Tall => true,
            _ => false,
        };
    }

    private static IEnumerable<WorkoutModifiers> GetRuleStateProfiles(
        ModifierRule rule)
    {
        yield return WorkoutModifiers.None;
        yield return rule.Flag;
        if (rule.Flag == WorkoutModifiers.Mirror)
        {
            yield return WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror;
        }
    }

    private static IEnumerable<(
        ModifierRule Rule,
        WorkoutModifiers ContextProfile,
        WorkoutModifiers EnabledStateProfile)>
        GetMaterialityEdges()
    {
        foreach (ModifierRule rule in Rules)
        {
            foreach (WorkoutModifiers enabledState in
                     GetRuleStateProfiles(rule).Where(state =>
                         state != WorkoutModifiers.None))
            {
                yield return (rule, WorkoutModifiers.None, enabledState);
            }
        }

        foreach ((ModifierRule First, ModifierRule Second) pair in
                 GetModifierRulePairs())
        {
            foreach (WorkoutModifiers firstEnabledState in
                     GetRuleStateProfiles(pair.First).Where(state =>
                         state != WorkoutModifiers.None))
            {
                foreach (WorkoutModifiers secondEnabledState in
                         GetRuleStateProfiles(pair.Second).Where(state =>
                             state != WorkoutModifiers.None))
                {
                    yield return (
                        pair.First,
                        secondEnabledState,
                        firstEnabledState);
                    yield return (
                        pair.Second,
                        firstEnabledState,
                        secondEnabledState);
                }
            }
        }
    }

    private static HashSet<int> GetSelectableExerciseIds(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> buckets,
        WorkoutModifiers profile)
    {
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        return exercises
            .Where(exercise => buckets.Any(bucket =>
                IsSequenceUnitEligible(
                    exercise,
                    exercisesById,
                    bucket,
                    profile)))
            .Select(GetSessionMovementId)
            .ToHashSet();
    }

    private static int GetPercentageFloor(int count, int percent)
    {
        return (int)Math.Ceiling(count * percent / 100d);
    }

}
