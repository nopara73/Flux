using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutMuscleBudgetPolicyTests
{
    [Fact]
    public void MuscularDemandControlsWhichAssociationsConsumeBudget()
    {
        Exercise incidental = CreateExercise(
            1,
            CanonicalMuscleGroup.HipAbductors,
            Exercise.MinimumMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise moderate = CreateExercise(
            2,
            CanonicalMuscleGroup.AbdominalWall,
            Exercise.ModerateMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise hard = CreateExercise(
            3,
            CanonicalMuscleGroup.ElbowFlexors,
            Exercise.MaximumMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.GlutealExtensors);

        IReadOnlyDictionary<CanonicalMuscleGroup, int> loadHalfUnits =
            WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits(
                [incidental, moderate, hard]);

        Assert.DoesNotContain(CanonicalMuscleGroup.HipAbductors, loadHalfUnits.Keys);
        Assert.Equal(1, loadHalfUnits[CanonicalMuscleGroup.AbdominalWall]);
        Assert.Equal(2, loadHalfUnits[CanonicalMuscleGroup.ElbowFlexors]);
        Assert.Equal(1, loadHalfUnits[CanonicalMuscleGroup.GlutealExtensors]);
    }

    [Fact]
    public void HardUnilateralPhasesCountOnceButRepeatedRoundsCountAgain()
    {
        Exercise unilateral = CreateExercise(
            1,
            CanonicalMuscleGroup.HipAbductors,
            Exercise.MaximumMuscularDemand,
            ExerciseSideSequence.ScreenLeftThenRight,
            CanonicalMuscleGroup.GlutealExtensors);

        IReadOnlyDictionary<CanonicalMuscleGroup, int> oneRound =
            WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits([unilateral]);
        IReadOnlyDictionary<CanonicalMuscleGroup, int> twoRounds =
            WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits(
                [unilateral, unilateral]);

        Assert.Equal(2, oneRound[CanonicalMuscleGroup.HipAbductors]);
        Assert.Equal(1, oneRound[CanonicalMuscleGroup.GlutealExtensors]);
        Assert.Equal(4, twoRounds[CanonicalMuscleGroup.HipAbductors]);
        Assert.Equal(2, twoRounds[CanonicalMuscleGroup.GlutealExtensors]);
    }

    [Fact]
    public void AddingAnExerciseUsesOnlyItsDemandWeightedLoad()
    {
        var existingLoadHalfUnits = new Dictionary<CanonicalMuscleGroup, int>
        {
            [CanonicalMuscleGroup.AbdominalWall] = 11,
            [CanonicalMuscleGroup.GlutealExtensors] = 13,
        };
        Exercise incidental = CreateExercise(
            1,
            CanonicalMuscleGroup.AbdominalWall,
            Exercise.MinimumMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise moderate = CreateExercise(
            2,
            CanonicalMuscleGroup.AbdominalWall,
            Exercise.ModerateMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise hard = CreateExercise(
            3,
            CanonicalMuscleGroup.AbdominalWall,
            Exercise.MaximumMuscularDemand,
            ExerciseSideSequence.Continuous,
            CanonicalMuscleGroup.GlutealExtensors);

        Assert.Equal(
            0,
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
                existingLoadHalfUnits,
                incidental));
        Assert.Equal(
            2,
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
                existingLoadHalfUnits,
                moderate));
        Assert.Equal(
            7,
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
                existingLoadHalfUnits,
                hard));
    }

    [Fact]
    public void EveryHalfUnitAboveFiveAddsOneTemporaryDownvoteHalfUnitPerTrainedMuscle()
    {
        var loadHalfUnits = new Dictionary<CanonicalMuscleGroup, int>
        {
            [CanonicalMuscleGroup.AbdominalWall] = 13,
            [CanonicalMuscleGroup.GlutealExtensors] = 11,
            [CanonicalMuscleGroup.HipFlexors] = 10,
        };

        int temporaryDownvoteHalfUnits =
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnits(
                loadHalfUnits,
                [
                    CanonicalMuscleGroup.AbdominalWall,
                    CanonicalMuscleGroup.GlutealExtensors,
                    CanonicalMuscleGroup.HipFlexors,
                    CanonicalMuscleGroup.AbdominalWall,
                ]);

        Assert.Equal(4, temporaryDownvoteHalfUnits);
        Assert.Equal(
            -1,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                savedScore: 0,
                temporaryDownvoteHalfUnits: 1));
        Assert.Equal(
            -2,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                savedScore: -1,
                temporaryDownvoteHalfUnits: 0));
        Assert.Equal(
            -4,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                savedScore: 0,
                temporaryDownvoteHalfUnits));
        Assert.Equal(
            -6,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                savedScore: -1,
                temporaryDownvoteHalfUnits));
    }

    private static Exercise CreateExercise(
        int id,
        CanonicalMuscleGroup primary,
        int muscularDemand,
        ExerciseSideSequence sideSequence,
        params CanonicalMuscleGroup[] secondary)
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
            SideSequence = sideSequence,
            SequenceBlocks =
            [
                new ExerciseSequenceBlock
                {
                    ExerciseId = id,
                    SideCue = ExerciseSequenceSideCue.None,
                    DirectionCue = ExerciseSequenceDirectionCue.None,
                    MirrorMedia = false,
                },
            ],
            InsectCompatibility = ExerciseInsectCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            MuscularDemand = muscularDemand,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = "None",
            Silent = true,
        };
    }
}
