namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 4;

    public Dictionary<MuscleGroup, string> SelectedExercises { get; set; } = [];

    public Dictionary<MuscleGroup, ExerciseOutcome> Outcomes { get; set; } = [];

    public MuscleGroup? PendingRestMuscleGroup { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public bool PendingRestKept { get; set; }

    public int LastWorkoutMinutes { get; set; } = 10;

    public int ActiveWorkoutMinutes { get; set; }

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }
}
