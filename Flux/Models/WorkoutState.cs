namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 3;

    public Dictionary<DominantRegion, string> SelectedExercises { get; set; } = [];

    public Dictionary<DominantRegion, ExerciseOutcome> Outcomes { get; set; } = [];

    public DominantRegion? PendingRestRegion { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public bool PendingRestKept { get; set; }

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }
}
