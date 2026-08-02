using Flux.Models;

namespace Flux.Services;

public sealed class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 20;
    public const int DefaultWorkoutMinutes = 10;

    private const int CurrentStateVersion = 4;

    private static readonly IReadOnlyList<MuscleGroup> OrderedMuscleGroups =
        Array.AsReadOnly<MuscleGroup>(
        [
            MuscleGroup.Glutes,
            MuscleGroup.Core,
            MuscleGroup.Quadriceps,
            MuscleGroup.Hamstrings,
            MuscleGroup.UpperBack,
            MuscleGroup.Shoulders,
            MuscleGroup.Chest,
            MuscleGroup.LowerBack,
            MuscleGroup.Calves,
            MuscleGroup.HipFlexors,
            MuscleGroup.Adductors,
            MuscleGroup.Abductors,
            MuscleGroup.MidBack,
            MuscleGroup.Trapezius,
            MuscleGroup.Forearms,
            MuscleGroup.Triceps,
            MuscleGroup.Biceps,
            MuscleGroup.RotatorCuff,
            MuscleGroup.Neck,
            MuscleGroup.Shins,
        ]);

    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly Random _random;

    public ExerciseSessionService(IReadOnlyList<Exercise> exercises, Random? random = null)
    {
        _exercises = exercises;
        _random = random ?? Random.Shared;
    }

    public static IReadOnlyList<MuscleGroup> MuscleGroupOrder => OrderedMuscleGroups;

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        NormalizeCollections(state);
        if (state.Version != CurrentStateVersion)
        {
            ResetIncompatibleSession(state);
        }

        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            ResetToDurationSelection(state);
            return;
        }

        RepairFullLineup(state);
        NormalizeOutcomes(state);
        NormalizeCompletionState(state);
        NormalizePendingRest(state);

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
                $"Workout duration must be between {MinimumWorkoutMinutes} and " +
                    $"{MaximumWorkoutMinutes} minutes.");
        }

        if (IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
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
        RepairFullLineup(state);
    }

    public IReadOnlyList<MuscleGroup> GetActiveMuscleGroups(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? OrderedMuscleGroups.Take(state.ActiveWorkoutMinutes).ToArray()
            : [];
    }

    public Exercise GetSelectedExercise(WorkoutState state, MuscleGroup muscleGroup)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.SelectedExercises.TryGetValue(muscleGroup, out string? exerciseName))
        {
            throw new InvalidOperationException(
                $"No exercise is selected for {muscleGroup}.");
        }

        return _exercises.Single(exercise =>
            exercise.MuscleGroups.Contains(muscleGroup) &&
            exercise.Name == exerciseName);
    }

    public MuscleGroup? GetNextMuscleGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (MuscleGroup muscleGroup in GetActiveMuscleGroups(state))
        {
            if (!state.Outcomes.ContainsKey(muscleGroup))
            {
                return muscleGroup;
            }
        }

        return null;
    }

    public Exercise RecordOutcome(
        WorkoutState state,
        MuscleGroup muscleGroup,
        bool keep)
    {
        ArgumentNullException.ThrowIfNull(state);

        MuscleGroup? nextMuscleGroup = GetNextMuscleGroup(state);
        if (nextMuscleGroup is null || nextMuscleGroup.Value != muscleGroup)
        {
            throw new InvalidOperationException(
                $"{muscleGroup} is not the next workout muscle group.");
        }

        Exercise exercise = GetSelectedExercise(state, muscleGroup);
        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        if (!keep)
        {
            exercise.Score--;
        }

        state.Outcomes[muscleGroup] = outcome;
        state.WorkoutCompleted = GetActiveMuscleGroups(state)
            .All(state.Outcomes.ContainsKey);
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

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            ResetToDurationSelection(state);
            return null;
        }

        Exercise? scorePenalty = null;
        if (state.PendingRestMuscleGroup is MuscleGroup pendingMuscleGroup)
        {
            bool keep = state.PendingRestKept;
            Exercise exercise = RecordOutcome(state, pendingMuscleGroup, keep);
            ClearPendingRest(state);
            if (!keep)
            {
                scorePenalty = exercise;
            }
        }

        PrepareNextSession(state);
        return scorePenalty;
    }

    public (int Replaced, int Kept) GetOutcomeCounts(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        HashSet<MuscleGroup> activeGroups = GetActiveMuscleGroups(state).ToHashSet();
        int replaced = state.Outcomes.Count(entry =>
            activeGroups.Contains(entry.Key) && entry.Value == ExerciseOutcome.X);
        int kept = state.Outcomes.Count(entry =>
            activeGroups.Contains(entry.Key) && entry.Value != ExerciseOutcome.X);
        return (replaced, kept);
    }

    public void ClearPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingRestMuscleGroup = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
    }

    private void PrepareNextSession(WorkoutState state)
    {
        MuscleGroup[] activeGroups = GetActiveMuscleGroups(state).ToArray();

        foreach (MuscleGroup muscleGroup in activeGroups.Where(muscleGroup =>
                     state.Outcomes.TryGetValue(muscleGroup, out ExerciseOutcome outcome) &&
                     outcome == ExerciseOutcome.X))
        {
            string currentName = state.SelectedExercises[muscleGroup];
            var excludedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                currentName,
            };
            Exercise replacement = ChooseFromHighestScoreBucket(
                muscleGroup,
                excludedNames);
            state.SelectedExercises[muscleGroup] = replacement.Name;
        }

        ResetToDurationSelection(state);
    }

    private Exercise ChooseFromHighestScoreBucket(
        MuscleGroup muscleGroup,
        IReadOnlySet<string> excludedNames)
    {
        Exercise[] candidates = _exercises
            .Where(exercise =>
                exercise.MuscleGroups.Contains(muscleGroup) &&
                !excludedNames.Contains(exercise.Name))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"No distinct replacement exercise exists for {muscleGroup}.");
        }

        int highestScore = candidates.Max(exercise => exercise.Score);
        Exercise[] highestScoreBucket = candidates
            .Where(exercise => exercise.Score == highestScore)
            .ToArray();

        return highestScoreBucket[_random.Next(highestScoreBucket.Length)];
    }

    private void RepairFullLineup(WorkoutState state)
    {
        var usedExerciseNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (MuscleGroup muscleGroup in OrderedMuscleGroups)
        {
            bool hasValidSelection = state.SelectedExercises.TryGetValue(
                    muscleGroup,
                    out string? selectedName) &&
                _exercises.Any(exercise =>
                    exercise.MuscleGroups.Contains(muscleGroup) &&
                    exercise.Name == selectedName);

            if (!hasValidSelection)
            {
                var excludedNames = new HashSet<string>(
                    usedExerciseNames,
                    StringComparer.Ordinal);
                if (selectedName is not null)
                {
                    excludedNames.Add(selectedName);
                }

                Exercise replacement;
                try
                {
                    replacement = ChooseFromHighestScoreBucket(
                        muscleGroup,
                        excludedNames);
                }
                catch (InvalidOperationException)
                {
                    // Existing kept slots take priority over strict uniqueness.
                    // This fallback is only reachable if every candidate for a
                    // small overlapping group is already used elsewhere.
                    excludedNames.Clear();
                    if (selectedName is not null)
                    {
                        excludedNames.Add(selectedName);
                    }

                    replacement = ChooseFromHighestScoreBucket(
                        muscleGroup,
                        excludedNames);
                }
                state.SelectedExercises[muscleGroup] = replacement.Name;
                state.Outcomes.Remove(muscleGroup);
                selectedName = replacement.Name;
            }

            usedExerciseNames.Add(selectedName!);
        }
    }

    private static void NormalizeCollections(WorkoutState state)
    {
        state.SelectedExercises ??= [];
        state.Outcomes ??= [];
    }

    private static void NormalizeOutcomes(WorkoutState state)
    {
        HashSet<MuscleGroup> activeGroups = OrderedMuscleGroups
            .Take(state.ActiveWorkoutMinutes)
            .ToHashSet();

        foreach (MuscleGroup muscleGroup in state.Outcomes.Keys
                     .Where(muscleGroup => !activeGroups.Contains(muscleGroup))
                     .ToArray())
        {
            state.Outcomes.Remove(muscleGroup);
        }

        foreach (MuscleGroup muscleGroup in state.Outcomes
                     .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            state.Outcomes[muscleGroup] = ExerciseOutcome.Tick;
        }
    }

    private void NormalizePendingRest(WorkoutState state)
    {
        if (state.PendingRestMuscleGroup is null)
        {
            ClearPendingRest(state);
            return;
        }

        MuscleGroup? nextMuscleGroup = GetNextMuscleGroup(state);
        if (state.WorkoutCompleted ||
            nextMuscleGroup != state.PendingRestMuscleGroup ||
            state.PendingRestEndsAtUnixMilliseconds <= 0)
        {
            ClearPendingRest(state);
        }
    }

    private static void NormalizeCompletionState(WorkoutState state)
    {
        MuscleGroup[] activeGroups = OrderedMuscleGroups
            .Take(state.ActiveWorkoutMinutes)
            .ToArray();
        state.WorkoutCompleted = activeGroups.Length > 0 &&
            activeGroups.All(state.Outcomes.ContainsKey);

        if (!state.WorkoutCompleted)
        {
            state.CompletionAcknowledged = false;
        }
    }

    private static void ResetIncompatibleSession(WorkoutState state)
    {
        state.SelectedExercises.Clear();
        ResetToDurationSelection(state);
    }

    private static void ResetToDurationSelection(WorkoutState state)
    {
        state.ActiveWorkoutMinutes = 0;
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        state.PendingRestMuscleGroup = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestKept = false;
    }

    private static int NormalizeLastWorkoutMinutes(int minutes)
    {
        return IsValidWorkoutMinutes(minutes) ? minutes : DefaultWorkoutMinutes;
    }

    private static bool IsValidWorkoutMinutes(int minutes)
    {
        return minutes is >= MinimumWorkoutMinutes and <= MaximumWorkoutMinutes;
    }
}
