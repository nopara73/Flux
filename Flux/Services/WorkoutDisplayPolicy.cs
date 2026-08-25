using Flux.Models;

namespace Flux.Services;

public enum WorkoutBlockAccent
{
    Neutral,
    Blue,
    Red,
}

public readonly record struct WorkoutDisplayProgress(int Position, int Total);

public sealed record WorkoutExecutionTimeline(
    IReadOnlyList<WorkoutBlockAccent> Blocks,
    int CurrentBlockIndex);

public static class WorkoutDisplayPolicy
{
    public static WorkoutDisplayProgress GetProgress(
        IReadOnlyList<WorkoutGroup> activeGroups,
        WorkoutGroup currentGroup)
    {
        ArgumentNullException.ThrowIfNull(activeGroups);
        ArgumentNullException.ThrowIfNull(currentGroup);

        string[] selectionKeys = activeGroups
            .OrderBy(group => group.Order)
            .Select(group => group.SelectionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        int position = Array.FindIndex(
            selectionKeys,
            key => string.Equals(
                key,
                currentGroup.SelectionKey,
                StringComparison.Ordinal));
        if (position < 0 || selectionKeys.Length == 0)
        {
            throw new InvalidOperationException(
                "The current workout group is not in the active workout.");
        }

        return new WorkoutDisplayProgress(position + 1, selectionKeys.Length);
    }

    public static WorkoutExecutionTimeline GetTimeline(
        IReadOnlyList<WorkoutGroup> activeGroups,
        WorkoutGroup currentGroup,
        bool selectUpcomingBlock = false)
    {
        ArgumentNullException.ThrowIfNull(activeGroups);
        ArgumentNullException.ThrowIfNull(currentGroup);

        WorkoutGroup[] blocks = activeGroups
            .Where(group => string.Equals(
                group.SelectionKey,
                currentGroup.SelectionKey,
                StringComparison.Ordinal))
            .OrderBy(group => group.Order)
            .ToArray();
        int currentBlockIndex = Array.FindIndex(
            blocks,
            group => string.Equals(
                group.Id,
                currentGroup.Id,
                StringComparison.Ordinal));
        if (currentBlockIndex < 0 || blocks.Length == 0)
        {
            throw new InvalidOperationException(
                "The current workout group has no active execution timeline.");
        }
        if (selectUpcomingBlock && currentBlockIndex + 1 < blocks.Length)
        {
            currentBlockIndex++;
        }

        return new WorkoutExecutionTimeline(
            blocks.Select(GetAccent).ToArray(),
            currentBlockIndex);
    }

    public static WorkoutBlockAccent GetAccent(WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.SequenceSideCue switch
        {
            ExerciseSequenceSideCue.ScreenRight or
                ExerciseSequenceSideCue.ShownLeadStance =>
                WorkoutBlockAccent.Blue,
            ExerciseSequenceSideCue.ScreenLeft or
                ExerciseSequenceSideCue.OppositeLeadStance =>
                WorkoutBlockAccent.Red,
            _ => group.SequenceDirectionCue switch
            {
                ExerciseSequenceDirectionCue.Forward or
                    ExerciseSequenceDirectionCue.Clockwise or
                    ExerciseSequenceDirectionCue.Inward =>
                    WorkoutBlockAccent.Blue,
                ExerciseSequenceDirectionCue.Backward or
                    ExerciseSequenceDirectionCue.Counterclockwise or
                    ExerciseSequenceDirectionCue.Outward =>
                    WorkoutBlockAccent.Red,
                _ => WorkoutBlockAccent.Neutral,
            },
        };
    }
}
