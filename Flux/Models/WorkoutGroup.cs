namespace Flux.Models;

public sealed record WorkoutGroup(
    string Id,
    string DisplayName,
    int Order,
    IReadOnlySet<CanonicalMuscleGroup> CanonicalGroups,
    string? SelectionGroupId = null)
{
    public string SelectionKey => SelectionGroupId ?? Id;
}

public sealed record WorkoutResolution(
    int Minutes,
    IReadOnlyList<WorkoutGroup> Groups);
