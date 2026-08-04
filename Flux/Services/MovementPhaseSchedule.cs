namespace Flux.Services;

public enum MovementPhase
{
    Continuous,
    FirstSide,
    ChangeSides,
    SecondSide,
    Complete,
}

public readonly record struct MovementPhaseState(
    MovementPhase Phase,
    int SecondsRemaining,
    int SegmentDurationSeconds,
    bool IsExercise);

public static class MovementPhaseSchedule
{
    public const int TotalDurationSeconds = 45;
    public const int SideDurationSeconds = 20;
    public const int SideChangeDurationSeconds = 5;

    private const long TotalDurationMilliseconds =
        TotalDurationSeconds * 1_000L;
    private const long SecondSideStartMilliseconds =
        SideDurationSeconds * 1_000L;
    private const long FirstSideEndMilliseconds =
        (SideDurationSeconds + SideChangeDurationSeconds) * 1_000L;

    public static MovementPhaseState GetState(
        long remainingMilliseconds,
        bool usesTimedSides)
    {
        if (remainingMilliseconds <= 0)
        {
            return new MovementPhaseState(
                MovementPhase.Complete,
                SecondsRemaining: 0,
                SegmentDurationSeconds: 0,
                IsExercise: false);
        }

        var boundedRemainingMilliseconds = Math.Min(
            remainingMilliseconds,
            TotalDurationMilliseconds);

        if (!usesTimedSides)
        {
            return new MovementPhaseState(
                MovementPhase.Continuous,
                ToDisplayedSeconds(boundedRemainingMilliseconds),
                TotalDurationSeconds,
                IsExercise: true);
        }

        if (boundedRemainingMilliseconds > FirstSideEndMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.FirstSide,
                ToDisplayedSeconds(
                    boundedRemainingMilliseconds - FirstSideEndMilliseconds),
                SideDurationSeconds,
                IsExercise: true);
        }

        if (boundedRemainingMilliseconds > SecondSideStartMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.ChangeSides,
                ToDisplayedSeconds(
                    boundedRemainingMilliseconds - SecondSideStartMilliseconds),
                SideChangeDurationSeconds,
                IsExercise: false);
        }

        return new MovementPhaseState(
            MovementPhase.SecondSide,
            ToDisplayedSeconds(boundedRemainingMilliseconds),
            SideDurationSeconds,
            IsExercise: true);
    }

    private static int ToDisplayedSeconds(long remainingMilliseconds) =>
        checked((int)((remainingMilliseconds + 999L) / 1_000L));
}
