using Flux.Models;

namespace Flux.Services;

public enum MovementDirectionCue
{
    None,
    Move,
    Switch,
    ScreenLeft,
    ScreenRight,
    Forward,
    Backward,
    Clockwise,
    Counterclockwise,
    Inward,
    Outward,
}

public enum ScreenSide
{
    Left,
    Right,
}

public readonly record struct MovementPhasePresentation(
    MovementDirectionCue Cue,
    bool MirrorMedia,
    ScreenSide? ActiveScreenSide);

public static class MovementPhasePresentationPolicy
{
    public static bool UsesTimedPair(
        ExerciseSideSequence sideSequence,
        ExerciseDirectionSequence directionSequence)
    {
        ValidateSequenceCombination(sideSequence, directionSequence);
        return sideSequence.UsesTimedSides() ||
            directionSequence != ExerciseDirectionSequence.None;
    }

    public static MovementPhasePresentation GetPresentation(
        ExerciseSideSequence sideSequence,
        ExerciseDirectionSequence directionSequence,
        MovementPhase phase)
    {
        ValidateSequenceCombination(sideSequence, directionSequence);

        if (phase == MovementPhase.Complete)
        {
            return new MovementPhasePresentation(
                MovementDirectionCue.None,
                MirrorMedia: false,
                ActiveScreenSide: null);
        }

        bool continuous = !sideSequence.UsesTimedSides() &&
            directionSequence == ExerciseDirectionSequence.None;
        if (continuous)
        {
            if (phase != MovementPhase.Continuous)
            {
                throw IncompatiblePhase(sideSequence, directionSequence, phase);
            }

            return new MovementPhasePresentation(
                MovementDirectionCue.Move,
                MirrorMedia: false,
                ActiveScreenSide: null);
        }

        if (phase == MovementPhase.Continuous)
        {
            throw IncompatiblePhase(sideSequence, directionSequence, phase);
        }

        if (phase == MovementPhase.ChangeSides)
        {
            return new MovementPhasePresentation(
                MovementDirectionCue.Switch,
                MirrorMedia: false,
                ActiveScreenSide: null);
        }

        bool secondDirection = phase switch
        {
            MovementPhase.FirstSide => false,
            MovementPhase.SecondSide => true,
            _ => throw IncompatiblePhase(sideSequence, directionSequence, phase),
        };
        (MovementDirectionCue first, MovementDirectionCue second) =
            GetDirectionPair(sideSequence, directionSequence);
        MovementDirectionCue cue = secondDirection ? second : first;
        ScreenSide? activeScreenSide = cue switch
        {
            MovementDirectionCue.ScreenLeft => ScreenSide.Left,
            MovementDirectionCue.ScreenRight => ScreenSide.Right,
            _ => null,
        };

        return new MovementPhasePresentation(
            cue,
            MirrorMedia: secondDirection && activeScreenSide is not null,
            activeScreenSide);
    }

    private static (MovementDirectionCue First, MovementDirectionCue Second)
        GetDirectionPair(
            ExerciseSideSequence sideSequence,
            ExerciseDirectionSequence directionSequence)
    {
        if (sideSequence.UsesTimedSides())
        {
            return sideSequence switch
            {
                ExerciseSideSequence.ScreenLeftThenRight =>
                (MovementDirectionCue.ScreenLeft, MovementDirectionCue.ScreenRight),
                ExerciseSideSequence.ScreenRightThenLeft =>
                (MovementDirectionCue.ScreenRight, MovementDirectionCue.ScreenLeft),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(sideSequence),
                    sideSequence,
                    null),
            };
        }

        return directionSequence switch
        {
            ExerciseDirectionSequence.ForwardThenBackward =>
                (MovementDirectionCue.Forward, MovementDirectionCue.Backward),
            ExerciseDirectionSequence.BackwardThenForward =>
                (MovementDirectionCue.Backward, MovementDirectionCue.Forward),
            ExerciseDirectionSequence.ClockwiseThenCounterclockwise =>
                (MovementDirectionCue.Clockwise, MovementDirectionCue.Counterclockwise),
            ExerciseDirectionSequence.CounterclockwiseThenClockwise =>
                (MovementDirectionCue.Counterclockwise, MovementDirectionCue.Clockwise),
            ExerciseDirectionSequence.InwardThenOutward =>
                (MovementDirectionCue.Inward, MovementDirectionCue.Outward),
            ExerciseDirectionSequence.OutwardThenInward =>
                (MovementDirectionCue.Outward, MovementDirectionCue.Inward),
            _ => throw new ArgumentOutOfRangeException(
                nameof(directionSequence),
                directionSequence,
                null),
        };
    }

    private static void ValidateSequenceCombination(
        ExerciseSideSequence sideSequence,
        ExerciseDirectionSequence directionSequence)
    {
        if (!Enum.IsDefined(sideSequence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sideSequence),
                sideSequence,
                null);
        }
        if (!Enum.IsDefined(directionSequence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(directionSequence),
                directionSequence,
                null);
        }
        if (sideSequence.UsesTimedSides() &&
            directionSequence != ExerciseDirectionSequence.None)
        {
            throw new ArgumentException(
                "An exercise cannot use both side and direction timed protocols.",
                nameof(directionSequence));
        }
    }

    private static ArgumentException IncompatiblePhase(
        ExerciseSideSequence sideSequence,
        ExerciseDirectionSequence directionSequence,
        MovementPhase phase)
    {
        return new ArgumentException(
            $"Movement phase {phase} is incompatible with side sequence " +
            $"{sideSequence} and direction sequence {directionSequence}.",
            nameof(phase));
    }
}
