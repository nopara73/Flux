namespace Flux.Models;

public enum ExerciseSideSequence
{
    Continuous,
    Alternating,
    ScreenLeftThenRight,
    ScreenRightThenLeft,
}

public static class ExerciseSideSequenceExtensions
{
    public static bool UsesTimedSides(this ExerciseSideSequence sequence) =>
        sequence is ExerciseSideSequence.ScreenLeftThenRight or
            ExerciseSideSequence.ScreenRightThenLeft;
}
