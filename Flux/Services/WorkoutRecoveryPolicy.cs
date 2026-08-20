using System.Globalization;
using Flux.Models;

namespace Flux.Services;

public static class WorkoutRecoveryPolicy
{
    public const int HardMuscularDemand = Exercise.MaximumMuscularDemand;

    private const string LocalDateFormat = "yyyy-MM-dd";

    public static string ToLocalDateKey(DateOnly localDate) =>
        localDate.ToString(LocalDateFormat, CultureInfo.InvariantCulture);

    public static bool IsValidLocalDateKey(string? value) =>
        DateOnly.TryParseExact(
            value,
            LocalDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    public static HashSet<int> GetPreviousDayHardKeptExerciseIds(
        IReadOnlySet<int> keptExerciseIds,
        IReadOnlyDictionary<int, string> lastKeptLocalDateByExerciseId,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        DateOnly currentLocalDate)
    {
        ArgumentNullException.ThrowIfNull(keptExerciseIds);
        ArgumentNullException.ThrowIfNull(lastKeptLocalDateByExerciseId);
        ArgumentNullException.ThrowIfNull(exercisesById);

        string previousLocalDateKey = ToLocalDateKey(currentLocalDate.AddDays(-1));
        return keptExerciseIds
            .Where(exerciseId =>
                lastKeptLocalDateByExerciseId.TryGetValue(
                    exerciseId,
                    out string? lastKeptLocalDate) &&
                string.Equals(
                    lastKeptLocalDate,
                    previousLocalDateKey,
                    StringComparison.Ordinal) &&
                exercisesById.TryGetValue(exerciseId, out Exercise? exercise) &&
                exercise.MuscularDemand == HardMuscularDemand)
            .ToHashSet();
    }
}
