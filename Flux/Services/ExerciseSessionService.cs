using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 30;
    public const int DefaultWorkoutMinutes = 10;

    private const int CurrentStateVersion = 5;

    private static readonly IReadOnlyDictionary<string, WorkoutGroup> KnownWorkoutGroups =
        MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly IReadOnlyDictionary<int, Exercise> _exercisesById;
    private readonly Random _random;

    public ExerciseSessionService(IReadOnlyList<Exercise> exercises, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        _exercises = exercises;
        _exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        _random = random ?? Random.Shared;
    }

    public static IReadOnlyList<int> SupportedWorkoutMinutes =>
        MassGroupingTaxonomy.SupportedMinutes;

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        NormalizeCollections(state);
        bool migratedLegacyState = state.Version < CurrentStateVersion ||
            state.LegacySelectedExerciseNames.Count > 0;
        if (migratedLegacyState)
        {
            MigrateLegacyLineups(state);
        }

        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);
        NormalizeSavedLineups(state);

        if (migratedLegacyState && state.ActiveWorkoutMinutes > 0)
        {
            if (state.WorkoutCompleted && state.CompletionAcknowledged)
            {
                ResetToDurationSelection(state);
                ClearLegacyMigrationState(state);
            }

            return;
        }

        if (state.ActiveWorkoutMinutes == 0)
        {
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return;
        }

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return;
        }

        NormalizeOutcomes(state);
        NormalizeCompletionState(state);
        NormalizePendingRest(state);
        RepairActiveLineup(state);
        NormalizeCompletionState(state);

        if (state.WorkoutCompleted && state.CompletionAcknowledged)
        {
            PrepareNextSession(state);
        }
    }

    public void StartWorkout(WorkoutState state, int minutes)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!IsValidWorkoutMinutes(minutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                minutes,
                "Workout duration must be one of 3, 5, 7, 10, 15, 20, or 30 minutes.");
        }

        if (state.ActiveWorkoutMinutes != 0)
        {
            throw new InvalidOperationException("A workout is already active.");
        }

        NormalizeCollections(state);
        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = minutes;
        state.ActiveWorkoutMinutes = minutes;
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingRest(state);
        ClearLegacyMigrationState(state);
        RepairActiveLineup(state);
    }

    public IReadOnlyList<WorkoutGroup> GetActiveGroups(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? MassGroupingTaxonomy.GetResolution(state.ActiveWorkoutMinutes).Groups
            : [];
    }

    public Exercise GetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (!state.SelectedExerciseIds.TryGetValue(group.Id, out int exerciseId) ||
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            !IsSavedSelectionValid(state, exercise, group))
        {
            throw new InvalidOperationException(
                $"No eligible exercise is selected for {group.DisplayName}.");
        }

        return exercise;
    }

    public WorkoutGroup? GetNextGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return GetActiveGroups(state)
            .FirstOrDefault(group => !state.Outcomes.ContainsKey(group.Id));
    }

    public Exercise RecordOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }

        Exercise exercise = GetSelectedExercise(state, group);
        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        if (!keep)
        {
            exercise.Score--;
        }

        state.Outcomes[group.Id] = outcome;
        state.WorkoutCompleted = GetActiveGroups(state)
            .All(activeGroup => state.Outcomes.ContainsKey(activeGroup.Id));
        state.CompletionAcknowledged = false;
        return exercise;
    }

    public void AcknowledgeCompletion(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.WorkoutCompleted)
        {
            throw new InvalidOperationException("The workout is not complete.");
        }

        state.CompletionAcknowledged = true;
    }

    public Exercise? FinishInterruptedWorkout(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.LegacySelectedExerciseNames.Count > 0)
        {
            Exercise? legacyPenalty = ResolveLegacyPendingRest(state);
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return legacyPenalty;
        }

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            ResetToDurationSelection(state);
            return null;
        }

        Exercise? scorePenalty = null;
        if (state.PendingRestGroupId is string pendingGroupId)
        {
            WorkoutGroup? pendingGroup = GetActiveGroups(state)
                .SingleOrDefault(group => group.Id == pendingGroupId);
            if (pendingGroup is not null && GetNextGroup(state)?.Id == pendingGroupId)
            {
                bool keep = state.PendingRestKept;
                Exercise exercise = RecordOutcome(state, pendingGroup, keep);
                if (!keep)
                {
                    scorePenalty = exercise;
                }
            }

            ClearPendingRest(state);
        }

        PrepareNextSession(state);
        return scorePenalty;
    }

    public (int Replaced, int Kept) GetOutcomeCounts(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        HashSet<string> activeGroupIds = GetActiveGroups(state)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        int replaced = state.Outcomes.Count(entry =>
            activeGroupIds.Contains(entry.Key) && entry.Value == ExerciseOutcome.X);
        int kept = state.Outcomes.Count(entry =>
            activeGroupIds.Contains(entry.Key) && entry.Value != ExerciseOutcome.X);
        return (replaced, kept);
    }

    public void ClearPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
    }

    private void PrepareNextSession(WorkoutState state)
    {
        WorkoutGroup[] activeGroups = GetActiveGroups(state).ToArray();
        var usedExerciseIds = activeGroups
            .Where(group => !state.Outcomes.TryGetValue(
                group.Id,
                out ExerciseOutcome outcome) || outcome != ExerciseOutcome.X)
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(group.Id))
            .Where(exerciseId => exerciseId != 0)
            .ToHashSet();

        foreach (WorkoutGroup group in activeGroups.Where(group =>
                     state.Outcomes.TryGetValue(group.Id, out ExerciseOutcome outcome) &&
                     outcome == ExerciseOutcome.X))
        {
            int currentExerciseId = state.SelectedExerciseIds[group.Id];
            foreach (string savedGroupId in state.SelectedExerciseIds
                         .Where(entry =>
                             entry.Key != group.Id &&
                             entry.Value == currentExerciseId)
                         .Select(entry => entry.Key)
                         .ToArray())
            {
                state.SelectedExerciseIds.Remove(savedGroupId);
            }

            var excludedExerciseIds = new HashSet<int>(usedExerciseIds)
            {
                currentExerciseId,
            };
            Exercise replacement = ChooseBestCandidate(
                group,
                excludedExerciseIds);
            state.SelectedExerciseIds[group.Id] = replacement.Id;
            usedExerciseIds.Add(replacement.Id);
        }

        ResetToDurationSelection(state);
    }

    private Exercise ChooseBestCandidate(
        WorkoutGroup group,
        IReadOnlySet<int> excludedExerciseIds)
    {
        Exercise[] candidates = _exercises
            .Where(exercise =>
                WorkoutCoveragePolicy.IsSelectable(exercise, group) &&
                !excludedExerciseIds.Contains(exercise.Id))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"No distinct primary-owned exercise with at least " +
                $"{WorkoutCoveragePolicy.MinimumCoveragePercent}% coverage exists for " +
                $"{group.DisplayName}.");
        }

        int highestScore = candidates.Max(exercise => exercise.Score);
        Exercise[] highestScoreBucket = candidates
            .Where(exercise => exercise.Score == highestScore)
            .ToArray();

        (Exercise Exercise, int Coverage)[] coveredCandidates = highestScoreBucket
            .Select(exercise => (
                exercise,
                WorkoutCoveragePolicy.GetCanonicalCoverage(exercise, group)))
            .ToArray();
        int highestCoverage = coveredCandidates.Max(candidate => candidate.Coverage);
        Exercise[] broadestCoverageBucket = coveredCandidates
            .Where(candidate => candidate.Coverage == highestCoverage)
            .Select(candidate => candidate.Exercise)
            .ToArray();

        return broadestCoverageBucket[_random.Next(broadestCoverageBucket.Length)];
    }

    private void RepairActiveLineup(WorkoutState state)
    {
        var usedExerciseIds = new HashSet<int>();

        foreach (WorkoutGroup group in GetActiveGroups(state))
        {
            bool hasValidSelection = state.SelectedExerciseIds.TryGetValue(
                    group.Id,
                    out int selectedExerciseId) &&
                !usedExerciseIds.Contains(selectedExerciseId) &&
                _exercisesById.TryGetValue(selectedExerciseId, out Exercise? selected) &&
                IsSavedSelectionValid(state, selected, group);

            if (!hasValidSelection)
            {
                var excludedExerciseIds = new HashSet<int>(usedExerciseIds);
                if (selectedExerciseId != 0)
                {
                    excludedExerciseIds.Add(selectedExerciseId);
                }

                Exercise replacement = ChooseBestCandidate(
                    group,
                    excludedExerciseIds);
                state.SelectedExerciseIds[group.Id] = replacement.Id;
                state.Outcomes.Remove(group.Id);
                selectedExerciseId = replacement.Id;
            }

            usedExerciseIds.Add(selectedExerciseId);
        }
    }

    private static bool IsSavedSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group)
    {
        return WorkoutCoveragePolicy.IsSelectable(exercise, group) ||
            (state.PendingRestGroupId == group.Id &&
             IsAssignedToGroup(exercise, group));
    }

    private static bool IsAssignedToGroup(Exercise exercise, WorkoutGroup group)
    {
        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Any(group.CanonicalGroups.Contains);
    }

    private void MigrateLegacyLineups(WorkoutState state)
    {
        foreach ((string legacyGroup, string exerciseName) in
                 state.LegacySelectedExerciseNames)
        {
            bool pendingNoKeep = legacyGroup == state.LegacyPendingRestGroup &&
                !state.PendingRestKept;
            bool wasRejected = state.LegacyOutcomes.TryGetValue(
                    legacyGroup,
                    out ExerciseOutcome outcome) &&
                outcome == ExerciseOutcome.X;
            if (pendingNoKeep || wasRejected)
            {
                continue;
            }

            Exercise? exercise = _exercises.FirstOrDefault(candidate =>
                candidate.Name == exerciseName);
            if (exercise is null)
            {
                continue;
            }

            foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
            {
                WorkoutGroup group = MassGroupingTaxonomy.GetGroup(
                    minutes,
                    exercise.PrimaryCanonicalGroup);
                state.SelectedExerciseIds.TryAdd(group.Id, exercise.Id);
            }
        }

        state.Outcomes.Clear();
    }

    private Exercise? ResolveLegacyPendingRest(WorkoutState state)
    {
        if (state.LegacyPendingRestGroup is null || state.PendingRestKept)
        {
            return null;
        }

        if (!state.LegacySelectedExerciseNames.TryGetValue(
                state.LegacyPendingRestGroup,
                out string? exerciseName))
        {
            return null;
        }

        Exercise? exercise = _exercises.FirstOrDefault(candidate =>
            candidate.Name == exerciseName);
        if (exercise is null)
        {
            return null;
        }

        exercise.Score--;
        return exercise;
    }

    private static void NormalizeCollections(WorkoutState state)
    {
        state.SelectedExerciseIds ??= [];
        state.Outcomes ??= [];
        state.LegacySelectedExerciseNames ??= [];
        state.LegacyOutcomes ??= [];
    }

    private void NormalizeSavedLineups(WorkoutState state)
    {
        foreach (string groupId in state.SelectedExerciseIds.Keys.ToArray())
        {
            if (!KnownWorkoutGroups.TryGetValue(groupId, out WorkoutGroup? group) ||
                !_exercisesById.TryGetValue(
                    state.SelectedExerciseIds[groupId],
                    out Exercise? exercise) ||
                !IsSavedSelectionValid(state, exercise, group))
            {
                state.SelectedExerciseIds.Remove(groupId);
            }
        }
    }

    private void NormalizeOutcomes(WorkoutState state)
    {
        HashSet<string> activeGroupIds = GetActiveGroups(state)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string groupId in state.Outcomes.Keys
                     .Where(groupId => !activeGroupIds.Contains(groupId))
                     .ToArray())
        {
            state.Outcomes.Remove(groupId);
        }

        foreach (string groupId in state.Outcomes
                     .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            state.Outcomes[groupId] = ExerciseOutcome.Tick;
        }
    }

    private void NormalizePendingRest(WorkoutState state)
    {
        if (state.PendingRestGroupId is null)
        {
            ClearPendingRest(state);
            return;
        }

        if (state.WorkoutCompleted ||
            GetNextGroup(state)?.Id != state.PendingRestGroupId ||
            state.PendingRestEndsAtUnixMilliseconds <= 0)
        {
            ClearPendingRest(state);
        }
    }

    private void NormalizeCompletionState(WorkoutState state)
    {
        WorkoutGroup[] activeGroups = GetActiveGroups(state).ToArray();
        state.WorkoutCompleted = activeGroups.Length > 0 &&
            activeGroups.All(group => state.Outcomes.ContainsKey(group.Id));

        if (!state.WorkoutCompleted)
        {
            state.CompletionAcknowledged = false;
        }
    }

    private static void ClearLegacyMigrationState(WorkoutState state)
    {
        state.LegacySelectedExerciseNames.Clear();
        state.LegacyOutcomes.Clear();
        state.LegacyPendingRestGroup = null;
    }

    private static void ResetToDurationSelection(WorkoutState state)
    {
        state.ActiveWorkoutMinutes = 0;
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
    }

    public static int NormalizeLastWorkoutMinutes(int minutes)
    {
        return MassGroupingTaxonomy.NormalizeMinutes(minutes);
    }

    public static bool IsValidWorkoutMinutes(int minutes)
    {
        return MassGroupingTaxonomy.SupportedMinutes.Contains(minutes);
    }
}
