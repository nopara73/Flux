using Flux.Models;

namespace Flux.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    private const string AlternatingPrefix = "Alternating ";
    public const int CurrentCatalogRevision = 17;
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
                [21] = new(
                    "Standing-Scale Balance",
                    "Standing-Scale Balance Hold"),
                [105] = new(
                    "Plie Squat",
                    "Wide Turned-Out Squat"),
                [188] = new(
                    "Parallel Demi-Plie",
                    "Narrow Turned-Out Shallow Squat"),
                [197] = new(
                    "First-Position Plie-Releve",
                    "Parallel Squat-to-Calf Raise"),
                [198] = new(
                    "Second-Position Plie-Releve",
                    "Wide Squat to Feet-Together Calf Raise"),
                [199] = new(
                    "Alternating Deep Side Lunge",
                    "Wide-Stance Side-to-Side Squat"),
                [255] = new(
                    "Standing Bent-Knee Calf Raise",
                    "Deep-Squat Calf Raise"),
                [145] = new(
                    "Standing Knee Extension",
                    "Standing Knee-Extension Hold"),
                [256] = new(
                    "Self-Resisted Overhead Pull",
                    "Self-Resisted Overhead Pull Hold"),
                [257] = new(
                    "Self-Resisted Chest-Level Pull",
                    "Self-Resisted Chest-Level Pull Hold"),
                [258] = new(
                    "Self-Resisted Low Pull",
                    "Self-Resisted Low Pull Hold"),
                [262] = new(
                    "Standing Hands-to-Thigh Abdominal Press",
                    "Standing Hands-to-Thigh Abdominal Press Hold"),
                [270] = new(
                    "Bodyweight Svend Press",
                    "Palm-Squeeze Forward Press"),
                [290] = new(
                    "Universe-in-Motion Qigong",
                    "Low Palm Scoop to Side Opening"),
                [231] = new(
                    "Karate Reverse Punch",
                    "Step-Through Karate Reverse Punch"),
                [394] = new(
                    "Standing Arms Open and Close",
                    "Inhale Arms Open, Exhale Arms Close and Round"),
                [395] = new(
                    "Standing Overhead Arm Sweep",
                    "Overhead Hold with Deep Ribcage Breaths"),
                [397] = new(
                    "Staggered-Stance Weight Shift",
                    "Exhale Forward, Inhale Back Weight Shift"),
                [398] = new(
                    "Standing Hug and Arm Expansion",
                    "Inhale Arms Open, Exhale Self-Hug and Fold"),
                [399] = new(
                    "Shallow Squat with Chest-Opening Arms",
                    "Inhale Chest Open, Exhale Arms Close with Shallow Squat"),
                [400] = new(
                    "Shallow Squat with Overhead Arm Circle",
                    "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down"),
                [401] = new(
                    "Alternating Weight Shift with Arm Swing",
                    "Alternating Inhale-Twist, Exhale-Push"),
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
                [396] = new(
                    "Unsupported Single-Leg Balance",
                    "Unsupported Single-Leg Balance Hold"),
                [510] = new(
                    "Clasped-Hands Chest-Opening Forward Fold",
                    "Clasped-Hands Chest-Opening Forward-Fold Hold"),
                [588] = new(
                    "Belly-Dance Alternating Shoulder Roll",
                    "Belly-Dance Alternating Shoulder Rolls"),
                [617] = new(
                    "Standing Side-Leg Circles",
                    "Standing Forward Side-Leg Circles"),
                [626] = new(
                    "Sumo Stance",
                    "Sumo Squat Hold"),
                [712] = new(
                    "Standing Arms-Back Chest Opener",
                    "Standing Arms-Back Chest-Opener Hold"),
                [969] = new(
                    "Chair-Pose Core Hold",
                    "Chair-Pose Hold"),
                [1000] = new(
                    "Standing Forward Fold",
                    "Standing Forward-Fold Hold"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalApprovedExerciseCorrectionPreviousNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [21] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Standing-Scale Balance",
                },
                [145] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Standing Knee Extension",
                },
                [394] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Open-and-Close Breathing",
                },
                [395] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Overhead Rib-Expansion Breathing",
                },
                [397] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Breath-Integrated Weight Shift",
                },
                [398] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Arm-Expansion Breathing",
                },
                [399] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Opening-the-Chest Breathing",
                },
                [400] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Separating-the-Clouds Breathing",
                },
                [401] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Alternating Swinging-Arms Breathing",
                },
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
                [195] = new(
                    "Lateral Lunge to Balance",
                    "Ballet Degage a la Seconde"),
                [211] = new(
                    "Open-Finger Wrist Extension",
                    "Karate Backfist Strike (Uraken-Uchi)"),
                [213] = new(
                    "Open-Finger Wrist Flexion",
                    "Karate Hammer-Fist Strike (Tetsui-Uchi)"),
                [214] = new(
                    "Neutral-Fist Wrist Flexion and Extension",
                    "Wing Chun Biu-Sau Palm Strike"),
                [215] = new(
                    "Up-and-Down Wrist Glides",
                    "Self-Resisted Wrist Radial-Deviation Pulses"),
                [216] = new(
                    "Side-to-Side Wrist Glides",
                    "Self-Resisted Wrist Ulnar-Deviation Pulses"),
                [217] = new(
                    "Bilateral Wrist Figure Eights",
                    "Self-Resisted Wrist-Extension Pulses"),
                [218] = new(
                    "Hook-to-Fist Tendon Glides",
                    "Self-Resisted Wrist-Flexion Pulses"),
                [232] = new(
                    "Palms-Down Fist Wrist Flexion and Extension",
                    "Karate Knife-Hand Chop"),
                [233] = new(
                    "Bilateral Wrist Circles",
                    "Karate Ridge-Hand Strike (Haito-Uchi)"),
                [234] = new(
                    "Opposite-Hand-Resisted Thumb Opposition Hold",
                    "Karate Flat-Fist Strike (Hiraken)"),
                [236] = new(
                    "Alternating Hand Open and Close",
                    "Karate Spear-Hand Strike (Nukite)"),
                [239] = new(
                    "Self-Resisted Finger Spread",
                    "Ninja Fireball Hand-Seal Sequence"),
                [240] = new(
                    "Self-Resisted Finger Squeeze",
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
                [283] = new(
                    "Sequential Finger Waves",
                    "Qigong Fist Rotation"),
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
                [513] = new(
                    "Standing Unilateral SCM Stretch",
                    "Scapular Retraction"),
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
                    "Behind-Back Wrist-Pull Neck Stretch",
                    "Standing Cobra Pose"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalPriorReviewedReplacementNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [234] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Palms-Up Fist Wrist Flexion and Extension",
                },
                [239] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Ninja Snake Hand-Seal Hold",
                },
                [240] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Ninja Ram Hand-Seal Hold",
                },
                [241] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb C Hold",
                    "Straight-Hand Knuckle-Bend Flow",
                },
                [289] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Adduction Hold",
                    "Alternating Thumb-to-Palm Tucks",
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
                [843] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Scalene Wrist-Anchor Stretch",
                },
            };

    private static readonly HashSet<int> ReplacedExerciseIdSet =
    [
        41, 56, 59, 98, 102, 116, 120, 126, 133, 135, 146, 159, 176, 177, 182, 183,
        185, 187, 191, 192, 193, 194, 195, 196, 199, 201, 203,
        211, 212, 213, 214,
        215, 216, 217, 218, 219, 223, 224, 225, 227, 228, 229, 230, 232, 233, 234, 236, 237,
        239, 240, 241, 242, 245, 246,
        260, 262, 267, 268, 272, 274, 275, 276, 280, 281, 283, 284, 285,
        286, 287, 288, 289, 291, 292, 293, 294, 295, 296, 326, 327, 338, 367, 390,
        391, 392, 393, 396, 422,
        423, 467, 474, 475, 477,
        481, 482, 483, 490, 491, 492, 493, 495, 497, 499, 500, 501,
        502, 503, 504, 505, 506, 507, 508, 509, 510, 512, 513, 572,
        573, 591, 609, 610, 611, 612, 613, 614, 615, 616, 618, 619, 625,
        636, 647, 649, 654, 677, 678, 681, 683, 684, 685, 686, 687, 712,
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
                [8] = new HashSet<int> { 211, 212, 213, 214, 232, 233, 234, 236 },
                [9] = new HashSet<int> { 195 },
                [10] = new HashSet<int> { 126, 135, 338, 686 },
                [11] = new HashSet<int>
                {
                    211, 213, 214, 215, 216, 217, 218, 232,
                    233, 234, 236, 237, 240, 241, 283, 289,
                },
                [12] = new HashSet<int> { 513, 843 },
                [13] = new HashSet<int> { 223, 224, 225, 245, 246 },
                [16] = new HashSet<int> { 234, 239, 240 },
            };

    private static readonly HashSet<int> ContinuousAlternationNormalizationIdSet =
    [
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
                bool approvedCorrectionMatches =
                    IsApprovedExerciseCorrection(
                        exerciseId,
                        stored.Name,
                        replacement.Name) &&
                    string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal);
                if (currentReviewedIdentityMatches || approvedCorrectionMatches)
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
                IsApprovedExerciseCorrection(
                    exerciseId,
                    stored.Name,
                    bundled.Name);
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

    private static bool IsApprovedExerciseCorrection(
        int exerciseId,
        string storedName,
        string bundledName) =>
        ApprovedExerciseCorrections.TryGetValue(
            exerciseId,
            out ApprovedExerciseCorrection? correction) &&
        (string.Equals(
                storedName,
                correction.PreviousName,
                StringComparison.Ordinal) ||
            (AdditionalApprovedExerciseCorrectionPreviousNames.TryGetValue(
                    exerciseId,
                    out IReadOnlySet<string>? additionalPreviousNames) &&
                additionalPreviousNames.Contains(storedName))) &&
        string.Equals(
            bundledName,
            correction.CurrentName,
            StringComparison.Ordinal);

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
        state.LastKeptExerciseIds ??= [];
        state.ActiveExtraSetSelectionGroupIds ??= [];
        state.ActiveFullSideSelectionGroupIds ??= [];
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
