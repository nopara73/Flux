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
        Assert.Equal(4, WorkoutModifierPolicy.ValidationProfiles.Count);
        Assert.Contains(WorkoutModifiers.Silence, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Insect | WorkoutModifiers.Silence,
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
                    result.Minutes == 30 && result.GroupId == group.Id)
                .ToArray();

        Assert.Empty(deficiencies);

        WorkoutModifierPairCoverageDeficiency[] fourExerciseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                    exercises.Take(4).ToArray())
                .Where(result =>
                    result.Minutes == 30 && result.GroupId == group.Id)
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
                .Where(result =>
                    result.Minutes == 30 && result.GroupId == group.Id)
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
                .Where(result =>
                    result.Minutes == 30 && result.GroupId == group.Id)
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

        Assert.Equal(120, deficiency.RelaxedExerciseCount);
        Assert.Equal(115, deficiency.ConstrainedExerciseCount);
        Assert.Equal(5, deficiency.ReleasedExerciseCount);
        Assert.Equal(6, deficiency.RequiredReleasedExerciseCount);
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

        Assert.Equal(5, deficiency.ReleasedExerciseCount);
        Assert.Equal(5, deficiency.RequiredReleasedExerciseCount);
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
        Assert.Equal(0, conditionalDeficiency.ReleasedExerciseCount);
        Assert.Equal(WorkoutModifierPolicy.MinimumReleasedExercises,
            conditionalDeficiency.RequiredReleasedExerciseCount);
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

        Assert.Equal(0, deficiency.ReleasedExerciseCount);
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
        bool silent = true)
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
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = "None",
            Silent = silent,
        };
    }
}
