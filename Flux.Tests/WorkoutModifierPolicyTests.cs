using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutModifierPolicyTests
{
    [Fact]
    public void NeutralProfileKeepsBothCompatibleAndExcludedExercisesEligible()
    {
        Exercise compatible = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible);
        Exercise excluded = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            excluded,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.Insect));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            excluded,
            WorkoutModifiers.Insect));
    }

    [Fact]
    public void SilenceAndInsectComposeAsIndependentPositiveRequirements()
    {
        Exercise quietBug = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible);
        Exercise noisyBug = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible,
            silent: false);
        Exercise quietNoBug = Exercise(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible);
        Exercise noisyNoBug = Exercise(
            4,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible,
            silent: false);

        Assert.All([quietBug, noisyBug, quietNoBug, noisyNoBug], exercise =>
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.None)));
        Assert.Equal(
            [quietBug.Id, noisyBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Insect))
                .Select(exercise => exercise.Id));
        Assert.Equal(
            [quietBug.Id, quietNoBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Silence))
                .Select(exercise => exercise.Id));
        Assert.Equal(
            [quietBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Insect | WorkoutModifiers.Silence))
                .Select(exercise => exercise.Id));
    }

    [Fact]
    public void MirrorOffExcludesMirrorOnlyWhileMirrorOnAdmitsAllThreeRelationships()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise mirrorOnly = Exercise(
            1,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly);
        Exercise benefitsGreatly = Exercise(
            2,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly);
        Exercise agnostic = Exercise(3, group);

        Assert.False(WorkoutModifierPolicy.IsCompatible(
            mirrorOnly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            benefitsGreatly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.None));
        Assert.All([mirrorOnly, benefitsGreatly, agnostic], exercise =>
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Mirror)));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            mirrorOnly,
            WorkoutModifiers.Mirror));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            benefitsGreatly,
            WorkoutModifiers.Mirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            agnostic,
            WorkoutModifiers.Mirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            benefitsGreatly,
            WorkoutModifiers.None));
    }

    [Fact]
    public void MirrorMetadataIsCompleteOnlyWhenRelationshipMatchesEquipment()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] valid =
        [
            Exercise(
                1,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly),
            Exercise(
                2,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly),
            Exercise(3, group),
        ];

        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(valid));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                4,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.Unreviewed),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                5,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                equipment: "None"),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                6,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                equipment: "Mirror"),
        ]));
    }

    [Fact]
    public void MirrorOnPairwiseFloorCountsEveryEligibleRelationship()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                primary,
                mirrorRelationship: ExerciseMirrorRelationship.Agnostic))
            .ToArray();

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result => result.Minutes == 30 &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Mirror &&
                    result.SecondModifierEnabled)
                .ToArray();

        Assert.Empty(deficiencies);
    }

    [Fact]
    public void MirrorMaterialityIsJointlySuppliedByOnlyAndGreatlyBenefitedExercises()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] mirrorRelevant = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                mirrorRelationship: id == 1
                    ? ExerciseMirrorRelationship.MirrorOnly
                    : ExerciseMirrorRelationship.BenefitsGreatly))
            .ToArray();
        Exercise[] agnostic = Enumerable.Range(6, 15)
            .Select((id, index) => Exercise(id, groups[index % groups.Length]))
            .ToArray();

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindMaterialityDeficiencies(
                mirrorRelevant.Concat(agnostic).ToArray()),
            result => result.Modifier == WorkoutModifiers.Mirror &&
                result.ContextProfile == WorkoutModifiers.None);
    }

    [Fact]
    public void ValidationProfilesGrowOnlyWithSinglesAndModifierPairs()
    {
        int primitiveModifierCount = System.Numerics.BitOperations.PopCount(
            (uint)WorkoutModifierPolicy.SupportedMask);

        Assert.Equal(
            1 + primitiveModifierCount +
                primitiveModifierCount * (primitiveModifierCount - 1) / 2,
            WorkoutModifierPolicy.ValidationProfiles.Count);
        Assert.Equal(
            WorkoutModifierPolicy.ValidationProfiles.Count,
            WorkoutModifierPolicy.ValidationProfiles.Distinct().Count());
        Assert.All(WorkoutModifierPolicy.ValidationProfiles, profile =>
            Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile)));
        Assert.Contains(WorkoutModifiers.None, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Equal(7, WorkoutModifierPolicy.ValidationProfiles.Count);
        Assert.Contains(WorkoutModifiers.Silence, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Insect | WorkoutModifiers.Silence,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Mirror, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Silence | WorkoutModifiers.Mirror,
            WorkoutModifierPolicy.ValidationProfiles);
    }

    [Fact]
    public void PairwiseAvailabilityTreatsDisabledModifiersAsRelaxed()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true))
            .ToArray();

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result =>
                    result.Minutes == 30 && result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Empty(deficiencies);

        WorkoutModifierPairCoverageDeficiency[] fourExerciseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                    exercises.Take(4).ToArray())
                .Where(result =>
                    result.Minutes == 30 && result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Equal(4, fourExerciseDeficiencies.Length);
        Assert.All(fourExerciseDeficiencies, deficiency =>
            Assert.Equal(4, deficiency.MatchingExerciseCount));
        Assert.Equal(
            4,
            fourExerciseDeficiencies
                .Select(deficiency => (
                    deficiency.FirstModifierEnabled,
                    deficiency.SecondModifierEnabled))
                .Distinct()
                .Count());
    }

    [Fact]
    public void PairwiseAvailabilityCountsTheActualNestedCandidateSets()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] exercises =
        [
            Exercise(1, primary,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: false),
            Exercise(2, primary,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: true),
            Exercise(3, primary,
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: false),
            Exercise(4, primary,
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true),
        ];

        Dictionary<(bool Insect, bool Silence), int> counts =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result => result.Minutes == 30 &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToDictionary(
                    result => (
                        result.FirstModifierEnabled,
                        result.SecondModifierEnabled),
                    result => result.MatchingExerciseCount);

        Assert.Equal(4, counts[(false, false)]);
        Assert.Equal(2, counts[(true, false)]);
        Assert.Equal(2, counts[(false, true)]);
        Assert.Equal(1, counts[(true, true)]);
    }

    [Fact]
    public void PairwiseAvailabilityNeverCountsUnreviewedMetadata()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] exercises = Enumerable.Range(1, 4)
            .Select(id => Exercise(
                id,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .Append(Exercise(
                5,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Unreviewed))
            .ToArray();

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result => result.Minutes == 30 &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Equal(4, deficiencies.Length);
        Assert.All(deficiencies, deficiency =>
            Assert.Equal(4, deficiency.MatchingExerciseCount));
    }

    [Fact]
    public void MaterialityChecksGrowQuadratically()
    {
        int primitiveModifierCount = System.Numerics.BitOperations.PopCount(
            (uint)WorkoutModifierPolicy.SupportedMask);

        Assert.Equal(
            primitiveModifierCount * primitiveModifierCount,
            WorkoutModifierPolicy.FindMaterialityDeficiencies([]).Count);
    }

    [Fact]
    public void TokenModifierFailsRelativeMaterialityFloor()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] compatibleExercises = Enumerable.Range(1, 115)
            .Select(index => Exercise(
                index,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] releasedExercises = Enumerable.Range(116, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(
                    compatibleExercises.Concat(releasedExercises).ToArray())
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(120, deficiency.BaselineExerciseCount);
        Assert.Equal(115, deficiency.ModifiedExerciseCount);
        Assert.Equal(5, deficiency.MaterialExerciseCount);
        Assert.Equal(6, deficiency.RequiredMaterialExerciseCount);
        Assert.Equal(3, deficiency.AffectedBucketCount);
        Assert.Equal(3, deficiency.RequiredAffectedBucketCount);
    }

    [Fact]
    public void MaterialityMustAffectEnoughCanonicalBuckets()
    {
        CanonicalMuscleGroup group = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups[0]
            .CanonicalGroups
            .Single();
        Exercise[] exercises = Enumerable.Range(1, 10)
            .Select(id => Exercise(
                id,
                group,
                insectCompatibility: id <= 5
                    ? ExerciseInsectCompatibility.Compatible
                    : ExerciseInsectCompatibility.Incompatible))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(5, deficiency.MaterialExerciseCount);
        Assert.Equal(5, deficiency.RequiredMaterialExerciseCount);
        Assert.Equal(1, deficiency.AffectedBucketCount);
        Assert.Equal(3, deficiency.RequiredAffectedBucketCount);
    }

    [Fact]
    public void MaterialityMustRemainWhenAnotherModifierIsEnabled()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] quietInsectCompatible = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true))
            .ToArray();
        Exercise[] noisyInsectIncompatible = Enumerable.Range(6, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: false))
            .ToArray();
        Exercise[] exercises = quietInsectCompatible
            .Concat(noisyInsectIncompatible)
            .ToArray();

        WorkoutModifierMaterialityDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .ToArray();

        Assert.DoesNotContain(deficiencies, result =>
            result.Modifier == WorkoutModifiers.Insect &&
            result.ContextProfile == WorkoutModifiers.None);
        WorkoutModifierMaterialityDeficiency conditionalDeficiency =
            Assert.Single(deficiencies, result =>
                result.Modifier == WorkoutModifiers.Insect &&
                result.ContextProfile == WorkoutModifiers.Silence);
        Assert.Equal(0, conditionalDeficiency.MaterialExerciseCount);
        Assert.Equal(WorkoutModifierPolicy.MinimumMaterialExercises,
            conditionalDeficiency.RequiredMaterialExerciseCount);
        Assert.Equal(0, conditionalDeficiency.AffectedBucketCount);
    }

    [Fact]
    public void MaterialityNeverCreditsUnreviewedMetadata()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .Concat(Enumerable.Range(6, 5).Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Unreviewed)))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(0, deficiency.MaterialExerciseCount);
        Assert.Equal(0, deficiency.AffectedBucketCount);
    }

    [Fact]
    public void MaximumDistinctLineupUsesAugmentingPathsInsteadOfGreedyCounts()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise[] exercises =
        [
            Exercise(
                1,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Exercise(3, CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
        ];

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    [Fact]
    public void MaximumDistinctLineupDetectsHallDeficitAfterModifierFiltering()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise[] exercises =
        [
            Exercise(
                1,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(
                2,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(
                3,
                CanonicalMuscleGroup.MajorHipAdductors,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
        ];

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.None));
        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    private static WorkoutGroup Group(
        string id,
        CanonicalMuscleGroup canonicalGroup)
    {
        return new WorkoutGroup(
            id,
            id,
            1,
            new HashSet<CanonicalMuscleGroup> { canonicalGroup });
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primaryCanonicalGroup,
        CanonicalMuscleGroup secondaryCanonicalGroup = default,
        CanonicalMuscleGroup tertiaryCanonicalGroup = default,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Compatible,
        bool silent = true,
        ExerciseMirrorRelationship mirrorRelationship =
            ExerciseMirrorRelationship.Agnostic,
        string? equipment = null)
    {
        CanonicalMuscleGroup[] secondaryCanonicalGroups =
            new[] { secondaryCanonicalGroup, tertiaryCanonicalGroup }
                .Where(group => group != default)
                .ToArray();
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primaryCanonicalGroup,
            SecondaryCanonicalGroups = secondaryCanonicalGroups,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            InsectCompatibility = insectCompatibility,
            MirrorRelationship = mirrorRelationship,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = equipment ??
                (mirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                    ? "Mirror"
                    : "None"),
            Silent = silent,
        };
    }
}
