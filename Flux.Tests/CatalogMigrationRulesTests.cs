using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogMigrationRulesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void AdditiveCatalogPreservesExistingIdentityMediaAndScore()
    {
        Exercise existing = Exercise(
            7,
            "Existing movement",
            "exercise_0007.mp4",
            score: 99);
        Exercise secondExisting = Exercise(
            8,
            "Second existing movement",
            "exercise_0008.mp4",
            score: -99);
        Exercise added = Exercise(31, "Added movement", "exercise_0031.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
            [secondExisting.Id] = new(
                secondExisting.Name,
                secondExisting.Video,
                6),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [existing, secondExisting, added],
            stored);

        Assert.Equal([existing.Id, secondExisting.Id], preserved.Order());
        Assert.Equal(-4, stored[existing.Id].Score);
        Assert.Equal(6, stored[secondExisting.Id].Score);
        Assert.Equal(99, existing.Score);
        Assert.Equal(-99, secondExisting.Score);
        Assert.Equal(0, added.Score);
        Assert.DoesNotContain(added.Id, preserved);
    }

    [Fact]
    public void MigrationRejectsRemovalOrDemonstrationReplacement()
    {
        Exercise existing = Exercise(7, "Existing movement", "exercise_0007.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
        };

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([], stored));

        Exercise changedMedia = Exercise(
            existing.Id,
            existing.Name,
            "replacement.mp4");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));

        Exercise changedName = Exercise(
            existing.Id,
            "Renamed movement",
            existing.Video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedName], stored));
    }

    [Fact]
    public void ReviewedReplacementIsRetiredInsteadOfPreservingItsScore()
    {
        const int replacedId = 56;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(
                "Retired movement",
                "exercise_0056.mp4",
                -7),
        };
        Exercise replacement = Exercise(
            replacedId,
            "Clear replacement movement",
            "exercise_0056.mp4",
            retiredName: "Retired movement");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.Equal(329, CatalogMigrationRules.ReplacedExerciseIds.Count);
        Assert.Contains(replacedId, CatalogMigrationRules.ReplacedExerciseIds);
        Assert.DoesNotContain(replacedId, preserved);
        Assert.Equal(-7, stored[replacedId].Score);
        Assert.Equal(0, replacement.Score);

        var preNormalizationStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(
                "Alternating Retired movement",
                replacement.Video,
                -6),
        };
        IReadOnlySet<int> preNormalizationPreserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                preNormalizationStored);
        Assert.DoesNotContain(replacedId, preNormalizationPreserved);
        Assert.Equal(-6, preNormalizationStored[replacedId].Score);

        Exercise wrongRetiredName = Exercise(
            replacedId,
            replacement.Name,
            replacement.Video,
            retiredName: "Some other retired movement");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongRetiredName],
                stored));

        Exercise missingRetiredName = Exercise(
            replacedId,
            replacement.Name,
            replacement.Video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [missingRetiredName],
                stored));

        Exercise wrongRetiredVideo = Exercise(
            replacedId,
            replacement.Name,
            "replacement.mp4",
            retiredName: "Retired movement");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongRetiredVideo],
                stored));
    }

    [Fact]
    public void RepeatedUpgradePreservesAnAlreadyReviewedReplacementAndItsScore()
    {
        const int replacedId = 56;
        const string video = "exercise_videos/exercise_0056.mp4";
        Exercise replacement = Exercise(
            replacedId,
            "Shibashi Shallow Squat with Arm Float",
            video,
            retiredName: "Hula Kāholo");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(replacement.Name, video, -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.Contains(replacedId, preserved);
        Assert.Equal(-7, stored[replacedId].Score);
    }

    [Theory]
    [InlineData(
        520,
        "Silent Vowel-Shape Sequence",
        "Mirror Facial-Expression Practice",
        "Scapular Clock")]
    [InlineData(
        521,
        "Smile-to-Neutral Transitions",
        "Smile at Yourself in the Mirror",
        "Scapular Figure Eight")]
    public void Version67MirrorReplacementsDiscardThePriorIdentityAndScore(
        int exerciseId,
        string storedName,
        string replacementName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:0000}.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            replacementName,
            video,
            retiredName: baselineRetiredName);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(storedName, video, -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void ReplacingRestoredExerciseAcceptsEveryReviewedIdentityWithoutItsScore()
    {
        const int exerciseId = 266;
        const string video = "exercise_videos/exercise_0266.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Alternating T-Arm Lifts",
            video,
            retiredName: "Standing Palms-Up Arm Raise");
        var replacedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Zyzz Diagonal-Reach Pose Hold",
                video,
                -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            replacedStored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, replacedStored[exerciseId].Score);

        var restoredStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Standing Palms-Up Arm Raise", video, -3),
        };
        preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            restoredStored);
        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-3, restoredStored[exerciseId].Score);

        var currentStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(replacement.Name, video, -2),
        };
        preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            currentStored);
        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-2, currentStored[exerciseId].Score);

        var unrelatedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Unrelated movement", video, -5),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                unrelatedStored));

        var wrongVideoStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Zyzz Diagonal-Reach Pose Hold",
                "different-video.mp4",
                -5),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                wrongVideoStored));
    }

    [Fact]
    public void ClarifiedStepThroughNamePreservesIdentityAndScore()
    {
        const int exerciseId = 231;
        const string video = "exercise_videos/exercise_0231.mp4";
        Exercise normalized = Exercise(
            exerciseId,
            "Step-Through Karate Reverse Punch",
            video);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Karate Reverse Punch", video, -3),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-3, stored[exerciseId].Score);
    }

    [Fact]
    public void ClarifiedStepThroughNameAcceptsHistoricalAlternatingIdentity()
    {
        const int exerciseId = 231;
        const string video = "exercise_videos/exercise_0231.mp4";
        Exercise normalized = Exercise(
            exerciseId,
            "Step-Through Karate Reverse Punch",
            video);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Alternating Karate Reverse Punch", video, -4),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-4, stored[exerciseId].Score);
    }

    [Fact]
    public void ReplacementAcceptsHistoricalUnprefixedRetiredIdentity()
    {
        const int exerciseId = 223;
        const string video = "exercise_videos/exercise_0223.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Self-Resisted Forearm Supination Hold",
            video,
            retiredName: "Alternating Karate Inside Block (Uchi-Uke)");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Karate Inside Block (Uchi-Uke)", video, -6),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-6, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(
        397,
        "Alternating Breath-Integrated Weight Shift",
        "Exhale Forward, Inhale Back Weight Shift")]
    [InlineData(
        617,
        "Alternating Standing Side-Leg Circles",
        "Standing Forward Side-Leg Circles")]
    public void ClarityCorrectionAcceptsHistoricalAlternatingIdentity(
        int exerciseId,
        string historicalName,
        string currentName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        Exercise corrected = Exercise(exerciseId, currentName, video);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(historicalName, video, -2),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-2, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(135, "Standing Snow Angels", "Mountain Pose to Upward Salute")]
    [InlineData(195, "Lateral Lunge to Balance", "Ballet Degage a la Seconde")]
    [InlineData(201, "Shibashi Split-Stance Rock and Palm Press", "Alternating Boxing Jab")]
    [InlineData(211, "Open-Finger Wrist Extension", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(212, "Bent-Over Triceps Pulse", "Karate Palm-Heel Strike (Teisho)")]
    [InlineData(213, "Open-Finger Wrist Flexion", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(214, "Neutral-Fist Wrist Flexion and Extension", "Wing Chun Biu-Sau Palm Strike")]
    [InlineData(215, "Up-and-Down Wrist Glides", "Self-Resisted Wrist Radial-Deviation Pulses")]
    [InlineData(216, "Side-to-Side Wrist Glides", "Self-Resisted Wrist Ulnar-Deviation Pulses")]
    [InlineData(217, "Bilateral Wrist Figure Eights", "Self-Resisted Wrist-Extension Pulses")]
    [InlineData(218, "Hook-to-Fist Tendon Glides", "Self-Resisted Wrist-Flexion Pulses")]
    [InlineData(223, "Self-Resisted Forearm Supination Hold", "Alternating Karate Inside Block (Uchi-Uke)")]
    [InlineData(224, "Opposite-Hand-Resisted Multi-Direction Wrist Hold", "Alternating Karate Downward Sweep Block (Gedan-Barai)")]
    [InlineData(232, "Palms-Down Fist Wrist Flexion and Extension", "Karate Knife-Hand Chop")]
    [InlineData(233, "Bilateral Wrist Circles", "Karate Ridge-Hand Strike (Haito-Uchi)")]
    [InlineData(234, "Opposite-Hand-Resisted Thumb Opposition Hold", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(236, "Alternating Hand Open and Close", "Karate Spear-Hand Strike (Nukite)")]
    [InlineData(237, "Opposed Thumb-and-Index Extension Isometric", "Forearm Pronation and Supination")]
    [InlineData(239, "Self-Resisted Finger Spread", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(240, "Self-Resisted Finger Squeeze", "Ninja Shadow-Possession Hand-Seal Sequence")]
    [InlineData(241, "Ninja Monkey Hand-Seal Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(242, "Ninja Boar Hand-Seal Hold", "Ninja Shadow-Clone Hand-Seal Sequence")]
    [InlineData(245, "Opposite-Hand-Resisted Elbow-Flexion Hold", "Alternating Karate Rising Block (Age-Uke)")]
    [InlineData(256, "Bent-Over Straight-Arm Lat Sweeps", "Self-Resisted Overhead Pull Hold")]
    [InlineData(257, "Karate Knife-Hand Block", "Self-Resisted Chest-Level Pull Hold")]
    [InlineData(260, "Standing Triceps Kickbacks", "Behind-the-Back Self-Resisted Press")]
    [InlineData(266, "Alternating T-Arm Lifts", "Standing Palms-Up Arm Raise")]
    [InlineData(267, "Floor Touch to Calf Raise", "T-Position Shoulder Rotation")]
    [InlineData(268, "Self-Resisted External-Rotation Push-Out", "Self-Resisted External-Rotation Isometric")]
    [InlineData(269, "C-Rotation Arm Curls", "Self-Resisted Curl-and-Press")]
    [InlineData(270, "Goalpost Elbow Open-and-Close", "Palm-Squeeze Forward Press")]
    [InlineData(274, "Side-Step Alternating High Curl", "Dynamic-Resistance Lat Pulldown")]
    [InlineData(276, "Alternating Diagonal Overhead Reach-and-Pull", "Dynamic-Resistance High Chest Press")]
    [InlineData(280, "Alternating Forward-and-Side Arm Press", "Ringing-the-Towel Wrist Inversion")]
    [InlineData(283, "Sequential Finger Waves", "Qigong Fist Rotation")]
    [InlineData(289, "Ninja Horse Hand-Seal Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Ninja Tiger Hand-Seal Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Ninja Dragon Hand-Seal Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Ninja Rat Hand-Seal Hold", "Tiger-Claw Grip Flow")]
    [InlineData(414, "Heel Raises with Fixed-Thumb Head Turns", "Ear-to-Shoulder Glide")]
    [InlineData(415, "Heel Raises with Fixed-Thumb Head Nods", "Chin-to-Collarbone Turn")]
    [InlineData(416, "Heel Raises with Fixed-Thumb Head Tilts", "Diagonal Head Tilt")]
    [InlineData(418, "Heel-Bounce Horizontal Thumb Tracking", "Forward-and-Back Head Translation")]
    [InlineData(419, "Heel-Bounce Vertical Thumb Tracking", "Occipital Nod")]
    [InlineData(482, "Front Half Neck Circles", "Continuous Spot-Turn Drill")]
    [InlineData(483, "Clockwise Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(490, "Assisted Cheek Lift", "Bharatanatyam Alolita Shiro")]
    [InlineData(491, "Cheek-Firming Air Hold", "Bharatanatyam Dhuta Shiro")]
    [InlineData(492, "Forehead Knuckle Massage", "Bharatanatyam Kampita Shiro")]
    [InlineData(493, "Face-and-Neck Lymphatic Sweep", "Alternating Bharatanatyam Paravritta Shiro")]
    [InlineData(495, "Jawline Knuckle Massage", "Bharatanatyam Parivahita Shiro")]
    [InlineData(499, "Eyebrow Pinch Massage", "Bharatanatyam Tiraschina Griva")]
    [InlineData(500, "Eye-Socket Finger Circles", "Bharatanatyam Parivartita Griva")]
    [InlineData(501, "Counterclockwise Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(505, "Temple Circle Massage", "Maximal Smile and Relax")]
    [InlineData(506, "Cheek Pinch Massage", "Eyebrow Raise and Relax")]
    [InlineData(508, "Diagonal Arm Reach-to-Row", "Tongue Protrusion and Retraction")]
    [InlineData(512, "Upper-Cervical Erector Stretch", "Scapular Protraction")]
    [InlineData(513, "Standing Unilateral SCM Stretch", "Scapular Retraction")]
    [InlineData(572, "Wide-Stance Bent-Knee Rotational Stretch", "Tai Chi White Crane Opens Wings")]
    [InlineData(591, "Standing Speed-Bag Punches", "Bharatanatyam Natyarambhe Hold")]
    [InlineData(611, "Warrior II-Stance Hip Circles", "Pelvic-Floor Heel-Raise Lift")]
    [InlineData(636, "Alternating Curtsy Floor Reach", "Deadlift Kickback")]
    [InlineData(649, "Standing Clamshell", "Standing Side-Leg Raise")]
    [InlineData(677, "T-Arm Side-to-Side Sweep", "Alternating Belly-Dance Hip Drop")]
    [InlineData(681, "Rear-Arm Sweep to Front Squeeze", "Belly-Dance Horizontal Figure Eight")]
    [InlineData(743, "Standing Backward Arm Circles", "Clasped-Hands-Behind-Back Chest Opener")]
    [InlineData(745, "Standing Overhead Presses", "Dynamic Hug")]
    [InlineData(843, "Behind-Back Wrist-Pull Neck Stretch", "Standing Cobra Pose")]
    public void SecondGenerationReplacementAcceptsImmediatelyPriorIdentity(
        int replacedId,
        string priorName,
        string baselineRetiredName)
    {
        string video = $"exercise_{replacedId:D4}.mp4";
        Exercise replacement = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: baselineRetiredName);

        foreach (string storedName in
            new[] { priorName, $"Alternating {priorName}" })
        {
            var stored = new Dictionary<int, StoredExerciseSnapshot>
            {
                [replacedId] = new(storedName, video, -7),
            };

            IReadOnlySet<int> preserved =
                CatalogMigrationRules.ValidatePreservedCatalog(
                    [replacement],
                    stored);

            Assert.DoesNotContain(replacedId, preserved);
            Assert.Equal(-7, stored[replacedId].Score);
        }
    }

    [Fact]
    public void SecondGenerationReplacementStillRequiresBaselineAndStableVideo()
    {
        const int replacedId = 291;
        const string priorName = "Ninja Tiger Hand-Seal Hold";
        const string baselineRetiredName = "Black Dragon Enters the Cave";
        const string video = "exercise_0291.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(priorName, video, -5),
        };

        Exercise wrongBaseline = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: "Unrelated baseline");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongBaseline],
                stored));

        Exercise wrongVideo = Exercise(
            replacedId,
            "Second-generation replacement",
            "replacement.mp4",
            retiredName: baselineRetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongVideo],
                stored));

        var unrelatedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new("Unrelated prior name", video, -5),
        };
        Exercise validReplacement = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: baselineRetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [validReplacement],
                unrelatedStored));
    }

    [Fact]
    public void CatalogRevisionDropsChangedTransientReferencesButPreservesKeepMarkers()
    {
        const int replacedId = 223;
        int[] latestReplacementIds =
        [
            211, 213, 214, 215, 218, 223, 224, 225, 234,
            236, 237, 239, 240, 241, 242, 245, 246, 283, 289,
        ];
        const int historicalReplacementId = 56;
        const int retainedId = 22;
        const string replacedGroup = "group.replaced";
        const string historicalReplacementGroup = "group.historical";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 12,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [replacedGroup] = replacedId,
                [historicalReplacementGroup] = historicalReplacementId,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [replacedGroup] = ExerciseOutcome.X,
                [historicalReplacementGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = replacedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = replacedId,
            PendingScoreValue = -8,
            LastKeptExerciseIds = [.. latestReplacementIds, historicalReplacementId, retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        Assert.DoesNotContain(replacedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(replacedGroup, state.Outcomes);
        Assert.Equal(
            historicalReplacementId,
            state.SelectedExerciseIds[historicalReplacementGroup]);
        Assert.Equal(
            ExerciseOutcome.Tick,
            state.Outcomes[historicalReplacementGroup]);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.All(latestReplacementIds, exerciseId =>
            Assert.Contains(exerciseId, state.LastKeptExerciseIds));
        Assert.Contains(historicalReplacementId, state.LastKeptExerciseIds);
        Assert.Contains(retainedId, state.LastKeptExerciseIds);

        state.SelectedExerciseIds[replacedGroup] = replacedId;
        state.PendingScoreExerciseId = replacedId;
        state.PendingScoreValue = -1;

        Assert.False(CatalogMigrationRules.ReconcileWorkoutState(state));
        Assert.Equal(replacedId, state.SelectedExerciseIds[replacedGroup]);
        Assert.Equal(replacedId, state.PendingScoreExerciseId);
        Assert.Equal(-1, state.PendingScoreValue);
    }

    [Fact]
    public void LegacyCatalogRevisionStillDropsAllHistoricalReplacements()
    {
        const int historicalReplacementId = 56;
        const string groupId = "group.historical";
        var state = new WorkoutState
        {
            CatalogRevision = 2,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = historicalReplacementId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.X,
            },
            PendingScoreExerciseId = historicalReplacementId,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MigrationAllowsOnlyExactAlternatingPrefixRemovalForTimedSides()
    {
        const string video = "exercise_0007.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [7] = new("Alternating Side Stretch", video, -4),
        };
        Exercise normalized = Exercise(
            7,
            "Side Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(7, preserved);
        Assert.Equal(-4, stored[7].Score);

        Exercise continuous = Exercise(7, "Side Stretch", video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([continuous], stored));

        Exercise arbitrary = Exercise(
            7,
            "Different Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([arbitrary], stored));

        Exercise changedMedia = Exercise(
            7,
            "Side Stretch",
            "replacement.mp4",
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));
    }

    [Fact]
    public void TimedSideNormalizationPreservesAlreadyReviewedReplacement()
    {
        const int exerciseId = 845;
        const string video = "exercise_videos/exercise_0845.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Alternating Overhead Side Stretch",
                video,
                -7),
        };
        Exercise normalized = Exercise(
            exerciseId,
            "Overhead Side Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft,
            retiredName: "Extended-Mountain Backline Reach and Lower");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);

        Exercise continuous = Exercise(
            exerciseId,
            normalized.Name,
            video,
            retiredName: normalized.RetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([continuous], stored));

        Exercise changedMedia = Exercise(
            exerciseId,
            normalized.Name,
            "replacement.mp4",
            sideSequence: normalized.SideSequence,
            retiredName: normalized.RetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));
    }

    [Fact]
    public void ReviewedExternalRotationReplacementRetiresTheCorrectedPriorIdentity()
    {
        const string video = "exercise_0268.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [268] = new(
                "Self-Resisted External-Rotation Push-Out",
                video,
                -3),
        };
        Exercise replacement = Exercise(
            268,
            "Thumbs-Up Diagonal Arm Raises",
            video,
            retiredName: "Self-Resisted External-Rotation Isometric");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(268, preserved);
        Assert.Equal(-3, stored[268].Score);

        Exercise wrongId = Exercise(
            266,
            replacement.Name,
            video,
            retiredName: "Self-Resisted External-Rotation Isometric");
        var wrongStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [266] = new(
                "Self-Resisted External-Rotation Push-Out",
                video,
                -3),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongId],
                wrongStored));
    }

    [Theory]
    [InlineData(21, "Standing-Scale Balance", "Standing-Scale Balance Hold")]
    [InlineData(105, "Plie Squat", "Wide Turned-Out Squat")]
    [InlineData(119, "Squat to Calf Raise", "Tiptoe Walk")]
    [InlineData(139, "Wide-Squat Heel Raise", "Wide-Squat Alternating Heel Raises")]
    [InlineData(188, "Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat")]
    [InlineData(197, "First-Position Plie-Releve", "Parallel Squat-to-Calf Raise")]
    [InlineData(198, "Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise")]
    [InlineData(199, "Alternating Deep Side Lunge", "Wide-Stance Side-to-Side Squat")]
    [InlineData(255, "Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise")]
    [InlineData(145, "Standing Knee Extension", "Standing Knee-Extension Hold")]
    [InlineData(256, "Self-Resisted Overhead Pull", "Self-Resisted Overhead Pull Hold")]
    [InlineData(257, "Self-Resisted Chest-Level Pull", "Self-Resisted Chest-Level Pull Hold")]
    [InlineData(258, "Self-Resisted Low Pull", "Self-Resisted Low Pull Hold")]
    [InlineData(262, "Standing Hands-to-Thigh Abdominal Press", "Standing Hands-to-Thigh Abdominal Press Hold")]
    [InlineData(270, "Bodyweight Svend Press", "Palm-Squeeze Forward Press")]
    [InlineData(290, "Universe-in-Motion Qigong", "Low Palm Scoop to Side Opening")]
    [InlineData(394, "Standing Arms Open and Close", "Inhale Arms Open, Exhale Arms Close and Round")]
    [InlineData(395, "Standing Overhead Arm Sweep", "Overhead Hold with Deep Ribcage Breaths")]
    [InlineData(397, "Staggered-Stance Weight Shift", "Exhale Forward, Inhale Back Weight Shift")]
    [InlineData(398, "Standing Hug and Arm Expansion", "Inhale Arms Open, Exhale Self-Hug and Fold")]
    [InlineData(399, "Shallow Squat with Chest-Opening Arms", "Inhale Chest Open, Exhale Arms Close with Shallow Squat")]
    [InlineData(400, "Shallow Squat with Overhead Arm Circle", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down")]
    [InlineData(401, "Alternating Weight Shift with Arm Swing", "Alternating Inhale-Twist, Exhale-Push")]
    [InlineData(402, "Shibashi Rowing-a-Boat Breathing", "Shallow Squat with Rowing Arm Circle")]
    [InlineData(403, "Shibashi Alternating Pushing-Palms Breathing", "Alternating Weight Shift with Palm Push")]
    [InlineData(404, "Shibashi Alternating Punch Breathing", "Wide-Stance Alternating Slow Punch")]
    [InlineData(405, "Shibashi Flying-Wild-Goose Breathing", "Shallow Squat with Wing Arm Raise")]
    [InlineData(406, "Shibashi Spinning-Wheels Breathing", "Standing Wheel Arm Circles")]
    [InlineData(409, "Neck Controlled Articular Rotation", "Full Neck Circles")]
    [InlineData(425, "Chin-Tuck Isometric", "Chin-Tuck Hold")]
    [InlineData(396, "Unsupported Single-Leg Balance", "Unsupported Single-Leg Balance Hold")]
    [InlineData(510, "Clasped-Hands Chest-Opening Forward Fold", "Clasped-Hands Chest-Opening Forward-Fold Hold")]
    [InlineData(588, "Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls")]
    [InlineData(617, "Standing Side-Leg Circles", "Standing Forward Side-Leg Circles")]
    [InlineData(626, "Sumo Stance", "Sumo Squat Hold")]
    [InlineData(712, "Standing Arms-Back Chest Opener", "Standing Arms-Back Chest-Opener Hold")]
    [InlineData(969, "Chair-Pose Core Hold", "Chair-Pose Hold")]
    [InlineData(1000, "Standing Forward Fold", "Standing Forward-Fold Hold")]
    [InlineData(136, "Goddess Pose", "Wide Turned-Out Squat Hold")]
    [InlineData(225, "Clenched-Fist Wrist Extensor Stretch", "Opposite-Hand Fist-Down Wrist Stretch")]
    [InlineData(241, "Hook-Fist Tendon Glide", "Open Hand to Hook Fist")]
    [InlineData(242, "Full-Fist Tendon Glide", "Open Hand to Full Fist")]
    [InlineData(248, "Side-Tap Palm Pushes", "Alternating Side-Tap Palm Pushes")]
    [InlineData(283, "Straight-Fist Tendon Glide", "Open Hand to Straight Fist")]
    [InlineData(291, "Open-to-Claw Tendon Glide", "Open Hand to Claw Fist")]
    [InlineData(293, "Finger-Web Space Stretch", "Opposite-Hand Finger-Web Stretches")]
    [InlineData(683, "Alternating Palm-Up T-Arm Flips", "Alternating Palm-Up Shoulder Rotations")]
    [InlineData(214, "Forward Wrist Circles", "Inward Wrist Circles")]
    [InlineData(223, "Forward Controlled Wrist Circles", "Inward Controlled Wrist Circles")]
    [InlineData(755, "Reverse Wrist Circles", "Outward Wrist Circles")]
    [InlineData(756, "Reverse Controlled Wrist Circles", "Outward Controlled Wrist Circles")]
    [InlineData(758, "Reverse Knee-and-Ankle Circles", "Backward Knee-and-Ankle Circles")]
    [InlineData(94, "Mirror-Guided Lateral Weight Shift", "Lateral Weight Shift")]
    [InlineData(95, "Mirror-Guided Single-Leg Pelvic Control", "Single-Leg Pelvic Control")]
    [InlineData(99, "Mirror-Guided Bent-Knee Front-to-Back Leg Swing", "Bent-Knee Front-to-Back Leg Swing")]
    [InlineData(100, "Mirror-Guided Bent-Knee Leg Swing with Pause", "Bent-Knee Leg Swing with Pause")]
    [InlineData(497, "Mirror-Guided Eyebrow Raise", "Eyebrow Raise")]
    [InlineData(498, "Mirror-Guided Firm Eye Closure", "Firm Eye Closure")]
    [InlineData(500, "Controlled Jaw Open and Close", "Straight Jaw Opening")]
    [InlineData(500, "Mirror-Guided Straight Jaw Opening", "Straight Jaw Opening")]
    [InlineData(511, "Mirror-Guided Lip Pucker", "Lip Pucker")]
    [InlineData(514, "Mirror-Guided Symmetric Smile", "Symmetric Smile")]
    public void MigrationAllowsReviewedClarityCorrectionWithoutResettingScore(
        int exerciseId,
        string previousName,
        string correctedName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -4),
        };
        Exercise corrected = Exercise(exerciseId, correctedName, video);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-4, stored[exerciseId].Score);
    }

    [Fact]
    public void LatestCatalogRevisionDropsEveryChangedExerciseReference()
    {
        const int replacedId = 212;
        const int retainedId = 101;
        const string replacedGroup = "group.replaced";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 17,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [replacedGroup] = replacedId,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [replacedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = replacedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestMillisecondsRemaining = 8_000,
            PendingRestPausedByUser = true,
            PendingRestKept = true,
            PendingScoreExerciseId = replacedId,
            PendingScoreValue = -2,
            LastKeptExerciseIds = [replacedId, retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(replacedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(replacedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(0, state.PendingRestMillisecondsRemaining);
        Assert.False(state.PendingRestPausedByUser);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Contains(replacedId, state.LastKeptExerciseIds);
        Assert.Contains(retainedId, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void CatalogRevisionClearsProfiledRepeatedRoundProgressForReplacement()
    {
        const int replacedId = 420;
        const string storageKey = "p2|r30.hip-abductors";
        const string firstRound = "r30.hip-abductors.set1";
        const string repeatedRound = "r30.hip-abductors.set2";
        var state = new WorkoutState
        {
            CatalogRevision = 23,
            ActiveWorkoutMinutes = 60,
            ActiveWorkoutModifiers = WorkoutModifiers.Silence,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [storageKey] = replacedId,
                ["p2|r30.chest"] = 281,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [firstRound] = ExerciseOutcome.Tick,
                [repeatedRound] = ExerciseOutcome.X,
                ["r30.chest"] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = firstRound,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(storageKey, state.SelectedExerciseIds);
        Assert.DoesNotContain(firstRound, state.Outcomes);
        Assert.DoesNotContain(repeatedRound, state.Outcomes);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes["r30.chest"]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
    }

    [Fact]
    public void CatalogRevisionPreservesActiveProgressWhenOnlyInactiveProfileIsRetired()
    {
        const string activeStorageKey = "p1|r30.hip-abductors";
        const string inactiveStorageKey = "p2|r30.hip-abductors";
        const string activeRound = "r30.hip-abductors.set1";
        var state = new WorkoutState
        {
            CatalogRevision = 23,
            ActiveWorkoutMinutes = 45,
            ActiveWorkoutModifiers = WorkoutModifiers.Insect,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [activeStorageKey] = 281,
                [inactiveStorageKey] = 420,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [activeRound] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = activeRound,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(281, state.SelectedExerciseIds[activeStorageKey]);
        Assert.DoesNotContain(inactiveStorageKey, state.SelectedExerciseIds);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[activeRound]);
        Assert.Equal(activeRound, state.PendingRestGroupId);
        Assert.Equal(123456, state.PendingRestEndsAtUnixMilliseconds);
        Assert.True(state.PendingRestKept);
    }

    [Theory]
    [InlineData(115)]
    [InlineData(119)]
    [InlineData(140)]
    [InlineData(260)]
    [InlineData(326)]
    [InlineData(340)]
    [InlineData(512)]
    [InlineData(649)]
    public void LatestCatalogRevisionDropsOtherChangedExerciseReferences(
        int changedExerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 17,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = changedExerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void LatestCatalogRevisionResetsOnlySemanticReplacementScores()
    {
        Assert.Equal(
            new HashSet<int> { 115, 212, 260, 512, 649 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[18]);

        Assert.DoesNotContain(119, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(140, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(326, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(340, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
    }

    [Fact]
    public void UnclearExerciseReplacementRevisionResetsEveryChangedScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                211, 213, 214, 215, 218, 223, 224,
                236, 237, 241, 242, 245, 283, 289,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[20]);
    }

    [Fact]
    public void CatalogClarityResetRevisionResetsEveryReplacedIdentity()
    {
        Assert.Equal(
            new HashSet<int>
            {
                15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
                179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
                256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
                283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
                394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
                615, 618, 677, 683, 685, 745, 816, 834,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[21]);
    }

    [Fact]
    public void ReviewerAuditRevisionResetsOnlySemanticReplacementScores()
    {
        Assert.Equal(
            new HashSet<int>
            {
                117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
                263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[22]);
    }

    [Fact]
    public void ModifierCoverageRevisionResetsEveryNewReplacementScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[23]);
    }

    [Fact]
    public void SilenceCatalogRevisionResetsEveryNoisyReplacementScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[24]);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(219)]
    [InlineData(248)]
    [InlineData(282)]
    [InlineData(390)]
    [InlineData(394)]
    [InlineData(395)]
    [InlineData(397)]
    [InlineData(508)]
    [InlineData(576)]
    [InlineData(577)]
    [InlineData(618)]
    [InlineData(816)]
    [InlineData(834)]
    public void UnilateralTimingRevisionRebuildsWorkoutButPreservesScore(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 24,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 25);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(282)]
    [InlineData(391)]
    [InlineData(507)]
    [InlineData(508)]
    [InlineData(577)]
    public void IllustrationCorrectionRevisionRebuildsWorkoutButPreservesScore(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 25,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 26);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(231)]
    [InlineData(685)]
    [InlineData(687)]
    public void KarateDemonstrationCorrectionRevisionRebuildsWorkout(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 26,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.Equal(
            new HashSet<int> { 687 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[27]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(239, "Straight-Finger Knuckle Bends", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(326, "Staggered-Stance Jab-Cross", "Wide-Stance Alternating Straight Punches")]
    [InlineData(687, "Karate Middle Side Punch", "Belly-Dance Hip Shimmy")]
    [InlineData(251, "Arm Sweep to Forward Hinge", "Waiter's Bow")]
    [InlineData(251, "Standing Swan-Dive Hinge", "Waiter's Bow")]
    public void CatalogClarityResetAcceptsReviewedPreviousIdentityAndResetsIt(
        int exerciseId,
        string previousName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -7),
        };
        Exercise replacement = Exercise(
            exerciseId,
            "Clear replacement",
            video,
            retiredName: baselineRetiredName);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);

        stored[exerciseId] = new(previousName, "wrong.mp4", -7);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([replacement], stored));
    }

    [Fact]
    public void ForwardFoldReplacementRevisionRebuildsWorkoutAndResetsScore()
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 27,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 251,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.Equal(
            new HashSet<int> { 251 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[28]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void ReactivatedReplacementRevisionDropsOnlyChangedProgressAndScores()
    {
        int[] changedIds =
        [
            435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
            446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
            457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
            469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
            486, 487, 488, 489, 494, 496, 517, 518, 519,
        ];
        var expectedIds = changedIds.ToHashSet();

        Assert.Equal(
            expectedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[29]);
        Assert.All(changedIds, exerciseId =>
            Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds));

        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 28,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = changedIds[0],
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = changedIds[0],
            PendingScoreValue = -4,
            LastKeptExerciseIds = [changedIds[0], retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal([changedIds[0], retainedId], state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);

        var stored = changedIds.ToDictionary(
            exerciseId => exerciseId,
            exerciseId => new StoredExerciseSnapshot(
                $"Retired {exerciseId}",
                $"exercise_videos/exercise_{exerciseId:D4}.mp4",
                -exerciseId));
        stored[retainedId] = new(
            "Retained movement",
            "exercise_videos/exercise_0022.mp4",
            -7);
        Exercise[] bundled =
        [
            .. changedIds.Select(exerciseId => Exercise(
                exerciseId,
                $"Replacement {exerciseId}",
                $"exercise_videos/exercise_{exerciseId:D4}.mp4",
                retiredName: $"Retired {exerciseId}")),
            Exercise(
                retainedId,
                "Retained movement",
                "exercise_videos/exercise_0022.mp4"),
        ];

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            bundled,
            stored);

        Assert.DoesNotContain(changedIds, preserved.Contains);
        Assert.Contains(retainedId, preserved);
    }

    [Fact]
    public void MediaRepairRevisionRetiresInvalidAssetsAndResetsSemanticScores()
    {
        int[] changedIds =
        [
            229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
            501, 504, 513, 516,
        ];
        int[] semanticIds = [229, 497, 501, 504, 513];
        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 29,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 467,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 501,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            semanticIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[30]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        Assert.All(changedIds, exerciseId =>
            Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds));
    }

    [Fact]
    public void MediaOnlyRepairPreservesPendingScoreRecovery()
    {
        var state = new WorkoutState
        {
            CatalogRevision = 29,
            PendingScoreExerciseId = 467,
            PendingScoreValue = -3,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(467, state.PendingScoreExerciseId);
        Assert.Equal(-3, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HeelIllustrationCorrectionRevisionResetsChangedWorkoutAndPendingScore()
    {
        int[] changedIds = [414, 415, 416, 418, 419];
        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 30,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 414,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 419,
            PendingScoreValue = -4,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[31]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void SingleSideClarityRevisionResetsCorrectedReplacements()
    {
        int[] correctedReplacementIds = [31, 219, 395, 507, 577, 618, 654, 834];
        const string kneePullGroup = "group.knee-pull";
        const string highKneeReachGroup = "group.high-knee-reach";
        const string nameOnlyGroup = "group.name-only";
        var state = new WorkoutState
        {
            CatalogRevision = 31,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [kneePullGroup] = 31,
                [highKneeReachGroup] = 618,
                [nameOnlyGroup] = 915,
            },
            PendingScoreExerciseId = 31,
            PendingScoreValue = -3,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(kneePullGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(highKneeReachGroup, state.SelectedExerciseIds);
        Assert.Equal(915, state.SelectedExerciseIds[nameOnlyGroup]);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            correctedReplacementIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[32]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void DirectionSplitRevisionResetsEveryLinkedIdentity()
    {
        int[] linkedDirectionIds =
        [
            214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
            755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
        ];
        const int retainedId = 22;
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 32,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 264,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 264,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            linkedDirectionIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[33]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void AlternatingCorrectionRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [98, 390, 508, 576, 816];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 33,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 576,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 576,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(576, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[34]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(34));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HighKneeAlternationCorrectionRebuildsWorkoutWithoutResettingScore()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 34,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 219,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 219,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(219, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 219 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[35]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(35));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void VagueElbowStrikeReplacementRebuildsWorkoutAndResetsScore()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 35,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 684,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 684,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 684 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[36]);
        Assert.Equal(
            new HashSet<int> { 684 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[36]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void AlternatingLoopCorrectionsRebuildWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [31, 176, 195, 391, 413, 884, 885];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 36,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[37]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(37));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void DirectionNameCorrectionPreservesActiveWorkoutState()
    {
        const string groupId = "direction.group";
        var state = new WorkoutState
        {
            CatalogRevision = 37,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 223,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = groupId,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 223,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(223, state.SelectedExerciseIds[groupId]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[groupId]);
        Assert.Equal(groupId, state.PendingRestGroupId);
        Assert.Equal(123456, state.PendingRestEndsAtUnixMilliseconds);
        Assert.True(state.PendingRestKept);
        Assert.Equal(223, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(38));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(38));
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(39));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(39));
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(40));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(40));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MirrorOnlyCorrectionDropsStaleSelectionButPreservesPendingScore()
    {
        const string groupId = "mirror.group";
        var state = new WorkoutState
        {
            CatalogRevision = 40,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 500,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = groupId,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 500,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(500, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 500 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[41]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(41));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MirrorRelationshipAndMuscleCorrectionsRebuildWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [105, 107, 108, 245, 280, 591, 884, 885, 905];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 41,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[42]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(42));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void GenuineMirrorPracticeRevisionRetiresDuplicateButPreservesCorrectedScores()
    {
        int[] changedIds = [90, 94, 95, 99, 100, 497, 498, 500, 511, 514];
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[44]);
        Assert.Equal(
            new HashSet<int> { 90 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[44]);

        const string correctedGroup = "mirror.corrected";
        var correctedState = new WorkoutState
        {
            CatalogRevision = 43,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [correctedGroup] = 94,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [correctedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = correctedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 94,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(correctedState));

        Assert.DoesNotContain(correctedGroup, correctedState.SelectedExerciseIds);
        Assert.DoesNotContain(correctedGroup, correctedState.Outcomes);
        Assert.Null(correctedState.PendingRestGroupId);
        Assert.Equal(94, correctedState.PendingScoreExerciseId);
        Assert.Equal(-4, correctedState.PendingScoreValue);
        Assert.Equal(
            CatalogMigrationRules.CurrentCatalogRevision,
            correctedState.CatalogRevision);

        var retiredState = new WorkoutState
        {
            CatalogRevision = 43,
            PendingScoreExerciseId = 90,
            PendingScoreValue = -6,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(retiredState));
        Assert.Equal(0, retiredState.PendingScoreExerciseId);
        Assert.Equal(0, retiredState.PendingScoreValue);
    }

    [Fact]
    public void CompleteDirectionRevisionRetiresDuplicatesAndPreservesRelinkedSideLegScore()
    {
        int[] workoutIds =
        [
            264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
            757, 759, 760, 761, 762, 763, 764,
        ];
        int[] scoreIds =
        [
            264, 275, 406, 409, 460, 588, 608, 611, 743,
            757, 759, 760, 761, 762, 763, 764,
        ];
        Assert.Equal(
            workoutIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[45]);
        Assert.Equal(
            scoreIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[45]);

        var changedState = new WorkoutState
        {
            CatalogRevision = 44,
            PendingScoreExerciseId = 409,
            PendingScoreValue = -4,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(changedState));
        Assert.Equal(0, changedState.PendingScoreExerciseId);
        Assert.Equal(0, changedState.PendingScoreValue);

        var relinkedState = new WorkoutState
        {
            CatalogRevision = 44,
            PendingScoreExerciseId = 617,
            PendingScoreValue = -2,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(relinkedState));
        Assert.Equal(617, relinkedState.PendingScoreExerciseId);
        Assert.Equal(-2, relinkedState.PendingScoreValue);
    }

    [Fact]
    public void LeadStanceTimingRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] leadStanceIds =
        [
            265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
        ];
        Assert.Equal(
            leadStanceIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[46]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(46));

        const string changedGroup = "lead-stance.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 45,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void UnilateralSetupCorrectionRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [198, 398, 421, 427, 468, 512, 515];
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[47]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(47));

        const string changedGroup = "unilateral-setup.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 46,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 512,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 512,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(512, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void PermanentlyRetiredExercisesMayBeRemovedButCannotReturn()
    {
        Assert.Equal(
            new HashSet<int> { 90, 229, 757, 759, 760, 761, 762, 763, 764 },
            CatalogMigrationRules.PermanentlyRetiredExerciseIds);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [90] = new(
                "Mirror-Guided Bodyweight Squat",
                "exercise_videos/exercise_0090.mp4",
                -6),
            [229] = new(
                "Alternating Boxing Jabs",
                "exercise_videos/exercise_0229.mp4",
                -4),
            [22] = new(
                "Retained movement",
                "exercise_videos/exercise_0022.mp4",
                -2),
        };
        Exercise retained = Exercise(
            22,
            "Retained movement",
            "exercise_videos/exercise_0022.mp4");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog([retained], stored);

        Assert.Equal(new HashSet<int> { 22 }, preserved);
        Exercise restoredRetired = Exercise(
            229,
            "Invalid restoration",
            "exercise_videos/exercise_0229.mp4",
            retiredName: "Alternating Boxing Uppercut");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [retained, restoredRetired],
                stored));
        Exercise restoredSquat = Exercise(
            90,
            "Invalid squat restoration",
            "exercise_videos/exercise_0090.mp4",
            retiredName: "Alternating Step Pivot");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [retained, restoredSquat],
                stored));
    }

    [Theory]
    [InlineData("Forehead Finger Sweep", "exercise_videos/exercise_0497.mp4")]
    [InlineData("Odissi Sundari Griva", "exercise_direction_videos/exercise_0497.mp4")]
    [InlineData("Track Finger in Circles", "exercise_direction_videos/exercise_0497.mp4")]
    public void RestoredIdDiscardsExactHistoricalIdentityAndScore(
        string oldName,
        string oldVideo)
    {
        const int restoredId = 497;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [restoredId] = new(oldName, oldVideo, -7),
        };
        Exercise replacement = Exercise(
            restoredId,
            "Eyebrow Raise",
            "exercise_videos/exercise_0497.mp4",
            retiredName: "Odissi Sundari Griva");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(restoredId, preserved);
        Assert.Equal(-7, stored[restoredId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void RestoredIdRejectsUnknownHistoricalIdentity()
    {
        const int restoredId = 497;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [restoredId] = new(
                "Unverified old movement",
                "exercise_direction_videos/exercise_0497.mp4",
                -7),
        };
        Exercise replacement = Exercise(
            restoredId,
            "Eyebrow Raise",
            "exercise_videos/exercise_0497.mp4",
            retiredName: "Odissi Sundari Griva");

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored));
    }

    [Theory]
    [InlineData(21, "Alternating Standing-Scale Balance", "Standing-Scale Balance Hold")]
    [InlineData(145, "Alternating Standing Knee Extension", "Standing Knee-Extension Hold")]
    [InlineData(394, "Standing Open-and-Close Breathing", "Inhale Arms Open, Exhale Arms Close and Round")]
    [InlineData(395, "Standing Overhead Rib-Expansion Breathing", "Overhead Hold with Deep Ribcage Breaths")]
    [InlineData(397, "Breath-Integrated Weight Shift", "Exhale Forward, Inhale Back Weight Shift")]
    [InlineData(398, "Standing Arm-Expansion Breathing", "Inhale Arms Open, Exhale Self-Hug and Fold")]
    [InlineData(399, "Shibashi Opening-the-Chest Breathing", "Inhale Chest Open, Exhale Arms Close with Shallow Squat")]
    [InlineData(400, "Shibashi Separating-the-Clouds Breathing", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down")]
    [InlineData(401, "Shibashi Alternating Swinging-Arms Breathing", "Alternating Inhale-Twist, Exhale-Push")]
    public void MigrationAllowsEarlierNameAcrossSecondClarityCorrection(
        int exerciseId,
        string earlierName,
        string correctedName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(earlierName, video, -5),
        };
        Exercise corrected = Exercise(exerciseId, correctedName, video);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-5, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(234, "Palms-Up Fist Wrist Flexion and Extension", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(234, "Alternating Thumb-to-Palm Tucks", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(239, "Ninja Snake Hand-Seal Hold", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(240, "Ninja Ram Hand-Seal Hold", "Ninja Shadow-Possession Hand-Seal Sequence")]
    [InlineData(241, "Self-Resisted Thumb C Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(241, "Straight-Hand Knuckle-Bend Flow", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(211, "Opposite-Hand-Resisted Wrist Extension Hold", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(211, "Assisted Wrist Flexion-Extension Glides", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(213, "Opposite-Hand-Resisted Wrist Flexion Hold", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(213, "Assisted Side-to-Side Wrist Glides", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(214, "Opposite-Hand-Resisted Wrist Ulnar-Deviation Hold", "Wing Chun Biu-Sau Palm Strike")]
    [InlineData(215, "Opposite-Hand-Resisted Wrist Radial-Deviation Hold", "Self-Resisted Wrist Radial-Deviation Pulses")]
    [InlineData(218, "Opposite-Hand-Resisted Little-Finger Abduction Hold", "Self-Resisted Wrist-Flexion Pulses")]
    [InlineData(236, "Opposite-Hand-Resisted Thumb Extension Hold", "Karate Spear-Hand Strike (Nukite)")]
    [InlineData(241, "Opposite-Hand-Resisted Thumb Adduction Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(242, "Five-Fingertip Press Isometric", "Ninja Shadow-Clone Hand-Seal Sequence")]
    [InlineData(283, "Opposite-Hand-Resisted Thumb Abduction Hold", "Qigong Fist Rotation")]
    [InlineData(289, "Opposite-Hand-Resisted Thumb Flexion Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Self-Resisted Thumb Adduction Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Alternating Thumb-to-Palm Tucks", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Thumb-to-Fingertip Opposition", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Self-Resisted Thumb Abduction Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Self-Resisted Thumb Flexion Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Self-Resisted Little-Finger Abduction Hold", "Tiger-Claw Grip Flow")]
    [InlineData(483, "Clockwise-First Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(501, "Counterclockwise-First Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(501, "Single-Leg Thumb-Focus Head Turns", "Standing Horizontal Saccades")]
    [InlineData(504, "Hands-Behind-Head Splenius-Capitis Stretch", "Vertical Eye-Head Shifts Between Thumbs")]
    [InlineData(513, "Single-Leg Thumb-Focus Head Nods", "Scapular Retraction")]
    [InlineData(843, "Standing Scalene Wrist-Anchor Stretch", "Standing Cobra Pose")]
    [InlineData(572, "Cossack Side-to-Side Shifts", "Tai Chi White Crane Opens Wings")]
    public void LatestReplacementAcceptsAdditionalReviewedPriorIdentity(
        int exerciseId,
        string priorName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Latest replacement",
            video,
            retiredName: baselineRetiredName);

        foreach (string storedName in new[]
        {
            priorName,
            $"Alternating {priorName}",
        })
        {
            var stored = new Dictionary<int, StoredExerciseSnapshot>
            {
                [exerciseId] = new(storedName, video, -6),
            };

            IReadOnlySet<int> preserved =
                CatalogMigrationRules.ValidatePreservedCatalog(
                    [replacement],
                    stored);

            Assert.DoesNotContain(exerciseId, preserved);
        }
    }

    [Theory]
    [InlineData(214, "Wrist Circles", "Inward Wrist Circles")]
    [InlineData(223, "Controlled Wrist Circles", "Inward Controlled Wrist Circles")]
    [InlineData(264, "Standing Arm Circles", "Backward Standing Arm Circles")]
    [InlineData(288, "Knee-and-Ankle Circles", "Forward Knee-and-Ankle Circles")]
    [InlineData(406, "Standing Wheel Arm Circles", "Clockwise Standing Wheel Arm Circles")]
    [InlineData(409, "Full Neck Circles", "Clockwise Full Neck Circles")]
    [InlineData(588, "Belly-Dance Alternating Shoulder Rolls", "Backward Belly-Dance Alternating Shoulder Rolls")]
    [InlineData(608, "Hip Circle", "Counterclockwise Hip Circles")]
    [InlineData(611, "Wide-Stance Hip Circles", "Counterclockwise Wide-Stance Hip Circles")]
    [InlineData(743, "Standing Large Arm Circles", "Backward Standing Large Arm Circles")]
    public void DirectionSplitAcceptsPreviouslyDeployedIdentityAndResetsScore(
        int exerciseId,
        string previousName,
        string currentName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -7),
        };
        Exercise splitDirection = Exercise(
            exerciseId,
            currentName,
            video,
            retiredName: "Historical reviewed replacement");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [splitDirection],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
    }

    [Fact]
    public void VersionFifteenInventoryReconcilesAdditivelyIntoBundledCatalog()
    {
        int[] versionSixteenIds = [400, 401, 402, 403, 404, 405, 406];
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Assert.All(versionSixteenIds, id =>
            Assert.Contains(bundled, exercise => exercise.Id == id));

        Dictionary<int, StoredExerciseSnapshot> versionFifteen = bundled
            .Where(exercise => !versionSixteenIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.RetiredName ?? exercise.Name,
                    exercise.Video,
                    -exercise.Id));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            bundled,
            versionFifteen);

        Assert.Equal(
            versionFifteen.Keys
                .Except(CatalogMigrationRules.ReplacedExerciseIds)
                .Order(),
            preserved.Order());
        Assert.All(versionFifteen, entry =>
            Assert.Equal(-entry.Key, entry.Value.Score));
        Assert.DoesNotContain(versionSixteenIds, preserved.Contains);
    }

    private static Exercise Exercise(
        int id,
        string name,
        string video,
        int score = 0,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        string? retiredName = null)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            RetiredName = retiredName,
            Video = video,
            PrimaryCanonicalGroup =
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            SecondaryCanonicalGroups = [],
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = sideSequence,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
