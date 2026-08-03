namespace Flux.Models;

public sealed record WorkoutGroup(
    string Id,
    string DisplayName,
    int Order,
    IReadOnlySet<CanonicalMuscleGroup> CanonicalGroups);

public sealed record WorkoutResolution(
    int Minutes,
    IReadOnlyList<WorkoutGroup> Groups);
