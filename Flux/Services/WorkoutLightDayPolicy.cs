using Flux.Models;

namespace Flux.Services;

public static class WorkoutLightDayPolicy
{
    public const int TrainingDaysPerCycle = 4;
    public const int ConsecutivePriorDaysRequired = TrainingDaysPerCycle - 1;
    public const int MinimumLegacyHardPrimaryMuscles = 3;

    public static WorkoutModifiers GetDefaultWorkoutModifiers(
        WorkoutModifiers persistentSetupModifiers,
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        WorkoutModifiers modifiers = WorkoutModifierPolicy
            .GetPersistentSetupModifiers(persistentSetupModifiers);
        return IsLightDayDue(
                workoutHistory,
                nowUnixMilliseconds,
                localTimeZone,
                legacyCompletedTrainingDayUnixMilliseconds)
            ? modifiers | WorkoutModifiers.Light
            : modifiers;
    }

    public static bool IsLightDayDue(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(workoutHistory);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        TimeZoneInfo timeZone = localTimeZone ?? TimeZoneInfo.Local;
        int today = GetLocalDayNumber(nowUnixMilliseconds, timeZone);
        HashSet<int> completedTrainingDays = workoutHistory
            .Where(session =>
                session is not null &&
                session.Status == WorkoutSessionStatus.Completed)
            .Select(session => session.StartedAtUnixMilliseconds > 0
                ? session.StartedAtUnixMilliseconds
                : session.EndedAtUnixMilliseconds)
            .Where(timestamp => timestamp > 0)
            .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
            .Where(dayNumber => dayNumber.HasValue)
            .Select(dayNumber => dayNumber!.Value)
            .ToHashSet();
        foreach (int dayNumber in (legacyCompletedTrainingDayUnixMilliseconds ?? [])
                     .Where(timestamp => timestamp > 0)
                     .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
                     .Where(dayNumber => dayNumber.HasValue)
                     .Select(dayNumber => dayNumber!.Value))
        {
            completedTrainingDays.Add(dayNumber);
        }

        int consecutivePriorDays = 0;
        for (int day = today - 1; completedTrainingDays.Contains(day); day--)
        {
            consecutivePriorDays++;
        }

        // Day four of an uninterrupted daily streak is light, then the same
        // four-day cadence repeats. Completing a light workout therefore does
        // not make every later day in the streak light as well.
        return consecutivePriorDays >= ConsecutivePriorDaysRequired &&
            (consecutivePriorDays + 1) % TrainingDaysPerCycle == 0;
    }

    public static IReadOnlyList<long> InferLegacyCompletedTrainingDays(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        IEnumerable<long> existingLegacyTrainingDayTimestamps,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null)
    {
        ArgumentNullException.ThrowIfNull(workoutHistory);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(existingLegacyTrainingDayTimestamps);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        TimeZoneInfo timeZone = localTimeZone ?? TimeZoneInfo.Local;
        int today = GetLocalDayNumber(nowUnixMilliseconds, timeZone);
        HashSet<int> loggedCompletedDays = workoutHistory
            .Where(session =>
                session is not null &&
                session.Status == WorkoutSessionStatus.Completed)
            .Select(session => session.StartedAtUnixMilliseconds > 0
                ? session.StartedAtUnixMilliseconds
                : session.EndedAtUnixMilliseconds)
            .Where(timestamp => timestamp > 0)
            .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
            .Where(dayNumber => dayNumber.HasValue)
            .Select(dayNumber => dayNumber!.Value)
            .ToHashSet();
        Dictionary<int, long> existingLegacyDays = existingLegacyTrainingDayTimestamps
            .Where(timestamp => timestamp > 0)
            .Select(timestamp => new
            {
                Timestamp = timestamp,
                Day = TryGetLocalDayNumber(timestamp, timeZone),
            })
            .Where(entry => entry.Day.HasValue)
            .GroupBy(entry => entry.Day!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Max(entry => entry.Timestamp));
        Dictionary<int, long[]> hardEvidenceByDay = lastHardWorkByPrimaryMuscle
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Key) &&
                entry.Value > 0)
            .Select(entry => new
            {
                entry.Value,
                Day = TryGetLocalDayNumber(entry.Value, timeZone),
            })
            .Where(entry => entry.Day.HasValue)
            .GroupBy(entry => entry.Day!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Value).ToArray());

        // Only bridge the uninterrupted streak immediately preceding the
        // current logged streak. Sparse older recovery timestamps must never
        // turn isolated historical activity into fabricated workout sessions.
        int cursor = loggedCompletedDays.Contains(today) ? today : today - 1;
        while (loggedCompletedDays.Contains(cursor) ||
               existingLegacyDays.ContainsKey(cursor))
        {
            cursor--;
        }

        var inferred = new List<long>();
        while (hardEvidenceByDay.TryGetValue(cursor, out long[]? timestamps) &&
               timestamps.Length >= MinimumLegacyHardPrimaryMuscles)
        {
            inferred.Add(timestamps.Max());
            cursor--;
        }

        return inferred;
    }

    private static int GetLocalDayNumber(
        long unixMilliseconds,
        TimeZoneInfo localTimeZone)
    {
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds),
            localTimeZone);
        return DateOnly.FromDateTime(localTime.DateTime).DayNumber;
    }

    private static int? TryGetLocalDayNumber(
        long unixMilliseconds,
        TimeZoneInfo localTimeZone)
    {
        try
        {
            return GetLocalDayNumber(unixMilliseconds, localTimeZone);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
