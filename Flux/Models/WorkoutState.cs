namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 1;

    public Dictionary<string, int> Scores { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<DominantRegion, string> SelectedExercises { get; set; } = [];

    public Dictionary<DominantRegion, ExerciseOutcome> Outcomes { get; set; } = [];

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }
}
