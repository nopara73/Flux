using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutCoveragePolicyTests
{
    [Theory]
    [InlineData(3, "r3.lower-limbs", 6)]
    [InlineData(3, "r3.torso-pelvic-complex", 3)]
    [InlineData(5, "r5.hips-thighs", 5)]
    [InlineData(7, "r7.lower-legs-feet", 2)]
    [InlineData(20, "r20.back-spinal-stabilization", 1)]
    [InlineData(30, "r30.medial-deep-knee-extensors", 1)]
    public void RequiredCoverageRoundsHalfUpToWholeCanonicalLeaves(
        int minutes,
        string groupId,
        int expected)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(minutes, groupId);

        Assert.Equal(
            expected,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group));
    }

    [Fact]
    public void SelectabilityRequiresPrimaryOwnershipAndAtLeastHalfOfBucket()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        CanonicalMuscleGroup[] leaves = group.CanonicalGroups.ToArray();
        Exercise belowThreshold = Exercise(
            1,
            leaves[0],
            leaves.Skip(1).Take(4).ToArray());
        Exercise exactlyHalf = Exercise(
            2,
            leaves[0],
            leaves.Skip(1).Take(5).ToArray());
        Exercise secondaryOnly = Exercise(
            3,
            CanonicalMuscleGroup.SpinalExtensors,
            leaves.Take(6).ToArray());

        Assert.Equal(5, WorkoutCoveragePolicy.GetCanonicalCoverage(belowThreshold, group));
        Assert.False(WorkoutCoveragePolicy.IsSelectable(belowThreshold, group));
        Assert.Equal(6, WorkoutCoveragePolicy.GetCanonicalCoverage(exactlyHalf, group));
        Assert.True(WorkoutCoveragePolicy.IsSelectable(exactlyHalf, group));
        Assert.Equal(6, WorkoutCoveragePolicy.GetCanonicalCoverage(secondaryOnly, group));
        Assert.False(WorkoutCoveragePolicy.IsSelectable(secondaryOnly, group));
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        CanonicalMuscleGroup[] secondary)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = secondary,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            HoldFramePercent = 0,
            Score = 0,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
