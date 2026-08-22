using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class MovementPhasePresentationPolicyTests
{
    [Fact]
    public void Only_an_exercise_without_a_pair_uses_the_continuous_schedule()
    {
        Assert.False(MovementPhasePresentationPolicy.UsesTimedPair(
            ExerciseSideSequence.Continuous,
            ExerciseDirectionSequence.None));
        Assert.False(MovementPhasePresentationPolicy.UsesTimedPair(
            ExerciseSideSequence.Alternating,
            ExerciseDirectionSequence.None));

        foreach (ExerciseSideSequence sideSequence in TimedSideSequences)
        {
            Assert.True(MovementPhasePresentationPolicy.UsesTimedPair(
                sideSequence,
                ExerciseDirectionSequence.None));
        }
        foreach (ExerciseDirectionSequence directionSequence in
                 TimedDirectionSequences)
        {
            Assert.True(MovementPhasePresentationPolicy.UsesTimedPair(
                ExerciseSideSequence.Continuous,
                directionSequence));
        }
    }

    [Theory]
    [InlineData(
        ExerciseSideSequence.ScreenLeftThenRight,
        MovementDirectionCue.ScreenLeft,
        MovementDirectionCue.ScreenRight,
        ScreenSide.Left,
        ScreenSide.Right)]
    [InlineData(
        ExerciseSideSequence.ScreenRightThenLeft,
        MovementDirectionCue.ScreenRight,
        MovementDirectionCue.ScreenLeft,
        ScreenSide.Right,
        ScreenSide.Left)]
    [InlineData(
        ExerciseSideSequence.ScreenLeftLeadThenRightLead,
        MovementDirectionCue.ShownLeadStance,
        MovementDirectionCue.OppositeLeadStance,
        ScreenSide.Left,
        ScreenSide.Right)]
    [InlineData(
        ExerciseSideSequence.ScreenRightLeadThenLeftLead,
        MovementDirectionCue.ShownLeadStance,
        MovementDirectionCue.OppositeLeadStance,
        ScreenSide.Right,
        ScreenSide.Left)]
    public void Side_pairs_preserve_split_hue_and_second_side_mirroring(
        ExerciseSideSequence sideSequence,
        MovementDirectionCue expectedFirstCue,
        MovementDirectionCue expectedSecondCue,
        ScreenSide expectedFirstSide,
        ScreenSide expectedSecondSide)
    {
        MovementPhasePresentation first = GetSidePresentation(
            sideSequence,
            MovementPhase.FirstSide);
        MovementPhasePresentation second = GetSidePresentation(
            sideSequence,
            MovementPhase.SecondSide);

        Assert.Equal(expectedFirstCue, first.Cue);
        Assert.False(first.MirrorMedia);
        Assert.Equal(expectedFirstSide, first.ActiveScreenSide);
        Assert.Equal(expectedSecondCue, second.Cue);
        Assert.True(second.MirrorMedia);
        Assert.Equal(expectedSecondSide, second.ActiveScreenSide);
    }

    [Theory]
    [InlineData(
        ExerciseDirectionSequence.ForwardThenBackward,
        MovementDirectionCue.Forward,
        MovementDirectionCue.Backward)]
    [InlineData(
        ExerciseDirectionSequence.BackwardThenForward,
        MovementDirectionCue.Backward,
        MovementDirectionCue.Forward)]
    [InlineData(
        ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
        MovementDirectionCue.Clockwise,
        MovementDirectionCue.Counterclockwise)]
    [InlineData(
        ExerciseDirectionSequence.CounterclockwiseThenClockwise,
        MovementDirectionCue.Counterclockwise,
        MovementDirectionCue.Clockwise)]
    [InlineData(
        ExerciseDirectionSequence.InwardThenOutward,
        MovementDirectionCue.Inward,
        MovementDirectionCue.Outward)]
    [InlineData(
        ExerciseDirectionSequence.OutwardThenInward,
        MovementDirectionCue.Outward,
        MovementDirectionCue.Inward)]
    public void Direction_pairs_map_to_wordless_cues_without_mirroring(
        ExerciseDirectionSequence directionSequence,
        MovementDirectionCue expectedFirstCue,
        MovementDirectionCue expectedSecondCue)
    {
        MovementPhasePresentation first = GetDirectionPresentation(
            directionSequence,
            MovementPhase.FirstSide);
        MovementPhasePresentation second = GetDirectionPresentation(
            directionSequence,
            MovementPhase.SecondSide);

        Assert.Equal(expectedFirstCue, first.Cue);
        Assert.False(first.MirrorMedia);
        Assert.Null(first.ActiveScreenSide);
        Assert.Equal(expectedSecondCue, second.Cue);
        Assert.False(second.MirrorMedia);
        Assert.Null(second.ActiveScreenSide);
    }

    [Fact]
    public void Every_timed_pair_uses_the_same_wordless_change_phase()
    {
        IEnumerable<MovementPhasePresentation> changes = TimedSideSequences
            .Select(sideSequence => GetSidePresentation(
                sideSequence,
                MovementPhase.ChangeSides))
            .Concat(TimedDirectionSequences.Select(directionSequence =>
                GetDirectionPresentation(
                    directionSequence,
                    MovementPhase.ChangeSides)));

        foreach (MovementPhasePresentation change in changes)
        {
            Assert.Equal(MovementDirectionCue.Switch, change.Cue);
            Assert.False(change.MirrorMedia);
            Assert.Null(change.ActiveScreenSide);
        }
    }

    [Fact]
    public void Continuous_and_complete_phases_have_stable_presentations()
    {
        MovementPhasePresentation continuous =
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSideSequence.Continuous,
                ExerciseDirectionSequence.None,
                MovementPhase.Continuous);
        MovementPhasePresentation complete =
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSideSequence.Continuous,
                ExerciseDirectionSequence.ForwardThenBackward,
                MovementPhase.Complete);

        Assert.Equal(MovementDirectionCue.Move, continuous.Cue);
        Assert.False(continuous.MirrorMedia);
        Assert.Null(continuous.ActiveScreenSide);
        MovementPhasePresentation alternating =
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSideSequence.Alternating,
                ExerciseDirectionSequence.None,
                MovementPhase.Continuous);
        Assert.Equal(MovementDirectionCue.Move, alternating.Cue);
        Assert.False(alternating.MirrorMedia);
        Assert.Null(alternating.ActiveScreenSide);
        Assert.Equal(MovementDirectionCue.None, complete.Cue);
        Assert.False(complete.MirrorMedia);
        Assert.Null(complete.ActiveScreenSide);
    }

    [Fact]
    public void Side_and_direction_protocols_cannot_be_combined()
    {
        Assert.Throws<ArgumentException>(() =>
            MovementPhasePresentationPolicy.UsesTimedPair(
                ExerciseSideSequence.ScreenLeftThenRight,
                ExerciseDirectionSequence.ForwardThenBackward));
    }

    [Theory]
    [InlineData(MovementPhase.Preparation)]
    [InlineData(MovementPhase.FirstSide)]
    [InlineData(MovementPhase.ChangeSides)]
    [InlineData(MovementPhase.SecondSide)]
    public void Continuous_sequence_rejects_timed_pair_phases(MovementPhase phase)
    {
        Assert.Throws<ArgumentException>(() =>
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSideSequence.Continuous,
                ExerciseDirectionSequence.None,
                phase));
    }

    [Fact]
    public void Timed_pair_rejects_continuous_phase()
    {
        Assert.Throws<ArgumentException>(() =>
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSideSequence.Continuous,
                ExerciseDirectionSequence.InwardThenOutward,
                MovementPhase.Continuous));
    }

    private static readonly ExerciseSideSequence[] TimedSideSequences =
    [
        ExerciseSideSequence.ScreenLeftThenRight,
        ExerciseSideSequence.ScreenRightThenLeft,
        ExerciseSideSequence.ScreenLeftLeadThenRightLead,
        ExerciseSideSequence.ScreenRightLeadThenLeftLead,
    ];

    private static readonly ExerciseDirectionSequence[] TimedDirectionSequences =
    [
        ExerciseDirectionSequence.ForwardThenBackward,
        ExerciseDirectionSequence.BackwardThenForward,
        ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
        ExerciseDirectionSequence.CounterclockwiseThenClockwise,
        ExerciseDirectionSequence.InwardThenOutward,
        ExerciseDirectionSequence.OutwardThenInward,
    ];

    private static MovementPhasePresentation GetSidePresentation(
        ExerciseSideSequence sideSequence,
        MovementPhase phase)
    {
        return MovementPhasePresentationPolicy.GetPresentation(
            sideSequence,
            ExerciseDirectionSequence.None,
            phase);
    }

    private static MovementPhasePresentation GetDirectionPresentation(
        ExerciseDirectionSequence directionSequence,
        MovementPhase phase)
    {
        return MovementPhasePresentationPolicy.GetPresentation(
            ExerciseSideSequence.Continuous,
            directionSequence,
            phase);
    }
}
