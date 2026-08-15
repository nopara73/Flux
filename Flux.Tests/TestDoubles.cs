using Flux.Data;
using Flux.Models;

namespace Flux.Tests;

internal sealed class FakeExerciseDatabase : IExerciseDatabase
{
    private readonly Dictionary<int, int> _persistedScores;

    public FakeExerciseDatabase(IReadOnlyList<Exercise> exercises)
    {
        Exercises = exercises;
        _persistedScores = exercises.ToDictionary(exercise => exercise.Id, exercise => exercise.Score);
    }

    public IReadOnlyList<Exercise> Exercises { get; }

    public List<(int ExerciseId, int Score)> Updates { get; } = [];

    public int PersistedScore(int exerciseId) => _persistedScores[exerciseId];

    public void UpdateScore(Exercise exercise)
    {
        _persistedScores[exercise.Id] = exercise.Score;
        Updates.Add((exercise.Id, exercise.Score));
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeWorkoutStateStore : IWorkoutStateStore
{
    private WorkoutState _storedState = new();

    public int SaveCalls { get; private set; }

    public WorkoutState Load() => Clone(_storedState);

    public void Save(WorkoutState state)
    {
        _storedState = Clone(state);
        SaveCalls++;
    }

    private static WorkoutState Clone(WorkoutState state)
    {
        return new WorkoutState
        {
            Version = state.Version,
            CatalogRevision = state.CatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>(
                state.SelectedExerciseIds,
                StringComparer.Ordinal),
            Outcomes = new Dictionary<string, ExerciseOutcome>(
                state.Outcomes,
                StringComparer.Ordinal),
            LastKeptExerciseIds = new HashSet<int>(state.LastKeptExerciseIds),
            ActiveExtraSetSelectionGroupIds = new HashSet<string>(
                state.ActiveExtraSetSelectionGroupIds,
                StringComparer.Ordinal),
            ActiveFullSideSelectionGroupIds = new HashSet<string>(
                state.ActiveFullSideSelectionGroupIds,
                StringComparer.Ordinal),
            PendingRestGroupId = state.PendingRestGroupId,
            PendingRestEndsAtUnixMilliseconds = state.PendingRestEndsAtUnixMilliseconds,
            PendingRestKept = state.PendingRestKept,
            PendingScoreExerciseId = state.PendingScoreExerciseId,
            PendingScoreValue = state.PendingScoreValue,
            LastWorkoutMinutes = state.LastWorkoutMinutes,
            LastWorkoutModifiers = state.LastWorkoutModifiers,
            ActiveWorkoutMinutes = state.ActiveWorkoutMinutes,
            ActiveWorkoutModifiers = state.ActiveWorkoutModifiers,
            WorkoutCompleted = state.WorkoutCompleted,
            CompletionAcknowledged = state.CompletionAcknowledged,
            LegacySelectedExerciseNames = new Dictionary<string, string>(
                state.LegacySelectedExerciseNames,
                StringComparer.Ordinal),
            LegacyOutcomes = new Dictionary<string, ExerciseOutcome>(
                state.LegacyOutcomes,
                StringComparer.Ordinal),
            LegacyPendingRestGroup = state.LegacyPendingRestGroup,
        };
    }
}

internal static class ScoreJournalProtocol
{
    public static void Stage(
        WorkoutState state,
        Exercise exercise,
        IWorkoutStateStore stateStore)
    {
        state.PendingScoreExerciseId = exercise.Id;
        state.PendingScoreValue = exercise.Score;
        stateStore.Save(state);
    }

    public static void Recover(
        WorkoutState state,
        IWorkoutStateStore stateStore,
        IExerciseDatabase database)
    {
        if (state.PendingScoreExerciseId <= 0)
        {
            return;
        }

        Exercise? exercise = database.Exercises.SingleOrDefault(candidate =>
            candidate.Id == state.PendingScoreExerciseId);
        if (exercise is not null)
        {
            exercise.Score = state.PendingScoreValue;
            database.UpdateScore(exercise);
        }

        state.PendingScoreExerciseId = 0;
        state.PendingScoreValue = 0;
        stateStore.Save(state);
    }
}
