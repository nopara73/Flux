using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 90;
    public const int DefaultWorkoutMinutes = 10;
    public const WorkoutModifiers DefaultWorkoutModifiers =
        WorkoutModifiers.Silence;

    private const int CurrentStateVersion = 11;
    private const int ImplicitSilenceStateVersion = 8;
    private const int LegacyLineupStateVersion = 7;
    private const string SelectionProfilePrefix = "p";
    private const char SelectionProfileSeparator = '|';
    private static readonly IReadOnlyList<int> WorkoutMinutes =
        Array.AsReadOnly([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);

    private static readonly IReadOnlyDictionary<string, WorkoutGroup> KnownWorkoutGroups =
        MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly IReadOnlyDictionary<int, Exercise> _exercisesById;
    private readonly Random _random;
    private readonly Func<DateOnly> _localDateProvider;

    public ExerciseSessionService(
        IReadOnlyList<Exercise> exercises,
        Random? random = null,
        Func<DateOnly>? localDateProvider = null)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        _exercises = exercises;
        _exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        _random = random ?? Random.Shared;
        _localDateProvider = localDateProvider ??
            (() => DateOnly.FromDateTime(DateTime.Now));
    }

    public static IReadOnlyList<int> SupportedWorkoutMinutes =>
        WorkoutMinutes;

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        NormalizeCollections(state);
        CatalogMigrationRules.ReconcileWorkoutState(state);
        bool migratedLegacyState = state.Version < LegacyLineupStateVersion ||
            state.LegacySelectedExerciseNames.Count > 0;
        if (migratedLegacyState)
        {
            MigrateLegacyLineups(state);
        }

        if (state.Version < ImplicitSilenceStateVersion)
        {
            MigrateImplicitSilenceModifier(state);
        }

        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);
        state.LastWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers);
        state.ActiveWorkoutModifiers = NormalizeWorkoutModifiers(
            state.ActiveWorkoutModifiers);
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

    public void StartWorkout(
        WorkoutState state,
        int minutes,
        WorkoutModifiers modifiers = DefaultWorkoutModifiers)
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
        NormalizeKeptExerciseIds(state);
        state.Version = CurrentStateVersion;
        modifiers = NormalizeWorkoutModifiers(modifiers);
        int previousWorkoutMinutes = NormalizeLastWorkoutMinutes(
            state.LastWorkoutMinutes);
        WorkoutModifiers previousWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers);
        state.LastWorkoutMinutes = minutes;
        state.LastWorkoutModifiers = modifiers;
        state.ActiveWorkoutMinutes = minutes;
        state.ActiveWorkoutModifiers = modifiers;
        state.ActiveRecoveryExcludedExerciseIds =
            WorkoutRecoveryPolicy.GetPreviousDayHardKeptExerciseIds(
                state.LastKeptExerciseIds,
                state.LastKeptLocalDateByExerciseId,
                _exercisesById,
                _localDateProvider());
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingRest(state);
        ClearLegacyMigrationState(state);
        CarryKeptExercisesForward(
            state,
            previousWorkoutMinutes,
            previousWorkoutModifiers);
        RepairActiveLineup(state);
        RebalanceNewExercisesByMuscleBudget(state);
        SetActiveLongWorkoutAllocation(state);
        state.NextWorkoutExcludedExerciseIds.Clear();
    }

    public IReadOnlyList<WorkoutGroup> GetActiveGroups(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? CreateWorkoutSchedule(
                state.ActiveWorkoutMinutes,
                GetEffectiveDirectionPartnerExercises(state),
                GetEffectiveFullSideRounds(state),
                GetEffectiveExtraSetSelectionGroups(state))
            : [];
    }

    public Exercise GetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (group.ExerciseOverrideId > 0)
        {
            if (!_exercisesById.TryGetValue(
                    group.ExerciseOverrideId,
                    out Exercise? overrideExercise) ||
                !IsSavedSelectionValid(
                    state,
                    overrideExercise,
                    group,
                    state.ActiveWorkoutModifiers))
            {
                throw new InvalidOperationException(
                    $"The linked direction exercise for {group.DisplayName} is unavailable.");
            }

            return overrideExercise;
        }

        string selectionStorageKey = GetSelectionStorageKey(
            group.SelectionKey,
            state.ActiveWorkoutModifiers);
        if (!state.SelectedExerciseIds.TryGetValue(
                selectionStorageKey,
                out int exerciseId) ||
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            !IsSavedSelectionValid(
                state,
                exercise,
                group,
                state.ActiveWorkoutModifiers))
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
        var resolvedRounds = activeRounds
            .Select(round => new
            {
                Round = round,
                Exercise = TryGetSelectedExercise(state, round),
            })
            .Where(entry => entry.Exercise is not null)
            .ToArray();
        HashSet<string> rejectedSelectionKeys = activeRounds
            .Where(round => state.Outcomes.TryGetValue(
                round.Id,
                out ExerciseOutcome outcome) && outcome == ExerciseOutcome.X &&
                round.ExerciseOverrideId == 0)
            .Select(round => round.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> newlyKeptExerciseIds = resolvedRounds
            .GroupBy(entry => entry.Exercise!.Id)
            .Where(rounds =>
                rounds.Any(entry => state.Outcomes.TryGetValue(
                    entry.Round.Id,
                    out ExerciseOutcome outcome) && outcome == ExerciseOutcome.Tick) &&
                rounds.All(entry => !state.Outcomes.TryGetValue(
                    entry.Round.Id,
                    out ExerciseOutcome outcome) || outcome != ExerciseOutcome.X))
            .Select(rounds => rounds.Key)
            .ToHashSet();
        HashSet<int> rejectedExerciseIds = resolvedRounds
            .Where(entry => state.Outcomes.TryGetValue(
                entry.Round.Id,
                out ExerciseOutcome outcome) && outcome == ExerciseOutcome.X)
            .Select(entry => entry.Exercise!.Id)
            .ToHashSet();
        state.NextWorkoutExcludedExerciseIds = rejectedExerciseIds;
        state.LastKeptExerciseIds.ExceptWith(rejectedExerciseIds);
        state.LastKeptExerciseIds.UnionWith(newlyKeptExerciseIds);
        foreach (int exerciseId in rejectedExerciseIds)
        {
            state.LastKeptLocalDateByExerciseId.Remove(exerciseId);
        }
        string currentLocalDateKey = WorkoutRecoveryPolicy.ToLocalDateKey(
            _localDateProvider());
        foreach (int exerciseId in newlyKeptExerciseIds)
        {
            state.LastKeptLocalDateByExerciseId[exerciseId] = currentLocalDateKey;
        }
        var currentExerciseIds = selectionGroups
            .Where(group => !rejectedSelectionKeys.Contains(group.Id))
            .Select(group => new
            {
                group.Id,
                ExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers)),
            })
            .Where(entry => entry.ExerciseId != 0)
            .ToDictionary(
                entry => entry.Id,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        var excludedExerciseIdsByGroup = new Dictionary<string, IReadOnlySet<int>>(
            StringComparer.Ordinal);
        foreach (WorkoutGroup group in selectionGroups.Where(group =>
                     rejectedSelectionKeys.Contains(group.Id)))
        {
            string selectionStorageKey = GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers);
            int currentExerciseId = state.SelectedExerciseIds[selectionStorageKey];
            excludedExerciseIdsByGroup[group.Id] = new HashSet<int>
            {
                currentExerciseId,
            };
            foreach (string savedGroupId in state.SelectedExerciseIds
                         .Where(entry =>
                             entry.Key != selectionStorageKey &&
                             entry.Value == currentExerciseId)
                         .Select(entry => entry.Key)
                         .ToArray())
            {
                state.SelectedExerciseIds.Remove(savedGroupId);
            }
        }

        IReadOnlyDictionary<string, int> nextLineup = ChooseBestDistinctLineup(
            state,
            selectionGroups,
            state.ActiveWorkoutModifiers,
            state.LastKeptExerciseIds,
            currentExerciseIds,
            excludedExerciseIdsByGroup);
        ApplyDistinctLineup(
            state,
            selectionGroups,
            nextLineup,
            clearChangedProgress: false);

        ResetToDurationSelection(state);
    }

    private Exercise? TryGetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        try
        {
            return GetSelectedExercise(state, group);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private IReadOnlyDictionary<string, int> ChooseBestDistinctLineup(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers modifiers,
        IReadOnlySet<int>? preferredExerciseIds = null,
        IReadOnlyDictionary<string, int>? currentExerciseIds = null,
        IReadOnlyDictionary<string, IReadOnlySet<int>>? excludedExerciseIdsByGroup = null,
        IReadOnlyList<int>? preferredTieOrder = null,
        bool allowSavedSelectionException = false)
    {
        if (groups.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        preferredExerciseIds ??= new HashSet<int>();
        currentExerciseIds ??= new Dictionary<string, int>(StringComparer.Ordinal);
        excludedExerciseIdsByGroup ??=
            new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal);

        bool IsAllowed(Exercise exercise, WorkoutGroup group)
        {
            if (state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id))
            {
                return false;
            }

            if (excludedExerciseIdsByGroup.TryGetValue(
                    group.Id,
                    out IReadOnlySet<int>? excludedExerciseIds) &&
                excludedExerciseIds.Contains(exercise.Id))
            {
                return false;
            }

            if (IsSelectable(exercise, group, modifiers))
            {
                return true;
            }

            return allowSavedSelectionException &&
                currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id &&
                IsSavedSelectionValid(state, exercise, group, modifiers);
        }

        var candidates = _exercises
            .Where(exercise => groups.Any(group => IsAllowed(exercise, group)))
            .ToList();
        Shuffle(candidates);
        if (preferredTieOrder is not null)
        {
            Dictionary<int, int> tieOrder = preferredTieOrder
                .Select((exerciseId, index) => (exerciseId, index))
                .GroupBy(entry => entry.exerciseId)
                .ToDictionary(group => group.Key, group => group.First().index);
            candidates = candidates
                .OrderBy(exercise => tieOrder.GetValueOrDefault(
                    exercise.Id,
                    int.MaxValue))
                .ToList();
        }

        if (candidates.Count < groups.Count)
        {
            throw CreateDistinctLineupException(groups, candidates.Count);
        }

        int[] orderedScores = candidates
            .Select(exercise => exercise.Score)
            .Distinct()
            .Order()
            .ToArray();
        Dictionary<int, int> scoreRanks = orderedScores
            .Select((score, rank) => (score, rank))
            .ToDictionary(entry => entry.score, entry => entry.rank);
        int maximumCoverage = groups.Max(group => group.CanonicalGroups.Count);
        long totalCoverageRange = checked((long)groups.Count * maximumCoverage);
        long primaryWeight = checked(totalCoverageRange + 1L);
        long totalPrimaryAndCoverageRange = checked(
            groups.Count * (primaryWeight + maximumCoverage));
        long mirrorPreferenceWeight = checked(
            totalPrimaryAndCoverageRange + 1L);
        long totalMirrorPreferenceRange = checked(
            groups.Count * mirrorPreferenceWeight +
            totalPrimaryAndCoverageRange);
        long scoreWeight = checked(totalMirrorPreferenceRange + 1L);
        long totalScoreRange = checked(
            groups.Count *
            ((long)(orderedScores.Length - 1) * scoreWeight +
             primaryWeight +
             maximumCoverage));
        long currentSelectionWeight = checked(totalScoreRange + 1L);
        long totalCurrentSelectionRange = checked(
            groups.Count * currentSelectionWeight + totalScoreRange);
        long hardPreferredExerciseWeight = checked(
            totalCurrentSelectionRange + 1L);
        long totalHardPreferredRange = checked(
            groups.Count * hardPreferredExerciseWeight +
            totalCurrentSelectionRange);
        long preferredExerciseWeight = checked(totalHardPreferredRange + 1L);

        var allowed = new bool[groups.Count, candidates.Count];
        var utilities = new long[groups.Count, candidates.Count];
        long maximumUtility = 0;
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            WorkoutGroup group = groups[groupIndex];
            for (int exerciseIndex = 0;
                 exerciseIndex < candidates.Count;
                 exerciseIndex++)
            {
                Exercise exercise = candidates[exerciseIndex];
                if (!IsAllowed(exercise, group))
                {
                    continue;
                }

                allowed[groupIndex, exerciseIndex] = true;
                long utility = checked(
                    (preferredExerciseIds.Contains(exercise.Id)
                        ? preferredExerciseWeight
                        : 0L) +
                    (preferredExerciseIds.Contains(exercise.Id) &&
                     exercise.MuscularDemand ==
                         WorkoutRecoveryPolicy.HardMuscularDemand
                        ? hardPreferredExerciseWeight
                        : 0L) +
                    (currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id
                        ? currentSelectionWeight
                        : 0L) +
                    (long)scoreRanks[exercise.Score] * scoreWeight +
                    (WorkoutModifierPolicy.IsMirrorPreferred(exercise, modifiers)
                        ? mirrorPreferenceWeight
                        : 0L) +
                    (WorkoutCoveragePolicy.IsPrimaryForGroup(exercise, group)
                        ? primaryWeight
                        : 0L) +
                    WorkoutCoveragePolicy.GetCanonicalCoverage(exercise, group));
                utilities[groupIndex, exerciseIndex] = utility;
                maximumUtility = Math.Max(maximumUtility, utility);
            }
        }

        int[] assignedCandidateIndexes = SolveMaximumWeightAssignment(
            utilities,
            allowed,
            maximumUtility);
        var result = new Dictionary<string, int>(
            groups.Count,
            StringComparer.Ordinal);
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            int candidateIndex = assignedCandidateIndexes[groupIndex];
            if (candidateIndex < 0 ||
                !allowed[groupIndex, candidateIndex])
            {
                throw CreateDistinctLineupException(groups, candidates.Count);
            }

            result[groups[groupIndex].Id] = candidates[candidateIndex].Id;
        }

        return result;
    }

    private void RepairActiveLineup(WorkoutState state)
    {
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        IReadOnlyList<WorkoutGroup> activeRounds = GetActiveGroups(state);
        Dictionary<string, int> currentExerciseIds = selectionGroups
            .Select(group => new
            {
                group.Id,
                ExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers)),
            })
            .Where(entry => entry.ExerciseId != 0)
            .ToDictionary(
                entry => entry.Id,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> repairedLineup = ChooseBestDistinctLineup(
            state,
            selectionGroups,
            state.ActiveWorkoutModifiers,
            currentExerciseIds: currentExerciseIds,
            allowSavedSelectionException: true);
        ApplyDistinctLineup(
            state,
            selectionGroups,
            repairedLineup,
            clearChangedProgress: true,
            activeRounds);
    }

    private void CarryKeptExercisesForward(
        WorkoutState state,
        int previousWorkoutMinutes,
        WorkoutModifiers previousWorkoutModifiers)
    {
        if (state.LastKeptExerciseIds.Count == 0)
        {
            return;
        }

        WorkoutGroup[] targetGroups = GetSelectionGroups(state).ToArray();
        int[] orderedKeptExerciseIds = GetBaseResolution(
                previousWorkoutMinutes)
            .Groups
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    previousWorkoutModifiers)))
            .Concat(state.LastKeptExerciseIds.Order())
            .Where(state.LastKeptExerciseIds.Contains)
            .Distinct()
            .ToArray();
        Dictionary<string, int> currentExerciseIds = targetGroups
            .Select(group => new
            {
                group.Id,
                ExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers)),
            })
            .Where(entry => entry.ExerciseId != 0)
            .ToDictionary(
                entry => entry.Id,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> carriedLineup = ChooseBestDistinctLineup(
            state,
            targetGroups,
            state.ActiveWorkoutModifiers,
            state.LastKeptExerciseIds,
            currentExerciseIds,
            preferredTieOrder: orderedKeptExerciseIds);
        ApplyDistinctLineup(
            state,
            targetGroups,
            carriedLineup,
            clearChangedProgress: false);
    }

    private void ApplyDistinctLineup(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> groups,
        IReadOnlyDictionary<string, int> lineup,
        bool clearChangedProgress,
        IReadOnlyList<WorkoutGroup>? activeRounds = null)
    {
        activeRounds ??= GetActiveGroups(state);
        foreach (WorkoutGroup group in groups)
        {
            string selectionStorageKey = GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers);
            int previousExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                selectionStorageKey);
            int nextExerciseId = lineup[group.Id];
            state.SelectedExerciseIds[selectionStorageKey] = nextExerciseId;
            if (!clearChangedProgress || previousExerciseId == nextExerciseId)
            {
                continue;
            }

            foreach (WorkoutGroup round in activeRounds.Where(round =>
                         round.SelectionKey == group.Id))
            {
                state.Outcomes.Remove(round.Id);
            }

            if (PendingRestMatchesSelectionGroup(state, group.Id))
            {
                ClearPendingRest(state);
            }
        }
    }

    private InvalidOperationException CreateDistinctLineupException(
        IReadOnlyList<WorkoutGroup> groups,
        int candidateCount)
    {
        return new InvalidOperationException(
            $"No distinct exercise lineup exists for the active workout profile " +
            $"across {groups.Count} groups and {candidateCount} eligible exercises " +
            $"with at least {WorkoutCoveragePolicy.MinimumCoveragePercent}% coverage.");
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static int[] SolveMaximumWeightAssignment(
        long[,] utilities,
        bool[,] allowed,
        long maximumUtility)
    {
        int groupCount = utilities.GetLength(0);
        int candidateCount = utilities.GetLength(1);
        if (candidateCount < groupCount)
        {
            return Enumerable.Repeat(-1, groupCount).ToArray();
        }

        long invalidCost = checked(
            (maximumUtility + 1L) * (groupCount + 1L));
        var costs = new long[groupCount, candidateCount];
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            for (int candidateIndex = 0;
                 candidateIndex < candidateCount;
                 candidateIndex++)
            {
                costs[groupIndex, candidateIndex] =
                    allowed[groupIndex, candidateIndex]
                        ? maximumUtility - utilities[groupIndex, candidateIndex]
                        : invalidCost;
            }
        }

        var rowPotential = new long[groupCount + 1];
        var columnPotential = new long[candidateCount + 1];
        var matchedRowByColumn = new int[candidateCount + 1];
        var previousColumn = new int[candidateCount + 1];
        const long infinity = long.MaxValue / 4;

        for (int row = 1; row <= groupCount; row++)
        {
            matchedRowByColumn[0] = row;
            int column = 0;
            var minimumReducedCost = Enumerable
                .Repeat(infinity, candidateCount + 1)
                .ToArray();
            var visitedColumns = new bool[candidateCount + 1];
            do
            {
                visitedColumns[column] = true;
                int currentRow = matchedRowByColumn[column];
                long delta = infinity;
                int nextColumn = 0;
                for (int candidateColumn = 1;
                     candidateColumn <= candidateCount;
                     candidateColumn++)
                {
                    if (visitedColumns[candidateColumn])
                    {
                        continue;
                    }

                    long reducedCost = costs[currentRow - 1, candidateColumn - 1] -
                        rowPotential[currentRow] -
                        columnPotential[candidateColumn];
                    if (reducedCost < minimumReducedCost[candidateColumn])
                    {
                        minimumReducedCost[candidateColumn] = reducedCost;
                        previousColumn[candidateColumn] = column;
                    }

                    if (minimumReducedCost[candidateColumn] < delta)
                    {
                        delta = minimumReducedCost[candidateColumn];
                        nextColumn = candidateColumn;
                    }
                }

                if (delta == infinity)
                {
                    return Enumerable.Repeat(-1, groupCount).ToArray();
                }

                for (int candidateColumn = 0;
                     candidateColumn <= candidateCount;
                     candidateColumn++)
                {
                    if (visitedColumns[candidateColumn])
                    {
                        rowPotential[matchedRowByColumn[candidateColumn]] += delta;
                        columnPotential[candidateColumn] -= delta;
                    }
                    else
                    {
                        minimumReducedCost[candidateColumn] -= delta;
                    }
                }

                column = nextColumn;
            }
            while (matchedRowByColumn[column] != 0);

            do
            {
                int priorColumn = previousColumn[column];
                matchedRowByColumn[column] = matchedRowByColumn[priorColumn];
                column = priorColumn;
            }
            while (column != 0);
        }

        var assignment = Enumerable.Repeat(-1, groupCount).ToArray();
        for (int column = 1; column <= candidateCount; column++)
        {
            int row = matchedRowByColumn[column];
            if (row != 0)
            {
                assignment[row - 1] = column - 1;
            }
        }

        return assignment;
    }

    private bool IsSavedSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id))
        {
            return false;
        }

        return IsSelectable(exercise, group, modifiers) ||
            (PendingRestMatchesSelectionGroup(state, group.SelectionKey) &&
             IsCompatibleWithModifiers(exercise, modifiers) &&
             IsAssignedToGroup(exercise, group));
    }

    private bool IsSelectable(
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        return WorkoutModifierPolicy.IsSelectable(exercise, group, modifiers);
    }

    private bool IsCompatibleWithModifiers(
        Exercise exercise,
        WorkoutModifiers modifiers)
    {
        return WorkoutModifierPolicy.IsCompatible(exercise, modifiers);
    }

    private bool PendingRestMatchesSelectionGroup(
        WorkoutState state,
        string selectionGroupId)
    {
        if (state.PendingRestGroupId is not string pendingRoundId)
        {
            return false;
        }

        int suffixIndex = pendingRoundId.LastIndexOf('.');
        if (suffixIndex < 0)
        {
            return string.Equals(
                pendingRoundId,
                selectionGroupId,
                StringComparison.Ordinal);
        }

        string suffix = pendingRoundId[(suffixIndex + 1)..];
        bool isDirectionRound = string.Equals(
            suffix,
            "direction",
            StringComparison.Ordinal);
        bool isSetRound = suffix.StartsWith("set", StringComparison.Ordinal) &&
            int.TryParse(suffix.AsSpan(3), out int setNumber) &&
            setNumber > 0;
        string pendingSelectionGroupId = isDirectionRound || isSetRound
            ? pendingRoundId[..suffixIndex]
            : pendingRoundId;
        return string.Equals(
            pendingSelectionGroupId,
            selectionGroupId,
            StringComparison.Ordinal);
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
        state.LastKeptLocalDateByExerciseId ??= [];
        state.NextWorkoutExcludedExerciseIds ??= [];
        state.ActiveRecoveryExcludedExerciseIds ??= [];
        state.ActiveExtraSetSelectionGroupIds ??= [];
        state.ActiveDirectionPartnerExerciseIds ??= [];
        state.ActiveFullSideRoundIds ??= [];
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
        WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
        HashSet<string> validGroupIds = groups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> selectedExerciseIds = groups
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers)))
            .Where(exerciseId => exerciseId > 0)
            .ToHashSet();
        int extraMinutes = GetExtraMinuteCount(state.ActiveWorkoutMinutes);
        int eligiblePartnerCount = groups.Count(group =>
        {
            int exerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers));
            return _exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.DirectionPartnerExerciseId > 0 &&
                !selectedExerciseIds.Contains(exercise.DirectionPartnerExerciseId) &&
                _exercisesById.TryGetValue(
                    exercise.DirectionPartnerExerciseId,
                    out Exercise? partner) &&
                IsSavedSelectionValid(
                    state,
                    partner,
                    group,
                    state.ActiveWorkoutModifiers);
        });
        int expectedPartnerCount = Math.Min(extraMinutes, eligiblePartnerCount);
        if (state.ActiveDirectionPartnerExerciseIds.Count != expectedPartnerCount ||
            state.ActiveDirectionPartnerExerciseIds.Any(entry =>
                !validGroupIds.Contains(entry.Key) ||
                selectedExerciseIds.Contains(entry.Value) ||
                !_exercisesById.TryGetValue(entry.Value, out Exercise? partner) ||
                !state.SelectedExerciseIds.TryGetValue(
                    GetSelectionStorageKey(
                        entry.Key,
                        state.ActiveWorkoutModifiers),
                    out int selectedId) ||
                !_exercisesById.TryGetValue(selectedId, out Exercise? selected) ||
                selected.DirectionPartnerExerciseId != partner.Id ||
                !IsSavedSelectionValid(
                    state,
                    partner,
                    groups.Single(group => group.Id == entry.Key),
                    state.ActiveWorkoutModifiers)))
        {
            return false;
        }

        var eligibleFullSideRoundIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkoutGroup group in groups)
        {
            int selectedId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers));
            if (_exercisesById.TryGetValue(selectedId, out Exercise? selected) &&
                selected.SideSequence.UsesTimedSides())
            {
                eligibleFullSideRoundIds.Add($"{group.Id}.set1");
            }
            if (state.ActiveDirectionPartnerExerciseIds.TryGetValue(
                    group.Id,
                    out int partnerId) &&
                _exercisesById.TryGetValue(partnerId, out Exercise? partner) &&
                partner.SideSequence.UsesTimedSides())
            {
                eligibleFullSideRoundIds.Add($"{group.Id}.direction");
            }
        }
        int remainingAfterPartners = extraMinutes - expectedPartnerCount;
        int expectedFullSideCount = Math.Min(
            remainingAfterPartners,
            eligibleFullSideRoundIds.Count);
        int repeatedMinutes = remainingAfterPartners - expectedFullSideCount;
        int expectedPartialExtraSets = groups.Length == 0
            ? 0
            : repeatedMinutes % groups.Length;
        return state.ActiveFullSideRoundIds.Count == expectedFullSideCount &&
            state.ActiveFullSideRoundIds.All(eligibleFullSideRoundIds.Contains) &&
            state.ActiveExtraSetSelectionGroupIds.Count == expectedPartialExtraSets &&
            state.ActiveExtraSetSelectionGroupIds.All(validGroupIds.Contains);
    }

    private void NormalizeKeptExerciseIds(WorkoutState state)
    {
        state.LastKeptExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
        foreach (int exerciseId in state.LastKeptLocalDateByExerciseId.Keys
                     .Where(exerciseId =>
                         !state.LastKeptExerciseIds.Contains(exerciseId) ||
                         !_exercisesById.ContainsKey(exerciseId) ||
                         !WorkoutRecoveryPolicy.IsValidLocalDateKey(
                             state.LastKeptLocalDateByExerciseId[exerciseId]))
                     .ToArray())
        {
            state.LastKeptLocalDateByExerciseId.Remove(exerciseId);
        }
        state.NextWorkoutExcludedExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
        state.ActiveRecoveryExcludedExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            exercise.MuscularDemand != WorkoutRecoveryPolicy.HardMuscularDemand);
    }

    private void MigrateImplicitSilenceModifier(WorkoutState state)
    {
        foreach ((string selectionStorageKey, int exerciseId) in
                 state.SelectedExerciseIds.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string selectionGroupId,
                    out WorkoutModifiers modifiers))
            {
                continue;
            }

            WorkoutModifiers quietProfile = NormalizeWorkoutModifiers(
                modifiers | WorkoutModifiers.Silence);
            state.SelectedExerciseIds.TryAdd(
                GetSelectionStorageKey(selectionGroupId, quietProfile),
                exerciseId);
        }

        state.LastWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers | WorkoutModifiers.Silence);
        if (state.ActiveWorkoutMinutes > 0)
        {
            state.ActiveWorkoutModifiers = NormalizeWorkoutModifiers(
                state.ActiveWorkoutModifiers | WorkoutModifiers.Silence);
        }
    }

    private void NormalizeSavedLineups(WorkoutState state)
    {
        foreach (string selectionStorageKey in
                 state.SelectedExerciseIds.Keys.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string groupId,
                    out WorkoutModifiers modifiers) ||
                !KnownWorkoutGroups.TryGetValue(groupId, out WorkoutGroup? group) ||
                !_exercisesById.TryGetValue(
                    state.SelectedExerciseIds[selectionStorageKey],
                    out Exercise? exercise) ||
                !IsSavedSelectionValid(state, exercise, group, modifiers))
            {
                state.SelectedExerciseIds.Remove(selectionStorageKey);
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
        if (pendingGroup is null)
        {
            return null;
        }

        try
        {
            _ = GetSelectedExercise(state, pendingGroup);
        }
        catch (InvalidOperationException)
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
        state.ActiveWorkoutModifiers = WorkoutModifiers.None;
        state.Outcomes.Clear();
        state.ActiveExtraSetSelectionGroupIds.Clear();
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
        state.ActiveRecoveryExcludedExerciseIds.Clear();
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

    public static WorkoutModifiers NormalizeWorkoutModifiers(
        WorkoutModifiers modifiers)
    {
        return WorkoutModifierPolicy.Normalize(modifiers);
    }

    private string GetSelectionStorageKey(
        string selectionGroupId,
        WorkoutModifiers modifiers)
    {
        WorkoutModifiers normalized = NormalizeWorkoutModifiers(modifiers);
        return normalized == WorkoutModifiers.None
            ? selectionGroupId
            : $"{SelectionProfilePrefix}{(int)normalized}" +
                $"{SelectionProfileSeparator}{selectionGroupId}";
    }

    private static bool TryParseSelectionStorageKey(
        string selectionStorageKey,
        out string selectionGroupId,
        out WorkoutModifiers modifiers)
    {
        int separatorIndex = selectionStorageKey.IndexOf(
            SelectionProfileSeparator);
        if (selectionStorageKey.StartsWith(
                SelectionProfilePrefix,
                StringComparison.Ordinal) &&
            separatorIndex > SelectionProfilePrefix.Length &&
            int.TryParse(
                selectionStorageKey.AsSpan(
                    SelectionProfilePrefix.Length,
                    separatorIndex - SelectionProfilePrefix.Length),
                out int modifierValue))
        {
            modifiers = NormalizeWorkoutModifiers(
                (WorkoutModifiers)modifierValue);
            selectionGroupId = selectionStorageKey[(separatorIndex + 1)..];
            return modifierValue > 0 &&
                (int)modifiers == modifierValue &&
                selectionGroupId.Length > 0;
        }

        selectionGroupId = selectionStorageKey;
        modifiers = WorkoutModifiers.None;
        return selectionGroupId.Length > 0;
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
            return new LongWorkoutAllocation([], [], []);
        }

        WorkoutGroup[] rankedGroups = GetSelectionGroups(state)
            .OrderByDescending(group =>
                state.SelectedExerciseIds.TryGetValue(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers),
                    out int exerciseId) &&
                state.LastKeptExerciseIds.Contains(exerciseId))
            .ThenByDescending(group =>
                state.SelectedExerciseIds.TryGetValue(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers),
                    out int exerciseId) &&
                state.LastKeptExerciseIds.Contains(exerciseId) &&
                _exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.MuscularDemand ==
                    WorkoutRecoveryPolicy.HardMuscularDemand)
            .ThenByDescending(group => group.Order)
            .ToArray();
        HashSet<int> selectedExerciseIds = rankedGroups
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers)))
            .Where(exerciseId => exerciseId > 0)
            .ToHashSet();
        var directionPartners = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorkoutGroup group in rankedGroups)
        {
            if (directionPartners.Count >= extraMinutes ||
                !state.SelectedExerciseIds.TryGetValue(
                    GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers),
                    out int exerciseId) ||
                !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
                exercise.DirectionPartnerExerciseId <= 0 ||
                selectedExerciseIds.Contains(exercise.DirectionPartnerExerciseId) ||
                directionPartners.ContainsValue(exercise.DirectionPartnerExerciseId) ||
                !_exercisesById.TryGetValue(
                    exercise.DirectionPartnerExerciseId,
                    out Exercise? partner) ||
                !IsSavedSelectionValid(
                    state,
                    partner,
                    group,
                    state.ActiveWorkoutModifiers))
            {
                continue;
            }

            directionPartners.Add(group.Id, partner.Id);
        }

        int remainingExtraMinutes = extraMinutes - directionPartners.Count;
        var sidedRoundIds = new List<string>();
        foreach (WorkoutGroup group in rankedGroups)
        {
            int exerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers));
            if (_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.SideSequence.UsesTimedSides())
            {
                sidedRoundIds.Add($"{group.Id}.set1");
            }
            if (directionPartners.TryGetValue(group.Id, out int partnerId) &&
                _exercisesById.TryGetValue(partnerId, out Exercise? partner) &&
                partner.SideSequence.UsesTimedSides())
            {
                sidedRoundIds.Add($"{group.Id}.direction");
            }
        }

        HashSet<string> fullSideRounds = sidedRoundIds
            .Take(remainingExtraMinutes)
            .ToHashSet(StringComparer.Ordinal);
        int repeatMinutes = remainingExtraMinutes - fullSideRounds.Count;
        int partialExtraSets = repeatMinutes % rankedGroups.Length;
        HashSet<string> extraSetGroups = rankedGroups
            .Take(partialExtraSets)
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        return new LongWorkoutAllocation(
            directionPartners,
            fullSideRounds,
            extraSetGroups);
    }

    private void RebalanceNewExercisesByMuscleBudget(WorkoutState state)
    {
        WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
        if (groups.Length == 0)
        {
            return;
        }

        var seenLineups = new HashSet<string>(StringComparer.Ordinal);
        for (int pass = 0;
             pass < WorkoutMuscleBudgetPolicy.MaximumRebalancePasses;
             pass++)
        {
            string signature = string.Join(
                ',',
                groups.Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers))));
            if (!seenLineups.Add(signature))
            {
                break;
            }

            bool changed = false;
            foreach (WorkoutGroup group in groups)
            {
                string selectionStorageKey = GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers);
                int currentExerciseId =
                    state.SelectedExerciseIds.GetValueOrDefault(selectionStorageKey);
                if (!_exercisesById.TryGetValue(
                        currentExerciseId,
                        out Exercise? currentExercise) ||
                    state.LastKeptExerciseIds.Contains(currentExerciseId))
                {
                    continue;
                }

                HashSet<int> unavailableExerciseIds = groups
                    .Where(candidateGroup => candidateGroup.Id != group.Id)
                    .Select(candidateGroup => state.SelectedExerciseIds.GetValueOrDefault(
                        GetSelectionStorageKey(
                            candidateGroup.Id,
                            state.ActiveWorkoutModifiers)))
                    .Where(exerciseId => exerciseId > 0)
                    .Concat(state.LastKeptExerciseIds)
                    .ToHashSet();
                IReadOnlyDictionary<CanonicalMuscleGroup, int>? loadWithoutCurrent =
                    state.ActiveWorkoutMinutes <= 30
                        ? WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits(
                            groups
                                .Where(candidateGroup => candidateGroup.Id != group.Id)
                                .Select(candidateGroup => GetSelectedExercise(
                                    state,
                                    candidateGroup)))
                        : null;
                MuscleBudgetCandidate current = loadWithoutCurrent is null
                    ? EvaluateMuscleBudgetCandidate(state, group, currentExercise)
                    : EvaluateSingleRoundMuscleBudgetCandidate(
                        group,
                        currentExercise,
                        loadWithoutCurrent,
                        state.ActiveWorkoutModifiers);
                MuscleBudgetCandidate? bestAlternative = _exercises
                    .Where(exercise =>
                        exercise.Id != currentExerciseId &&
                        WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                            exercise.Score,
                            temporaryDownvoteHalfUnits: 0) >
                            current.AdjustedScoreHalfUnits &&
                        !unavailableExerciseIds.Contains(exercise.Id) &&
                        !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                        !state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id) &&
                        IsSelectable(
                            exercise,
                            group,
                            state.ActiveWorkoutModifiers))
                    .Select(exercise => loadWithoutCurrent is null
                        ? EvaluateMuscleBudgetCandidate(state, group, exercise)
                        : EvaluateSingleRoundMuscleBudgetCandidate(
                            group,
                            exercise,
                            loadWithoutCurrent,
                            state.ActiveWorkoutModifiers))
                    .OrderByDescending(candidate => candidate.AdjustedScoreHalfUnits)
                    .ThenByDescending(candidate => candidate.RealScore)
                    .ThenByDescending(candidate => candidate.IsMirrorPreferred)
                    .ThenByDescending(candidate => candidate.IsPrimary)
                    .ThenByDescending(candidate => candidate.CanonicalCoverage)
                    .ThenBy(candidate => candidate.ExerciseId)
                    .FirstOrDefault();
                if (bestAlternative is null ||
                    bestAlternative.AdjustedScoreHalfUnits <=
                        current.AdjustedScoreHalfUnits)
                {
                    continue;
                }

                state.SelectedExerciseIds[selectionStorageKey] =
                    bestAlternative.ExerciseId;
                changed = true;
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private MuscleBudgetCandidate EvaluateMuscleBudgetCandidate(
        WorkoutState state,
        WorkoutGroup group,
        Exercise candidate)
    {
        string selectionStorageKey = GetSelectionStorageKey(
            group.Id,
            state.ActiveWorkoutModifiers);
        int previousExerciseId =
            state.SelectedExerciseIds.GetValueOrDefault(selectionStorageKey);
        state.SelectedExerciseIds[selectionStorageKey] = candidate.Id;
        try
        {
            LongWorkoutAllocation allocation = ChooseLongWorkoutAllocation(state);
            IReadOnlyList<WorkoutGroup> rounds = CreateWorkoutSchedule(
                state.ActiveWorkoutMinutes,
                allocation.DirectionPartnerExerciseIds,
                allocation.FullSideRoundIds,
                allocation.ExtraSetSelectionGroupIds);
            var scheduledExercises = rounds
                .Select(round => GetSelectedExercise(state, round))
                .ToArray();
            IReadOnlyDictionary<CanonicalMuscleGroup, int> loadHalfUnits =
                WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits(scheduledExercises);
            CanonicalMuscleGroup[] candidateMuscleGroups = rounds
                .Where(round => round.SelectionKey == group.Id)
                .Select(round => GetSelectedExercise(state, round))
                .SelectMany(exercise =>
                    exercise.SecondaryCanonicalGroups
                        .Append(exercise.PrimaryCanonicalGroup))
                .Distinct()
                .ToArray();
            int temporaryDownvoteHalfUnits =
                WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnits(
                    loadHalfUnits,
                    candidateMuscleGroups);
            return new MuscleBudgetCandidate(
                candidate.Id,
                candidate.Score,
                WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                    candidate.Score,
                    temporaryDownvoteHalfUnits),
                WorkoutModifierPolicy.IsMirrorPreferred(
                    candidate,
                    state.ActiveWorkoutModifiers),
                WorkoutCoveragePolicy.IsPrimaryForGroup(candidate, group),
                WorkoutCoveragePolicy.GetCanonicalCoverage(candidate, group));
        }
        finally
        {
            state.SelectedExerciseIds[selectionStorageKey] = previousExerciseId;
        }
    }

    private static MuscleBudgetCandidate EvaluateSingleRoundMuscleBudgetCandidate(
        WorkoutGroup group,
        Exercise candidate,
        IReadOnlyDictionary<CanonicalMuscleGroup, int> loadWithoutCandidate,
        WorkoutModifiers modifiers)
    {
        int temporaryDownvoteHalfUnits =
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
                loadWithoutCandidate,
                candidate);
        return new MuscleBudgetCandidate(
            candidate.Id,
            candidate.Score,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                candidate.Score,
                temporaryDownvoteHalfUnits),
            WorkoutModifierPolicy.IsMirrorPreferred(candidate, modifiers),
            WorkoutCoveragePolicy.IsPrimaryForGroup(candidate, group),
            WorkoutCoveragePolicy.GetCanonicalCoverage(candidate, group));
    }

    private void SetActiveLongWorkoutAllocation(WorkoutState state) =>
        ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state));

    private static void ApplyLongWorkoutAllocation(
        WorkoutState state,
        LongWorkoutAllocation allocation)
    {
        state.ActiveDirectionPartnerExerciseIds = new Dictionary<string, int>(
            allocation.DirectionPartnerExerciseIds,
            StringComparer.Ordinal);
        state.ActiveFullSideRoundIds =
            new HashSet<string>(allocation.FullSideRoundIds, StringComparer.Ordinal);
        state.ActiveExtraSetSelectionGroupIds =
            new HashSet<string>(allocation.ExtraSetSelectionGroupIds, StringComparer.Ordinal);
    }

    private IReadOnlySet<string> GetEffectiveExtraSetSelectionGroups(WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveExtraSetSelectionGroupIds
            : ChooseLongWorkoutAllocation(state).ExtraSetSelectionGroupIds;
    }

    private IReadOnlyDictionary<string, int> GetEffectiveDirectionPartnerExercises(
        WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveDirectionPartnerExerciseIds
            : ChooseLongWorkoutAllocation(state).DirectionPartnerExerciseIds;
    }

    private IReadOnlySet<string> GetEffectiveFullSideRounds(WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveFullSideRoundIds
            : ChooseLongWorkoutAllocation(state).FullSideRoundIds;
    }

    private static IReadOnlyList<WorkoutGroup> CreateWorkoutSchedule(
        int workoutMinutes,
        IReadOnlyDictionary<string, int> directionPartnerExerciseIds,
        IReadOnlySet<string> fullSideRoundIds,
        IReadOnlySet<string> extraSetSelectionGroupIds)
    {
        WorkoutResolution resolution = GetBaseResolution(workoutMinutes);
        if (workoutMinutes <= 30)
        {
            return resolution.Groups;
        }

        int extraMinutes = workoutMinutes - resolution.Groups.Count;
        int repeatedMinutes = extraMinutes - directionPartnerExerciseIds.Count -
            fullSideRoundIds.Count;
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
            string firstRoundId = $"{selectionGroup.Id}.set1";
            rounds.Add(selectionGroup with
            {
                Id = firstRoundId,
                Order = rounds.Count + 1,
                SelectionGroupId = selectionGroup.Id,
                UsesFullSideTiming = fullSideRoundIds.Contains(firstRoundId),
            });
            if (directionPartnerExerciseIds.TryGetValue(
                    selectionGroup.Id,
                    out int partnerExerciseId))
            {
                string directionRoundId = $"{selectionGroup.Id}.direction";
                rounds.Add(selectionGroup with
                {
                    Id = directionRoundId,
                    Order = rounds.Count + 1,
                    SelectionGroupId = selectionGroup.Id,
                    UsesFullSideTiming = fullSideRoundIds.Contains(directionRoundId),
                    ExerciseOverrideId = partnerExerciseId,
                });
            }
            for (int setNumber = 2; setNumber <= setCount; setNumber++)
            {
                rounds.Add(selectionGroup with
                {
                    Id = $"{selectionGroup.Id}.set{setNumber}",
                    Order = rounds.Count + 1,
                    SelectionGroupId = selectionGroup.Id,
                });
            }
        }

        return rounds.AsReadOnly();
    }

    private sealed record LongWorkoutAllocation(
        Dictionary<string, int> DirectionPartnerExerciseIds,
        HashSet<string> FullSideRoundIds,
        HashSet<string> ExtraSetSelectionGroupIds);

    private sealed record MuscleBudgetCandidate(
        int ExerciseId,
        int RealScore,
        long AdjustedScoreHalfUnits,
        bool IsMirrorPreferred,
        bool IsPrimary,
        int CanonicalCoverage);
}
