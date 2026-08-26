using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 90;
    public const int DefaultWorkoutMinutes = 10;
    public const WorkoutModifiers DefaultWorkoutModifiers =
        WorkoutModifiers.Silence;

    private const int CurrentStateVersion = 19;
    private const long RestDurationMilliseconds = 15_000L;
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
    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly IReadOnlyDictionary<int, Exercise> _exercisesById;
    private readonly IReadOnlyDictionary<int, Exercise> _sequenceRootByExerciseId;
    private readonly Random _random;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    public ExerciseSessionService(
        IReadOnlyList<Exercise> exercises,
        Random? random = null,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        _exercises = exercises;
        _exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        var sequenceRootByExerciseId = new Dictionary<int, Exercise>();
        foreach (Exercise root in exercises.Where(exercise =>
                     exercise.SequenceBlocks.Length > 0))
        {
            foreach (int memberId in root.SequenceBlocks
                         .Select(block => block.ExerciseId)
                         .Distinct())
            {
                if (!_exercisesById.ContainsKey(memberId) ||
                    !sequenceRootByExerciseId.TryAdd(memberId, root))
                {
                    throw new ArgumentException(
                        $"Exercise {memberId} has an invalid sequence owner.",
                        nameof(exercises));
                }
            }
        }
        if (sequenceRootByExerciseId.Count != exercises.Count)
        {
            throw new ArgumentException(
                "Every exercise must belong to exactly one sequence.",
                nameof(exercises));
        }
        _sequenceRootByExerciseId = sequenceRootByExerciseId;
        _random = random ?? Random.Shared;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<int> SupportedWorkoutMinutes =>
        WorkoutMinutes;

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        NormalizeCollections(state);
        LegacyActiveProgressSnapshot? atomicSequenceMigration =
            state.Version is 16 or 17 && state.ActiveWorkoutMinutes > 0
                ? CaptureLegacyActiveProgress(state)
                : null;
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

        // Only a valid, resumable rest may preserve a below-threshold active
        // selection. Clear stale checkpoints before lineup arbitration.
        NormalizePendingRest(state);
        RepairActiveLineup(state);
        NormalizeActiveLongWorkoutAllocation(state);
        if (atomicSequenceMigration is not null)
        {
            MigrateLegacyActiveProgress(state, atomicSequenceMigration);
        }
        NormalizeOutcomes(state);
        NormalizeCompletionState(state);
        NormalizePendingRest(state);
        NormalizePendingMovement(state);
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
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingMovement(state);
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
                state,
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
                !IsSequenceOverrideValid(
                    state,
                    overrideExercise,
                    group,
                    state.ActiveWorkoutModifiers))
            {
                throw new InvalidOperationException(
                    $"The linked sequence block for {group.DisplayName} is unavailable.");
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

    public bool IsIntermediateSequenceBlock(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        int groupIndex = activeGroups
            .Select((activeGroup, index) => (activeGroup, index))
            .Where(entry => entry.activeGroup.Id == group.Id)
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .Single();
        return groupIndex >= 0 &&
            groupIndex + 1 < activeGroups.Count &&
            activeGroups[groupIndex + 1].SelectionKey == group.SelectionKey;
    }

    public bool IsSequenceContinuationBlock(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        int groupIndex = activeGroups
            .Select((activeGroup, index) => (activeGroup, index))
            .Where(entry => entry.activeGroup.Id == group.Id)
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .Single();
        return groupIndex > 0 &&
            activeGroups[groupIndex - 1].SelectionKey == group.SelectionKey;
    }

    public bool CanShuffleNextExercise(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        return GetNextGroup(state)?.Id == group.Id &&
            !IsSequenceContinuationBlock(state, group) &&
            GetCompatibleShuffleCandidates(state, group).Count > 0;
    }

    public ShuffledExerciseResult? ShuffleNextExercise(
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
        if (IsSequenceContinuationBlock(state, group))
        {
            return null;
        }

        List<ShuffleCandidate> candidates =
            GetCompatibleShuffleCandidates(state, group);
        if (candidates.Count == 0)
        {
            return null;
        }

        Exercise rejectedExercise = GetSelectedExercise(state, group);
        Exercise rejectedRoot = GetSequenceRoot(rejectedExercise);
        Exercise[] scoreUpdates = GetSequenceExercises(rejectedRoot);

        Shuffle(candidates);
        ShuffleCandidate selected = candidates[0];

        foreach (WorkoutGroup coveredGroup in selected.CoveredGroups)
        {
            state.SelectedExerciseIds[GetSelectionStorageKey(
                coveredGroup.Id,
                state.ActiveWorkoutModifiers)] = selected.Exercise.Id;
        }
        ApplyShuffleRejection(state, scoreUpdates);
        ApplyLongWorkoutAllocation(state, selected.Allocation);

        return new ShuffledExerciseResult(
            rejectedExercise,
            selected.Exercise,
            Array.AsReadOnly(scoreUpdates));
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

    public void BeginRest(
        WorkoutState state,
        WorkoutGroup group,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (endsAtUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endsAtUnixMilliseconds));
        }

        ClearPendingMovement(state);
        state.PendingRestGroupId = group.Id;
        state.PendingRestEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
        state.PendingRestKept = false;
        WorkoutRecoveryPolicy.RecordCompletedMuscularWork(
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
            GetSelectedExercise(state, group),
            GetCurrentUnixTimeMilliseconds());
    }

    public void BeginMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        ValidatePendingMovement(
            state,
            group,
            millisecondsRemaining,
            endsAtUnixMilliseconds,
            allowPausedDeadline: false);
        ClearPendingRest(state);
        state.PendingMovementGroupId = group.Id;
        state.PendingMovementMillisecondsRemaining = millisecondsRemaining;
        state.PendingMovementEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingMovementPausedByUser = false;
    }

    public void PauseMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        bool pausedByUser)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        ValidatePendingMovement(
            state,
            group,
            millisecondsRemaining,
            endsAtUnixMilliseconds: 0,
            allowPausedDeadline: true);
        ClearPendingRest(state);
        state.PendingMovementGroupId = group.Id;
        state.PendingMovementMillisecondsRemaining = millisecondsRemaining;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = pausedByUser;
    }

    public WorkoutGroup? GetPendingMovementGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return GetValidPendingMovementGroup(state);
    }

    public WorkoutGroup? GetPendingRestGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WorkoutGroup? pendingGroup = GetValidPendingRestGroup(state);
        return pendingGroup is not null &&
            GetNextGroup(state)?.Id == pendingGroup.Id
                ? pendingGroup
                : null;
    }

    public bool KeepPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup is null ||
            IsIntermediateSequenceBlock(state, pendingGroup))
        {
            return false;
        }

        state.PendingRestKept = true;
        return true;
    }

    public void PauseRest(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup?.Id != group.Id || state.PendingRestPausedByUser)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} does not have a running rest.");
        }
        if (millisecondsRemaining <= 0 ||
            millisecondsRemaining > RestDurationMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsRemaining));
        }

        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = millisecondsRemaining;
        state.PendingRestPausedByUser = true;
    }

    public void ResumeRest(
        WorkoutState state,
        WorkoutGroup group,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup?.Id != group.Id || !state.PendingRestPausedByUser)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} does not have a paused rest.");
        }
        if (endsAtUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(endsAtUnixMilliseconds));
        }

        state.PendingRestEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
    }

    public long GetPendingRestMillisecondsRemaining(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }
        if (GetPendingRestGroup(state) is null)
        {
            return 0;
        }

        long remaining = state.PendingRestPausedByUser
            ? state.PendingRestMillisecondsRemaining
            : state.PendingRestEndsAtUnixMilliseconds - nowUnixMilliseconds;
        return Math.Clamp(remaining, 0L, RestDurationMilliseconds);
    }

    public long GetPendingMovementMillisecondsRemaining(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        WorkoutGroup? group = GetValidPendingMovementGroup(state);
        if (group is null)
        {
            return 0;
        }

        long remaining = state.PendingMovementMillisecondsRemaining;
        if (state.PendingMovementEndsAtUnixMilliseconds > nowUnixMilliseconds)
        {
            remaining = Math.Min(
                remaining,
                state.PendingMovementEndsAtUnixMilliseconds - nowUnixMilliseconds);
        }

        // An expired wall-clock deadline means the app stopped before its
        // lifecycle pause could checkpoint the monotonic countdown. Resume
        // conservatively from the last stored time instead of crediting an
        // exercise that may not have been performed while Flux was absent.
        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, group)) * 1_000L;
        return Math.Clamp(remaining, 1L, maximum);
    }

    public void ClearPendingMovement(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.PendingMovementGroupId = null;
        state.PendingMovementMillisecondsRemaining = 0;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = false;
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
        if (!group.IsFinalSequenceRound)
        {
            throw new InvalidOperationException(
                "A sequence can only be rated after its final block.");
        }

        ClearPendingMovement(state);
        return ApplySequenceOutcome(state, group, keep);
    }

    public RecordedWorkoutOutcome RejectCurrentSequenceWithScoreUpdates(
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

        WorkoutGroup[] sequenceRounds = GetActiveGroups(state)
            .Where(round => round.SelectionKey == group.SelectionKey)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup decisionRound = sequenceRounds[^1];
        foreach (WorkoutGroup round in sequenceRounds)
        {
            if (round.Id != decisionRound.Id)
            {
                state.Outcomes.TryAdd(round.Id, ExerciseOutcome.Neutral);
            }
        }

        ClearPendingMovement(state);
        ClearPendingRest(state);
        return ApplySequenceOutcome(state, decisionRound, keep: false);
    }

    public void AdvanceSequence(
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
        if (!IsIntermediateSequenceBlock(state, group))
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not an intermediate sequence block.");
        }

        state.Outcomes[group.Id] = ExerciseOutcome.Neutral;
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
    }

    private RecordedWorkoutOutcome ApplySequenceOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep)
    {
        Exercise exercise = GetSelectedExercise(state, group);
        Exercise root = GetSequenceRoot(exercise);
        Exercise[] sequenceExercises = GetSequenceExercises(root);
        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        if (!keep)
        {
            foreach (Exercise sequenceExercise in sequenceExercises)
            {
                sequenceExercise.Score--;
            }
        }

        state.Outcomes[group.Id] = outcome;
        state.WorkoutCompleted = GetActiveGroups(state)
            .All(activeGroup => state.Outcomes.ContainsKey(activeGroup.Id));
        state.CompletionAcknowledged = false;
        return new RecordedWorkoutOutcome(
            exercise,
            keep ? [] : Array.AsReadOnly(sequenceExercises));
    }

    private static void ApplyShuffleRejection(
        WorkoutState state,
        IReadOnlyList<Exercise> exercises)
    {
        HashSet<int> rejectedExerciseIds = exercises
            .Select(exercise => exercise.Id)
            .ToHashSet();
        foreach (Exercise exercise in exercises)
        {
            exercise.Score--;
        }

        state.NextWorkoutExcludedExerciseIds.UnionWith(rejectedExerciseIds);
        state.LastKeptExerciseIds.ExceptWith(rejectedExerciseIds);

        foreach (string savedGroupId in state.SelectedExerciseIds
                     .Where(entry => rejectedExerciseIds.Contains(entry.Value))
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            state.SelectedExerciseIds.Remove(savedGroupId);
        }
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
                if (IsIntermediateSequenceBlock(state, pendingGroup))
                {
                    AdvanceSequence(state, pendingGroup);
                }
                else
                {
                    bool keep = state.PendingRestKept;
                    scoreUpdates = ApplySequenceOutcome(
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

        ExerciseOutcome[] decisionOutcomes = GetActiveGroups(state)
            .GroupBy(group => group.SelectionKey)
            .Select(rounds => rounds.OrderBy(round => round.Order).Last())
            .Where(round => state.Outcomes.ContainsKey(round.Id))
            .Select(round => state.Outcomes[round.Id])
            .ToArray();
        int replaced = decisionOutcomes.Count(outcome =>
            outcome == ExerciseOutcome.X);
        int kept = decisionOutcomes.Count(outcome =>
            outcome == ExerciseOutcome.Tick);
        return (replaced, kept);
    }

    public void ClearPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
        state.PendingRestKept = false;
    }

    private void PrepareNextSession(WorkoutState state)
    {
        WorkoutGroup[] activeRounds = GetActiveGroups(state).ToArray();
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        var rejectedSelectionKeys = new HashSet<string>(StringComparer.Ordinal);
        var newlyKeptExerciseIds = new HashSet<int>();
        var rejectedExerciseIds = new HashSet<int>();
        foreach (WorkoutGroup selectionGroup in selectionGroups)
        {
            WorkoutGroup? decisionRound = activeRounds
                .Where(round => round.SelectionKey == selectionGroup.Id)
                .OrderBy(round => round.Order)
                .LastOrDefault();
            if (decisionRound is null ||
                !state.Outcomes.TryGetValue(
                    decisionRound.Id,
                    out ExerciseOutcome outcome))
            {
                continue;
            }

            int rootId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    selectionGroup.Id,
                    state.ActiveWorkoutModifiers));
            if (!_exercisesById.TryGetValue(rootId, out Exercise? root))
            {
                continue;
            }

            int[] sequenceExerciseIds = GetSequenceExercises(root)
                .Select(exercise => exercise.Id)
                .ToArray();
            if (outcome == ExerciseOutcome.Tick)
            {
                newlyKeptExerciseIds.UnionWith(sequenceExerciseIds);
            }
            else if (outcome == ExerciseOutcome.X)
            {
                rejectedSelectionKeys.Add(selectionGroup.Id);
                rejectedExerciseIds.UnionWith(sequenceExerciseIds);
            }
        }
        state.NextWorkoutExcludedExerciseIds.UnionWith(rejectedExerciseIds);
        state.LastKeptExerciseIds.ExceptWith(rejectedExerciseIds);
        state.LastKeptExerciseIds.UnionWith(newlyKeptExerciseIds);
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

        SelectedSequencePlacement? currentPlacement;
        try
        {
            currentPlacement = GetSelectedSequencePlacements(state)
                .SingleOrDefault(placement =>
                    placement.Anchor.Id == currentRound.SelectionKey);
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        if (currentPlacement is null)
        {
            return [];
        }

        Exercise currentExercise = currentPlacement.Root;
        int currentExerciseId = currentExercise.Id;
        HashSet<string> coveredGroupIds = currentPlacement.CoveredGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<int> rejectedExerciseIds = GetSequenceExercises(currentExercise)
            .Select(exercise => exercise.Id)
            .ToHashSet();
        HashSet<string> startedSelectionGroupIds = activeRounds
            .Where(round => state.Outcomes.ContainsKey(round.Id))
            .Select(round => round.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<int> unavailableExerciseIds = GetSelectionGroups(state)
            .Where(group => !coveredGroupIds.Contains(group.Id))
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers)))
            .Where(exerciseId => exerciseId > 0)
            .ToHashSet();
        HashSet<int> unavailableMovementIds = unavailableExerciseIds
            .Where(_exercisesById.ContainsKey)
            .Select(exerciseId => WorkoutModifierPolicy.GetSessionMovementId(
                _exercisesById[exerciseId]))
            .Append(WorkoutModifierPolicy.GetSessionMovementId(currentExercise))
            .ToHashSet();
        var candidates = new List<ShuffleCandidate>();
        foreach (Exercise exercise in _exercises.Where(exercise =>
                     exercise.Id != currentExerciseId &&
                     !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                     !unavailableExerciseIds.Contains(exercise.Id) &&
                     !unavailableMovementIds.Contains(
                          WorkoutModifierPolicy.GetSessionMovementId(exercise)) &&
                     GetSequencePlacementOptions(
                             exercise,
                             GetSelectionGroups(state))
                         .Any(option => option.Select(group => group.Id)
                             .ToHashSet(StringComparer.Ordinal)
                             .SetEquals(coveredGroupIds)) &&
                     IsWorkoutSelectionCandidate(
                         state,
                         exercise,
                         currentPlacement.Anchor,
                         state.ActiveWorkoutModifiers)))
        {
            if (TryGetCompatibleShuffleAllocation(
                    state,
                    currentPlacement.CoveredGroups,
                    exercise,
                    startedSelectionGroupIds,
                    rejectedExerciseIds,
                    out LongWorkoutAllocation? allocation))
            {
                candidates.Add(new ShuffleCandidate(
                    exercise,
                    currentPlacement.CoveredGroups,
                    allocation));
            }
        }

        return candidates;
    }

    private bool TryGetCompatibleShuffleAllocation(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> coveredGroups,
        Exercise candidate,
        IReadOnlySet<string> startedSelectionGroupIds,
        IReadOnlySet<int> rejectedExerciseIds,
        [NotNullWhen(true)] out LongWorkoutAllocation? allocation)
    {
        Dictionary<string, int> previousExerciseIds = coveredGroups.ToDictionary(
            group => GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers),
            group => state.SelectedExerciseIds[GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers)],
            StringComparer.Ordinal);
        int[] temporarilyRemovedKeptExerciseIds = rejectedExerciseIds
            .Where(state.LastKeptExerciseIds.Contains)
            .ToArray();
        foreach (string selectionStorageKey in previousExerciseIds.Keys)
        {
            state.SelectedExerciseIds[selectionStorageKey] = candidate.Id;
        }
        state.LastKeptExerciseIds.ExceptWith(rejectedExerciseIds);
        try
        {
            allocation = ChooseLongWorkoutAllocation(
                state,
                startedSelectionGroupIds);
            return true;
        }
        catch (InvalidOperationException)
        {
            allocation = null;
            return false;
        }
        finally
        {
            foreach ((string selectionStorageKey, int previousExerciseId) in
                     previousExerciseIds)
            {
                state.SelectedExerciseIds[selectionStorageKey] = previousExerciseId;
            }
            state.LastKeptExerciseIds.UnionWith(
                temporarilyRemovedKeptExerciseIds);
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
            Exercise[] sequenceExercises = GetSequenceExercises(exercise);
            if (sequenceExercises.Any(sequenceExercise =>
                    state.NextWorkoutExcludedExerciseIds.Contains(
                        sequenceExercise.Id)))
            {
                return false;
            }
            if (excludedExerciseIdsByGroup.TryGetValue(
                    group.Id,
                    out IReadOnlySet<int>? excludedExerciseIds) &&
                sequenceExercises.Any(sequenceExercise =>
                    excludedExerciseIds.Contains(sequenceExercise.Id)))
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

        int[] orderedScores = candidates
            .Select(GetSelectionScore)
            .Distinct()
            .Order()
            .ToArray();
        Dictionary<int, int> scoreRanks = orderedScores
            .Select((score, rank) => (score, rank))
            .ToDictionary(entry => entry.score, entry => entry.rank);
        Dictionary<string, int> highestScoreByGroup = groups.ToDictionary(
            group => group.Id,
            group => candidates
                .Where(exercise => IsAllowed(exercise, group))
                .Select(GetSelectionScore)
                .DefaultIfEmpty(int.MinValue)
                .Max(),
            StringComparer.Ordinal);
        long selectionTimeUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        Dictionary<long, int> freshHardMuscleRanks = candidates
            .SelectMany(exercise => groups
                .Where(group => IsAllowed(exercise, group))
                .Select(group => GetSequenceSelectionExerciseForGroup(
                    exercise,
                    group)))
            .Where(exercise =>
                WorkoutRecoveryPolicy.IsHardExercise(exercise) &&
                !WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    exercise.PrimaryCanonicalGroup,
                    selectionTimeUnixMilliseconds))
            .Select(exercise =>
                WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    exercise.PrimaryCanonicalGroup))
            .Distinct()
            .OrderDescending()
            .Select((timestamp, rank) => (timestamp, rank))
            .ToDictionary(entry => entry.timestamp, entry => entry.rank);
        int maximumCoverage = groups.Max(group => group.CanonicalGroups.Count);
        // These are exact lexicographic assignment dimensions, not hardness
        // points. BigInteger keeps the ordering lossless for arbitrary saved
        // score histories without writing any derived value back to the user.
        BigInteger totalLowerPriorityRange =
            (BigInteger)groups.Count * maximumCoverage;
        BigInteger AddPriorityDimension(long maximumValue)
        {
            BigInteger weight = totalLowerPriorityRange + BigInteger.One;
            totalLowerPriorityRange +=
                (BigInteger)groups.Count * maximumValue * weight;
            return weight;
        }

        BigInteger primaryWeight = AddPriorityDimension(1L);
        BigInteger mirrorPreferenceWeight = AddPriorityDimension(1L);
        BigInteger currentSelectionWeight = AddPriorityDimension(1L);
        BigInteger hardMuscleAgeWeight = AddPriorityDimension(
            Math.Max(0, freshHardMuscleRanks.Count - 1));
        BigInteger moderateRecoveryAvoidanceWeight = AddPriorityDimension(1L);
        BigInteger hardRecoveryAvoidanceWeight = AddPriorityDimension(1L);
        BigInteger freshHardWeight = AddPriorityDimension(1L);
        BigInteger scoreWeight = AddPriorityDimension(
            Math.Max(0, orderedScores.Length - 1));
        BigInteger keptExerciseWeight = AddPriorityDimension(1L);
        BigInteger hardOpportunityWeight = AddPriorityDimension(1L);
        BigInteger preservedActiveSelectionWeight = allowSavedSelectionException
            ? totalLowerPriorityRange + BigInteger.One
            : BigInteger.Zero;

        var allowed = new bool[groups.Count, candidates.Count];
        var utilities = new BigInteger[groups.Count, candidates.Count];
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
                Exercise selectionExercise =
                    GetSequenceSelectionExerciseForGroup(exercise, group);
                HardExerciseRotationStatus hardRotationStatus =
                    WorkoutRecoveryPolicy.GetRotationStatus(
                        selectionExercise,
                        group,
                        state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                        selectionTimeUnixMilliseconds);
                bool isRecoveringModerate =
                    WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
                        selectionExercise,
                        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
                        selectionTimeUnixMilliseconds);
                int hardMuscleAgeRank = hardRotationStatus ==
                        HardExerciseRotationStatus.FreshHard
                    ? freshHardMuscleRanks.GetValueOrDefault(
                        WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                            state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                            selectionExercise.PrimaryCanonicalGroup))
                    : 0;
                bool isKept = GetSequenceExercises(exercise)
                    .All(member => preferredExerciseIds.Contains(member.Id));
                // A non-kept hard exercise can displace a keep only while it is
                // fresh, primary for this slot, and already in that slot's top
                // saved-score bucket. A fresh hard keep is the explicit second
                // path; rejection removes keep status before this comparison.
                bool hasHardOpportunity = hardRotationStatus ==
                        HardExerciseRotationStatus.FreshHard &&
                    (isKept ||
                     GetSelectionScore(exercise) == highestScoreByGroup[group.Id]);
                bool hasContextualKeepPreference = isKept &&
                    hardRotationStatus !=
                        HardExerciseRotationStatus.RecoveringHard &&
                    !isRecoveringModerate;
                BigInteger utility =
                    (allowSavedSelectionException &&
                     currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id
                        ? preservedActiveSelectionWeight
                        : BigInteger.Zero) +
                    (hasHardOpportunity
                        ? hardOpportunityWeight
                        : BigInteger.Zero) +
                    (hasContextualKeepPreference
                        ? keptExerciseWeight
                        : BigInteger.Zero) +
                    scoreRanks[GetSelectionScore(exercise)] * scoreWeight +
                    (hardRotationStatus != HardExerciseRotationStatus.RecoveringHard
                        ? hardRecoveryAvoidanceWeight
                        : BigInteger.Zero) +
                    (!isRecoveringModerate
                        ? moderateRecoveryAvoidanceWeight
                        : BigInteger.Zero) +
                    (hardRotationStatus == HardExerciseRotationStatus.FreshHard
                        ? freshHardWeight
                        : BigInteger.Zero) +
                    hardMuscleAgeRank * hardMuscleAgeWeight +
                    (currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id
                        ? currentSelectionWeight
                        : BigInteger.Zero) +
                    (WorkoutModifierPolicy.IsMirrorPreferred(
                            selectionExercise,
                            modifiers)
                        ? mirrorPreferenceWeight
                        : BigInteger.Zero) +
                    (WorkoutCoveragePolicy.IsPrimaryForGroup(
                            selectionExercise,
                            group)
                        ? primaryWeight
                        : BigInteger.Zero) +
                    WorkoutSequencePolicy.GetCanonicalCoverage(
                        exercise,
                        _exercisesById,
                        group);
                utilities[groupIndex, exerciseIndex] = utility;
            }
        }

        var atomicCandidates = new List<AtomicSequenceCandidate>();
        for (int candidateIndex = 0;
             candidateIndex < candidates.Count;
             candidateIndex++)
        {
            Exercise candidate = candidates[candidateIndex];
            var placementOptions = GetSequencePlacementOptions(
                    candidate,
                    groups)
                .ToList();
            if (allowSavedSelectionException)
            {
                foreach (int groupIndex in Enumerable.Range(0, groups.Count)
                             .Where(groupIndex =>
                                 currentExerciseIds.GetValueOrDefault(
                                     groups[groupIndex].Id) == candidate.Id &&
                                 allowed[groupIndex, candidateIndex] &&
                                 placementOptions.All(option =>
                                     option.All(candidateGroup =>
                                         candidateGroup.Id !=
                                            groups[groupIndex].Id))))
                {
                    placementOptions.Add([groups[groupIndex]]);
                }
            }

            foreach (WorkoutGroup[] placementGroups in placementOptions)
            {
                if (candidate.SequenceBlocks.Length +
                        (groups.Count - placementGroups.Length) >
                    state.ActiveWorkoutMinutes)
                {
                    // Even if every remaining slot used one block, this
                    // placement could not fit the requested duration.
                    continue;
                }
                ulong coverageMask = 0;
                bool placementAllowed = true;
                foreach (WorkoutGroup placementGroup in placementGroups)
                {
                    int groupIndex = groups
                        .Select((group, index) => (group, index))
                        .Single(entry => entry.group.Id == placementGroup.Id)
                        .index;
                    if (!allowed[groupIndex, candidateIndex])
                    {
                        placementAllowed = false;
                        break;
                    }
                    coverageMask |= 1UL << groupIndex;
                }
                if (!placementAllowed)
                {
                    continue;
                }

                var utilitiesByGroup = new BigInteger[groups.Count];
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    utilitiesByGroup[groupIndex] =
                        utilities[groupIndex, candidateIndex];
                }
                atomicCandidates.Add(new AtomicSequenceCandidate(
                    candidate.Id,
                    WorkoutModifierPolicy.GetSessionMovementId(candidate),
                    coverageMask,
                    candidate.SequenceBlocks.Length,
                    utilitiesByGroup,
                    candidateIndex));
            }
        }

        AtomicSequenceLineup? solution = AtomicSequenceLineupSolver.Solve(
            groups.Count,
            state.ActiveWorkoutMinutes,
            atomicCandidates);
        if (solution is null)
        {
            int movementCount = atomicCandidates
                .Select(candidate => candidate.MovementId)
                .Distinct()
                .Count();
            throw CreateDistinctLineupException(groups, movementCount);
        }

        return solution.ExerciseIdByGroupIndex.ToDictionary(
            entry => groups[entry.Key].Id,
            entry => entry.Value,
            StringComparer.Ordinal);
    }

    private void RepairActiveLineup(WorkoutState state)
    {
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        IReadOnlyList<WorkoutGroup> activeRounds;
        try
        {
            activeRounds = GetActiveGroups(state);
        }
        catch (InvalidOperationException)
        {
            activeRounds = [];
        }
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
        activeRounds ??= clearChangedProgress
            ? GetActiveGroups(state)
            : [];
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
        int movementCount)
    {
        return new InvalidOperationException(
            $"No distinct exercise lineup exists for the active workout profile " +
            $"across {groups.Count} groups and {movementCount} eligible session " +
            $"movements " +
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
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        return GetSequenceExercises(exercise).All(member =>
            IsCompatibleWithModifiers(member, modifiers) &&
            IsAssignedToGroup(member, group));
    }

    private bool IsSequenceOverrideValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (group.SequenceBlockIndex < 0 ||
            group.SequenceBlockIndex >= group.SequenceBlockCount)
        {
            return false;
        }

        string selectionStorageKey = GetSelectionStorageKey(
            group.SelectionKey,
            modifiers);
        return state.SelectedExerciseIds.TryGetValue(
            selectionStorageKey,
                out int rootExerciseId) &&
            _exercisesById.TryGetValue(rootExerciseId, out Exercise? root) &&
            root.SequenceBlocks.Length == group.SequenceBlockCount &&
            root.SequenceBlocks[group.SequenceBlockIndex].ExerciseId == exercise.Id &&
            root.SequenceBlocks[group.SequenceBlockIndex].SideCue ==
                group.SequenceSideCue &&
            root.SequenceBlocks[group.SequenceBlockIndex].DirectionCue ==
                group.SequenceDirectionCue &&
            root.SequenceBlocks[group.SequenceBlockIndex].MirrorMedia ==
                group.MirrorSequenceMedia &&
            root.SequenceBlocks[group.SequenceBlockIndex].MediaSegment ==
                group.SequenceMediaSegment &&
            IsWorkoutSelectionCandidate(state, root, group, modifiers);
    }

    private bool IsWorkoutSelectionCandidate(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (exercise.SequenceBlocks.Length == 0 ||
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups = GetSelectionGroups(state);
        if (resolutionGroups.Count == 0)
        {
            resolutionGroups = GetResolutionGroupsForGroup(group);
        }
        WorkoutGroup[][] placementOptions = GetSequencePlacementOptions(
            exercise,
            resolutionGroups);
        return placementOptions.Any(option => option.Any(candidate =>
                candidate.CanonicalGroups.SetEquals(group.CanonicalGroups))) &&
            GetSequenceExercises(exercise).All(member =>
                IsCompatibleWithModifiers(member, modifiers));
    }

    private Exercise GetSequenceRoot(Exercise exercise)
    {
        return _sequenceRootByExerciseId.TryGetValue(
                exercise.Id,
                out Exercise? root)
            ? root
            : throw new InvalidOperationException(
                $"Exercise {exercise.Id} has no sequence root.");
    }

    private Exercise[] GetSequenceExercises(Exercise exercise)
    {
        Exercise root = GetSequenceRoot(exercise);
        return root.SequenceBlocks
            .Select(block => _exercisesById[block.ExerciseId])
            .DistinctBy(member => member.Id)
            .ToArray();
    }

    private Exercise GetSequenceSelectionExerciseForGroup(
        Exercise exercise,
        WorkoutGroup group)
    {
        Exercise root = GetSequenceRoot(exercise);
        Exercise[] primaryMembers = root.SequenceBlocks
            .Select(block => _exercisesById[block.ExerciseId])
            .Where(member => group.CanonicalGroups.Contains(
                member.PrimaryCanonicalGroup))
            .ToArray();
        return primaryMembers
            .OrderByDescending(member => member.MuscularDemand)
            .ThenByDescending(member => member.Id == root.Id)
            .FirstOrDefault() ??
            root;
    }

    private WorkoutGroup[][] GetSequencePlacementOptions(
        Exercise exercise,
        IReadOnlyList<WorkoutGroup> groups)
    {
        Exercise root = GetSequenceRoot(exercise);
        return WorkoutSequencePolicy.GetPlacementOptions(
            root,
            _exercisesById,
            groups);
    }

    private int GetSelectionScore(Exercise exercise)
    {
        return GetSequenceExercises(exercise)
            .Min(member => member.Score);
    }

    private long GetCurrentUnixTimeMilliseconds() =>
        _utcNowProvider().ToUnixTimeMilliseconds();

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

        return string.Equals(
                pendingRoundId,
                selectionGroupId,
                StringComparison.Ordinal) ||
            pendingRoundId.StartsWith(
                selectionGroupId + ".",
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
        state.LastHardWorkUnixMillisecondsByPrimaryMuscle ??= [];
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle ??= [];
        state.NextWorkoutExcludedExerciseIds ??= [];
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
            if (state.ActiveDirectionPartnerExerciseIds.Count != 0 ||
                state.ActiveFullSideRoundIds.Count != 0 ||
                state.ActiveExtraSetSelectionGroupIds.Count != 0 ||
                state.ActiveSetCountsBySelectionGroupId.Count != 0)
            {
                return false;
            }
            try
            {
                LongWorkoutAllocation allocation =
                    ChooseLongWorkoutAllocation(state);
                return CreateWorkoutSchedule(
                    state,
                    allocation.SetCountsBySelectionGroupId).Count ==
                        state.ActiveWorkoutMinutes;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        LongWorkoutAllocation expected = ChooseLongWorkoutAllocation(state);
        if (state.ActiveDirectionPartnerExerciseIds.Count != 0 ||
            state.ActiveFullSideRoundIds.Count != 0)
        {
            return false;
        }

        SelectedSequencePlacement[] placements =
            GetSelectedSequencePlacements(state);
        if (state.ActiveSetCountsBySelectionGroupId.Count !=
                placements.Length ||
            placements.Any(placement =>
                state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(
                    placement.Anchor.Id) < 1))
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
                state,
                state.ActiveSetCountsBySelectionGroupId);
            return rounds.Count == state.ActiveWorkoutMinutes &&
                state.ActiveSetCountsBySelectionGroupId.Count ==
                    expected.SetCountsBySelectionGroupId.Count &&
                state.ActiveSetCountsBySelectionGroupId.All(entry =>
                    expected.SetCountsBySelectionGroupId.GetValueOrDefault(
                        entry.Key) == entry.Value);
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
        RemovePartialSequenceIds(state.LastKeptExerciseIds);
        NormalizeWorkHistory(state);
        state.NextWorkoutExcludedExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
        ExpandSequenceIds(state.NextWorkoutExcludedExerciseIds);
    }

    private void RemovePartialSequenceIds(HashSet<int> exerciseIds)
    {
        HashSet<int> completeSequenceExerciseIds = exerciseIds
            .Select(exerciseId => _exercisesById[exerciseId])
            .Select(GetSequenceRoot)
            .DistinctBy(root => root.Id)
            .Where(root => GetSequenceExercises(root)
                .All(member => exerciseIds.Contains(member.Id)))
            .SelectMany(GetSequenceExercises)
            .Select(member => member.Id)
            .ToHashSet();
        exerciseIds.IntersectWith(completeSequenceExerciseIds);
    }

    private bool IsSequenceKept(WorkoutState state, Exercise exercise) =>
        GetSequenceExercises(exercise)
            .All(member => state.LastKeptExerciseIds.Contains(member.Id));

    private static void NormalizeWorkHistory(WorkoutState state)
    {
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
            NormalizeWorkHistory(
                state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
        state.LastHardWorkUnixMillisecondsByPrimaryMuscle =
            NormalizeWorkHistory(
                state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
    }

    private static Dictionary<string, long> NormalizeWorkHistory(
        IReadOnlyDictionary<string, long> history)
    {
        var normalized = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach ((string muscleKey, long completedAtUnixMilliseconds) in
                 history)
        {
            if (completedAtUnixMilliseconds <= 0 ||
                !Enum.TryParse(
                    muscleKey,
                    ignoreCase: true,
                    out CanonicalMuscleGroup primaryMuscle) ||
                !Enum.IsDefined(primaryMuscle))
            {
                continue;
            }

            string canonicalKey = primaryMuscle.ToString();
            normalized[canonicalKey] = Math.Max(
                completedAtUnixMilliseconds,
                normalized.GetValueOrDefault(canonicalKey));
        }

        return normalized;
    }

    private void ExpandSequenceIds(HashSet<int> exerciseIds)
    {
        foreach (int exerciseId in exerciseIds.ToArray())
        {
            if (_exercisesById.TryGetValue(exerciseId, out Exercise? exercise))
            {
                exerciseIds.UnionWith(GetSequenceExercises(exercise)
                    .Select(member => member.Id));
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
            if (IsIntermediateSequenceBlock(state, group))
            {
                continue;
            }

            state.Outcomes[groupId] = ExerciseOutcome.Tick;
        }
    }

    private void NormalizePendingRest(WorkoutState state)
    {
        if (state.PendingRestGroupId is string pendingGroupId &&
            HasValidPendingRestTiming(state) &&
            KnownWorkoutGroups.TryGetValue(
                pendingGroupId,
                out WorkoutGroup? pendingBaseGroup) &&
            state.SelectedExerciseIds.TryGetValue(
                GetSelectionStorageKey(
                    pendingBaseGroup.Id,
                    state.ActiveWorkoutModifiers),
                out int pendingRootId) &&
            _exercisesById.TryGetValue(pendingRootId, out Exercise? pendingRoot) &&
            IsSavedSelectionValid(
                state,
                pendingRoot,
                pendingBaseGroup,
                state.ActiveWorkoutModifiers))
        {
            return;
        }

        try
        {
            if (GetValidPendingRestGroup(state) is not null)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // An obsolete schedule may be impossible to construct until its
            // lineup is repaired. Such a checkpoint cannot identify a valid
            // current block in the new schedule.
        }

        ClearPendingRest(state);
    }

    private static LegacyActiveProgressSnapshot CaptureLegacyActiveProgress(
        WorkoutState state) =>
        new(
            new Dictionary<string, ExerciseOutcome>(
                state.Outcomes,
                StringComparer.Ordinal),
            new Dictionary<string, int>(
                state.SelectedExerciseIds,
                StringComparer.Ordinal),
            new Dictionary<string, int>(
                state.ActiveDirectionPartnerExerciseIds,
                StringComparer.Ordinal),
            new HashSet<string>(
                state.ActiveFullSideRoundIds,
                StringComparer.Ordinal),
            state.PendingMovementGroupId,
            state.PendingMovementMillisecondsRemaining,
            state.PendingMovementEndsAtUnixMilliseconds,
            state.PendingMovementPausedByUser,
            state.PendingRestGroupId,
            state.PendingRestEndsAtUnixMilliseconds,
            state.PendingRestKept);

    private void MigrateLegacyActiveProgress(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot)
    {
        WorkoutGroup[] rounds = GetActiveGroups(state)
            .OrderBy(round => round.Order)
            .ToArray();
        state.Outcomes.Clear();
        ClearPendingMovement(state);
        ClearPendingRest(state);

        foreach (IGrouping<string, WorkoutGroup> sequence in rounds.GroupBy(
                     round => round.SelectionKey,
                     StringComparer.Ordinal))
        {
            if (!LegacySelectionMatchesCurrentSequence(
                    state,
                    snapshot,
                    sequence.Key))
            {
                continue;
            }

            KeyValuePair<string, ExerciseOutcome>[] oldOutcomes = snapshot.Outcomes
                .Where(entry => ResolveLegacySelectionKey(entry.Key) == sequence.Key)
                .ToArray();
            WorkoutGroup[] sequenceRounds = sequence
                .OrderBy(round => round.Order)
                .ToArray();
            ExerciseOutcome? decision = oldOutcomes
                .Where(entry => entry.Value is ExerciseOutcome.Tick or ExerciseOutcome.X)
                .Select(entry => (ExerciseOutcome?)entry.Value)
                .LastOrDefault();
            if (decision is not null)
            {
                foreach (WorkoutGroup round in sequenceRounds[..^1])
                {
                    state.Outcomes[round.Id] = ExerciseOutcome.Neutral;
                }
                state.Outcomes[sequenceRounds[^1].Id] = decision.Value;
                continue;
            }

            foreach (KeyValuePair<string, ExerciseOutcome> completed in oldOutcomes
                         .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                         .OrderBy(entry => GetLegacyRoundOrdinal(entry.Key)))
            {
                foreach (WorkoutGroup round in ResolveLegacyRepresentedRounds(
                             state,
                             snapshot,
                             sequenceRounds,
                             completed.Key))
                {
                    if (!round.IsFinalSequenceRound)
                    {
                        state.Outcomes[round.Id] = ExerciseOutcome.Neutral;
                    }
                }
            }
        }

        string? legacyPendingRoundId =
            snapshot.PendingRestGroupId ?? snapshot.PendingMovementGroupId;
        string? pendingSelectionKey = ResolveLegacySelectionKey(legacyPendingRoundId);
        if (legacyPendingRoundId is null || pendingSelectionKey is null)
        {
            return;
        }
        if (!LegacySelectionMatchesCurrentSequence(
                state,
                snapshot,
                pendingSelectionKey))
        {
            return;
        }

        WorkoutGroup[] pendingSequenceRounds = rounds
            .Where(round => round.SelectionKey == pendingSelectionKey)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup[] representedRounds = ResolveLegacyRepresentedRounds(
            state,
            snapshot,
            pendingSequenceRounds,
            legacyPendingRoundId);
        if (representedRounds.Length == 0)
        {
            return;
        }

        if (snapshot.PendingRestGroupId is not null &&
            snapshot.PendingRestEndsAtUnixMilliseconds > 0)
        {
            WorkoutGroup pendingRestRound = representedRounds[^1];
            MarkSequenceRoundsBeforePending(
                state,
                pendingSequenceRounds,
                pendingRestRound);
            state.PendingRestGroupId = pendingRestRound.Id;
            state.PendingRestEndsAtUnixMilliseconds =
                snapshot.PendingRestEndsAtUnixMilliseconds;
            state.PendingRestKept = snapshot.PendingRestKept &&
                pendingRestRound.IsFinalSequenceRound;
            return;
        }

        if (snapshot.PendingMovementGroupId is null ||
            snapshot.PendingMovementMillisecondsRemaining <= 0)
        {
            return;
        }

        (int representedRoundIndex, long remainingMilliseconds) =
            MapLegacyMovementProgress(
                state,
                snapshot,
                legacyPendingRoundId,
                representedRounds.Length);
        WorkoutGroup pendingRound = representedRounds[Math.Clamp(
            representedRoundIndex,
            0,
            representedRounds.Length - 1)];
        MarkSequenceRoundsBeforePending(
            state,
            pendingSequenceRounds,
            pendingRound);
        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(
                state,
                pendingRound)) * 1_000L;
        state.PendingMovementGroupId = pendingRound.Id;
        state.PendingMovementMillisecondsRemaining = Math.Clamp(
            remainingMilliseconds,
            1L,
            maximum);
        state.PendingMovementEndsAtUnixMilliseconds =
            representedRounds.Length == 1 &&
                remainingMilliseconds ==
                    snapshot.PendingMovementMillisecondsRemaining
                ? snapshot.PendingMovementEndsAtUnixMilliseconds
                : 0;
        state.PendingMovementPausedByUser =
            snapshot.PendingMovementPausedByUser;
    }

    private bool LegacySelectionMatchesCurrentSequence(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        string selectionKey)
    {
        int legacyExerciseId = ResolveLegacyMemberExerciseId(
            state,
            snapshot,
            selectionKey,
            selectionKey);
        int currentExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
            GetSelectionStorageKey(selectionKey, state.ActiveWorkoutModifiers));
        return legacyExerciseId > 0 &&
            currentExerciseId > 0 &&
            _exercisesById.TryGetValue(legacyExerciseId, out Exercise? legacyExercise) &&
            _exercisesById.TryGetValue(currentExerciseId, out Exercise? currentExercise) &&
            GetSequenceRoot(legacyExercise).Id == GetSequenceRoot(currentExercise).Id;
    }

    private WorkoutGroup[] ResolveLegacyRepresentedRounds(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        IReadOnlyList<WorkoutGroup> sequenceRounds,
        string legacyRoundId)
    {
        string? selectionKey = ResolveLegacySelectionKey(legacyRoundId);
        if (selectionKey is null)
        {
            return [];
        }

        int setNumber = GetLegacySetNumber(legacyRoundId);
        WorkoutGroup[] setRounds = sequenceRounds
            .Where(round => round.SetNumber == setNumber)
            .OrderBy(round => round.SequenceBlockIndex)
            .ToArray();
        if (setRounds.Length == 0)
        {
            return [];
        }

        int memberExerciseId = ResolveLegacyMemberExerciseId(
            state,
            snapshot,
            selectionKey,
            legacyRoundId);
        WorkoutGroup[] represented = memberExerciseId > 0
            ? setRounds
                .Where(round => round.ExerciseOverrideId == memberExerciseId ||
                    round.ExerciseOverrideId == 0 &&
                    GetSelectedExercise(state, round).Id == memberExerciseId)
                .ToArray()
            : [];
        return represented.Length > 0 ? represented : setRounds;
    }

    private int ResolveLegacyMemberExerciseId(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        string selectionKey,
        string legacyRoundId)
    {
        if (legacyRoundId.StartsWith(
                selectionKey + ".direction",
                StringComparison.Ordinal) &&
            snapshot.DirectionPartnerExerciseIds.TryGetValue(
                selectionKey,
                out int partnerExerciseId))
        {
            return partnerExerciseId;
        }

        string activeStorageKey = GetSelectionStorageKey(
            selectionKey,
            state.ActiveWorkoutModifiers);
        if (snapshot.SelectedExerciseIds.TryGetValue(
                activeStorageKey,
                out int selectedExerciseId))
        {
            return selectedExerciseId;
        }

        return snapshot.SelectedExerciseIds
            .Where(entry =>
            {
                if (!TryParseSelectionStorageKey(
                        entry.Key,
                        out string storedGroupId,
                        out WorkoutModifiers modifiers))
                {
                    return false;
                }
                return storedGroupId == selectionKey &&
                    WorkoutModifierPolicy.Normalize(modifiers) ==
                        state.ActiveWorkoutModifiers;
            })
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }

    private (int RepresentedRoundIndex, long RemainingMilliseconds)
        MapLegacyMovementProgress(
            WorkoutState state,
            LegacyActiveProgressSnapshot snapshot,
            string legacyRoundId,
            int representedRoundCount)
    {
        long remaining = snapshot.PendingMovementMillisecondsRemaining;
        if (representedRoundCount < 2)
        {
            return (0, remaining);
        }

        string? selectionKey = ResolveLegacySelectionKey(legacyRoundId);
        int memberExerciseId = selectionKey is null
            ? 0
            : ResolveLegacyMemberExerciseId(
                state,
                snapshot,
                selectionKey,
                legacyRoundId);
        bool usedTimedPair = memberExerciseId > 0 &&
            _exercisesById.TryGetValue(memberExerciseId, out Exercise? member) &&
            (member.SideSequence.UsesTimedSides() ||
                member.DirectionSequence != ExerciseDirectionSequence.None);
        if (!usedTimedPair)
        {
            return (0, remaining);
        }

        if (snapshot.FullSideRoundIds.Contains(legacyRoundId))
        {
            return remaining switch
            {
                > 60_000L => (0, remaining - 60_000L),
                > 45_000L => (1, 45_000L),
                _ => (1, remaining),
            };
        }

        return remaining switch
        {
            > 25_000L => (0, remaining),
            > 20_000L => (1, 45_000L),
            _ => (1, remaining + 25_000L),
        };
    }

    private static void MarkSequenceRoundsBeforePending(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> sequenceRounds,
        WorkoutGroup pendingRound)
    {
        foreach (WorkoutGroup round in sequenceRounds.TakeWhile(round =>
                     round.Id != pendingRound.Id))
        {
            state.Outcomes.TryAdd(round.Id, ExerciseOutcome.Neutral);
        }
    }

    private static int GetLegacySetNumber(string roundId)
    {
        foreach (string marker in new[] { ".set", ".direction" })
        {
            int index = roundId.LastIndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 &&
                int.TryParse(roundId.AsSpan(index + marker.Length), out int value) &&
                value > 0)
            {
                return value;
            }
        }

        return 1;
    }

    private static int GetLegacyRoundOrdinal(string roundId)
    {
        int setNumber = GetLegacySetNumber(roundId);
        int directionOffset = roundId.Contains(
            ".direction",
            StringComparison.Ordinal)
            ? 1
            : 0;
        return (setNumber - 1) * 2 + directionOffset;
    }

    private static string? ResolveLegacySelectionKey(string? roundId)
    {
        if (string.IsNullOrWhiteSpace(roundId))
        {
            return null;
        }

        return KnownWorkoutGroups.Keys
            .Where(groupId =>
                string.Equals(roundId, groupId, StringComparison.Ordinal) ||
                roundId.StartsWith(groupId + ".", StringComparison.Ordinal))
            .OrderByDescending(groupId => groupId.Length)
            .FirstOrDefault();
    }

    private void NormalizePendingMovement(WorkoutState state)
    {
        if (GetValidPendingMovementGroup(state) is null)
        {
            ClearPendingMovement(state);
            return;
        }

        // Movement and rest are mutually exclusive persisted phases. A valid
        // rest means movement already completed, so rest takes precedence.
        if (GetValidPendingRestGroup(state) is not null)
        {
            ClearPendingMovement(state);
        }
    }

    private WorkoutGroup? GetValidPendingMovementGroup(WorkoutState state)
    {
        if (state.PendingMovementGroupId is not string pendingGroupId ||
            state.PendingMovementMillisecondsRemaining <= 0 ||
            state.Outcomes.ContainsKey(pendingGroupId))
        {
            return null;
        }

        WorkoutGroup? pendingGroup = GetActiveGroups(state)
            .SingleOrDefault(group => group.Id == pendingGroupId);
        if (pendingGroup is null || GetNextGroup(state)?.Id != pendingGroup.Id)
        {
            return null;
        }

        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, pendingGroup)) *
            1_000L;
        if (state.PendingMovementMillisecondsRemaining > maximum ||
            state.PendingMovementEndsAtUnixMilliseconds < 0)
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

    private void ValidatePendingMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        long endsAtUnixMilliseconds,
        bool allowPausedDeadline)
    {
        if (GetNextGroup(state)?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }

        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, group)) * 1_000L;
        if (millisecondsRemaining <= 0 || millisecondsRemaining > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsRemaining));
        }
        if ((!allowPausedDeadline && endsAtUnixMilliseconds <= 0) ||
            (allowPausedDeadline && endsAtUnixMilliseconds != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(endsAtUnixMilliseconds));
        }

        _ = GetSelectedExercise(state, group);
    }

    private WorkoutGroup? GetValidPendingRestGroup(WorkoutState state)
    {
        if (state.PendingRestGroupId is not string pendingGroupId ||
            !HasValidPendingRestTiming(state) ||
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

    private static bool HasValidPendingRestTiming(WorkoutState state) =>
        state.PendingRestPausedByUser
            ? state.PendingRestEndsAtUnixMilliseconds == 0 &&
                state.PendingRestMillisecondsRemaining > 0 &&
                state.PendingRestMillisecondsRemaining <= RestDurationMilliseconds
            : state.PendingRestEndsAtUnixMilliseconds > 0 &&
                state.PendingRestMillisecondsRemaining == 0;

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
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        state.PendingMovementGroupId = null;
        state.PendingMovementMillisecondsRemaining = 0;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = false;
        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
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

    private SelectedSequencePlacement[] GetSelectedSequencePlacements(
        WorkoutState state)
    {
        IReadOnlyList<WorkoutGroup> selectionGroups = GetSelectionGroups(state);
        var selectedGroupsByRootId = new Dictionary<int, List<WorkoutGroup>>();
        foreach (WorkoutGroup group in selectionGroups)
        {
            int rootId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers));
            if (!_exercisesById.TryGetValue(rootId, out Exercise? root) ||
                root.SequenceBlocks.Length == 0 ||
                GetSequenceRoot(root).Id != root.Id)
            {
                throw new InvalidOperationException(
                    $"{group.DisplayName} has no selected sequence.");
            }
            if (!selectedGroupsByRootId.TryGetValue(
                    root.Id,
                    out List<WorkoutGroup>? selectedGroups))
            {
                selectedGroups = [];
                selectedGroupsByRootId[root.Id] = selectedGroups;
            }
            selectedGroups.Add(group);
        }

        var placements = new List<SelectedSequencePlacement>();
        var movementIds = new HashSet<int>();
        foreach ((int rootId, List<WorkoutGroup> selectedGroups) in
                 selectedGroupsByRootId)
        {
            Exercise root = _exercisesById[rootId];
            WorkoutGroup[]? coveredGroups = GetSequencePlacementOptions(
                    root,
                    selectionGroups)
                .SingleOrDefault(option => option.Select(group => group.Id)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(selectedGroups.Select(group => group.Id)));
            if (coveredGroups is null &&
                selectedGroups.Count == 1 &&
                (PendingRestMatchesSelectionGroup(
                     state,
                     selectedGroups[0].Id) ||
                 state.Outcomes.ContainsKey(selectedGroups[0].Id)) &&
                GetSequenceExercises(root).All(member =>
                    IsCompatibleWithModifiers(
                        member,
                        state.ActiveWorkoutModifiers) &&
                    IsAssignedToGroup(member, selectedGroups[0])))
            {
                coveredGroups = [selectedGroups[0]];
            }
            if (coveredGroups is null ||
                !movementIds.Add(WorkoutModifierPolicy.GetSessionMovementId(root)))
            {
                throw new InvalidOperationException(
                    "The selected atomic sequence placements do not match " +
                    "their primary-muscle workout slots.");
            }

            placements.Add(new SelectedSequencePlacement(
                root,
                coveredGroups.OrderBy(group => group.Order).First(),
                coveredGroups));
        }

        return placements
            .OrderBy(placement => placement.Anchor.Order)
            .ToArray();
    }

    private LongWorkoutAllocation ChooseLongWorkoutAllocation(
        WorkoutState state,
        IReadOnlySet<string>? lockedSelectionGroupIds = null,
        IReadOnlyList<SelectedSequencePlacement>? selectedPlacements = null)
    {
        lockedSelectionGroupIds ??= new HashSet<string>(StringComparer.Ordinal);
        SelectedSequencePlacement[] rankedPlacements =
            (selectedPlacements ?? GetSelectedSequencePlacements(state))
            .OrderByDescending(placement =>
                GetSequenceExercises(placement.Root)
                    .Any(WorkoutRecoveryPolicy.IsHardExercise))
            .ThenByDescending(placement => IsSequenceKept(state, placement.Root))
            .ThenByDescending(placement => placement.Anchor.Order)
            .ToArray();
        Dictionary<string, int> blockCostByGroup = rankedPlacements.ToDictionary(
            placement => placement.Anchor.Id,
            placement => placement.Root.SequenceBlocks.Length,
            StringComparer.Ordinal);
        Dictionary<string, int> setCounts = rankedPlacements.ToDictionary(
            placement => placement.Anchor.Id,
            _ => 1,
            StringComparer.Ordinal);
        int remainingMinutes = state.ActiveWorkoutMinutes -
            blockCostByGroup.Values.Sum();

        foreach (SelectedSequencePlacement placement in rankedPlacements.Where(
                     placement => lockedSelectionGroupIds.Contains(
                         placement.Anchor.Id)))
        {
            int lockedSetCount = state.ActiveSetCountsBySelectionGroupId
                .GetValueOrDefault(placement.Anchor.Id, 1);
            if (lockedSetCount < 1)
            {
                throw new InvalidOperationException(
                    $"The completed set allocation for " +
                    $"{placement.Anchor.DisplayName} is invalid.");
            }
            setCounts[placement.Anchor.Id] = lockedSetCount;
            remainingMinutes -=
                (lockedSetCount - 1) * blockCostByGroup[placement.Anchor.Id];
        }
        if (remainingMinutes < 0)
        {
            throw new InvalidOperationException(
                "The selected mandatory sequences exceed the workout duration.");
        }

        SelectedSequencePlacement[] repeatablePlacements = rankedPlacements
            .Where(placement => !lockedSelectionGroupIds.Contains(
                placement.Anchor.Id))
            .ToArray();
        int[] repeatableCosts = repeatablePlacements
            .Select(placement => blockCostByGroup[placement.Anchor.Id])
            .Distinct()
            .ToArray();
        bool CanFill(int minutes)
        {
            var fillable = new bool[minutes + 1];
            fillable[0] = true;
            for (int value = 1; value <= minutes; value++)
            {
                fillable[value] = repeatableCosts.Any(cost =>
                    cost <= value && fillable[value - cost]);
            }
            return fillable[minutes];
        }

        while (remainingMinutes > 0)
        {
            SelectedSequencePlacement? selectedPlacement = repeatablePlacements
                .OrderBy(placement => setCounts[placement.Anchor.Id])
                .FirstOrDefault(placement =>
                {
                    int cost = blockCostByGroup[placement.Anchor.Id];
                    return cost <= remainingMinutes &&
                        CanFill(remainingMinutes - cost);
                });
            if (selectedPlacement is null)
            {
                throw new InvalidOperationException(
                    "The selected sequence lengths cannot fill the workout duration.");
            }

            setCounts[selectedPlacement.Anchor.Id]++;
            remainingMinutes -= blockCostByGroup[selectedPlacement.Anchor.Id];
        }

        HashSet<string> extraSetGroups = setCounts
            .Where(entry => entry.Value > 1)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        return new LongWorkoutAllocation(extraSetGroups, setCounts);
    }

    private void RebalanceNewExercisesByMuscleBudget(WorkoutState state)
    {
        WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
        if (groups.Length == 0)
        {
            return;
        }
        long selectionTimeUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        var allocationCache = new Dictionary<string, LongWorkoutAllocation?>(
            StringComparer.Ordinal);

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
                    IsSequenceKept(state, currentExercise) ||
                    groups.Count(candidateGroup =>
                        state.SelectedExerciseIds.GetValueOrDefault(
                            GetSelectionStorageKey(
                                candidateGroup.Id,
                                state.ActiveWorkoutModifiers)) ==
                            currentExerciseId) > 1)
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
                HashSet<int> unavailableMovementIds = unavailableExerciseIds
                    .Where(_exercisesById.ContainsKey)
                    .Select(exerciseId =>
                        WorkoutModifierPolicy.GetSessionMovementId(
                            _exercisesById[exerciseId]))
                    .Append(WorkoutModifierPolicy.GetSessionMovementId(
                        currentExercise))
                    .ToHashSet();
                SelectedSequencePlacement[] placementsWithoutCurrent =
                    GetSelectedSequencePlacements(state)
                        .Where(placement => placement.Anchor.Id != group.Id)
                        .ToArray();
                MuscleBudgetCandidate current = EvaluateMuscleBudgetCandidate(
                    state,
                    group,
                    currentExercise,
                    placementsWithoutCurrent,
                    allocationCache,
                    selectionTimeUnixMilliseconds);
                MuscleBudgetCandidate? bestAlternative = _exercises
                    .Where(exercise =>
                        exercise.Id != currentExerciseId &&
                        WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                            GetSelectionScore(exercise),
                            temporaryDownvoteHalfUnits: 0) >
                            current.AdjustedScoreHalfUnits &&
                        !unavailableExerciseIds.Contains(exercise.Id) &&
                        !unavailableMovementIds.Contains(
                            WorkoutModifierPolicy.GetSessionMovementId(exercise)) &&
                        !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                        GetSequencePlacementOptions(exercise, groups).Any(option =>
                            option.Length == 1 && option[0].Id == group.Id) &&
                        IsWorkoutSelectionCandidate(
                            state,
                            exercise,
                            group,
                            state.ActiveWorkoutModifiers))
                    .Select(exercise => EvaluateMuscleBudgetCandidate(
                            state,
                            group,
                            exercise,
                            placementsWithoutCurrent,
                            allocationCache,
                            selectionTimeUnixMilliseconds))
                    .OrderByDescending(candidate => candidate.AdjustedScoreHalfUnits)
                    .ThenByDescending(candidate => candidate.RealScore)
                    .ThenByDescending(candidate => candidate.IsFreshHard)
                    .ThenBy(candidate => candidate.IsRecoveringHard)
                    .ThenBy(candidate => candidate.IsRecoveringModerate)
                    .ThenByDescending(candidate => candidate.IsKept)
                    .ThenBy(candidate => candidate.LastHardWorkUnixMilliseconds)
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
            exercise.SequenceBlocks.Length == 0 ||
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups =
            GetResolutionGroupsForGroup(group);
        WorkoutGroup[][] placementOptions = GetSequencePlacementOptions(
            exercise,
            resolutionGroups);
        return placementOptions.Any(option => option.Any(candidate =>
                candidate.CanonicalGroups.SetEquals(group.CanonicalGroups))) &&
            GetSequenceExercises(exercise).All(member =>
                IsCompatibleWithModifiers(member, modifiers));
    }

    private static IReadOnlyList<WorkoutGroup> GetResolutionGroupsForGroup(
        WorkoutGroup group) =>
        MassGroupingTaxonomy.SupportedMinutes
            .Select(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Single(groups => groups.Any(candidate => candidate.Id == group.Id));

    private MuscleBudgetCandidate EvaluateMuscleBudgetCandidate(
        WorkoutState state,
        WorkoutGroup group,
        Exercise candidate,
        IReadOnlyList<SelectedSequencePlacement> placementsWithoutCandidate,
        IDictionary<string, LongWorkoutAllocation?> allocationCache,
        long selectionTimeUnixMilliseconds)
    {
        try
        {
            SelectedSequencePlacement[] placements = placementsWithoutCandidate
                .Append(new SelectedSequencePlacement(candidate, group, [group]))
                .OrderBy(placement => placement.Anchor.Order)
                .ToArray();
            LongWorkoutAllocation allocation = GetCachedLongWorkoutAllocation(
                state,
                placements,
                allocationCache);
            IReadOnlyDictionary<CanonicalMuscleGroup, int> loadHalfUnits =
                CalculateScheduledLoadHalfUnits(placements, allocation);
            CanonicalMuscleGroup[] candidateMuscleGroups =
                GetSequenceExercises(candidate)
                .SelectMany(exercise => exercise.SecondaryCanonicalGroups
                    .Append(exercise.PrimaryCanonicalGroup))
                .Distinct()
                .ToArray();
            int temporaryDownvoteHalfUnits =
                WorkoutMuscleBudgetPolicy.GetTemporaryDownvoteHalfUnits(
                    loadHalfUnits,
                    candidateMuscleGroups);
            int selectionScore = GetSelectionScore(candidate);
            Exercise selectionExercise =
                GetSequenceSelectionExerciseForGroup(candidate, group);
            HardExerciseRotationStatus rotationStatus =
                WorkoutRecoveryPolicy.GetRotationStatus(
                    selectionExercise,
                    group,
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    selectionTimeUnixMilliseconds);
            bool isRecoveringModerate =
                WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
                    selectionExercise,
                    state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
                    selectionTimeUnixMilliseconds);
            return new MuscleBudgetCandidate(
                candidate.Id,
                selectionScore,
                WorkoutMuscleBudgetPolicy.GetAdjustedScoreHalfUnits(
                    selectionScore,
                    temporaryDownvoteHalfUnits),
                rotationStatus ==
                    HardExerciseRotationStatus.FreshHard,
                rotationStatus ==
                    HardExerciseRotationStatus.RecoveringHard,
                isRecoveringModerate,
                IsSequenceKept(state, candidate),
                rotationStatus == HardExerciseRotationStatus.FreshHard
                    ? WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                        state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                        selectionExercise.PrimaryCanonicalGroup)
                    : 0L,
                WorkoutModifierPolicy.IsMirrorPreferred(
                    selectionExercise,
                    state.ActiveWorkoutModifiers),
                WorkoutCoveragePolicy.IsPrimaryForGroup(selectionExercise, group),
                WorkoutSequencePolicy.GetCanonicalCoverage(
                    candidate,
                    _exercisesById,
                    group));
        }
        catch (InvalidOperationException)
        {
            return new MuscleBudgetCandidate(
                candidate.Id,
                GetSelectionScore(candidate),
                long.MinValue,
                IsFreshHard: false,
                IsRecoveringHard: false,
                IsRecoveringModerate: false,
                IsKept: false,
                LastHardWorkUnixMilliseconds: 0L,
                IsMirrorPreferred: false,
                IsPrimary: false,
                CanonicalCoverage: 0);
        }
    }

    private LongWorkoutAllocation GetCachedLongWorkoutAllocation(
        WorkoutState state,
        IReadOnlyList<SelectedSequencePlacement> placements,
        IDictionary<string, LongWorkoutAllocation?> allocationCache)
    {
        string signature = string.Join(
            '|',
            placements
                .OrderBy(placement => placement.Anchor.Order)
                .Select(placement =>
                    $"{placement.Anchor.Id}:" +
                    $"{placement.Root.SequenceBlocks.Length}:" +
                    $"{GetSequenceExercises(placement.Root).Any(WorkoutRecoveryPolicy.IsHardExercise)}:" +
                    IsSequenceKept(state, placement.Root)));
        if (allocationCache.TryGetValue(
                signature,
                out LongWorkoutAllocation? cached))
        {
            return cached ?? throw new InvalidOperationException(
                "The selected sequence lengths cannot fill the workout duration.");
        }

        try
        {
            LongWorkoutAllocation allocation = ChooseLongWorkoutAllocation(
                state,
                selectedPlacements: placements);
            allocationCache[signature] = allocation;
            return allocation;
        }
        catch (InvalidOperationException)
        {
            allocationCache[signature] = null;
            throw;
        }
    }

    private IReadOnlyDictionary<CanonicalMuscleGroup, int>
        CalculateScheduledLoadHalfUnits(
            IReadOnlyList<SelectedSequencePlacement> placements,
            LongWorkoutAllocation allocation)
    {
        var loadHalfUnits = new Dictionary<CanonicalMuscleGroup, int>();
        foreach (SelectedSequencePlacement placement in
                 placements)
        {
            int setCount = allocation.SetCountsBySelectionGroupId
                .GetValueOrDefault(placement.Anchor.Id, 1);
            foreach (Exercise exercise in GetSequenceExercises(placement.Root))
            {
                loadHalfUnits[exercise.PrimaryCanonicalGroup] =
                    loadHalfUnits.GetValueOrDefault(exercise.PrimaryCanonicalGroup) +
                    WorkoutMuscleBudgetPolicy.PrimaryLoadHalfUnits * setCount;
                foreach (CanonicalMuscleGroup secondary in
                         exercise.SecondaryCanonicalGroups.Distinct())
                {
                    loadHalfUnits[secondary] =
                        loadHalfUnits.GetValueOrDefault(secondary) +
                        WorkoutMuscleBudgetPolicy.SecondaryLoadHalfUnits * setCount;
                }
            }
        }

        return loadHalfUnits;
    }

    private void SetActiveLongWorkoutAllocation(WorkoutState state) =>
        ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state));

    private static void ApplyLongWorkoutAllocation(
        WorkoutState state,
        LongWorkoutAllocation allocation)
    {
        if (state.ActiveWorkoutMinutes <= 30)
        {
            state.ActiveExtraSetSelectionGroupIds.Clear();
            state.ActiveSetCountsBySelectionGroupId.Clear();
            state.ActiveDirectionPartnerExerciseIds.Clear();
            state.ActiveFullSideRoundIds.Clear();
            return;
        }

        state.ActiveExtraSetSelectionGroupIds =
            new HashSet<string>(allocation.ExtraSetSelectionGroupIds, StringComparer.Ordinal);
        state.ActiveSetCountsBySelectionGroupId = new Dictionary<string, int>(
            allocation.SetCountsBySelectionGroupId,
            StringComparer.Ordinal);
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
    }

    private IReadOnlyDictionary<string, int> GetEffectiveSetCounts(
        WorkoutState state)
    {
        if (state.ActiveWorkoutMinutes <= 30)
        {
            return ChooseLongWorkoutAllocation(state)
                .SetCountsBySelectionGroupId;
        }
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveSetCountsBySelectionGroupId
            : ChooseLongWorkoutAllocation(state).SetCountsBySelectionGroupId;
    }

    private IReadOnlyList<WorkoutGroup> CreateWorkoutSchedule(
        WorkoutState state,
        IReadOnlyDictionary<string, int> setCountsBySelectionGroupId)
    {
        var rounds = new List<WorkoutGroup>(state.ActiveWorkoutMinutes);
        foreach (SelectedSequencePlacement placement in
                 GetSelectedSequencePlacements(state))
        {
            int setCount = Math.Max(
                1,
                setCountsBySelectionGroupId.GetValueOrDefault(
                    placement.Anchor.Id,
                    1));
            for (int setNumber = 1; setNumber <= setCount; setNumber++)
            {
                for (int blockIndex = 0;
                     blockIndex < placement.Root.SequenceBlocks.Length;
                     blockIndex++)
                {
                    ExerciseSequenceBlock block =
                        placement.Root.SequenceBlocks[blockIndex];
                    Exercise blockExercise = _exercisesById[block.ExerciseId];
                    WorkoutGroup blockGroup = placement.CoveredGroups.Count == 1
                        ? placement.Anchor
                        : placement.CoveredGroups.Single(group =>
                            group.CanonicalGroups.Contains(
                                blockExercise.PrimaryCanonicalGroup));
                    if (state.ActiveWorkoutMinutes <= 30 &&
                        placement.Root.SequenceBlocks.Length == 1 &&
                        setCount == 1)
                    {
                        rounds.Add(blockGroup with { Order = rounds.Count + 1 });
                        continue;
                    }

                    rounds.Add(blockGroup with
                    {
                        Id = $"{placement.Anchor.Id}.set{setNumber}." +
                            $"block{blockIndex + 1}",
                        Order = rounds.Count + 1,
                        SelectionGroupId = placement.Anchor.Id,
                        ExerciseOverrideId = block.ExerciseId,
                        SequenceBlockIndex = blockIndex,
                        SequenceBlockCount = placement.Root.SequenceBlocks.Length,
                        SetNumber = setNumber,
                        SetCount = setCount,
                        SequenceSideCue = block.SideCue,
                        SequenceDirectionCue = block.DirectionCue,
                        MirrorSequenceMedia = block.MirrorMedia,
                        SequenceMediaSegment = block.MediaSegment,
                    });
                }
            }
        }

        if (rounds.Count != state.ActiveWorkoutMinutes)
        {
            throw new InvalidOperationException(
                $"The {state.ActiveWorkoutMinutes}-minute workout scheduled " +
                $"{rounds.Count} exercise blocks.");
        }

        return rounds.AsReadOnly();
    }

    private sealed record LongWorkoutAllocation(
        HashSet<string> ExtraSetSelectionGroupIds,
        Dictionary<string, int> SetCountsBySelectionGroupId);

    private sealed record SelectedSequencePlacement(
        Exercise Root,
        WorkoutGroup Anchor,
        IReadOnlyList<WorkoutGroup> CoveredGroups);

    private sealed record ShuffleCandidate(
        Exercise Exercise,
        IReadOnlyList<WorkoutGroup> CoveredGroups,
        LongWorkoutAllocation Allocation);

    private sealed record MuscleBudgetCandidate(
        int ExerciseId,
        int RealScore,
        long AdjustedScoreHalfUnits,
        bool IsFreshHard,
        bool IsRecoveringHard,
        bool IsRecoveringModerate,
        bool IsKept,
        long LastHardWorkUnixMilliseconds,
        bool IsMirrorPreferred,
        bool IsPrimary,
        int CanonicalCoverage);

    private sealed record LegacyActiveProgressSnapshot(
        Dictionary<string, ExerciseOutcome> Outcomes,
        Dictionary<string, int> SelectedExerciseIds,
        Dictionary<string, int> DirectionPartnerExerciseIds,
        HashSet<string> FullSideRoundIds,
        string? PendingMovementGroupId,
        long PendingMovementMillisecondsRemaining,
        long PendingMovementEndsAtUnixMilliseconds,
        bool PendingMovementPausedByUser,
        string? PendingRestGroupId,
        long PendingRestEndsAtUnixMilliseconds,
        bool PendingRestKept);
}
