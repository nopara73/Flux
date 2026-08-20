namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 10;

    public int CatalogRevision { get; set; }

    public Dictionary<string, int> SelectedExerciseIds { get; set; } = [];

    public Dictionary<string, ExerciseOutcome> Outcomes { get; set; } = [];

    public HashSet<int> LastKeptExerciseIds { get; set; } = [];

    public Dictionary<int, string> LastKeptLocalDateByExerciseId { get; set; } = [];

    public HashSet<int> NextWorkoutExcludedExerciseIds { get; set; } = [];

    public HashSet<int> ActiveRecoveryExcludedExerciseIds { get; set; } = [];

    public HashSet<string> ActiveExtraSetSelectionGroupIds { get; set; } = [];

    public Dictionary<string, int> ActiveDirectionPartnerExerciseIds { get; set; } = [];

    public HashSet<string> ActiveFullSideRoundIds { get; set; } = [];

    public string? PendingRestGroupId { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public bool PendingRestKept { get; set; }

    public int PendingScoreExerciseId { get; set; }

    public int PendingScoreValue { get; set; }

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
