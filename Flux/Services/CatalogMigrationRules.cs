using Flux.Models;

namespace Flux.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    private const string AlternatingPrefix = "Alternating ";
    public const int CurrentCatalogRevision = 2;

    private sealed record PriorReviewedReplacementIdentity(
        string Name,
        string BaselineRetiredName);

    private sealed record ApprovedExerciseCorrection(
        string PreviousName,
        string CurrentName);

    private static readonly IReadOnlyDictionary<int, ApprovedExerciseCorrection>
        ApprovedExerciseCorrections =
            new Dictionary<int, ApprovedExerciseCorrection>
            {
                [255] = new(
                    "Standing Bent-Knee Calf Raise",
                    "Deep-Squat Calf Raise"),
                [268] = new(
                    "Self-Resisted External-Rotation Push-Out",
                    "Self-Resisted External-Rotation Isometric"),
                [425] = new(
                    "Chin-Tuck Isometric",
                    "Chin-Tuck Hold"),
            };

    private static readonly IReadOnlyDictionary<int, PriorReviewedReplacementIdentity>
        PriorReviewedReplacementIdentities =
            new Dictionary<int, PriorReviewedReplacementIdentity>
            {
                [239] = new(
                    "Ninja Snake Hand-Seal Hold",
                    "Ninja Fireball Hand-Seal Sequence"),
                [240] = new(
                    "Ninja Ram Hand-Seal Hold",
                    "Ninja Shadow-Possession Hand-Seal Sequence"),
                [241] = new(
                    "Ninja Monkey Hand-Seal Hold",
                    "Ninja Water-Dragon 44 Hand-Seal Sequence"),
                [242] = new(
                    "Ninja Boar Hand-Seal Hold",
                    "Ninja Shadow-Clone Hand-Seal Sequence"),
                [274] = new(
                    "Side-Step Alternating High Curl",
                    "Dynamic-Resistance Lat Pulldown"),
                [276] = new(
                    "Alternating Diagonal Overhead Reach-and-Pull",
                    "Dynamic-Resistance High Chest Press"),
                [280] = new(
                    "Alternating Forward-and-Side Arm Press",
                    "Ringing-the-Towel Wrist Inversion"),
                [289] = new(
                    "Ninja Horse Hand-Seal Hold",
                    "Heaven-to-Earth Finger Rotation"),
                [291] = new(
                    "Ninja Tiger Hand-Seal Hold",
                    "Black Dragon Enters the Cave"),
                [293] = new(
                    "Ninja Dragon Hand-Seal Hold",
                    "Sword-Fingers Qigong Sequence"),
                [294] = new(
                    "Ninja Rat Hand-Seal Hold",
                    "Tiger-Claw Grip Flow"),
                [483] = new(
                    "Clockwise Full Neck Circles",
                    "Pirouette Spotting Drill"),
                [501] = new(
                    "Counterclockwise Full Neck Circles",
                    "Standing Horizontal Saccades"),
                [572] = new(
                    "Wide-Stance Bent-Knee Rotational Stretch",
                    "Tai Chi White Crane Opens Wings"),
                [681] = new(
                    "Rear-Arm Sweep to Front Squeeze",
                    "Belly-Dance Horizontal Figure Eight"),
                [743] = new(
                    "Standing Backward Arm Circles",
                    "Clasped-Hands-Behind-Back Chest Opener"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalPriorReviewedReplacementNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [289] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Adduction Hold",
                },
            };

    private static readonly HashSet<int> ReplacedExerciseIdSet =
    [
        41, 56, 59, 98, 102, 120, 146, 159, 176, 177, 182, 183,
        185, 187, 191, 192, 193, 194, 195, 196, 199, 201, 203, 219,
        227, 228, 229, 230, 239, 240, 241, 242, 260, 262, 267, 272, 274,
        275, 276, 280, 281, 284, 285, 286, 287, 288, 289, 291, 292,
        293, 294, 295, 296, 327, 367, 393, 396, 422, 423, 467, 474,
        481, 482, 483, 490, 491, 492, 493, 495, 497, 499, 500, 501,
        502, 503, 504, 505, 506, 507, 508, 509, 510, 512, 513, 572,
        573, 609, 610, 611, 612, 613, 614, 615, 616, 618, 619, 625,
        636, 647, 654, 677, 678, 681, 683, 684, 685, 687, 712, 743,
        843, 987,
    ];

    private static readonly HashSet<int> ContinuousAlternationNormalizationIdSet =
    [
        223, 224, 245, 246,
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

        var alreadyReviewedReplacementIds = new HashSet<int>();

        foreach ((int exerciseId, StoredExerciseSnapshot stored) in storedExercises)
        {
            if (ReplacedExerciseIdSet.Contains(exerciseId))
            {
                if (!bundledById.TryGetValue(exerciseId, out Exercise? replacement))
                {
                    throw new InvalidOperationException(
                        $"The bundled catalog is missing reviewed replacement {exerciseId}.");
                }

                bool currentReviewedIdentityMatches =
                    string.Equals(
                        stored.Name,
                        replacement.Name,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal);
                if (currentReviewedIdentityMatches)
                {
                    alreadyReviewedReplacementIds.Add(exerciseId);
                    continue;
                }

                bool baselineRetiredNameMatches =
                    !string.IsNullOrWhiteSpace(replacement.RetiredName) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        replacement.RetiredName);
                bool priorReviewedIdentityMatches =
                    PriorReviewedReplacementIdentities.TryGetValue(
                        exerciseId,
                        out PriorReviewedReplacementIdentity? priorIdentity) &&
                    string.Equals(
                        replacement.RetiredName,
                        priorIdentity.BaselineRetiredName,
                        StringComparison.Ordinal) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        priorIdentity.Name);
                bool additionalPriorReviewedIdentityMatches =
                    AdditionalPriorReviewedReplacementNames.TryGetValue(
                        exerciseId,
                        out IReadOnlySet<string>? priorNames) &&
                    priorIdentity is not null &&
                    string.Equals(
                        replacement.RetiredName,
                        priorIdentity.BaselineRetiredName,
                        StringComparison.Ordinal) &&
                    priorNames.Any(priorName =>
                        NameMatchesWithOptionalAlternatingPrefix(
                            stored.Name,
                            priorName));
                if ((!baselineRetiredNameMatches &&
                        !priorReviewedIdentityMatches &&
                        !additionalPriorReviewedIdentityMatches) ||
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
            bool nameIsApprovedContinuousAlternationNormalization =
                ContinuousAlternationNormalizationIdSet.Contains(exerciseId) &&
                bundled.SideSequence == ExerciseSideSequence.Continuous &&
                bundled.Name.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
                string.Equals(
                    stored.Name,
                    bundled.Name[AlternatingPrefix.Length..],
                    StringComparison.Ordinal);
            bool nameIsApprovedExerciseCorrection =
                ApprovedExerciseCorrections.TryGetValue(
                    exerciseId,
                    out ApprovedExerciseCorrection? correction) &&
                string.Equals(
                    stored.Name,
                    correction.PreviousName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    bundled.Name,
                    correction.CurrentName,
                    StringComparison.Ordinal);
            if ((!nameIsPreserved &&
                    !nameIsApprovedTimedSideNormalization &&
                    !nameIsApprovedContinuousAlternationNormalization &&
                    !nameIsApprovedExerciseCorrection) ||
                !string.Equals(stored.Video, bundled.Video, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would change the stable identity or " +
                    $"demonstration of existing exercise {exerciseId}.");
            }
        }

        return storedExercises.Keys
            .Where(exerciseId =>
                !ReplacedExerciseIdSet.Contains(exerciseId) ||
                alreadyReviewedReplacementIds.Contains(exerciseId))
            .ToHashSet();
    }

    private static bool NameMatchesWithOptionalAlternatingPrefix(
        string storedName,
        string expectedName) =>
        string.Equals(storedName, expectedName, StringComparison.Ordinal) ||
        (storedName.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
            string.Equals(
                storedName[AlternatingPrefix.Length..],
                expectedName,
                StringComparison.Ordinal));

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
