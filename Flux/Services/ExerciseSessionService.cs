using System.Diagnostics.CodeAnalysis;
using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 90;
    public const int DefaultWorkoutMinutes = 10;
    public const WorkoutModifiers DefaultWorkoutModifiers =
        WorkoutModifiers.Silence;

    private const int CurrentStateVersion = 13;
    private const int ExplicitMirrorEquipmentStateVersion = 12;
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
    private static readonly HashSet<string> LongWorkoutSelectionGroupIds =
        MassGroupingTaxonomy.GetResolution(30).Groups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);

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

        if (state.Version < ExplicitMirrorEquipmentStateVersion)
        {
            MigrateExplicitMirrorEquipment(state);
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
                GetEffectiveSetCounts(state))
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
                !IsDirectionPartnerOverrideValid(
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

    public bool CanShuffleNextExercise(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        return GetNextGroup(state)?.Id == group.Id &&
            GetCompatibleShuffleCandidates(state, group).Count > 0;
    }

    public Exercise? ShuffleNextExercise(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (GetNextGroup(state)?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }

        List<ShuffleCandidate> candidates =
            GetCompatibleShuffleCandidates(state, group);
        if (candidates.Count == 0)
        {
            return null;
        }

        Shuffle(candidates);
        WorkoutGroup selectionGroup = GetSelectionGroups(state)
            .Single(candidate => candidate.Id == group.SelectionKey);
        IReadOnlyDictionary<CanonicalMuscleGroup, int>? loadWithoutCurrent =
            state.ActiveWorkoutMinutes <= 30
                ? WorkoutMuscleBudgetPolicy.CalculateLoadHalfUnits(
                    GetSelectionGroups(state)
                        .Where(candidate => candidate.Id != selectionGroup.Id)
                        .Select(candidate => GetSelectedExercise(state, candidate)))
                : null;
        ShuffleCandidate selected = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Ranking = loadWithoutCurrent is null
                    ? EvaluateMuscleBudgetCandidate(
                        state,
                        selectionGroup,
                        candidate.Exercise)
                    : EvaluateSingleRoundMuscleBudgetCandidate(
                        selectionGroup,
                        candidate.Exercise,
                        loadWithoutCurrent,
                        state.ActiveWorkoutModifiers),
            })
            .OrderByDescending(candidate =>
                candidate.Ranking.AdjustedScoreHalfUnits)
            .ThenByDescending(candidate => candidate.Ranking.RealScore)
            .ThenByDescending(candidate => candidate.Ranking.IsMirrorPreferred)
            .ThenByDescending(candidate => candidate.Ranking.IsPrimary)
            .ThenByDescending(candidate => candidate.Ranking.CanonicalCoverage)
            .Select(candidate => candidate.Candidate)
            .First();

        state.SelectedExerciseIds[GetSelectionStorageKey(
            selectionGroup.Id,
            state.ActiveWorkoutModifiers)] = selected.Exercise.Id;
        ApplyLongWorkoutAllocation(state, selected.Allocation);
        return selected.Exercise;
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
        bool keep) =>
        RecordOutcomeWithScoreUpdates(state, group, keep).Exercise;

    public RecordedWorkoutOutcome RecordOutcomeWithScoreUpdates(
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
        if (keep && group.IsDirectionPairLead)
        {
            throw new InvalidOperationException(
                "A direction pair can only be kept after its second direction.");
        }

        return group.IsDirectionPairRound
            ? ApplyDirectionPairOutcome(state, group, keep)
            : ApplySingleOutcome(state, group, keep);
    }

    public void AdvanceDirectionPair(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (!group.IsDirectionPairLead)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the first direction of a pair.");
        }

        _ = GetPairedRound(state, group);
        state.Outcomes[group.Id] = ExerciseOutcome.Neutral;
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
    }

    private RecordedWorkoutOutcome ApplySingleOutcome(
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
        return new RecordedWorkoutOutcome(
            exercise,
            keep ? [] : Array.AsReadOnly([exercise]));
    }

    private RecordedWorkoutOutcome ApplyDirectionPairOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep)
    {
        WorkoutGroup pairedRound = GetPairedRound(state, group);
        WorkoutGroup[] orderedRounds = [group, pairedRound];
        Array.Sort(orderedRounds, (left, right) => left.Order.CompareTo(right.Order));
        Exercise[] exercises = orderedRounds
            .Select(round => GetSelectedExercise(state, round))
            .DistinctBy(exercise => exercise.Id)
            .ToArray();
        if (exercises.Length != 2)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} does not resolve to two direction exercises.");
        }

        if (!keep)
        {
            foreach (Exercise exercise in exercises)
            {
                exercise.Score--;
            }
        }

        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        state.Outcomes[group.Id] = outcome;
        state.Outcomes[pairedRound.Id] = outcome;
        state.WorkoutCompleted = GetActiveGroups(state)
            .All(activeGroup => state.Outcomes.ContainsKey(activeGroup.Id));
        state.CompletionAcknowledged = false;
        return new RecordedWorkoutOutcome(
            GetSelectedExercise(state, group),
            keep ? [] : Array.AsReadOnly(exercises));
    }

    private WorkoutGroup GetPairedRound(WorkoutState state, WorkoutGroup group)
    {
        if (group.PairedRoundId is not string pairedRoundId)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not part of a direction pair.");
        }

        return GetActiveGroups(state).SingleOrDefault(round =>
                   string.Equals(round.Id, pairedRoundId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The paired direction for {group.DisplayName} is unavailable.");
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

    public Exercise? FinishInterruptedWorkout(WorkoutState state) =>
        FinishInterruptedWorkoutWithScoreUpdates(state).FirstOrDefault();

    public IReadOnlyList<Exercise> FinishInterruptedWorkoutWithScoreUpdates(
        WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.LegacySelectedExerciseNames.Count > 0)
        {
            Exercise? legacyPenalty = ResolveLegacyPendingRest(state);
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return legacyPenalty is null
                ? []
                : Array.AsReadOnly([legacyPenalty]);
        }

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            ResetToDurationSelection(state);
            return [];
        }

        IReadOnlyList<Exercise> scoreUpdates = [];
        if (state.PendingRestGroupId is not null)
        {
            WorkoutGroup? pendingGroup = GetValidPendingRestGroup(state);
            if (pendingGroup is not null)
            {
                if (pendingGroup.IsDirectionPairLead)
                {
                    _ = GetPairedRound(state, pendingGroup);
                    state.Outcomes[pendingGroup.Id] = ExerciseOutcome.Neutral;
                }
                else if (pendingGroup.IsDirectionPairRound)
                {
                    bool keep = state.PendingRestKept;
                    scoreUpdates = ApplyDirectionPairOutcome(
                            state,
                            pendingGroup,
                            keep)
                        .ScoreUpdates;
                }
                else
                {
                    bool keep = state.PendingRestKept;
                    scoreUpdates = ApplySingleOutcome(
                            state,
                            pendingGroup,
                            keep)
                        .ScoreUpdates;
                }
            }

            ClearPendingRest(state);
        }

        PrepareNextSession(state);
        return scoreUpdates;
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
            activeGroupIds.Contains(entry.Key) && entry.Value == ExerciseOutcome.Tick);
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
                out ExerciseOutcome outcome) && outcome == ExerciseOutcome.X)
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
                    out ExerciseOutcome outcome) || outcome != ExerciseOutcome.X) &&
                rounds.Where(entry => state.Outcomes.GetValueOrDefault(
                        entry.Round.Id) == ExerciseOutcome.Tick)
                    .All(entry =>
                        !entry.Round.IsDirectionPairRound ||
                        state.Outcomes.GetValueOrDefault(
                            entry.Round.PairedRoundId!) == ExerciseOutcome.Tick))
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

    private List<ShuffleCandidate> GetCompatibleShuffleCandidates(
        WorkoutState state,
        WorkoutGroup currentRound)
    {
        IReadOnlyList<WorkoutGroup> activeRounds = GetActiveGroups(state);
        if (activeRounds.Any(round =>
                round.SelectionKey == currentRound.SelectionKey &&
                state.Outcomes.ContainsKey(round.Id)) ||
            !IsLongWorkoutAllocationValid(state))
        {
            return [];
        }

        WorkoutGroup? selectionGroup = GetSelectionGroups(state)
            .SingleOrDefault(group => group.Id == currentRound.SelectionKey);
        if (selectionGroup is null)
        {
            return [];
        }

        string selectionStorageKey = GetSelectionStorageKey(
            selectionGroup.Id,
            state.ActiveWorkoutModifiers);
        int currentExerciseId =
            state.SelectedExerciseIds.GetValueOrDefault(selectionStorageKey);
        if (currentExerciseId <= 0)
        {
            return [];
        }

        HashSet<int> unavailableExerciseIds = GetSelectionGroups(state)
            .Where(group => group.Id != selectionGroup.Id)
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers)))
            .Where(exerciseId => exerciseId > 0)
            .ToHashSet();
        var candidates = new List<ShuffleCandidate>();
        foreach (Exercise exercise in _exercises.Where(exercise =>
                     exercise.Id != currentExerciseId &&
                     !unavailableExerciseIds.Contains(exercise.Id) &&
                     IsWorkoutSelectionCandidate(
                         state,
                         exercise,
                         selectionGroup,
                         state.ActiveWorkoutModifiers)))
        {
            if (TryGetCompatibleShuffleAllocation(
                    state,
                    selectionGroup.Id,
                    selectionStorageKey,
                    exercise,
                    out LongWorkoutAllocation? allocation))
            {
                candidates.Add(new ShuffleCandidate(exercise, allocation));
            }
        }

        return candidates;
    }

    private bool TryGetCompatibleShuffleAllocation(
        WorkoutState state,
        string selectionGroupId,
        string selectionStorageKey,
        Exercise candidate,
        [NotNullWhen(true)] out LongWorkoutAllocation? allocation)
    {
        int previousExerciseId = state.SelectedExerciseIds[selectionStorageKey];
        state.SelectedExerciseIds[selectionStorageKey] = candidate.Id;
        try
        {
            LongWorkoutAllocation proposedAllocation =
                ChooseLongWorkoutAllocation(state);
            if (!state.ActiveExtraSetSelectionGroupIds.SetEquals(
                    proposedAllocation.ExtraSetSelectionGroupIds) ||
                !state.ActiveFullSideRoundIds.SetEquals(
                    proposedAllocation.FullSideRoundIds) ||
                state.ActiveSetCountsBySelectionGroupId.Count !=
                    proposedAllocation.SetCountsBySelectionGroupId.Count ||
                state.ActiveSetCountsBySelectionGroupId.Any(entry =>
                    proposedAllocation.SetCountsBySelectionGroupId.GetValueOrDefault(
                        entry.Key) != entry.Value) ||
                state.ActiveDirectionPartnerExerciseIds.Count !=
                    proposedAllocation.DirectionPartnerExerciseIds.Count ||
                state.ActiveDirectionPartnerExerciseIds.Keys.Any(groupId =>
                    !proposedAllocation.DirectionPartnerExerciseIds.ContainsKey(groupId)) ||
                state.ActiveDirectionPartnerExerciseIds.Any(entry =>
                    entry.Key != selectionGroupId &&
                    proposedAllocation.DirectionPartnerExerciseIds.GetValueOrDefault(
                        entry.Key) != entry.Value))
            {
                allocation = null;
                return false;
            }

            allocation = proposedAllocation;
            return true;
        }
        catch (InvalidOperationException)
        {
            allocation = null;
            return false;
        }
        finally
        {
            state.SelectedExerciseIds[selectionStorageKey] = previousExerciseId;
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
            if (excludedExerciseIdsByGroup.TryGetValue(
                    group.Id,
                    out IReadOnlySet<int>? excludedExerciseIds) &&
                (excludedExerciseIds.Contains(exercise.Id) ||
                 TryGetDirectionPartner(exercise, out Exercise? excludedPartner) &&
                 excludedExerciseIds.Contains(excludedPartner.Id)))
            {
                return false;
            }

            if (IsWorkoutSelectionCandidate(state, exercise, group, modifiers))
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
            .Select(GetSelectionScore)
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
                    (long)scoreRanks[GetSelectionScore(exercise)] * scoreWeight +
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
        if (IsWorkoutSelectionCandidate(state, exercise, group, modifiers))
        {
            return true;
        }

        if (!PendingRestMatchesSelectionGroup(state, group.SelectionKey) ||
            state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id) ||
            !IsCompatibleWithModifiers(exercise, modifiers) ||
            !IsAssignedToGroup(exercise, group))
        {
            return false;
        }

        if (!TryGetDirectionPartner(exercise, out Exercise? partner))
        {
            return true;
        }

        return state.ActiveWorkoutMinutes > 30 &&
            exercise.Id < partner.Id &&
            partner.DirectionPartnerExerciseId == exercise.Id &&
            !state.ActiveRecoveryExcludedExerciseIds.Contains(partner.Id) &&
            IsCompatibleWithModifiers(partner, modifiers) &&
            IsAssignedToGroup(partner, group);
    }

    private bool IsDirectionPartnerOverrideValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (state.ActiveWorkoutMinutes <= 30 ||
            state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id) ||
            !WorkoutModifierPolicy.IsSelectable(exercise, group, modifiers))
        {
            return false;
        }

        string selectionStorageKey = GetSelectionStorageKey(
            group.SelectionKey,
            modifiers);
        return state.SelectedExerciseIds.TryGetValue(
                selectionStorageKey,
                out int baseExerciseId) &&
            _exercisesById.TryGetValue(baseExerciseId, out Exercise? baseExercise) &&
            baseExercise.DirectionPartnerExerciseId == exercise.Id &&
            exercise.DirectionPartnerExerciseId == baseExercise.Id &&
            IsWorkoutSelectionCandidate(state, baseExercise, group, modifiers);
    }

    private bool IsWorkoutSelectionCandidate(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (state.ActiveRecoveryExcludedExerciseIds.Contains(exercise.Id) ||
            !WorkoutModifierPolicy.IsSelectable(exercise, group, modifiers))
        {
            return false;
        }

        if (!TryGetDirectionPartner(exercise, out Exercise? partner))
        {
            return true;
        }

        return state.ActiveWorkoutMinutes > 30 &&
            exercise.Id < partner.Id &&
            partner.DirectionPartnerExerciseId == exercise.Id &&
            !state.ActiveRecoveryExcludedExerciseIds.Contains(partner.Id) &&
            WorkoutModifierPolicy.IsSelectable(partner, group, modifiers);
    }

    private bool TryGetDirectionPartner(
        Exercise exercise,
        [NotNullWhen(true)]
        out Exercise? partner)
    {
        if (exercise.DirectionPartnerExerciseId > 0 &&
            _exercisesById.TryGetValue(
                exercise.DirectionPartnerExerciseId,
                out partner))
        {
            return true;
        }

        partner = null;
        return false;
    }

    private int GetSelectionScore(Exercise exercise)
    {
        return TryGetDirectionPartner(exercise, out Exercise? partner)
            ? Math.Min(exercise.Score, partner.Score)
            : exercise.Score;
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
        bool isDirectionRound =
            suffix.StartsWith("direction", StringComparison.Ordinal) &&
            int.TryParse(suffix.AsSpan(9), out int directionSetNumber) &&
            directionSetNumber > 0;
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
        state.ActiveSetCountsBySelectionGroupId ??= [];
        state.ActiveDirectionPartnerExerciseIds ??= [];
        state.ActiveFullSideRoundIds ??= [];
        state.PendingScoreUpdates ??= [];
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
        if (state.ActiveWorkoutMinutes <= 30)
        {
            return state.ActiveDirectionPartnerExerciseIds.Count == 0 &&
                state.ActiveFullSideRoundIds.Count == 0 &&
                state.ActiveExtraSetSelectionGroupIds.Count == 0 &&
                state.ActiveSetCountsBySelectionGroupId.Count == 0;
        }

        LongWorkoutAllocation expected = ChooseLongWorkoutAllocation(state);
        if (state.ActiveDirectionPartnerExerciseIds.Count !=
                expected.DirectionPartnerExerciseIds.Count ||
            state.ActiveDirectionPartnerExerciseIds.Any(entry =>
                expected.DirectionPartnerExerciseIds.GetValueOrDefault(entry.Key) !=
                    entry.Value))
        {
            return false;
        }

        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        if (state.ActiveSetCountsBySelectionGroupId.Count !=
                selectionGroups.Length ||
            selectionGroups.Any(group =>
                state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(
                    group.Id) < 1))
        {
            return false;
        }

        HashSet<string> expectedExtraSetGroups =
            state.ActiveSetCountsBySelectionGroupId
                .Where(entry => entry.Value > 1)
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);
        if (!state.ActiveExtraSetSelectionGroupIds.SetEquals(
                expectedExtraSetGroups))
        {
            return false;
        }

        try
        {
            IReadOnlyList<WorkoutGroup> rounds = CreateWorkoutSchedule(
                state.ActiveWorkoutMinutes,
                state.ActiveDirectionPartnerExerciseIds,
                state.ActiveFullSideRoundIds,
                state.ActiveSetCountsBySelectionGroupId);
            return state.ActiveFullSideRoundIds.All(roundId =>
            {
                WorkoutGroup? round = rounds.SingleOrDefault(candidate =>
                    candidate.Id == roundId);
                if (round is null)
                {
                    return false;
                }

                Exercise exercise = GetSelectedExercise(state, round);
                return MovementPhasePresentationPolicy.UsesTimedPair(
                    exercise.SideSequence,
                    exercise.DirectionSequence);
            });
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void NormalizeKeptExerciseIds(WorkoutState state)
    {
        state.LastKeptExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
        ExpandDirectionPairIds(state.LastKeptExerciseIds);
        foreach (int exerciseId in state.LastKeptExerciseIds.ToArray())
        {
            if (!_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
                !TryGetDirectionPartner(exercise, out Exercise? partner) ||
                exercise.Id > partner.Id)
            {
                continue;
            }

            string? sharedDate = new[]
                {
                    state.LastKeptLocalDateByExerciseId.GetValueOrDefault(exercise.Id),
                    state.LastKeptLocalDateByExerciseId.GetValueOrDefault(partner.Id),
                }
                .Where(WorkoutRecoveryPolicy.IsValidLocalDateKey)
                .OrderDescending()
                .FirstOrDefault();
            if (sharedDate is not null)
            {
                state.LastKeptLocalDateByExerciseId[exercise.Id] = sharedDate;
                state.LastKeptLocalDateByExerciseId[partner.Id] = sharedDate;
            }
        }
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
        ExpandDirectionPairIds(state.NextWorkoutExcludedExerciseIds);
        state.ActiveRecoveryExcludedExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            exercise.MuscularDemand != WorkoutRecoveryPolicy.HardMuscularDemand);
    }

    private void ExpandDirectionPairIds(HashSet<int> exerciseIds)
    {
        foreach (int exerciseId in exerciseIds.ToArray())
        {
            if (_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                TryGetDirectionPartner(exercise, out Exercise? partner))
            {
                exerciseIds.Add(partner.Id);
            }
        }
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

    private void MigrateExplicitMirrorEquipment(WorkoutState state)
    {
        foreach (string selectionStorageKey in
                 state.SelectedExerciseIds.Keys.ToArray())
        {
            if (TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out _,
                    out WorkoutModifiers modifiers) &&
                modifiers.HasFlag(WorkoutModifiers.Mirror))
            {
                state.SelectedExerciseIds.Remove(selectionStorageKey);
            }
        }

        // The former binary state did not record mirror height. Treating it as
        // compact or tall would silently claim equipment the user never chose.
        state.LastWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            state.LastWorkoutModifiers,
            MirrorEquipment.None);
        state.ActiveWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            state.ActiveWorkoutModifiers,
            MirrorEquipment.None);
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
                !IsStoredLineupSelectionValid(state, exercise, group, modifiers))
            {
                state.SelectedExerciseIds.Remove(selectionStorageKey);
            }
        }
    }

    private void NormalizeOutcomes(WorkoutState state)
    {
        Dictionary<string, WorkoutGroup> activeGroups = GetActiveGroups(state)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

        foreach (string groupId in state.Outcomes.Keys
                     .Where(groupId => !activeGroups.ContainsKey(groupId))
                     .ToArray())
        {
            state.Outcomes.Remove(groupId);
        }

        foreach (string groupId in state.Outcomes
                     .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            WorkoutGroup group = activeGroups[groupId];
            if (group.IsDirectionPairLead &&
                group.PairedRoundId is string pairedRoundId)
            {
                if (!state.Outcomes.TryGetValue(
                        pairedRoundId,
                        out ExerciseOutcome pairedOutcome))
                {
                    continue;
                }
                if (pairedOutcome is ExerciseOutcome.Tick or ExerciseOutcome.X)
                {
                    state.Outcomes[groupId] = pairedOutcome;
                    continue;
                }
            }

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
        state.ActiveSetCountsBySelectionGroupId.Clear();
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
            return new LongWorkoutAllocation([], [], [], []);
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
        var directionPartners = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorkoutGroup group in rankedGroups)
        {
            if (!state.SelectedExerciseIds.TryGetValue(
                    GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers),
                    out int exerciseId) ||
                !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
                !TryGetDirectionPartner(exercise, out Exercise? partner))
            {
                continue;
            }

            directionPartners.Add(group.Id, partner.Id);
        }

        if (directionPartners.Count > extraMinutes)
        {
            throw new InvalidOperationException(
                "The selected direction pairs do not fit in this workout duration.");
        }

        int remainingExtraMinutes = extraMinutes - directionPartners.Count;
        var timedPairRoundIds = new List<string>();
        foreach (WorkoutGroup group in rankedGroups)
        {
            int exerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(group.Id, state.ActiveWorkoutModifiers));
            if (_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                MovementPhasePresentationPolicy.UsesTimedPair(
                    exercise.SideSequence,
                    exercise.DirectionSequence))
            {
                timedPairRoundIds.Add($"{group.Id}.set1");
            }
            if (directionPartners.TryGetValue(group.Id, out int partnerId) &&
                _exercisesById.TryGetValue(partnerId, out Exercise? partner) &&
                MovementPhasePresentationPolicy.UsesTimedPair(
                    partner.SideSequence,
                    partner.DirectionSequence))
            {
                timedPairRoundIds.Add($"{group.Id}.direction1");
            }
        }

        HashSet<string> fullSideRounds = timedPairRoundIds
            .Take(remainingExtraMinutes)
            .ToHashSet(StringComparer.Ordinal);
        int repeatMinutes = remainingExtraMinutes - fullSideRounds.Count;
        Dictionary<string, int> setCounts = rankedGroups.ToDictionary(
            group => group.Id,
            _ => 1,
            StringComparer.Ordinal);
        while (repeatMinutes > 0)
        {
            bool allocated = false;
            foreach (WorkoutGroup group in rankedGroups)
            {
                int setCost = directionPartners.ContainsKey(group.Id) ? 2 : 1;
                if (setCost > repeatMinutes)
                {
                    continue;
                }

                setCounts[group.Id]++;
                repeatMinutes -= setCost;
                allocated = true;
                if (repeatMinutes == 0)
                {
                    break;
                }
            }

            if (!allocated)
            {
                throw new InvalidOperationException(
                    "The long-workout direction units cannot fill the selected duration.");
            }
        }

        HashSet<string> extraSetGroups = setCounts
            .Where(entry => entry.Value > 1)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        return new LongWorkoutAllocation(
            directionPartners,
            fullSideRounds,
            extraSetGroups,
            setCounts);
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
                            GetSelectionScore(exercise),
                            temporaryDownvoteHalfUnits: 0) >
                            current.AdjustedScoreHalfUnits &&
                        !unavailableExerciseIds.Contains(exercise.Id) &&
                        !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                        IsWorkoutSelectionCandidate(
                            state,
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

    private bool IsStoredLineupSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (IsSavedSelectionValid(state, exercise, group, modifiers))
        {
            return true;
        }

        if (state.ActiveWorkoutMinutes != 0 ||
            !LongWorkoutSelectionGroupIds.Contains(group.Id) ||
            !TryGetDirectionPartner(exercise, out Exercise? partner))
        {
            return false;
        }

        return exercise.Id < partner.Id &&
            partner.DirectionPartnerExerciseId == exercise.Id &&
            IsCompatibleWithModifiers(exercise, modifiers) &&
            IsCompatibleWithModifiers(partner, modifiers) &&
            IsAssignedToGroup(exercise, group) &&
            IsAssignedToGroup(partner, group);
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
                allocation.SetCountsBySelectionGroupId);
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
            int selectionScore = GetSelectionScore(candidate);
            return new MuscleBudgetCandidate(
                candidate.Id,
                selectionScore,
                WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                    selectionScore,
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

    private MuscleBudgetCandidate EvaluateSingleRoundMuscleBudgetCandidate(
        WorkoutGroup group,
        Exercise candidate,
        IReadOnlyDictionary<CanonicalMuscleGroup, int> loadWithoutCandidate,
        WorkoutModifiers modifiers)
    {
        int temporaryDownvoteHalfUnits =
            WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnitsAfterAddingExercise(
                loadWithoutCandidate,
                candidate);
        int selectionScore = GetSelectionScore(candidate);
        return new MuscleBudgetCandidate(
            candidate.Id,
            selectionScore,
            WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                selectionScore,
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
        state.ActiveSetCountsBySelectionGroupId = new Dictionary<string, int>(
            allocation.SetCountsBySelectionGroupId,
            StringComparer.Ordinal);
    }

    private IReadOnlyDictionary<string, int> GetEffectiveSetCounts(
        WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveSetCountsBySelectionGroupId
            : ChooseLongWorkoutAllocation(state).SetCountsBySelectionGroupId;
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
        IReadOnlyDictionary<string, int> setCountsBySelectionGroupId)
    {
        WorkoutResolution resolution = GetBaseResolution(workoutMinutes);
        if (workoutMinutes <= 30)
        {
            return resolution.Groups;
        }

        var rounds = new List<WorkoutGroup>(workoutMinutes);

        for (int groupIndex = 0;
             groupIndex < resolution.Groups.Count;
            groupIndex++)
        {
            WorkoutGroup selectionGroup = resolution.Groups[groupIndex];
            int setCount = Math.Max(
                1,
                setCountsBySelectionGroupId.GetValueOrDefault(
                    selectionGroup.Id,
                    1));
            bool hasDirectionPartner = directionPartnerExerciseIds.TryGetValue(
                selectionGroup.Id,
                out int partnerExerciseId);
            for (int setNumber = 1; setNumber <= setCount; setNumber++)
            {
                string setRoundId = $"{selectionGroup.Id}.set{setNumber}";
                string? directionRoundId = hasDirectionPartner
                    ? $"{selectionGroup.Id}.direction{setNumber}"
                    : null;
                rounds.Add(selectionGroup with
                {
                    Id = setRoundId,
                    Order = rounds.Count + 1,
                    SelectionGroupId = selectionGroup.Id,
                    UsesFullSideTiming = fullSideRoundIds.Contains(setRoundId),
                    PairedRoundId = directionRoundId,
                });
                if (directionRoundId is not null)
                {
                    rounds.Add(selectionGroup with
                    {
                        Id = directionRoundId,
                        Order = rounds.Count + 1,
                        SelectionGroupId = selectionGroup.Id,
                        UsesFullSideTiming = fullSideRoundIds.Contains(
                            directionRoundId),
                        ExerciseOverrideId = partnerExerciseId,
                        PairedRoundId = setRoundId,
                        IsPairDecisionRound = true,
                    });
                }
            }
        }

        int scheduledMinutes = rounds.Sum(round =>
            round.UsesFullSideTiming ? 2 : 1);
        if (scheduledMinutes != workoutMinutes)
        {
            throw new InvalidOperationException(
                $"The {workoutMinutes}-minute workout scheduled " +
                $"{scheduledMinutes} minutes.");
        }

        return rounds.AsReadOnly();
    }

    private sealed record LongWorkoutAllocation(
        Dictionary<string, int> DirectionPartnerExerciseIds,
        HashSet<string> FullSideRoundIds,
        HashSet<string> ExtraSetSelectionGroupIds,
        Dictionary<string, int> SetCountsBySelectionGroupId);

    private sealed record ShuffleCandidate(
        Exercise Exercise,
        LongWorkoutAllocation Allocation);

    private sealed record MuscleBudgetCandidate(
        int ExerciseId,
        int RealScore,
        long AdjustedScoreHalfUnits,
        bool IsMirrorPreferred,
        bool IsPrimary,
        int CanonicalCoverage);
}
