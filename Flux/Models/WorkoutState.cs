namespace Flux.Models;

public sealed class WorkoutState
{
    public int Version { get; set; } = 23;

    public int CatalogRevision { get; set; }

    public Dictionary<string, int> SelectedExerciseIds { get; set; } = [];

    public Dictionary<string, ExerciseOutcome> Outcomes { get; set; } = [];

    public HashSet<int> LastKeptExerciseIds { get; set; } = [];

    // Preferences are keyed by the stable logical selection slot (for example,
    // r10.upper-limbs), never by modifier profile. Values are sequence-root IDs:
    // one Keep/reject decision therefore remains one preference even when the
    // selected sequence contains several exercise blocks or repeated sets.
    public Dictionary<string, HashSet<int>>
        KeptExerciseRootIdsBySelectionGroupId { get; set; } = [];

    // Version 22 migration input only. Downvotes stopped following anatomical
    // selection slots in version 23; retain this field so those votes can be
    // migrated without losing the user's feedback.
    public Dictionary<string, Dictionary<int, int>>
        ExerciseScoreAdjustmentsBySelectionGroupId { get; set; } = [];

    // New downvotes follow the sequence root only within the workout phase in
    // which the user rejected it. The immutable catalog score remains the
    // baseline for feedback recorded before phase-scoped persistence existed.
    public Dictionary<WorkoutExercisePhase, Dictionary<int, int>>
        ExerciseScoreAdjustmentsByPhase { get; set; } = [];

    public Dictionary<string, long>
        LastHardWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    public Dictionary<string, long>
        LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    public long NextWorkoutSessionId { get; set; } = 1;

    public WorkoutSessionLog? ActiveWorkoutSession { get; set; }

    public List<WorkoutSessionLog> WorkoutHistory { get; set; } = [];

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
        WorkoutModifiers.HardFloor | WorkoutModifiers.Silence;

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
