namespace Flux.Services;

public enum MovementPhase
{
    Preparation,
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
    public const int PreparationDurationSeconds = 5;
    public const int TotalDurationSeconds = 45;
    public const int SideDurationSeconds = 20;
    public const int SideChangeDurationSeconds = 5;
    public const int FullSideDurationSeconds = 45;
    public const int FullSideChangeDurationSeconds = 15;
    public const int FullSideTotalDurationSeconds = 105;

    public static int GetCountdownDurationSeconds(bool usesFullSideTiming) =>
        PreparationDurationSeconds +
        (usesFullSideTiming
            ? FullSideTotalDurationSeconds
            : TotalDurationSeconds);

    private const long TotalDurationMilliseconds =
        TotalDurationSeconds * 1_000L;
    public static MovementPhaseState GetState(
        long remainingMilliseconds,
        bool usesTimedSides,
        bool usesFullSideTiming = false)
    {
        if (remainingMilliseconds <= 0)
        {
            return new MovementPhaseState(
                MovementPhase.Complete,
                SecondsRemaining: 0,
                SegmentDurationSeconds: 0,
                IsExercise: false);
        }

        long movementDurationMilliseconds = usesFullSideTiming
            ? FullSideTotalDurationSeconds * 1_000L
            : TotalDurationMilliseconds;
        long totalDurationMilliseconds = movementDurationMilliseconds +
            PreparationDurationSeconds * 1_000L;
        var boundedRemainingMilliseconds = Math.Min(
            remainingMilliseconds,
            totalDurationMilliseconds);

        if (boundedRemainingMilliseconds > movementDurationMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.Preparation,
                ToDisplayedSeconds(
                    boundedRemainingMilliseconds - movementDurationMilliseconds),
                PreparationDurationSeconds,
                IsExercise: false);
        }

        long boundedMovementMilliseconds = Math.Min(
            boundedRemainingMilliseconds,
            movementDurationMilliseconds);

        if (!usesTimedSides)
        {
            return new MovementPhaseState(
                MovementPhase.Continuous,
                ToDisplayedSeconds(boundedMovementMilliseconds),
                TotalDurationSeconds,
                IsExercise: true);
        }

        int sideDurationSeconds = usesFullSideTiming
            ? FullSideDurationSeconds
            : SideDurationSeconds;
        int sideChangeDurationSeconds = usesFullSideTiming
            ? FullSideChangeDurationSeconds
            : SideChangeDurationSeconds;
        long secondSideStartMilliseconds = sideDurationSeconds * 1_000L;
        long firstSideEndMilliseconds =
            (sideDurationSeconds + sideChangeDurationSeconds) * 1_000L;

        if (boundedMovementMilliseconds > firstSideEndMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.FirstSide,
                ToDisplayedSeconds(
                    boundedMovementMilliseconds - firstSideEndMilliseconds),
                sideDurationSeconds,
                IsExercise: true);
        }

        if (boundedMovementMilliseconds > secondSideStartMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.ChangeSides,
                ToDisplayedSeconds(
                    boundedMovementMilliseconds - secondSideStartMilliseconds),
                sideChangeDurationSeconds,
                IsExercise: false);
        }

        return new MovementPhaseState(
            MovementPhase.SecondSide,
            ToDisplayedSeconds(boundedMovementMilliseconds),
            sideDurationSeconds,
            IsExercise: true);
    }

    private static int ToDisplayedSeconds(long remainingMilliseconds) =>
        checked((int)((remainingMilliseconds + 999L) / 1_000L));
}
