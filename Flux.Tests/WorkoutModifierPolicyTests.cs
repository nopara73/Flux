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
    public void SupportedProfilesAreThePowerSetOfRegisteredPrimitiveModifiers()
    {
        int primitiveModifierCount = System.Numerics.BitOperations.PopCount(
            (uint)WorkoutModifierPolicy.SupportedMask);

        Assert.Equal(
            1 << primitiveModifierCount,
            WorkoutModifierPolicy.SupportedProfiles.Count);
        Assert.Equal(
            WorkoutModifierPolicy.SupportedProfiles.Count,
            WorkoutModifierPolicy.SupportedProfiles.Distinct().Count());
        Assert.All(WorkoutModifierPolicy.SupportedProfiles, profile =>
            Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile)));
        Assert.Contains(WorkoutModifiers.None, WorkoutModifierPolicy.SupportedProfiles);
        Assert.Equal(4, WorkoutModifierPolicy.SupportedProfiles.Count);
        Assert.Contains(WorkoutModifiers.Silence, WorkoutModifierPolicy.SupportedProfiles);
        Assert.Contains(
            WorkoutModifiers.Insect | WorkoutModifiers.Silence,
            WorkoutModifierPolicy.SupportedProfiles);
        Assert.Contains(WorkoutModifierPolicy.SupportedMask, WorkoutModifierPolicy.SupportedProfiles);
    }

    [Fact]
    public void ModifierExclusionFloorCountsOnlyExplicitNormalSelectableFailures()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise[] exercises =
        [
            Exercise(
                1,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
            Exercise(
                2,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
            Exercise(
                3,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Compatible),
            Exercise(
                4,
                primary,
                insectCompatibility: ExerciseInsectCompatibility.Unreviewed),
            Exercise(
                5,
                MassGroupingTaxonomy.GetResolution(30).Groups[1]
                    .CanonicalGroups.Single(),
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
        ];

        WorkoutModifierExclusionDeficiency deficiency =
            WorkoutModifierPolicy.FindModifierExclusionDeficiencies(exercises)
                .Single(result =>
                    result.Minutes == 30 &&
                    result.GroupId == group.Id &&
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(WorkoutModifiers.Insect, deficiency.Modifier);
        Assert.Equal(WorkoutModifiers.None, deficiency.ContextProfile);
        Assert.Equal(2, deficiency.ExcludedExerciseCount);
        Assert.Equal(
            WorkoutModifierPolicy.MinimumExcludedExercisesPerGroup,
            deficiency.RequiredExcludedExerciseCount);
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
