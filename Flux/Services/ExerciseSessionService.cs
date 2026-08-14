using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 90;
    public const int DefaultWorkoutMinutes = 10;

    private const int CurrentStateVersion = 7;

    private static readonly IReadOnlyList<int> WorkoutMinutes =
        Array.AsReadOnly([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);

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
        WorkoutMinutes;

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        NormalizeCollections(state);
        CatalogMigrationRules.ReconcileWorkoutState(state);
        bool migratedLegacyState = state.Version < CurrentStateVersion ||
            state.LegacySelectedExerciseNames.Count > 0;
        if (migratedLegacyState)
        {
            MigrateLegacyLineups(state);
        }

        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);
        NormalizeSavedLineups(state);
        NormalizeKeptExerciseIds(state);

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

        NormalizeActiveLongWorkoutAllocation(state);
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
                "Workout duration must be one of 3, 5, 7, 10, 15, 20, 30, 45, 60, or 90 minutes.");
        }

        if (state.ActiveWorkoutMinutes != 0)
        {
            throw new InvalidOperationException("A workout is already active.");
        }

        NormalizeCollections(state);
        state.Version = CurrentStateVersion;
        int previousWorkoutMinutes = NormalizeLastWorkoutMinutes(
            state.LastWorkoutMinutes);
        state.LastWorkoutMinutes = minutes;
        state.ActiveWorkoutMinutes = minutes;
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingRest(state);
        ClearLegacyMigrationState(state);
        CarryKeptExercisesForward(state, previousWorkoutMinutes);
        RepairActiveLineup(state);
        SetActiveLongWorkoutAllocation(state);
    }

    public IReadOnlyList<WorkoutGroup> GetActiveGroups(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? CreateWorkoutSchedule(
                state.ActiveWorkoutMinutes,
                GetEffectiveFullSideSelectionGroups(state),
                GetEffectiveExtraSetSelectionGroups(state))
            : [];
    }

    public Exercise GetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (!state.SelectedExerciseIds.TryGetValue(
                group.SelectionKey,
                out int exerciseId) ||
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

    public bool IsFinalPendingGroup(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        WorkoutGroup? nextGroup = activeGroups
            .FirstOrDefault(activeGroup => !state.Outcomes.ContainsKey(activeGroup.Id));
        return nextGroup?.Id == group.Id &&
            state.Outcomes.Count == activeGroups.Count - 1;
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

        return ApplyOutcome(state, group, keep);
    }

    private Exercise ApplyOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep)
    {
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
        PrepareNextSession(state);
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
        if (state.PendingRestGroupId is not null)
        {
            WorkoutGroup? pendingGroup = GetValidPendingRestGroup(state);
            if (pendingGroup is not null)
            {
                bool keep = state.PendingRestKept;
                Exercise exercise = ApplyOutcome(state, pendingGroup, keep);
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
        WorkoutGroup[] activeRounds = GetActiveGroups(state).ToArray();
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        HashSet<string> rejectedSelectionKeys = activeRounds
            .Where(round => state.Outcomes.TryGetValue(
                round.Id,
                out ExerciseOutcome outcome) && outcome == ExerciseOutcome.X)
            .Select(round => round.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> newlyKeptExerciseIds = activeRounds
            .GroupBy(round => round.SelectionKey, StringComparer.Ordinal)
            .Where(rounds =>
                rounds.Any(round => state.Outcomes.TryGetValue(
                    round.Id,
                    out ExerciseOutcome outcome) && outcome == ExerciseOutcome.Tick) &&
                rounds.All(round => !state.Outcomes.TryGetValue(
                    round.Id,
                    out ExerciseOutcome outcome) || outcome != ExerciseOutcome.X))
            .Select(rounds => state.SelectedExerciseIds.GetValueOrDefault(rounds.Key))
            .Where(exerciseId => exerciseId != 0)
            .ToHashSet();
        HashSet<int> rejectedExerciseIds = rejectedSelectionKeys
            .Select(selectionKey =>
                state.SelectedExerciseIds.GetValueOrDefault(selectionKey))
            .Where(exerciseId => exerciseId != 0)
            .ToHashSet();
        state.LastKeptExerciseIds.ExceptWith(rejectedExerciseIds);
        state.LastKeptExerciseIds.UnionWith(newlyKeptExerciseIds);
        var usedExerciseIds = selectionGroups
            .Where(group => !rejectedSelectionKeys.Contains(group.Id))
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(group.Id))
            .Where(exerciseId => exerciseId != 0)
            .ToHashSet();

        foreach (WorkoutGroup group in selectionGroups.Where(group =>
                     rejectedSelectionKeys.Contains(group.Id)))
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
        IReadOnlyList<WorkoutGroup> activeRounds = GetActiveGroups(state);

        foreach (WorkoutGroup group in GetSelectionGroups(state))
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
                foreach (WorkoutGroup round in activeRounds.Where(round =>
                             round.SelectionKey == group.Id))
                {
                    state.Outcomes.Remove(round.Id);
                }
                if (PendingRestMatchesSelectionGroup(state, group.Id))
                {
                    ClearPendingRest(state);
                }
                selectedExerciseId = replacement.Id;
            }

            usedExerciseIds.Add(selectedExerciseId);
        }
    }

    private void CarryKeptExercisesForward(
        WorkoutState state,
        int previousWorkoutMinutes)
    {
        if (state.LastKeptExerciseIds.Count == 0)
        {
            return;
        }

        IReadOnlyList<WorkoutGroup> targetGroups = GetSelectionGroups(state);
        var assignedTargetGroupIds = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<int> orderedKeptExerciseIds = GetBaseResolution(
                previousWorkoutMinutes)
            .Groups
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(group.Id))
            .Concat(state.LastKeptExerciseIds.Order())
            .Where(state.LastKeptExerciseIds.Contains)
            .Distinct();

        foreach (int exerciseId in orderedKeptExerciseIds)
        {
            if (!_exercisesById.TryGetValue(exerciseId, out Exercise? exercise))
            {
                continue;
            }

            WorkoutGroup? targetGroup = targetGroups.SingleOrDefault(group =>
                WorkoutCoveragePolicy.IsSelectable(exercise, group));
            if (targetGroup is null ||
                !assignedTargetGroupIds.Add(targetGroup.Id))
            {
                continue;
            }

            state.SelectedExerciseIds[targetGroup.Id] = exerciseId;
        }
    }

    private bool IsSavedSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group)
    {
        return WorkoutCoveragePolicy.IsSelectable(exercise, group) ||
            (PendingRestMatchesSelectionGroup(state, group.SelectionKey) &&
             IsAssignedToGroup(exercise, group));
    }

    private bool PendingRestMatchesSelectionGroup(
        WorkoutState state,
        string selectionGroupId)
    {
        return state.PendingRestGroupId is string pendingRoundId &&
            GetActiveGroups(state).Any(round =>
                round.Id == pendingRoundId &&
                round.SelectionKey == selectionGroupId);
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
        state.LastKeptExerciseIds ??= [];
        state.ActiveExtraSetSelectionGroupIds ??= [];
        state.ActiveFullSideSelectionGroupIds ??= [];
        state.LegacySelectedExerciseNames ??= [];
        state.LegacyOutcomes ??= [];
    }

    private void NormalizeActiveLongWorkoutAllocation(WorkoutState state)
    {
        if (!IsLongWorkoutAllocationValid(state))
        {
            SetActiveLongWorkoutAllocation(state);
        }
    }

    private bool IsLongWorkoutAllocationValid(WorkoutState state)
    {
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        HashSet<string> validSelectionGroupIds = selectionGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> sidedSelectionGroupIds = selectionGroups
            .Where(group =>
                state.SelectedExerciseIds.TryGetValue(group.Id, out int exerciseId) &&
                _exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.SideSequence != ExerciseSideSequence.Continuous)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        int extraMinutes = GetExtraMinuteCount(state.ActiveWorkoutMinutes);
        int expectedFullSides = Math.Min(extraMinutes, sidedSelectionGroupIds.Count);
        int repeatedMinutes = extraMinutes - expectedFullSides;
        int expectedPartialExtraSets = selectionGroups.Length == 0
            ? 0
            : repeatedMinutes % selectionGroups.Length;
        return state.ActiveFullSideSelectionGroupIds.Count == expectedFullSides &&
            state.ActiveFullSideSelectionGroupIds.All(sidedSelectionGroupIds.Contains) &&
            state.ActiveExtraSetSelectionGroupIds.Count == expectedPartialExtraSets &&
            state.ActiveExtraSetSelectionGroupIds.All(validSelectionGroupIds.Contains);
    }

    private void NormalizeKeptExerciseIds(WorkoutState state)
    {
        state.LastKeptExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
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
        if (GetValidPendingRestGroup(state) is null)
        {
            ClearPendingRest(state);
        }
    }

    private WorkoutGroup? GetValidPendingRestGroup(WorkoutState state)
    {
        if (state.PendingRestGroupId is not string pendingGroupId ||
            state.PendingRestEndsAtUnixMilliseconds <= 0 ||
            state.Outcomes.ContainsKey(pendingGroupId))
        {
            return null;
        }

        WorkoutGroup? pendingGroup = GetActiveGroups(state)
            .SingleOrDefault(group => group.Id == pendingGroupId);
        if (pendingGroup is null ||
            !state.SelectedExerciseIds.TryGetValue(
                pendingGroup.SelectionKey,
                out int exerciseId) ||
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            !IsAssignedToGroup(exercise, pendingGroup))
        {
            return null;
        }

        return pendingGroup;
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
        state.ActiveExtraSetSelectionGroupIds.Clear();
        state.ActiveFullSideSelectionGroupIds.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
    }

    public static int NormalizeLastWorkoutMinutes(int minutes)
    {
        return WorkoutMinutes
            .OrderBy(candidate => Math.Abs(candidate - minutes))
            .ThenByDescending(candidate => candidate)
            .First();
    }

    public static bool IsValidWorkoutMinutes(int minutes)
    {
        return WorkoutMinutes.Contains(minutes);
    }

    private static IReadOnlyList<WorkoutGroup> GetSelectionGroups(
        WorkoutState state)
    {
        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? GetBaseResolution(state.ActiveWorkoutMinutes).Groups
            : [];
    }

    private static WorkoutResolution GetBaseResolution(int workoutMinutes)
    {
        int resolutionMinutes = workoutMinutes > 30 ? 30 : workoutMinutes;
        return MassGroupingTaxonomy.GetResolution(resolutionMinutes);
    }

    private static int GetExtraMinuteCount(int workoutMinutes)
    {
        if (workoutMinutes <= 30)
        {
            return 0;
        }

        return workoutMinutes - GetBaseResolution(workoutMinutes).Groups.Count;
    }

    private LongWorkoutAllocation ChooseLongWorkoutAllocation(WorkoutState state)
    {
        int extraMinutes = GetExtraMinuteCount(state.ActiveWorkoutMinutes);
        if (extraMinutes == 0)
        {
            return new LongWorkoutAllocation([], []);
        }

        WorkoutGroup[] rankedGroups = GetSelectionGroups(state)
            .OrderByDescending(group =>
                state.SelectedExerciseIds.TryGetValue(group.Id, out int exerciseId) &&
                state.LastKeptExerciseIds.Contains(exerciseId))
            .ThenByDescending(group => group.Order)
            .ToArray();
        HashSet<string> fullSideGroups = rankedGroups
            .Where(group =>
                state.SelectedExerciseIds.TryGetValue(group.Id, out int exerciseId) &&
                _exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.SideSequence != ExerciseSideSequence.Continuous)
            .Take(extraMinutes)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        int repeatMinutes = extraMinutes - fullSideGroups.Count;
        int partialExtraSets = repeatMinutes % rankedGroups.Length;
        HashSet<string> extraSetGroups = rankedGroups
            .Take(partialExtraSets)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        return new LongWorkoutAllocation(fullSideGroups, extraSetGroups);
    }

    private void SetActiveLongWorkoutAllocation(WorkoutState state) =>
        ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state));

    private static void ApplyLongWorkoutAllocation(
        WorkoutState state,
        LongWorkoutAllocation allocation)
    {
        state.ActiveFullSideSelectionGroupIds =
            new HashSet<string>(allocation.FullSideSelectionGroupIds, StringComparer.Ordinal);
        state.ActiveExtraSetSelectionGroupIds =
            new HashSet<string>(allocation.ExtraSetSelectionGroupIds, StringComparer.Ordinal);
    }

    private IReadOnlySet<string> GetEffectiveExtraSetSelectionGroups(WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveExtraSetSelectionGroupIds
            : ChooseLongWorkoutAllocation(state).ExtraSetSelectionGroupIds;
    }

    private IReadOnlySet<string> GetEffectiveFullSideSelectionGroups(WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveFullSideSelectionGroupIds
            : ChooseLongWorkoutAllocation(state).FullSideSelectionGroupIds;
    }

    private static IReadOnlyList<WorkoutGroup> CreateWorkoutSchedule(
        int workoutMinutes,
        IReadOnlySet<string> fullSideSelectionGroupIds,
        IReadOnlySet<string> extraSetSelectionGroupIds)
    {
        WorkoutResolution resolution = GetBaseResolution(workoutMinutes);
        if (workoutMinutes <= 30)
        {
            return resolution.Groups;
        }

        int extraMinutes = workoutMinutes - resolution.Groups.Count;
        int repeatedMinutes = extraMinutes - fullSideSelectionGroupIds.Count;
        int completeExtraSets = repeatedMinutes / resolution.Groups.Count;
        var rounds = new List<WorkoutGroup>(
            resolution.Groups.Count + repeatedMinutes);

        for (int groupIndex = 0;
             groupIndex < resolution.Groups.Count;
             groupIndex++)
        {
            WorkoutGroup selectionGroup = resolution.Groups[groupIndex];
            int setCount = 1 + completeExtraSets +
                (extraSetSelectionGroupIds.Contains(selectionGroup.Id) ? 1 : 0);
            for (int setNumber = 1; setNumber <= setCount; setNumber++)
            {
                rounds.Add(selectionGroup with
                {
                    Id = $"{selectionGroup.Id}.set{setNumber}",
                    Order = rounds.Count + 1,
                    SelectionGroupId = selectionGroup.Id,
                    UsesFullSideTiming = setNumber == 1 &&
                        fullSideSelectionGroupIds.Contains(selectionGroup.Id),
                });
            }
        }

        return rounds.AsReadOnly();
    }

    private sealed record LongWorkoutAllocation(
        HashSet<string> FullSideSelectionGroupIds,
        HashSet<string> ExtraSetSelectionGroupIds);
}
