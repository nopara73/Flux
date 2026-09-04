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
        return GetTrainingDaysUntilLightDay(
            workoutHistory,
            nowUnixMilliseconds,
            localTimeZone,
            legacyCompletedTrainingDayUnixMilliseconds) == 0;
    }

    public static int GetTrainingDaysUntilLightDay(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        int consecutiveRegularDays = GetConsecutiveRegularTrainingDays(
            workoutHistory,
            nowUnixMilliseconds,
            localTimeZone,
            legacyCompletedTrainingDayUnixMilliseconds);

        // The badge describes the next workout. A completed regular workout
        // today therefore advances it immediately, while a completed Light
        // workout starts a fresh three-regular-day cycle. Once Light is due,
        // regular workouts do not silently skip the recovery opportunity.
        return ConsecutivePriorDaysRequired - consecutiveRegularDays;
    }

    private static int GetConsecutiveRegularTrainingDays(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(workoutHistory);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        TimeZoneInfo timeZone = localTimeZone ?? TimeZoneInfo.Local;
        int today = GetLocalDayNumber(nowUnixMilliseconds, timeZone);
        var completedTrainingDays = new Dictionary<int, bool>();
        foreach (WorkoutSessionLog session in workoutHistory.Where(session =>
                     session is not null &&
                     session.Status == WorkoutSessionStatus.Completed))
        {
            long timestamp = session.StartedAtUnixMilliseconds > 0
                ? session.StartedAtUnixMilliseconds
                : session.EndedAtUnixMilliseconds;
            int? dayNumber = timestamp > 0
                ? TryGetLocalDayNumber(timestamp, timeZone)
                : null;
            if (!dayNumber.HasValue)
            {
                continue;
            }

            bool isLightDay = session.IsLightDay ||
                (session.Modifiers & WorkoutModifiers.Light) != 0;
            completedTrainingDays[dayNumber.Value] = isLightDay ||
                completedTrainingDays.GetValueOrDefault(dayNumber.Value);
        }
        foreach (int dayNumber in (legacyCompletedTrainingDayUnixMilliseconds ?? [])
                     .Where(timestamp => timestamp > 0)
                     .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
                     .Where(dayNumber => dayNumber.HasValue)
                     .Select(dayNumber => dayNumber!.Value))
        {
            completedTrainingDays.TryAdd(dayNumber, false);
        }

        int day = completedTrainingDays.ContainsKey(today)
            ? today
            : today - 1;
        int consecutiveRegularDays = 0;
        while (completedTrainingDays.TryGetValue(day, out bool isLightDay))
        {
            if (isLightDay)
            {
                break;
            }

            consecutiveRegularDays++;
            if (consecutiveRegularDays >= ConsecutivePriorDaysRequired)
            {
                return ConsecutivePriorDaysRequired;
            }
            day--;
        }
        return consecutiveRegularDays;
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
