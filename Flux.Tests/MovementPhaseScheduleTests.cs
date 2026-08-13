using Flux.Services;

namespace Flux.Tests;

public sealed class MovementPhaseScheduleTests
{
    [Theory]
    [InlineData(45_000, MovementPhase.Continuous, 45)]
    [InlineData(44_999, MovementPhase.Continuous, 45)]
    [InlineData(44_000, MovementPhase.Continuous, 44)]
    [InlineData(1_001, MovementPhase.Continuous, 2)]
    [InlineData(1_000, MovementPhase.Continuous, 1)]
    [InlineData(1, MovementPhase.Continuous, 1)]
    public void Continuous_exercises_expose_the_overall_countdown(
        long remainingMilliseconds,
        MovementPhase expectedPhase,
        int expectedSeconds)
    {
        var state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: false);

        Assert.Equal(expectedPhase, state.Phase);
        Assert.Equal(expectedSeconds, state.SecondsRemaining);
        Assert.Equal(45, state.SegmentDurationSeconds);
        Assert.True(state.IsExercise);
    }

    [Theory]
    [InlineData(45_000, MovementPhase.FirstSide, 20, 20, true)]
    [InlineData(44_999, MovementPhase.FirstSide, 20, 20, true)]
    [InlineData(26_000, MovementPhase.FirstSide, 1, 20, true)]
    [InlineData(25_001, MovementPhase.FirstSide, 1, 20, true)]
    [InlineData(25_000, MovementPhase.ChangeSides, 5, 5, false)]
    [InlineData(24_999, MovementPhase.ChangeSides, 5, 5, false)]
    [InlineData(21_000, MovementPhase.ChangeSides, 1, 5, false)]
    [InlineData(20_001, MovementPhase.ChangeSides, 1, 5, false)]
    [InlineData(20_000, MovementPhase.SecondSide, 20, 20, true)]
    [InlineData(19_999, MovementPhase.SecondSide, 20, 20, true)]
    [InlineData(1_001, MovementPhase.SecondSide, 2, 20, true)]
    [InlineData(1_000, MovementPhase.SecondSide, 1, 20, true)]
    [InlineData(1, MovementPhase.SecondSide, 1, 20, true)]
    public void Timed_side_exercises_follow_the_twenty_five_twenty_schedule(
        long remainingMilliseconds,
        MovementPhase expectedPhase,
        int expectedSeconds,
        int expectedDuration,
        bool expectedIsExercise)
    {
        var state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: true);

        Assert.Equal(expectedPhase, state.Phase);
        Assert.Equal(expectedSeconds, state.SecondsRemaining);
        Assert.Equal(expectedDuration, state.SegmentDurationSeconds);
        Assert.Equal(expectedIsExercise, state.IsExercise);
    }

    [Theory]
    [InlineData(105_000, MovementPhase.FirstSide, 45, 45, true)]
    [InlineData(60_001, MovementPhase.FirstSide, 1, 45, true)]
    [InlineData(60_000, MovementPhase.ChangeSides, 15, 15, false)]
    [InlineData(45_001, MovementPhase.ChangeSides, 1, 15, false)]
    [InlineData(45_000, MovementPhase.SecondSide, 45, 45, true)]
    [InlineData(1, MovementPhase.SecondSide, 1, 45, true)]
    public void Full_side_exercises_follow_the_forty_five_fifteen_forty_five_schedule(
        long remainingMilliseconds,
        MovementPhase expectedPhase,
        int expectedSeconds,
        int expectedDuration,
        bool expectedIsExercise)
    {
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: true,
            usesFullSideTiming: true);

        Assert.Equal(expectedPhase, state.Phase);
        Assert.Equal(expectedSeconds, state.SecondsRemaining);
        Assert.Equal(expectedDuration, state.SegmentDurationSeconds);
        Assert.Equal(expectedIsExercise, state.IsExercise);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Finished_countdowns_are_complete(long remainingMilliseconds)
    {
        var continuous = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: false);
        var timedSides = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: true);

        AssertComplete(continuous);
        AssertComplete(timedSides);
    }

    [Theory]
    [InlineData(45_001)]
    [InlineData(long.MaxValue)]
    public void Countdown_values_above_the_workout_duration_are_bounded(
        long remainingMilliseconds)
    {
        var continuous = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: false);
        var timedSides = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            usesTimedSides: true);

        Assert.Equal(45, continuous.SecondsRemaining);
        Assert.Equal(MovementPhase.Continuous, continuous.Phase);
        Assert.Equal(20, timedSides.SecondsRemaining);
        Assert.Equal(MovementPhase.FirstSide, timedSides.Phase);
    }

    private static void AssertComplete(MovementPhaseState state)
    {
        Assert.Equal(MovementPhase.Complete, state.Phase);
        Assert.Equal(0, state.SecondsRemaining);
        Assert.Equal(0, state.SegmentDurationSeconds);
        Assert.False(state.IsExercise);
    }
}
