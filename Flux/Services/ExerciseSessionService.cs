using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    private static readonly IReadOnlyList<DominantRegion> OrderedRegions =
        Array.AsReadOnly(Enum.GetValues<DominantRegion>());

    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly Random _random;

    public ExerciseSessionService(IReadOnlyList<Exercise> exercises, Random? random = null)
    {
        _exercises = exercises;
        _random = random ?? Random.Shared;
    }

    public static IReadOnlyList<DominantRegion> RegionOrder => OrderedRegions;

    public void Initialize(WorkoutState state)
    {
        NormalizeCollections(state);
        ApplyPersistedScores(state);

        if (state.SelectedExercises.Count == 0)
        {
            CreateInitialLineup(state);
        }

        RepairLineup(state);
        NormalizeCompletionState(state);

        if (state.WorkoutCompleted && state.CompletionAcknowledged)
        {
            PrepareNextSession(state);
        }
    }

    public Exercise GetSelectedExercise(WorkoutState state, DominantRegion region)
    {
        if (!state.SelectedExercises.TryGetValue(region, out string? exerciseName))
        {
            throw new InvalidOperationException($"No exercise is selected for {region}.");
        }

        return _exercises.Single(exercise =>
            exercise.DominantRegion == region && exercise.Name == exerciseName);
    }

    public DominantRegion? GetNextRegion(WorkoutState state)
    {
        foreach (DominantRegion region in OrderedRegions)
        {
            if (!state.Outcomes.ContainsKey(region))
            {
                return region;
            }
        }

        return null;
    }

    public void RecordOutcome(
        WorkoutState state,
        DominantRegion region,
        ExerciseOutcome outcome)
    {
        DominantRegion? nextRegion = GetNextRegion(state);

        if (nextRegion is null || nextRegion.Value != region)
        {
            throw new InvalidOperationException($"{region} is not the next workout region.");
        }

        Exercise exercise = GetSelectedExercise(state, region);

        if (outcome == ExerciseOutcome.X)
        {
            exercise.Score--;
            state.Scores[exercise.Name] = exercise.Score;
        }

        state.Outcomes[region] = outcome;
        state.WorkoutCompleted = OrderedRegions.All(state.Outcomes.ContainsKey);
        state.CompletionAcknowledged = false;
    }

    public void AcknowledgeCompletion(WorkoutState state)
    {
        if (!state.WorkoutCompleted)
        {
            throw new InvalidOperationException("The workout is not complete.");
        }

        state.CompletionAcknowledged = true;
    }

    public (int Failed, int Neutral, int Completed) GetOutcomeCounts(WorkoutState state)
    {
        int failed = state.Outcomes.Count(entry => entry.Value == ExerciseOutcome.X);
        int neutral = state.Outcomes.Count(entry => entry.Value == ExerciseOutcome.Neutral);
        int completed = state.Outcomes.Count(entry => entry.Value == ExerciseOutcome.Tick);
        return (failed, neutral, completed);
    }

    private void CreateInitialLineup(WorkoutState state)
    {
        foreach (DominantRegion region in OrderedRegions)
        {
            Exercise exercise = ChooseFromHighestScoreBucket(region, excludedName: null);
            state.SelectedExercises[region] = exercise.Name;
        }

        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
    }

    private void PrepareNextSession(WorkoutState state)
    {
        var regionsToReplace = state.Outcomes
            .Where(entry => entry.Value == ExerciseOutcome.X)
            .Select(entry => entry.Key)
            .ToHashSet();

        if (regionsToReplace.Count == 0)
        {
            DominantRegion[] neutralRegions = state.Outcomes
                .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                .Select(entry => entry.Key)
                .ToArray();

            if (neutralRegions.Length > 0)
            {
                regionsToReplace.Add(neutralRegions[_random.Next(neutralRegions.Length)]);
            }
        }

        foreach (DominantRegion region in regionsToReplace)
        {
            string currentName = state.SelectedExercises[region];
            Exercise replacement = ChooseFromHighestScoreBucket(region, currentName);
            state.SelectedExercises[region] = replacement.Name;
        }

        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
    }

    private Exercise ChooseFromHighestScoreBucket(
        DominantRegion region,
        string? excludedName)
    {
        Exercise[] candidates = _exercises
            .Where(exercise =>
                exercise.DominantRegion == region && exercise.Name != excludedName)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException($"No replacement exercise exists for {region}.");
        }

        int highestScore = candidates.Max(exercise => exercise.Score);
        Exercise[] highestScoreBucket = candidates
            .Where(exercise => exercise.Score == highestScore)
            .ToArray();

        return highestScoreBucket[_random.Next(highestScoreBucket.Length)];
    }

    private void ApplyPersistedScores(WorkoutState state)
    {
        foreach (Exercise exercise in _exercises)
        {
            if (state.Scores.TryGetValue(exercise.Name, out int score))
            {
                exercise.Score = score;
            }
            else
            {
                state.Scores[exercise.Name] = exercise.Score;
            }
        }
    }

    private void RepairLineup(WorkoutState state)
    {
        foreach (DominantRegion region in OrderedRegions)
        {
            bool hasValidSelection = state.SelectedExercises.TryGetValue(
                    region,
                    out string? selectedName) &&
                _exercises.Any(exercise =>
                    exercise.DominantRegion == region && exercise.Name == selectedName);

            if (!hasValidSelection)
            {
                state.SelectedExercises[region] =
                    ChooseFromHighestScoreBucket(region, excludedName: null).Name;
                state.Outcomes.Remove(region);
            }
        }
    }

    private static void NormalizeCollections(WorkoutState state)
    {
        state.Scores ??= new Dictionary<string, int>(StringComparer.Ordinal);
        state.SelectedExercises ??= [];
        state.Outcomes ??= [];
    }

    private static void NormalizeCompletionState(WorkoutState state)
    {
        state.WorkoutCompleted = OrderedRegions.All(state.Outcomes.ContainsKey);

        if (!state.WorkoutCompleted)
        {
            state.CompletionAcknowledged = false;
        }
    }
}
