using Flux.Models;

namespace Flux.Services;

public static class WorkoutModifierPolicy
{
    public const int BroadCoverageResolutionMinutes = 3;


    // Insect mode needs visible continuous whole-body movement. Pelvic-floor
    // isolation cannot honestly meet that contract under Flux's feet-only
    // rules. Intrinsic-hand work can meet it only when a wall is available.
    // Keep these exceptions anatomical and exact instead of inventing
    // secondary claims or artificial marching variants to make a quota pass.
    private static readonly HashSet<CanonicalMuscleGroup>
        InsectFineCoverageExceptions =
    [
        CanonicalMuscleGroup.PelvicFloorAndPerineum,
    ];

    private static readonly HashSet<CanonicalMuscleGroup>
        WallFreeInsectFineCoverageExceptions =
    [
        CanonicalMuscleGroup.IntrinsicHand,
    ];

    private sealed record ModifierRule(
        WorkoutModifiers Flag,
        Func<Exercise, bool> IsReviewed,
        Func<Exercise, WorkoutModifiers, bool> IsCompatibleForProfile);

    private static readonly ModifierRule[] Rules =
    [
        new(
            WorkoutModifiers.UpperBodyClothing,
            exercise => exercise.UpperBodyClothingRequirement !=
                ExerciseUpperBodyClothingRequirement.Unreviewed,
            (exercise, profile) => exercise.UpperBodyClothingRequirement switch
            {
                ExerciseUpperBodyClothingRequirement.ClothingRequired =>
                    profile.HasFlag(WorkoutModifiers.UpperBodyClothing),
                ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired =>
                    !profile.HasFlag(WorkoutModifiers.UpperBodyClothing),
                ExerciseUpperBodyClothingRequirement.Agnostic => true,
                _ => false,
            }),
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
            WorkoutModifiers.Shy,
            exercise => exercise.ShyCompatibility !=
                ExerciseShyCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Shy) ||
                exercise.ShyCompatibility ==
                    ExerciseShyCompatibility.Compatible),
        new(
            WorkoutModifiers.Mirror,
            IsMirrorMetadataReviewed,
            IsMirrorCompatible),
    ];

    private static readonly WorkoutModifiers SupportedModifierMask =
        Rules.Aggregate(
            WorkoutModifiers.TallMirror |
                WorkoutModifiers.Wall |
                WorkoutModifiers.SoleWallContact |
                WorkoutModifiers.Light,
            (mask, rule) => mask | rule.Flag);

    private static readonly IReadOnlyList<WorkoutModifiers> ProfilesForValidation =
        Array.AsReadOnly(CreatePairwiseValidationProfiles());

    public static WorkoutModifiers SupportedMask => SupportedModifierMask;

    public static IReadOnlyList<WorkoutModifiers> ValidationProfiles =>
        ProfilesForValidation;

    public static WorkoutModifiers GetPersistentSetupModifiers(
        WorkoutModifiers modifiers) =>
        Normalize(modifiers) & ~WorkoutModifiers.Light;

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
        if (!normalized.HasFlag(WorkoutModifiers.Wall))
        {
            normalized &= ~WorkoutModifiers.SoleWallContact;
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

    public static WallEquipment GetWallEquipment(WorkoutModifiers profile)
    {
        WorkoutModifiers normalized = Normalize(profile);
        if (!normalized.HasFlag(WorkoutModifiers.Wall))
        {
            return WallEquipment.None;
        }

        return normalized.HasFlag(WorkoutModifiers.SoleWallContact)
            ? WallEquipment.SolesMayTouch
            : WallEquipment.SolesStayOff;
    }

    public static WorkoutModifiers WithWallEquipment(
        WorkoutModifiers profile,
        WallEquipment equipment)
    {
        if (!Enum.IsDefined(equipment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null);
        }

        WorkoutModifiers withoutWall = Normalize(profile) &
            ~(WorkoutModifiers.Wall | WorkoutModifiers.SoleWallContact);
        return equipment switch
        {
            WallEquipment.None => withoutWall,
            WallEquipment.SolesStayOff =>
                withoutWall | WorkoutModifiers.Wall,
            WallEquipment.SolesMayTouch =>
                withoutWall |
                    WorkoutModifiers.Wall |
                    WorkoutModifiers.SoleWallContact,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };
    }

    public static bool IsCatalogMetadataComplete(
        IEnumerable<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return exercises.All(exercise =>
            (!exercise.SoleWallContactRequired || exercise.WallRequired) &&
            Rules.All(rule => rule.IsReviewed(exercise)));
    }

    public static bool IsCompatible(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        WorkoutModifiers normalized = Normalize(profile);
        WallEquipment wallEquipment = GetWallEquipment(normalized);
        return (!exercise.WallRequired ||
                wallEquipment != WallEquipment.None) &&
            (!normalized.HasFlag(WorkoutModifiers.Insect) ||
                exercise.PrimaryCanonicalGroup != CanonicalMuscleGroup.PelvicFloorAndPerineum &&
                (exercise.PrimaryCanonicalGroup != CanonicalMuscleGroup.IntrinsicHand || wallEquipment != WallEquipment.None)) &&
            (!exercise.SoleWallContactRequired ||
                wallEquipment == WallEquipment.SolesMayTouch) &&
            (!normalized.HasFlag(WorkoutModifiers.Light) ||
                exercise.MuscularDemand == Exercise.MinimumMuscularDemand) &&
            Rules.All(rule =>
                rule.IsReviewed(exercise) && rule.IsCompatibleForProfile(exercise, normalized));
    }

    public static bool IsWallPreferred(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.WallRequired &&
            GetWallEquipment(profile) != WallEquipment.None;
    }

    public static int GetEquipmentPreferenceCount(
        Exercise exercise,
        WorkoutModifiers profile) =>
        (IsWallPreferred(exercise, profile) ? 1 : 0) +
        (IsMirrorPreferred(exercise, profile) ? 1 : 0);

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

    private static bool IsWallFreeInsectFineCoverageException(
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        if (!profile.HasFlag(WorkoutModifiers.Insect) ||
            group.CanonicalGroups.Count == 0)
        {
            return false;
        }

        bool wallAvailable = profile.HasFlag(WorkoutModifiers.Wall);
        return group.CanonicalGroups.All(canonicalGroup =>
            InsectFineCoverageExceptions.Contains(canonicalGroup) ||
            (!wallAvailable &&
                WallFreeInsectFineCoverageExceptions.Contains(canonicalGroup)));
    }

    public static bool IsSelectionGroupAvailable(
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(group);
        return !IsWallFreeInsectFineCoverageException(group, profile);
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

}
