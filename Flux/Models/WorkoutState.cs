namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 19;

    public int CatalogRevision { get; set; }

    public Dictionary<string, int> SelectedExerciseIds { get; set; } = [];

    public Dictionary<string, ExerciseOutcome> Outcomes { get; set; } = [];

    public HashSet<int> LastKeptExerciseIds { get; set; } = [];

    public Dictionary<string, long>
        LastHardWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    public Dictionary<string, long>
        LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    public HashSet<int> NextWorkoutExcludedExerciseIds { get; set; } = [];

    public HashSet<string> ActiveExtraSetSelectionGroupIds { get; set; } = [];

    public Dictionary<string, int> ActiveSetCountsBySelectionGroupId { get; set; } = [];

    // Version 16 migration inputs only. Atomic sequences replace both legacy
    // direction-partner allocation and split-side timing in version 17; the
    // fields remain readable so in-progress upgrades can still be recovered.
    public Dictionary<string, int> ActiveDirectionPartnerExerciseIds { get; set; } = [];

    public HashSet<string> ActiveFullSideRoundIds { get; set; } = [];

    public string? PendingMovementGroupId { get; set; }

    public long PendingMovementMillisecondsRemaining { get; set; }

    public long PendingMovementEndsAtUnixMilliseconds { get; set; }

    public bool PendingMovementPausedByUser { get; set; }

    public string? PendingRestGroupId { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public long PendingRestMillisecondsRemaining { get; set; }

    public bool PendingRestPausedByUser { get; set; }

    public bool PendingRestKept { get; set; }

    public int PendingScoreExerciseId { get; set; }

    public int PendingScoreValue { get; set; }

    public Dictionary<int, int> PendingScoreUpdates { get; set; } = [];

    public int LastWorkoutMinutes { get; set; } = 10;

    public WorkoutModifiers LastWorkoutModifiers { get; set; } =
        WorkoutModifiers.Silence;

    public int ActiveWorkoutMinutes { get; set; }

    public WorkoutModifiers ActiveWorkoutModifiers { get; set; } =
        WorkoutModifiers.None;

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, string> LegacySelectedExerciseNames { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, ExerciseOutcome> LegacyOutcomes { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public string? LegacyPendingRestGroup { get; set; }
}
