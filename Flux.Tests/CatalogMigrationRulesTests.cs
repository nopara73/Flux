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

        Assert.Equal(148, CatalogMigrationRules.ReplacedExerciseIds.Count);
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

    [Fact]
    public void RemovedReviewedReplacementRestoresBaselineWithoutItsScore()
    {
        const int exerciseId = 266;
        const string video = "exercise_videos/exercise_0266.mp4";
        Exercise restored = Exercise(
            exerciseId,
            "Standing Palms-Up Arm Raise",
            video);
        var replacedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Zyzz Diagonal-Reach Pose Hold",
                video,
                -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [restored],
            replacedStored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, replacedStored[exerciseId].Score);

        var restoredStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(restored.Name, video, -3),
        };
        preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [restored],
            restoredStored);
        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-3, restoredStored[exerciseId].Score);

        var unrelatedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Unrelated movement", video, -5),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [restored],
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
                [restored],
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

    [Theory]
    [InlineData(195, "Lateral Lunge to Balance", "Ballet Degage a la Seconde")]
    [InlineData(211, "Open-Finger Wrist Extension", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(213, "Open-Finger Wrist Flexion", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(214, "Neutral-Fist Wrist Flexion and Extension", "Wing Chun Biu-Sau Palm Strike")]
    [InlineData(215, "Up-and-Down Wrist Glides", "Self-Resisted Wrist Radial-Deviation Pulses")]
    [InlineData(216, "Side-to-Side Wrist Glides", "Self-Resisted Wrist Ulnar-Deviation Pulses")]
    [InlineData(217, "Bilateral Wrist Figure Eights", "Self-Resisted Wrist-Extension Pulses")]
    [InlineData(218, "Hook-to-Fist Tendon Glides", "Self-Resisted Wrist-Flexion Pulses")]
    [InlineData(232, "Palms-Down Fist Wrist Flexion and Extension", "Karate Knife-Hand Chop")]
    [InlineData(233, "Bilateral Wrist Circles", "Karate Ridge-Hand Strike (Haito-Uchi)")]
    [InlineData(234, "Palms-Up Fist Wrist Flexion and Extension", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(236, "Alternating Hand Open and Close", "Karate Spear-Hand Strike (Nukite)")]
    [InlineData(239, "Ninja Snake Hand-Seal Hold", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(240, "Ninja Ram Hand-Seal Hold", "Ninja Shadow-Possession Hand-Seal Sequence")]
    [InlineData(241, "Ninja Monkey Hand-Seal Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(242, "Ninja Boar Hand-Seal Hold", "Ninja Shadow-Clone Hand-Seal Sequence")]
    [InlineData(268, "Self-Resisted External-Rotation Push-Out", "Self-Resisted External-Rotation Isometric")]
    [InlineData(274, "Side-Step Alternating High Curl", "Dynamic-Resistance Lat Pulldown")]
    [InlineData(276, "Alternating Diagonal Overhead Reach-and-Pull", "Dynamic-Resistance High Chest Press")]
    [InlineData(280, "Alternating Forward-and-Side Arm Press", "Ringing-the-Towel Wrist Inversion")]
    [InlineData(283, "Sequential Finger Waves", "Qigong Fist Rotation")]
    [InlineData(289, "Ninja Horse Hand-Seal Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Ninja Tiger Hand-Seal Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Ninja Dragon Hand-Seal Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Ninja Rat Hand-Seal Hold", "Tiger-Claw Grip Flow")]
    [InlineData(482, "Front Half Neck Circles", "Continuous Spot-Turn Drill")]
    [InlineData(483, "Clockwise Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(490, "Assisted Cheek Lift", "Bharatanatyam Alolita Shiro")]
    [InlineData(491, "Cheek-Firming Air Hold", "Bharatanatyam Dhuta Shiro")]
    [InlineData(492, "Forehead Knuckle Massage", "Bharatanatyam Kampita Shiro")]
    [InlineData(493, "Face-and-Neck Lymphatic Sweep", "Alternating Bharatanatyam Paravritta Shiro")]
    [InlineData(495, "Jawline Knuckle Massage", "Bharatanatyam Parivahita Shiro")]
    [InlineData(497, "Forehead Finger Sweep", "Odissi Sundari Griva")]
    [InlineData(499, "Eyebrow Pinch Massage", "Bharatanatyam Tiraschina Griva")]
    [InlineData(500, "Eye-Socket Finger Circles", "Bharatanatyam Parivartita Griva")]
    [InlineData(501, "Counterclockwise Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(505, "Temple Circle Massage", "Maximal Smile and Relax")]
    [InlineData(506, "Cheek Pinch Massage", "Eyebrow Raise and Relax")]
    [InlineData(508, "Diagonal Arm Reach-to-Row", "Tongue Protrusion and Retraction")]
    [InlineData(513, "Standing Unilateral SCM Stretch", "Scapular Retraction")]
    [InlineData(572, "Wide-Stance Bent-Knee Rotational Stretch", "Tai Chi White Crane Opens Wings")]
    [InlineData(591, "Standing Speed-Bag Punches", "Bharatanatyam Natyarambhe Hold")]
    [InlineData(611, "Warrior II-Stance Hip Circles", "Pelvic-Floor Heel-Raise Lift")]
    [InlineData(681, "Rear-Arm Sweep to Front Squeeze", "Belly-Dance Horizontal Figure Eight")]
    [InlineData(743, "Standing Backward Arm Circles", "Clasped-Hands-Behind-Back Chest Opener")]
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
            223, 224, 225, 245, 246,
        ];
        const int historicalReplacementId = 591;
        const int retainedId = 15;
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
    [InlineData(105, "Plie Squat", "Wide Turned-Out Squat")]
    [InlineData(188, "Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat")]
    [InlineData(197, "First-Position Plie-Releve", "Parallel Squat-to-Calf Raise")]
    [InlineData(198, "Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise")]
    [InlineData(255, "Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise")]
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
    [InlineData(588, "Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls")]
    [InlineData(617, "Standing Side-Leg Circles", "Standing Forward Side-Leg Circles")]
    [InlineData(626, "Sumo Stance", "Sumo Squat Hold")]
    [InlineData(969, "Chair-Pose Core Hold", "Chair-Pose Hold")]
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

    [Theory]
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
    [InlineData(241, "Self-Resisted Thumb C Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(241, "Straight-Hand Knuckle-Bend Flow", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(289, "Self-Resisted Thumb Adduction Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Alternating Thumb-to-Palm Tucks", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Self-Resisted Thumb Abduction Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Self-Resisted Thumb Flexion Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Self-Resisted Little-Finger Abduction Hold", "Tiger-Claw Grip Flow")]
    [InlineData(483, "Clockwise-First Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(501, "Counterclockwise-First Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(843, "Standing Scalene Wrist-Anchor Stretch", "Standing Cobra Pose")]
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
