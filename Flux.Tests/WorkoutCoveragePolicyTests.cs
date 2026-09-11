using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutCoveragePolicyTests
{
    [Fact]
    public void PrimaryAloneEstablishesRegionAndSecondaryClaimsNeverChangeIt()
    {
        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            foreach (WorkoutGroup group in MassGroupingTaxonomy.GetResolution(minutes).Groups)
            {
                CanonicalMuscleGroup primary = group.CanonicalGroups.First();
                Exercise direct = Exercise(1, primary, []);
                Assert.True(WorkoutCoveragePolicy.IsSelectable(direct, group));
                CanonicalMuscleGroup outside = Enum.GetValues<CanonicalMuscleGroup>().First(muscle => !group.CanonicalGroups.Contains(muscle));
                Exercise indirect = Exercise(2, outside, group.CanonicalGroups.ToArray());
                Assert.False(WorkoutCoveragePolicy.IsSelectable(indirect, group));
            }
        }
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
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            UpperBodyClothingRequirement =
                ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            Score = 0,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
