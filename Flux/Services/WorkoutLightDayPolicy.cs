using Flux.Models;

namespace Flux.Services;

public static class WorkoutLightDayPolicy
{
    public const int TrainingDaysPerCycle = 4;
    public const int ConsecutivePriorDaysRequired = TrainingDaysPerCycle - 1;

    public static bool IsLightDayDue(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null)
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
