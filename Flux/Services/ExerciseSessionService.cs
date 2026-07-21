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
        NormalizeLegacyOutcomes(state);
        state.Version = 3;

        if (state.SelectedExercises.Count == 0)
        {
            CreateInitialLineup(state);
        }

        RepairLineup(state);
        NormalizeCompletionState(state);
        NormalizePendingRest(state);

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

    public Exercise RecordOutcome(
        WorkoutState state,
        DominantRegion region,
        bool keep)
    {
        DominantRegion? nextRegion = GetNextRegion(state);

        if (nextRegion is null || nextRegion.Value != region)
        {
            throw new InvalidOperationException($"{region} is not the next workout region.");
        }

        Exercise exercise = GetSelectedExercise(state, region);

        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        if (!keep)
        {
            exercise.Score--;
        }

        state.Outcomes[region] = outcome;
        state.WorkoutCompleted = OrderedRegions.All(state.Outcomes.ContainsKey);
        state.CompletionAcknowledged = false;
        return exercise;
    }

    public void AcknowledgeCompletion(WorkoutState state)
    {
        if (!state.WorkoutCompleted)
        {
            throw new InvalidOperationException("The workout is not complete.");
        }

        state.CompletionAcknowledged = true;
    }

    public (int Replaced, int Kept) GetOutcomeCounts(WorkoutState state)
    {
        int replaced = state.Outcomes.Count(entry => entry.Value == ExerciseOutcome.X);
        int kept = state.Outcomes.Count - replaced;
        return (replaced, kept);
    }

    public void ClearPendingRest(WorkoutState state)
    {
        state.PendingRestRegion = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
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
        ClearPendingRest(state);
    }

    private void PrepareNextSession(WorkoutState state)
    {
        var regionsToReplace = state.Outcomes
            .Where(entry => entry.Value == ExerciseOutcome.X)
            .Select(entry => entry.Key)
            .ToHashSet();

        foreach (DominantRegion region in regionsToReplace)
        {
            string currentName = state.SelectedExercises[region];
            Exercise replacement = ChooseFromHighestScoreBucket(region, currentName);
            state.SelectedExercises[region] = replacement.Name;
        }

        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingRest(state);
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
        state.SelectedExercises ??= [];
        state.Outcomes ??= [];
    }

    private static void NormalizeLegacyOutcomes(WorkoutState state)
    {
        foreach (DominantRegion region in state.Outcomes
                     .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            state.Outcomes[region] = ExerciseOutcome.Tick;
        }
    }

    private void NormalizePendingRest(WorkoutState state)
    {
        if (state.PendingRestRegion is null)
        {
            ClearPendingRest(state);
            return;
        }

        DominantRegion? nextRegion = GetNextRegion(state);
        if (state.WorkoutCompleted ||
            nextRegion != state.PendingRestRegion ||
            state.PendingRestEndsAtUnixMilliseconds <= 0)
        {
            ClearPendingRest(state);
        }
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
