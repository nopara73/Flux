using System.Text.Json;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutLightDayPolicyTests
{
    private static readonly DateTimeOffset DayOne =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3, 60)]
    [InlineData(5, 36)]
    [InlineData(7, 26)]
    [InlineData(10, 18)]
    [InlineData(15, 12)]
    [InlineData(20, 9)]
    [InlineData(30, 6)]
    [InlineData(45, 4)]
    [InlineData(60, 3)]
    [InlineData(90, 3)]
    public void DurationScalesCompletedWorkRatherThanCalendarDayCount(
        int duration, int regularDays)
    {
        var history = new List<WorkoutSessionLog>();
        Assert.Equal(regularDays, Remaining(history, duration, DayOne));
        for (int day = 0; day < regularDays; day++)
        {
            DateTimeOffset now = DayOne.AddDays(day);
            Assert.False(Due(history, now));
            history.Add(Session(now, duration));
        }
        Assert.True(Due(history, DayOne.AddDays(regularDays)));
    }

    [Fact]
    public void TenThreeMinuteWorkoutsDailyMakeTheSeventhDayLightAfterReload()
    {
        var history = new List<WorkoutSessionLog>();
        for (int day = 0; day < 6; day++)
        {
            Assert.False(Due(history, DayOne.AddDays(day)));
            for (int workout = 0; workout < 10; workout++)
            {
                history.Add(Session(DayOne.AddDays(day).AddMinutes(workout * 10), 3));
            }
        }
        var state = new WorkoutState
        {
            WorkoutHistory = history,
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.Warmup] = new() { [507] = -2 },
            },
            LastKeptExerciseIds = [507],
        };
        string json = JsonSerializer.Serialize(state);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(json)!;
        Assert.Equal(60, restored.WorkoutHistory.Count);
        Assert.True(Due(restored.WorkoutHistory, DayOne.AddDays(6)));
        Assert.Equal(-2, restored.ExerciseScoreAdjustmentsByPhase[
            WorkoutExercisePhase.Warmup][507]);
        Assert.Contains(507, restored.LastKeptExerciseIds);
    }

    [Fact]
    public void DailyCapIsAppliedAfterAggregatingSessions()
    {
        WorkoutSessionLog[] history = Enumerable.Range(0, 10)
            .Select(index => Session(DayOne.AddMinutes(index * 10), 10)).ToArray();
        Assert.Equal(2, Remaining(history, 60, DayOne.AddHours(3)));
        Assert.Equal(40, Remaining(history, 3, DayOne.AddHours(3)));
    }

    [Fact]
    public void InterruptedWorkCountsCompletedBlocksAndAllSkippedWorkCountsZero()
    {
        WorkoutSessionLog partial = Session(DayOne, 10);
        partial.WorkoutMinutes = 90;
        partial.Status = WorkoutSessionStatus.Interrupted;
        WorkoutSessionLog skipped = Session(DayOne.AddDays(1), 0);
        skipped.WorkoutMinutes = 90;
        skipped.InitialSelections = [new WorkoutSelectionSnapshot()];
        skipped.Decisions = [new WorkoutDecisionLog()];
        Assert.Equal(17, Remaining([partial, skipped], 10, DayOne.AddDays(1).AddHours(2)));
        Assert.Equal(18, Remaining([skipped], 10, DayOne.AddDays(1).AddHours(2)));
    }

    [Fact]
    public void DueLightRemainsLockedAfterTokenCompletionUntilNextLocalDay()
    {
        List<WorkoutSessionLog> history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToList();
        DateTimeOffset dueDay = DayOne.AddDays(3);
        Assert.True(Due(history, dueDay));
        history.Add(Session(dueDay, 3, light: true));
        Assert.True(Due(history, dueDay.AddHours(1)));
        Assert.True(Due(history, dueDay.Date.AddHours(23).AddMinutes(59)));
        Assert.False(Due(history, dueDay.AddDays(1)));
        Assert.Equal(6, Remaining(history, 30, dueDay.AddDays(1)));
    }

    [Fact]
    public void LightAfterThresholdReachedTodayResetsOnlyTomorrow()
    {
        List<WorkoutSessionLog> history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToList();
        DateTimeOffset today = DayOne.AddDays(2).AddHours(2);
        history.Add(Session(today, 3, light: true));
        Assert.True(Due(history, today.AddHours(1)));
        Assert.False(Due(history, today.AddDays(1)));
    }

    [Fact]
    public void FullRestDayResetsButOvernightDoesNot()
    {
        WorkoutSessionLog[] history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToArray();
        Assert.True(Due(history, DayOne.AddDays(3)));
        Assert.False(Due(history, DayOne.AddDays(4)));
        Assert.Equal(60, Remaining(history, 3, DayOne.AddDays(4)));
    }

    [Fact]
    public void ModifierChangesDoNotRewriteWorkAlreadyCompleted()
    {
        WorkoutSessionLog session = Session(DayOne, 30);
        session.Status = WorkoutSessionStatus.Interrupted;
        session.ModifierChanges = [new WorkoutModifierChangeLog
        {
            ChangedAtUnixMilliseconds = DayOne.AddMinutes(10).ToUnixTimeMilliseconds(),
            NewModifiers = WorkoutModifiers.Light,
        }];
        // Blocks finish at :01 through :30; nine preceded the mode change.
        Assert.Equal(171, Remaining([session], 1, DayOne.AddHours(1)));
    }

    [Fact]
    public void BlocksCrossingMidnightUseTheirOwnLocalCalendarDate()
    {
        DateTimeOffset start = new(2026, 9, 1, 23, 30, 0, TimeSpan.Zero);
        WorkoutSessionLog session = Session(start, 90);
        // 29 blocks on September 1, 61 on September 2: 29 + capped 60.
        Assert.Equal(91, Remaining([session], 1, start.AddHours(2)));
    }

    [Fact]
    public void LegacyDurationAndInferredDaysRemainBackwardCompatible()
    {
        WorkoutSessionLog old = Session(DayOne, 30);
        old.Blocks.Clear();
        Assert.Equal(5, Remaining([old], 30, DayOne.AddDays(1)));
        Assert.Equal(1, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            [Session(DayOne.AddDays(1), 60)], 60,
            DayOne.AddDays(2).ToUnixTimeMilliseconds(), TimeZoneInfo.Utc,
            [DayOne.ToUnixTimeMilliseconds()]));
        Assert.Equal(5, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            [old], 30, DayOne.AddDays(1).ToUnixTimeMilliseconds(),
            TimeZoneInfo.Utc, [DayOne.ToUnixTimeMilliseconds()]));
    }

    private static bool Due(IEnumerable<WorkoutSessionLog> history, DateTimeOffset now) =>
        WorkoutLightDayPolicy.IsLightDayDue(history, now.ToUnixTimeMilliseconds(),
            TimeZoneInfo.Utc);

    private static int Remaining(IEnumerable<WorkoutSessionLog> history,
        int duration, DateTimeOffset now) =>
        WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(history, duration,
            now.ToUnixTimeMilliseconds(), TimeZoneInfo.Utc);

    private static WorkoutSessionLog Session(DateTimeOffset start, int minutes,
        bool light = false) => new()
    {
        StartedAtUnixMilliseconds = start.ToUnixTimeMilliseconds(),
        EndedAtUnixMilliseconds = start.AddMinutes(minutes).ToUnixTimeMilliseconds(),
        WorkoutMinutes = minutes,
        Status = WorkoutSessionStatus.Completed,
        IsLightDay = light,
        Modifiers = light ? WorkoutModifiers.Light : WorkoutModifiers.None,
        Blocks = Enumerable.Range(1, minutes).Select(minute => new WorkoutBlockLog
        {
            CompletedAtUnixMilliseconds = start.AddMinutes(minute).ToUnixTimeMilliseconds(),
        }).ToList(),
    };
}
