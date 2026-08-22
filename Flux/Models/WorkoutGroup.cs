namespace Flux.Models;

public sealed record WorkoutGroup(
    string Id,
    string DisplayName,
    int Order,
    IReadOnlySet<CanonicalMuscleGroup> CanonicalGroups,
    string? SelectionGroupId = null,
    bool UsesFullSideTiming = false,
    int ExerciseOverrideId = 0,
    string? PairedRoundId = null,
    bool IsPairDecisionRound = false)
{
    public string SelectionKey => SelectionGroupId ?? Id;

    public bool IsDirectionPairRound => PairedRoundId is not null;

    public bool IsDirectionPairLead =>
        IsDirectionPairRound && !IsPairDecisionRound;
}

public sealed record WorkoutResolution(
    int Minutes,
    IReadOnlyList<WorkoutGroup> Groups);
