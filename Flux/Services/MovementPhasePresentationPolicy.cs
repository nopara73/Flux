using Flux.Models;

namespace Flux.Services;

public enum ScreenSide
{
    Left,
    Right,
}

public readonly record struct MovementPhasePresentation(
    ExerciseSequenceSideCue SideCue,
    ExerciseSequenceDirectionCue DirectionCue,
    bool MirrorMedia,
    ScreenSide? ActiveScreenSide);

public static class MovementPhasePresentationPolicy
{
    public static MovementPhasePresentation GetPresentation(
        ExerciseSequenceSideCue sideCue,
        ExerciseSequenceDirectionCue directionCue,
        bool mirrorMedia,
        MovementPhase phase)
    {
        if (phase == MovementPhase.Complete)
        {
            return new MovementPhasePresentation(
                ExerciseSequenceSideCue.None,
                ExerciseSequenceDirectionCue.None,
                MirrorMedia: false,
                ActiveScreenSide: null);
        }

        ScreenSide? activeScreenSide = sideCue switch
        {
            ExerciseSequenceSideCue.ScreenLeft => ScreenSide.Left,
            ExerciseSequenceSideCue.ScreenRight => ScreenSide.Right,
            _ => null,
        };
        return new MovementPhasePresentation(
            sideCue,
            directionCue,
            mirrorMedia,
            activeScreenSide);
    }
}
