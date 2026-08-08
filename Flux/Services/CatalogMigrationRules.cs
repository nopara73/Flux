using Flux.Models;

namespace Flux.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    private const string AlternatingPrefix = "Alternating ";
    public const int CurrentCatalogRevision = 7;
    private const int LastCumulativeWorkoutStateRevision = 3;

    private sealed record PriorReviewedReplacementIdentity(
        string Name,
        string BaselineRetiredName);

    private sealed record ApprovedExerciseCorrection(
        string PreviousName,
        string CurrentName);

    private sealed record RestoredReviewedExerciseIdentity(
        string PreviousReplacementName,
        string RestoredName);

    private static readonly IReadOnlyDictionary<int, ApprovedExerciseCorrection>
        ApprovedExerciseCorrections =
            new Dictionary<int, ApprovedExerciseCorrection>
            {
                [255] = new(
                    "Standing Bent-Knee Calf Raise",
                    "Deep-Squat Calf Raise"),
                [270] = new(
                    "Bodyweight Svend Press",
                    "Palm-Squeeze Forward Press"),
                [290] = new(
                    "Universe-in-Motion Qigong",
                    "Low Palm Scoop to Side Opening"),
                [394] = new(
                    "Standing Open-and-Close Breathing",
                    "Standing Arms Open and Close"),
                [395] = new(
                    "Standing Overhead Rib-Expansion Breathing",
                    "Standing Overhead Arm Sweep"),
                [397] = new(
                    "Breath-Integrated Weight Shift",
                    "Staggered-Stance Weight Shift"),
                [398] = new(
                    "Standing Arm-Expansion Breathing",
                    "Standing Hug and Arm Expansion"),
                [399] = new(
                    "Shibashi Opening-the-Chest Breathing",
                    "Shallow Squat with Chest-Opening Arms"),
                [400] = new(
                    "Shibashi Separating-the-Clouds Breathing",
                    "Shallow Squat with Overhead Arm Circle"),
                [401] = new(
                    "Shibashi Alternating Swinging-Arms Breathing",
                    "Alternating Weight Shift with Arm Swing"),
                [402] = new(
                    "Shibashi Rowing-a-Boat Breathing",
                    "Shallow Squat with Rowing Arm Circle"),
                [403] = new(
                    "Shibashi Alternating Pushing-Palms Breathing",
                    "Alternating Weight Shift with Palm Push"),
                [404] = new(
                    "Shibashi Alternating Punch Breathing",
                    "Wide-Stance Alternating Slow Punch"),
                [405] = new(
                    "Shibashi Flying-Wild-Goose Breathing",
                    "Shallow Squat with Wing Arm Raise"),
                [406] = new(
                    "Shibashi Spinning-Wheels Breathing",
                    "Standing Wheel Arm Circles"),
                [409] = new(
                    "Neck Controlled Articular Rotation",
                    "Full Neck Circles"),
                [425] = new(
                    "Chin-Tuck Isometric",
                    "Chin-Tuck Hold"),
                [588] = new(
                    "Belly-Dance Alternating Shoulder Roll",
                    "Belly-Dance Alternating Shoulder Rolls"),
                [626] = new(
                    "Sumo Stance",
                    "Sumo Squat Hold"),
                [969] = new(
                    "Chair-Pose Core Hold",
                    "Chair-Pose Hold"),
            };

    private static readonly IReadOnlyDictionary<int, RestoredReviewedExerciseIdentity>
        RestoredReviewedExerciseIdentities =
            new Dictionary<int, RestoredReviewedExerciseIdentity>
            {
                [266] = new(
                    "Zyzz Diagonal-Reach Pose Hold",
                    "Standing Palms-Up Arm Raise"),
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
                [268] = new(
                    "Self-Resisted External-Rotation Push-Out",
                    "Self-Resisted External-Rotation Isometric"),
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
                [482] = new(
                    "Front Half Neck Circles",
                    "Continuous Spot-Turn Drill"),
                [483] = new(
                    "Clockwise Full Neck Circles",
                    "Pirouette Spotting Drill"),
                [490] = new(
                    "Assisted Cheek Lift",
                    "Bharatanatyam Alolita Shiro"),
                [491] = new(
                    "Cheek-Firming Air Hold",
                    "Bharatanatyam Dhuta Shiro"),
                [492] = new(
                    "Forehead Knuckle Massage",
                    "Bharatanatyam Kampita Shiro"),
                [493] = new(
                    "Face-and-Neck Lymphatic Sweep",
                    "Alternating Bharatanatyam Paravritta Shiro"),
                [495] = new(
                    "Jawline Knuckle Massage",
                    "Bharatanatyam Parivahita Shiro"),
                [497] = new(
                    "Forehead Finger Sweep",
                    "Odissi Sundari Griva"),
                [499] = new(
                    "Eyebrow Pinch Massage",
                    "Bharatanatyam Tiraschina Griva"),
                [500] = new(
                    "Eye-Socket Finger Circles",
                    "Bharatanatyam Parivartita Griva"),
                [501] = new(
                    "Counterclockwise Full Neck Circles",
                    "Standing Horizontal Saccades"),
                [505] = new(
                    "Temple Circle Massage",
                    "Maximal Smile and Relax"),
                [506] = new(
                    "Cheek Pinch Massage",
                    "Eyebrow Raise and Relax"),
                [508] = new(
                    "Diagonal Arm Reach-to-Row",
                    "Tongue Protrusion and Retraction"),
                [572] = new(
                    "Wide-Stance Bent-Knee Rotational Stretch",
                    "Tai Chi White Crane Opens Wings"),
                [591] = new(
                    "Standing Speed-Bag Punches",
                    "Bharatanatyam Natyarambhe Hold"),
                [611] = new(
                    "Warrior II-Stance Hip Circles",
                    "Pelvic-Floor Heel-Raise Lift"),
                [681] = new(
                    "Rear-Arm Sweep to Front Squeeze",
                    "Belly-Dance Horizontal Figure Eight"),
                [743] = new(
                    "Standing Backward Arm Circles",
                    "Clasped-Hands-Behind-Back Chest Opener"),
                [843] = new(
                    "Standing Scalene Wrist-Anchor Stretch",
                    "Standing Cobra Pose"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalPriorReviewedReplacementNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [241] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb C Hold",
                },
                [289] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Adduction Hold",
                },
                [291] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Abduction Hold",
                },
                [293] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Flexion Hold",
                },
                [294] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Little-Finger Abduction Hold",
                },
                [483] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Clockwise-First Full Neck Circles",
                },
                [501] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Counterclockwise-First Full Neck Circles",
                },
            };

    private static readonly HashSet<int> ReplacedExerciseIdSet =
    [
        41, 56, 59, 98, 102, 116, 120, 133, 146, 159, 176, 177, 182, 183,
        185, 187, 191, 192, 193, 194, 195, 196, 199, 201, 203,
        215, 216, 217, 218, 219, 227, 228, 229, 230, 239, 240, 241, 242,
        260, 262, 267, 268, 272, 274, 275, 276, 280, 281, 283, 284, 285,
        286, 287, 288, 289, 291, 292, 293, 294, 295, 296, 326, 327, 367, 390,
        391, 392, 393, 396, 422,
        423, 467, 474, 475, 477,
        481, 482, 483, 490, 491, 492, 493, 495, 497, 499, 500, 501,
        502, 503, 504, 505, 506, 507, 508, 509, 510, 512, 513, 572,
        573, 591, 609, 610, 611, 612, 613, 614, 615, 616, 618, 619, 625,
        636, 647, 649, 654, 677, 678, 681, 683, 684, 685, 687, 712,
        743, 843, 845, 971, 986, 987,
    ];

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<int>>
        ScopedWorkoutStateInvalidationsByRevision =
            new Dictionary<int, IReadOnlySet<int>>
            {
                [4] = new HashSet<int> { 591 },
                [5] = new HashSet<int> { 266 },
                [6] = new HashSet<int> { 266 },
                [7] = new HashSet<int> { 326 },
            };

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
        var restoredReviewedExerciseIds = new HashSet<int>();

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
            bool nameIsApprovedReviewedRestoration =
                RestoredReviewedExerciseIdentities.TryGetValue(
                    exerciseId,
                    out RestoredReviewedExerciseIdentity? restoration) &&
                string.Equals(
                    stored.Name,
                    restoration.PreviousReplacementName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    bundled.Name,
                    restoration.RestoredName,
                    StringComparison.Ordinal);
            if ((!nameIsPreserved &&
                    !nameIsApprovedTimedSideNormalization &&
                    !nameIsApprovedContinuousAlternationNormalization &&
                    !nameIsApprovedExerciseCorrection &&
                    !nameIsApprovedReviewedRestoration) ||
                !string.Equals(stored.Video, bundled.Video, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would change the stable identity or " +
                    $"demonstration of existing exercise {exerciseId}.");
            }

            if (nameIsApprovedReviewedRestoration)
            {
                restoredReviewedExerciseIds.Add(exerciseId);
            }
        }

        return storedExercises.Keys
            .Where(exerciseId =>
                (!ReplacedExerciseIdSet.Contains(exerciseId) ||
                    alreadyReviewedReplacementIds.Contains(exerciseId)) &&
                !restoredReviewedExerciseIds.Contains(exerciseId))
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

    private static IReadOnlySet<int> GetWorkoutStateInvalidationExerciseIds(
        int priorCatalogRevision)
    {
        var invalidatedExerciseIds = priorCatalogRevision <
            LastCumulativeWorkoutStateRevision
                ? new HashSet<int>(ReplacedExerciseIdSet)
                : [];

        foreach ((int revision, IReadOnlySet<int> exerciseIds) in
            ScopedWorkoutStateInvalidationsByRevision)
        {
            if (revision > priorCatalogRevision)
            {
                invalidatedExerciseIds.UnionWith(exerciseIds);
            }
        }

        return invalidatedExerciseIds;
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
        IReadOnlySet<int> invalidatedExerciseIds =
            GetWorkoutStateInvalidationExerciseIds(state.CatalogRevision);

        string[] groupsWithRetiredSelections = state.SelectedExerciseIds
            .Where(selection => invalidatedExerciseIds.Contains(selection.Value))
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

        if (invalidatedExerciseIds.Contains(state.PendingScoreExerciseId))
        {
            state.PendingScoreExerciseId = 0;
            state.PendingScoreValue = 0;
        }

        state.CatalogRevision = CurrentCatalogRevision;
        return true;
    }
}
