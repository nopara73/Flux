using Flux.Models;

namespace Flux.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    private const string AlternatingPrefix = "Alternating ";
    public const int CurrentCatalogRevision = 1;

    private static readonly HashSet<int> ReplacedExerciseIdSet =
    [
        56, 59, 98, 102, 159, 176, 185, 193, 199, 201, 203, 219,
        227, 228, 229, 230, 239, 240, 241, 242, 262, 267, 274, 275,
        276, 280, 281, 284, 285, 286, 287, 288, 289, 291, 292, 293,
        294, 295, 296, 367, 393, 396, 422, 423, 467, 474, 481, 482,
        483, 490, 491, 492, 493, 495, 497, 499, 500, 501, 502, 503,
        504, 505, 506, 507, 508, 509, 510, 512, 513, 572, 573, 609,
        610, 611, 612, 613, 614, 615, 616, 618, 619, 625, 636, 647,
        654, 677, 678, 681, 683, 684, 685, 687, 743, 843,
    ];

    public static IReadOnlySet<int> ReplacedExerciseIds => ReplacedExerciseIdSet;

    public static IReadOnlySet<int> ValidatePreservedCatalog(
        IReadOnlyCollection<Exercise> bundledCatalog,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> storedExercises)
    {
        ArgumentNullException.ThrowIfNull(bundledCatalog);
        ArgumentNullException.ThrowIfNull(storedExercises);

        Dictionary<int, Exercise> bundledById;
        try
        {
            bundledById = bundledCatalog.ToDictionary(exercise => exercise.Id);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The bundled catalog contains a duplicate stable exercise ID.",
                exception);
        }

        foreach ((int exerciseId, StoredExerciseSnapshot stored) in storedExercises)
        {
            if (ReplacedExerciseIdSet.Contains(exerciseId))
            {
                if (!bundledById.TryGetValue(exerciseId, out Exercise? replacement))
                {
                    throw new InvalidOperationException(
                        $"The bundled catalog is missing reviewed replacement {exerciseId}.");
                }

                bool retiredNameMatches = !string.IsNullOrWhiteSpace(
                        replacement.RetiredName) &&
                    (string.Equals(
                            stored.Name,
                            replacement.RetiredName,
                            StringComparison.Ordinal) ||
                        (stored.Name.StartsWith(
                                AlternatingPrefix,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                stored.Name[AlternatingPrefix.Length..],
                                replacement.RetiredName,
                                StringComparison.Ordinal)));
                if (!retiredNameMatches ||
                    !string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The bundled catalog cannot verify the retired identity " +
                        $"of reviewed replacement {exerciseId}.");
                }

                continue;
            }

            if (!bundledById.TryGetValue(exerciseId, out Exercise? bundled))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would remove existing exercise {exerciseId}.");
            }

            bool nameIsPreserved = string.Equals(
                stored.Name,
                bundled.Name,
                StringComparison.Ordinal);
            bool nameIsApprovedTimedSideNormalization =
                bundled.SideSequence != ExerciseSideSequence.Continuous &&
                stored.Name.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
                string.Equals(
                    stored.Name[AlternatingPrefix.Length..],
                    bundled.Name,
                    StringComparison.Ordinal);
            bool nameIsApprovedExerciseCorrection =
                exerciseId == 268 &&
                string.Equals(
                    stored.Name,
                    "Self-Resisted External-Rotation Push-Out",
                    StringComparison.Ordinal) &&
                string.Equals(
                    bundled.Name,
                    "Self-Resisted External-Rotation Isometric",
                    StringComparison.Ordinal);
            if ((!nameIsPreserved &&
                    !nameIsApprovedTimedSideNormalization &&
                    !nameIsApprovedExerciseCorrection) ||
                !string.Equals(stored.Video, bundled.Video, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would change the stable identity or " +
                    $"demonstration of existing exercise {exerciseId}.");
            }
        }

        return storedExercises.Keys
            .Where(exerciseId => !ReplacedExerciseIdSet.Contains(exerciseId))
            .ToHashSet();
    }

    public static bool ReconcileWorkoutState(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.CatalogRevision >= CurrentCatalogRevision)
        {
            return false;
        }

        state.SelectedExerciseIds ??= [];
        state.Outcomes ??= [];

        string[] groupsWithRetiredSelections = state.SelectedExerciseIds
            .Where(selection => ReplacedExerciseIdSet.Contains(selection.Value))
            .Select(selection => selection.Key)
            .ToArray();

        foreach (string groupId in groupsWithRetiredSelections)
        {
            state.SelectedExerciseIds.Remove(groupId);
            state.Outcomes.Remove(groupId);
        }

        if (state.PendingRestGroupId is not null &&
            groupsWithRetiredSelections.Contains(
                state.PendingRestGroupId,
                StringComparer.Ordinal))
        {
            state.PendingRestGroupId = null;
            state.PendingRestEndsAtUnixMilliseconds = 0;
            state.PendingRestKept = false;
        }

        if (ReplacedExerciseIdSet.Contains(state.PendingScoreExerciseId))
        {
            state.PendingScoreExerciseId = 0;
            state.PendingScoreValue = 0;
        }

        state.CatalogRevision = CurrentCatalogRevision;
        return true;
    }
}
