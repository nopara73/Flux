using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutModifierPolicyTests
{
    [Fact]
    public void UpperBodyClothingStatesExcludeOnlyTheOppositeRequirement()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise clothingRequired = Exercise(
            1,
            group,
            upperBodyClothingRequirement:
                ExerciseUpperBodyClothingRequirement.ClothingRequired);
        Exercise bareRequired = Exercise(
            2,
            group,
            upperBodyClothingRequirement:
                ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired);
        Exercise agnostic = Exercise(3, group);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            clothingRequired,
            WorkoutModifiers.UpperBodyClothing));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            clothingRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            bareRequired,
            WorkoutModifiers.UpperBodyClothing));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            bareRequired,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.UpperBodyClothing));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.None));
    }

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
    public void HardFloorFiltersOnlyReviewedIncompatibleExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise compatible = Exercise(
            1,
            group,
            hardFloorCompatibility: ExerciseHardFloorCompatibility.Compatible);
        Exercise incompatible = Exercise(
            2,
            group,
            hardFloorCompatibility: ExerciseHardFloorCompatibility.Incompatible);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.HardFloor));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.HardFloor));
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
    public void WallEquipmentStatesUnlockExactlyTheirAllowedExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise wallRequired = Exercise(1, group, wallRequired: true);
        Exercise soleWallRequired = Exercise(
            2,
            group,
            wallRequired: true,
            soleWallContactRequired: true);
        Exercise ordinary = Exercise(3, group);
        WorkoutModifiers solesMayTouch = WorkoutModifierPolicy.WithWallEquipment(
            WorkoutModifiers.None,
            WallEquipment.SolesMayTouch);

        Assert.False(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            WorkoutModifiers.Wall));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            solesMayTouch));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            solesMayTouch));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            ordinary,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            ordinary,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsWallPreferred(
            wallRequired,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsWallPreferred(
            soleWallRequired,
            solesMayTouch));
        Assert.False(WorkoutModifierPolicy.IsWallPreferred(
            wallRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsWallPreferred(
            ordinary,
            WorkoutModifiers.Wall));
    }

    [Fact]
    public void WallAndMirrorPreferencesComposeWithoutOneHidingTheOther()
    {
        Exercise wallAndMirrorRelevant = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            wallRequired: true);

        Assert.Equal(0, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.None));
        Assert.Equal(1, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Wall));
        Assert.Equal(1, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Mirror));
        Assert.Equal(2, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Wall | WorkoutModifiers.Mirror));
    }

    [Fact]
    public void WallSingletonFloorCountsDistinctSessionMovementsOnly()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] exercises = Enumerable.Range(
                1,
                WorkoutModifierPolicy.MinimumWallRequiredSessionMovements)
            .Select(id => Exercise(
                id,
                group,
                sessionMovementId: id,
                wallRequired: true))
            .ToArray();

        Assert.Empty(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));

        exercises[^1] = Exercise(
            exercises[^1].Id,
            group,
            sessionMovementId: exercises[^2].SessionMovementId,
            wallRequired: true);
        WorkoutWallRequiredCatalogDeficiency deficiency = Assert.Single(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));
        Assert.Equal(
            WorkoutModifierPolicy.MinimumWallRequiredSessionMovements - 1,
            deficiency.MatchingSessionMovementCount);
        Assert.Equal(
            WorkoutModifierPolicy.MinimumWallRequiredSessionMovements,
            deficiency.RequiredSessionMovementCount);
    }

    [Fact]
    public void SoleWallFloorIsSeparateAndCountsDistinctSessionMovementsOnly()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] exercises = Enumerable.Range(
                1,
                WorkoutModifierPolicy
                    .MinimumSoleWallContactRequiredSessionMovements)
            .Select(id => Exercise(
                id,
                group,
                sessionMovementId: id,
                wallRequired: true,
                soleWallContactRequired: true))
            .ToArray();

        Assert.Empty(WorkoutModifierPolicy
            .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.Single(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));

        exercises[^1] = Exercise(
            exercises[^1].Id,
            group,
            sessionMovementId: exercises[^2].SessionMovementId,
            wallRequired: true,
            soleWallContactRequired: true);
        WorkoutSoleWallContactRequiredCatalogDeficiency deficiency =
            Assert.Single(WorkoutModifierPolicy
                .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.Equal(
            WorkoutModifierPolicy
                .MinimumSoleWallContactRequiredSessionMovements - 1,
            deficiency.MatchingSessionMovementCount);
        Assert.Equal(
            WorkoutModifierPolicy
                .MinimumSoleWallContactRequiredSessionMovements,
            deficiency.RequiredSessionMovementCount);
    }

    [Fact]
    public void MirrorEquipmentAppliesCoverageWithoutFilteringOrdinaryExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise mirrorOnly = Exercise(
            1,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody);
        Exercise fullBodyMirrorOnly = Exercise(
            2,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody);
        Exercise benefitsGreatly = Exercise(
            3,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody);
        Exercise fullBodyBenefitsGreatly = Exercise(
            4,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody);
        Exercise agnostic = Exercise(5, group);

        Assert.False(WorkoutModifierPolicy.IsCompatible(
            mirrorOnly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            benefitsGreatly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            fullBodyMirrorOnly,
            WorkoutModifiers.Mirror));
        Assert.All([mirrorOnly, benefitsGreatly, fullBodyBenefitsGreatly, agnostic], exercise =>
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Mirror)));
        WorkoutModifiers tallMirror = WorkoutModifierPolicy.WithMirrorEquipment(
            WorkoutModifiers.None,
            MirrorEquipment.Tall);
        Assert.All(
            [mirrorOnly, fullBodyMirrorOnly, benefitsGreatly,
                fullBodyBenefitsGreatly, agnostic],
            exercise => Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                tallMirror)));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            mirrorOnly,
            WorkoutModifiers.Mirror));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            benefitsGreatly,
            WorkoutModifiers.Mirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            fullBodyBenefitsGreatly,
            WorkoutModifiers.Mirror));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            fullBodyBenefitsGreatly,
            tallMirror));
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
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
            Exercise(
                2,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody),
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
                equipment: "None",
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                6,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                equipment: "Mirror",
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                7,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.None),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                8,
                group,
                soleWallContactRequired: true),
        ]));
    }

    [Fact]
    public void WallEquipmentRoundTripsAndOrphanSoleQualifierIsDiscarded()
    {
        WorkoutModifiers context = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence;

        Assert.Equal(
            WallEquipment.None,
            WorkoutModifierPolicy.GetWallEquipment(
                WorkoutModifiers.SoleWallContact));
        Assert.Equal(
            WorkoutModifiers.None,
            WorkoutModifierPolicy.Normalize(
                WorkoutModifiers.SoleWallContact));

        WorkoutModifiers solesStayOff =
            WorkoutModifierPolicy.WithWallEquipment(
                context,
                WallEquipment.SolesStayOff);
        Assert.Equal(
            WallEquipment.SolesStayOff,
            WorkoutModifierPolicy.GetWallEquipment(solesStayOff));
        Assert.Equal(context | WorkoutModifiers.Wall, solesStayOff);

        WorkoutModifiers solesMayTouch =
            WorkoutModifierPolicy.WithWallEquipment(
                solesStayOff,
                WallEquipment.SolesMayTouch);
        Assert.Equal(
            WallEquipment.SolesMayTouch,
            WorkoutModifierPolicy.GetWallEquipment(solesMayTouch));
        Assert.Equal(
            context | WorkoutModifiers.Wall |
                WorkoutModifiers.SoleWallContact,
            solesMayTouch);

        Assert.Equal(
            context,
            WorkoutModifierPolicy.WithWallEquipment(
                solesMayTouch,
                WallEquipment.None));
    }

    [Fact]
    public void MirrorEquipmentRoundTripsAndOrphanTallQualifierIsDiscarded()
    {
        WorkoutModifiers context = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence;

        Assert.Equal(
            MirrorEquipment.None,
            WorkoutModifierPolicy.GetMirrorEquipment(
                WorkoutModifiers.TallMirror));
        Assert.Equal(
            WorkoutModifiers.None,
            WorkoutModifierPolicy.Normalize(WorkoutModifiers.TallMirror));

        WorkoutModifiers compact = WorkoutModifierPolicy.WithMirrorEquipment(
            context,
            MirrorEquipment.Compact);
        Assert.Equal(MirrorEquipment.Compact,
            WorkoutModifierPolicy.GetMirrorEquipment(compact));
        Assert.Equal(context | WorkoutModifiers.Mirror, compact);

        WorkoutModifiers tall = WorkoutModifierPolicy.WithMirrorEquipment(
            compact,
            MirrorEquipment.Tall);
        Assert.Equal(MirrorEquipment.Tall,
            WorkoutModifierPolicy.GetMirrorEquipment(tall));
        Assert.Equal(
            context | WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror,
            tall);

        Assert.Equal(
            context,
            WorkoutModifierPolicy.WithMirrorEquipment(
                tall,
                MirrorEquipment.None));
    }

    [Fact]
    public void MirrorCategoryFloorRequiresFiveInEveryRelationshipCoverageCell()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        var exercises = new List<Exercise>();
        int id = 1;
        foreach ((ExerciseMirrorRelationship relationship,
                 ExerciseMirrorCoverage coverage) in new[]
        {
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
        })
        {
            for (int index = 0;
                 index < WorkoutModifierPolicy.MinimumExercisesPerMirrorCategory;
                 index++)
            {
                exercises.Add(Exercise(
                    id++,
                    group,
                    mirrorRelationship: relationship,
                    minimumMirrorCoverage: coverage));
            }
        }

        Assert.Empty(
            WorkoutModifierPolicy.FindMirrorCategoryDeficiencies(exercises));

        exercises.Remove(exercises.First(exercise =>
            exercise.MirrorRelationship ==
                ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage ==
                ExerciseMirrorCoverage.FullBody));
        WorkoutMirrorCategoryDeficiency deficiency = Assert.Single(
            WorkoutModifierPolicy.FindMirrorCategoryDeficiencies(exercises));
        Assert.Equal(ExerciseMirrorRelationship.MirrorOnly,
            deficiency.Relationship);
        Assert.Equal(ExerciseMirrorCoverage.FullBody,
            deficiency.MinimumCoverage);
        Assert.Equal(4, deficiency.MatchingExerciseCount);
        Assert.Equal(5, deficiency.RequiredExerciseCount);
    }

    [Fact]
    public void MirrorCategoryFloorDoesNotDoubleCountMovementAliases()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] exercises =
        [
            Exercise(
                1,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody,
                sessionMovementId: 1),
            Exercise(
                2,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody,
                sessionMovementId: 1),
            .. Enumerable.Range(3, 3).Select(id => Exercise(
                id,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody)),
        ];

        WorkoutMirrorCategoryDeficiency deficiency =
            WorkoutModifierPolicy.FindMirrorCategoryDeficiencies(exercises)
                .Single(result =>
                    result.Relationship ==
                        ExerciseMirrorRelationship.BenefitsGreatly &&
                    result.MinimumCoverage == ExerciseMirrorCoverage.UpperBody);

        Assert.Equal(4, deficiency.MatchingExerciseCount);
    }

    [Fact]
    public void MirrorOnPairwiseFloorRequiresMirrorRelevantRelationships()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] agnosticExercises = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                primary,
                mirrorRelationship: ExerciseMirrorRelationship.Agnostic))
            .ToArray();

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                    agnosticExercises)
                .Where(result => result.Minutes == 30 &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Mirror &&
                    result.SecondModifierEnabled)
                .ToArray();

        Assert.Equal(4, deficiencies.Length);
        Assert.All(deficiencies, deficiency =>
            Assert.Equal(0, deficiency.MatchingExerciseCount));

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                agnosticExercises),
            result => result.Minutes == 30 &&
                result.GroupId == group.Id &&
                result.FirstModifier == WorkoutModifiers.Insect &&
                result.SecondModifier == WorkoutModifiers.Mirror &&
                !result.SecondModifierEnabled);

        Exercise[] greatlyBenefitedExercises = Enumerable.Range(6, 5)
            .Select(id => Exercise(
                id,
                primary,
                mirrorRelationship:
                    ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody))
            .ToArray();
        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                agnosticExercises.Concat(greatlyBenefitedExercises).ToArray()),
            result => result.Minutes == 30 &&
                result.GroupId == group.Id &&
                result.FirstModifier == WorkoutModifiers.Insect &&
                result.SecondModifier == WorkoutModifiers.Mirror);
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
                    : ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody))
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
        Assert.Equal(
            WorkoutModifierPolicy.ValidationProfiles.Count,
            WorkoutModifierPolicy.ValidationProfiles.Distinct().Count());
        Assert.All(WorkoutModifierPolicy.ValidationProfiles, profile =>
            Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile)));
        Assert.Contains(WorkoutModifiers.None, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Equal(21, WorkoutModifierPolicy.ValidationProfiles.Count);
        Assert.Contains(
            WorkoutModifiers.UpperBodyClothing,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.HardFloor, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Silence, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Insect | WorkoutModifiers.Silence,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Mirror, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Silence | WorkoutModifiers.Mirror,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Silence | WorkoutModifiers.Mirror |
                WorkoutModifiers.TallMirror,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            profile => profile.HasFlag(WorkoutModifiers.Wall));
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            profile => profile.HasFlag(WorkoutModifiers.Light));
    }

    [Fact]
    public void LightIsSupportedButNeverPersistedOrUsedAsAQuotaAxis()
    {
        WorkoutModifiers profile = WorkoutModifiers.Insect |
            WorkoutModifiers.Light;

        Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile));
        Assert.Equal(
            WorkoutModifiers.Insect,
            WorkoutModifierPolicy.GetPersistentSetupModifiers(profile));
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            candidate => candidate.HasFlag(WorkoutModifiers.Light));
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
    public void HardFloorCategoryCoverageDoesNotLetCompatibleExercisesHideTheReleasedSet()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] compatible = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                primary,
                hardFloorCompatibility:
                    ExerciseHardFloorCompatibility.Compatible))
            .ToArray();
        Exercise[] incompatible = Enumerable.Range(6, 4)
            .Select(id => Exercise(
                id,
                primary,
                hardFloorCompatibility:
                    ExerciseHardFloorCompatibility.Incompatible))
            .ToArray();

        WorkoutHardFloorCategoryCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(
                    compatible.Concat(incompatible).ToArray())
                .Where(result => result.Minutes == 30 &&
                    result.GroupId == group.Id)
                .ToArray();

        Assert.Equal(5, deficiencies.Length);
        Assert.All(deficiencies, deficiency =>
        {
            Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                deficiency.HardFloorCompatibility);
            Assert.Equal(4, deficiency.MatchingExerciseCount);
        });

        Exercise fifthIncompatible = Exercise(
            10,
            primary,
            hardFloorCompatibility:
                ExerciseHardFloorCompatibility.Incompatible);
        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(
                [.. compatible, .. incompatible, fifthIncompatible]),
            result => result.Minutes == 30 && result.GroupId == group.Id);
    }

    [Fact]
    public void DemandCoverageRequiresWholeLightSequencesAndSlotOwnedHardMembers()
    {
        WorkoutGroup targetGroup = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        WorkoutGroup otherGroup = MassGroupingTaxonomy.GetResolution(30).Groups[1];
        CanonicalMuscleGroup target = targetGroup.CanonicalGroups.Single();
        CanonicalMuscleGroup other = otherGroup.CanonicalGroups.Single();
        Exercise pureLight = Exercise(1, target, muscularDemand: 0);
        Exercise mixedRoot = Exercise(
            2,
            target,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 3, MirrorMedia = false },
            ],
            muscularDemand: 0);
        Exercise mixedMember = Exercise(
            3,
            target,
            sequenceBlocks: [],
            muscularDemand: 1);
        Exercise hardElsewhereRoot = Exercise(
            4,
            target,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 4, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 5, MirrorMedia = false },
            ],
            muscularDemand: 0);
        Exercise hardElsewhereMember = Exercise(
            5,
            other,
            sequenceBlocks: [],
            muscularDemand: 2);
        Exercise hardForTargetRoot = Exercise(
            6,
            other,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 6, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 7, MirrorMedia = false },
            ],
            muscularDemand: 1);
        Exercise hardForTargetMember = Exercise(
            7,
            target,
            sequenceBlocks: [],
            muscularDemand: 2);
        Exercise duplicateHardA = Exercise(
            8,
            target,
            sessionMovementId: 99,
            muscularDemand: 2);
        Exercise duplicateHardB = Exercise(
            9,
            target,
            sessionMovementId: 99,
            muscularDemand: 2);

        WorkoutMuscularDemandCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindMuscularDemandCoverageDeficiencies(
                [
                    pureLight,
                    mixedRoot,
                    mixedMember,
                    hardElsewhereRoot,
                    hardElsewhereMember,
                    hardForTargetRoot,
                    hardForTargetMember,
                    duplicateHardA,
                    duplicateHardB,
                ])
            .Where(result =>
                result.Minutes == 30 &&
                result.GroupId == targetGroup.Id &&
                result.Profile == WorkoutModifiers.None)
            .ToArray();

        Assert.Equal(2, deficiencies.Length);
        Assert.Equal(
            1,
            deficiencies.Single(result => result.MuscularDemand == 0)
                .MatchingExerciseCount);
        Assert.Equal(
            2,
            deficiencies.Single(result => result.MuscularDemand == 2)
                .MatchingExerciseCount);
    }

    [Fact]
    public void DemandCoverageUsesFiveDistinctSessionMovementsPerCategory()
    {
        WorkoutGroup targetGroup = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup target = targetGroup.CanonicalGroups.Single();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select(id => Exercise(id, target, muscularDemand: 0))
            .Concat(Enumerable.Range(6, 5)
                .Select(id => Exercise(id, target, muscularDemand: 2)))
            .ToArray();

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindMuscularDemandCoverageDeficiencies(exercises),
            result => result.Minutes == 30 && result.GroupId == targetGroup.Id);
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
        // Clothing is a bidirectional setup state, not a restrictive filter,
        // so materiality remains the five restrictive single-state checks,
        // six directed binary/binary edges, and four edges for each of the
        // three binary/Mirror pairs.
        Assert.Equal(
            23,
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

    [Fact]
    public void MaximumDistinctLineupCountsAliasesAsOneSessionMovement()
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
                CanonicalMuscleGroup.MajorHipAdductors,
                sessionMovementId: 1),
            Exercise(
                2,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors,
                sessionMovementId: 1),
            Exercise(3, CanonicalMuscleGroup.MajorHipAdductors),
        ];

        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    [Fact]
    public void MaximumDistinctLineupCreditsCrossPrimarySequenceSlots()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise member = Exercise(
            2,
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
            sequenceBlocks: []);
        Exercise root = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 1, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
            ]);
        Exercise singleton = Exercise(
            3,
            CanonicalMuscleGroup.MajorHipAdductors);

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                [root, member, singleton],
                groups,
                WorkoutModifiers.Insect,
                workoutMinutes: 3));
    }

    [Fact]
    public void MaximumDistinctLineupLetsSamePrimarySequenceYieldToCapacity()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise member = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks: []);
        Exercise root = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 1, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
            ]);

        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                [
                    root,
                    member,
                    Exercise(3, CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
                    Exercise(4, CanonicalMuscleGroup.MajorHipAdductors),
                ],
                groups,
                WorkoutModifiers.Insect,
                workoutMinutes: 3));
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
        string? equipment = null,
        ExerciseMirrorCoverage? minimumMirrorCoverage = null,
        int sessionMovementId = 0,
        ExerciseSequenceBlock[]? sequenceBlocks = null,
        ExerciseHardFloorCompatibility hardFloorCompatibility =
            ExerciseHardFloorCompatibility.Compatible,
        bool wallRequired = false,
        bool soleWallContactRequired = false,
        ExerciseUpperBodyClothingRequirement upperBodyClothingRequirement =
            ExerciseUpperBodyClothingRequirement.Agnostic,
        int muscularDemand = 0)
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
            SequenceBlocks = sequenceBlocks ??
                [
                    new ExerciseSequenceBlock
                    {
                        ExerciseId = id,
                        SideCue = ExerciseSequenceSideCue.None,
                        DirectionCue = ExerciseSequenceDirectionCue.None,
                        MirrorMedia = false,
                    },
                ],
            SessionMovementId = sessionMovementId,
            InsectCompatibility = insectCompatibility,
            HardFloorCompatibility = hardFloorCompatibility,
            UpperBodyClothingRequirement = upperBodyClothingRequirement,
            MirrorRelationship = mirrorRelationship,
            MinimumMirrorCoverage = minimumMirrorCoverage ??
                (mirrorRelationship is ExerciseMirrorRelationship.MirrorOnly or
                    ExerciseMirrorRelationship.BenefitsGreatly
                    ? ExerciseMirrorCoverage.UpperBody
                    : ExerciseMirrorCoverage.None),
            WallRequired = wallRequired,
            SoleWallContactRequired = soleWallContactRequired,
            MuscularDemand = muscularDemand,
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
