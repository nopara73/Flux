namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 2;

    public Dictionary<DominantRegion, string> SelectedExercises { get; set; } = [];

    public Dictionary<DominantRegion, ExerciseOutcome> Outcomes { get; set; } = [];

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }
}
