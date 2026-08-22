namespace Flux.Models;

public sealed record RecordedWorkoutOutcome(
    Exercise Exercise,
    IReadOnlyList<Exercise> ScoreUpdates);
