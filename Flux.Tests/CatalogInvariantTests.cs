using System.Text.Json;
using System.Text.Json.Serialization;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class CatalogInvariantTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void BundledCatalogHasStableCanonicalAssignmentsAndRequiredCoverage()
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        string json = File.ReadAllText(catalogPath);
        using JsonDocument document = JsonDocument.Parse(json);
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");

        Assert.DoesNotContain(document.RootElement.EnumerateArray(), element =>
            element.TryGetProperty("muscleGroups", out _));
        Assert.True(exercises.Length >= 300);
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Id).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Name).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Video).Distinct().Count());
        Assert.DoesNotContain(exercises, exercise =>
            exercise.InsectCompatibility == ExerciseInsectCompatibility.Unreviewed);
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.All(
            exercises.Where(exercise => exercise.Mode == ExerciseMode.Hold),
            exercise => Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                exercise.InsectCompatibility));
        WorkoutModifierPairCoverageDeficiency[] pairwiseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises).ToArray();
        Assert.True(
            pairwiseDeficiencies.Length == 0,
            string.Join(Environment.NewLine, pairwiseDeficiencies.Select(deficiency =>
                $"{deficiency.GroupId}: " +
                $"{deficiency.FirstModifier}={deficiency.FirstModifierEnabled}, " +
                $"{deficiency.SecondModifier}={deficiency.SecondModifierEnabled}: " +
                $"{deficiency.MatchingExerciseCount}/" +
                $"{deficiency.RequiredExerciseCount}")));
        WorkoutModifierMaterialityDeficiency[] materialityDeficiencies =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises).ToArray();
        Assert.True(
            materialityDeficiencies.Length == 0,
            string.Join(Environment.NewLine, materialityDeficiencies.Select(deficiency =>
                $"{deficiency.Modifier} in {deficiency.ContextProfile}: " +
                $"released {deficiency.ReleasedExerciseCount}/" +
                $"{deficiency.RequiredReleasedExerciseCount} exercises and affected " +
                $"{deficiency.AffectedBucketCount}/" +
                $"{deficiency.RequiredAffectedBucketCount} buckets")));
        WorkoutProfileLineupDeficiency[] lineupDeficiencies =
            WorkoutModifierPolicy.FindDistinctLineupDeficiencies(exercises).ToArray();
        Assert.True(
            lineupDeficiencies.Length == 0,
            string.Join(Environment.NewLine, lineupDeficiencies.Select(deficiency =>
                $"{deficiency.Minutes} minutes + {deficiency.Profile}: " +
                $"{deficiency.MaximumDistinctExerciseCount}/" +
                $"{deficiency.RequiredDistinctExerciseCount} distinct exercises")));
        var profileService = new ExerciseSessionService(exercises, new Random(1));
        foreach (WorkoutModifiers profile in WorkoutModifierPolicy.ValidationProfiles)
        {
            foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
            {
                var profileState = new WorkoutState();
                profileService.StartWorkout(profileState, minutes, profile);
                Assert.All(profileService.GetActiveGroups(profileState), group =>
                    Assert.True(WorkoutModifierPolicy.IsSelectable(
                        profileService.GetSelectedExercise(profileState, group),
                        group,
                        profile)));
            }
        }
        Exercise[] breathingExercises = exercises
            .Where(exercise =>
                exercise.PrimaryCanonicalGroup == CanonicalMuscleGroup.BreathingMuscles)
            .ToArray();
        Assert.Equal(15, breathingExercises.Length);
        Assert.All(breathingExercises, exercise =>
            Assert.Matches(
                "(?i)\\b(inhale|exhale|breath|laugh|laughter)",
                exercise.Name));
        Exercise overheadBreathingFlow = exercises.Single(exercise => exercise.Id == 395);
        Assert.Equal(
            "Single-Side Inhale Reach Up, Exhale Knee Lift",
            overheadBreathingFlow.Name);
        Assert.Equal(ExerciseMode.Repetition, overheadBreathingFlow.Mode);
        Assert.Equal(ExercisePresentation.Motion, overheadBreathingFlow.Presentation);
        Exercise standingKneeExtensionHold = exercises.Single(exercise => exercise.Id == 145);
        Assert.Equal("Standing Knee-Extension Hold", standingKneeExtensionHold.Name);
        Assert.Equal(ExerciseMode.Hold, standingKneeExtensionHold.Mode);
        Assert.Equal(ExercisePresentation.Still, standingKneeExtensionHold.Presentation);
        Assert.Equal(90, standingKneeExtensionHold.HoldFramePercent);
        Assert.Equal(
            ExerciseSideSequence.ScreenRightThenLeft,
            standingKneeExtensionHold.SideSequence);
        Exercise[] timedSideExercises = exercises
            .Where(exercise => exercise.SideSequence.UsesTimedSides())
            .ToArray();
        Assert.Equal(132, timedSideExercises.Length);
        Assert.DoesNotContain(timedSideExercises, exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] alternatingExercises = exercises
            .Where(exercise =>
                exercise.SideSequence == ExerciseSideSequence.Alternating)
            .ToArray();
        Assert.Equal(129, alternatingExercises.Length);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 219);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 15);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 429);
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Assert.Empty(timedDirectionExercises);
        Dictionary<int, int> auditedDirectionPartners = new()
        {
            [214] = 755,
            [223] = 756,
            [264] = 757,
            [288] = 758,
            [406] = 759,
            [409] = 760,
            [588] = 761,
            [608] = 762,
            [611] = 763,
            [743] = 764,
        };
        Assert.Equal(
            auditedDirectionPartners.Count * 2,
            exercises.Count(exercise => exercise.DirectionPartnerExerciseId > 0));
        Assert.All(auditedDirectionPartners, expected =>
        {
            Exercise first = exercises.Single(exercise => exercise.Id == expected.Key);
            Exercise second = exercises.Single(exercise => exercise.Id == expected.Value);
            Assert.Equal(second.Id, first.DirectionPartnerExerciseId);
            Assert.Equal(first.Id, second.DirectionPartnerExerciseId);
            Assert.Equal(first.PrimaryCanonicalGroup, second.PrimaryCanonicalGroup);
            Assert.Equal(first.SecondaryCanonicalGroups.Order(), second.SecondaryCanonicalGroups.Order());
        });
        int[] declaredReplacementIds = exercises
            .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
            .Select(exercise => exercise.Id)
            .Order()
            .ToArray();
        Assert.Equal(
            CatalogMigrationRules.ReplacedExerciseIds
                .Except(CatalogMigrationRules.PermanentlyRetiredExerciseIds)
                .Order(),
            declaredReplacementIds);
        Assert.DoesNotContain(exercises, exercise =>
            CatalogMigrationRules.PermanentlyRetiredExerciseIds.Contains(exercise.Id));
        Assert.All(
            exercises.Where(exercise =>
                CatalogMigrationRules.ReplacedExerciseIds.Contains(exercise.Id)),
            exercise => Assert.NotEqual(exercise.Name, exercise.RetiredName));

        Dictionary<int, ExerciseSideSequence> auditedSideSequences = new()
        {
            [58] = ExerciseSideSequence.ScreenLeftThenRight,
            [98] = ExerciseSideSequence.Alternating,
            [115] = ExerciseSideSequence.ScreenLeftThenRight,
            [116] = ExerciseSideSequence.Alternating,
            [117] = ExerciseSideSequence.ScreenRightThenLeft,
            [123] = ExerciseSideSequence.ScreenRightThenLeft,
            [126] = ExerciseSideSequence.Alternating,
            [135] = ExerciseSideSequence.Continuous,
            [143] = ExerciseSideSequence.ScreenRightThenLeft,
            [184] = ExerciseSideSequence.ScreenRightThenLeft,
            [186] = ExerciseSideSequence.ScreenRightThenLeft,
            [211] = ExerciseSideSequence.ScreenLeftThenRight,
            [212] = ExerciseSideSequence.Continuous,
            [213] = ExerciseSideSequence.ScreenLeftThenRight,
            [214] = ExerciseSideSequence.ScreenRightThenLeft,
            [215] = ExerciseSideSequence.ScreenRightThenLeft,
            [216] = ExerciseSideSequence.Continuous,
            [217] = ExerciseSideSequence.ScreenLeftThenRight,
            [218] = ExerciseSideSequence.Continuous,
            [220] = ExerciseSideSequence.ScreenRightThenLeft,
            [232] = ExerciseSideSequence.ScreenLeftThenRight,
            [233] = ExerciseSideSequence.ScreenLeftThenRight,
            [225] = ExerciseSideSequence.ScreenLeftThenRight,
            [234] = ExerciseSideSequence.ScreenLeftThenRight,
            [236] = ExerciseSideSequence.Continuous,
            [237] = ExerciseSideSequence.Continuous,
            [239] = ExerciseSideSequence.ScreenRightThenLeft,
            [240] = ExerciseSideSequence.Continuous,
            [241] = ExerciseSideSequence.ScreenRightThenLeft,
            [242] = ExerciseSideSequence.ScreenRightThenLeft,
            [245] = ExerciseSideSequence.ScreenRightThenLeft,
            [256] = ExerciseSideSequence.ScreenRightThenLeft,
            [257] = ExerciseSideSequence.Continuous,
            [258] = ExerciseSideSequence.ScreenRightThenLeft,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenRightThenLeft,
            [283] = ExerciseSideSequence.ScreenRightThenLeft,
            [289] = ExerciseSideSequence.Continuous,
            [291] = ExerciseSideSequence.ScreenRightThenLeft,
            [292] = ExerciseSideSequence.ScreenRightThenLeft,
            [293] = ExerciseSideSequence.ScreenLeftThenRight,
            [294] = ExerciseSideSequence.ScreenRightThenLeft,
            [326] = ExerciseSideSequence.ScreenRightThenLeft,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [31] = ExerciseSideSequence.Alternating,
            [176] = ExerciseSideSequence.Alternating,
            [195] = ExerciseSideSequence.Alternating,
            [219] = ExerciseSideSequence.Alternating,
            [248] = ExerciseSideSequence.ScreenRightThenLeft,
            [282] = ExerciseSideSequence.ScreenLeftThenRight,
            [390] = ExerciseSideSequence.Alternating,
            [391] = ExerciseSideSequence.Alternating,
            [394] = ExerciseSideSequence.ScreenLeftThenRight,
            [395] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.ScreenRightThenLeft,
            [407] = ExerciseSideSequence.Continuous,
            [408] = ExerciseSideSequence.ScreenRightThenLeft,
            [410] = ExerciseSideSequence.ScreenLeftThenRight,
            [411] = ExerciseSideSequence.ScreenLeftThenRight,
            [412] = ExerciseSideSequence.ScreenLeftThenRight,
            [413] = ExerciseSideSequence.Alternating,
            [414] = ExerciseSideSequence.ScreenRightThenLeft,
            [415] = ExerciseSideSequence.ScreenRightThenLeft,
            [416] = ExerciseSideSequence.ScreenRightThenLeft,
            [417] = ExerciseSideSequence.Continuous,
            [418] = ExerciseSideSequence.Alternating,
            [419] = ExerciseSideSequence.ScreenLeftThenRight,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.Continuous,
            [507] = ExerciseSideSequence.ScreenRightThenLeft,
            [508] = ExerciseSideSequence.Alternating,
            [513] = ExerciseSideSequence.ScreenLeftThenRight,
            [576] = ExerciseSideSequence.Alternating,
            [577] = ExerciseSideSequence.ScreenRightThenLeft,
            [611] = ExerciseSideSequence.Continuous,
            [617] = ExerciseSideSequence.ScreenLeftThenRight,
            [618] = ExerciseSideSequence.ScreenLeftThenRight,
            [619] = ExerciseSideSequence.Continuous,
            [620] = ExerciseSideSequence.ScreenLeftThenRight,
            [648] = ExerciseSideSequence.ScreenRightThenLeft,
            [649] = ExerciseSideSequence.ScreenRightThenLeft,
            [572] = ExerciseSideSequence.ScreenRightThenLeft,
            [636] = ExerciseSideSequence.ScreenRightThenLeft,
            [685] = ExerciseSideSequence.ScreenLeftThenRight,
            [686] = ExerciseSideSequence.ScreenLeftThenRight,
            [745] = ExerciseSideSequence.ScreenLeftThenRight,
            [816] = ExerciseSideSequence.Alternating,
            [834] = ExerciseSideSequence.ScreenLeftThenRight,
            [884] = ExerciseSideSequence.Alternating,
            [885] = ExerciseSideSequence.Alternating,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
            [996] = ExerciseSideSequence.ScreenLeftThenRight,
            [997] = ExerciseSideSequence.ScreenLeftThenRight,
            [998] = ExerciseSideSequence.Alternating,
            [999] = ExerciseSideSequence.Alternating,
        };
        Assert.All(auditedSideSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).SideSequence));

        Exercise forwardSideLegCircles = exercises.Single(exercise => exercise.Id == 617);
        Exercise backwardSideLegCircles = exercises.Single(exercise => exercise.Id == 620);
        Assert.Equal("Standing Forward Side-Leg Circles", forwardSideLegCircles.Name);
        Assert.Equal("Standing Backward Side-Leg Circles", backwardSideLegCircles.Name);
        Assert.NotEqual(forwardSideLegCircles.Video, backwardSideLegCircles.Video);
        Assert.Equal(
            CanonicalMuscleGroup.HipAbductors,
            backwardSideLegCircles.PrimaryCanonicalGroup);

        int[] auditedSidedClarityReplacementIds =
        [
            16, 20, 47, 97, 117, 179, 180, 184, 186, 211,
            213, 220, 225, 234, 239, 241, 242, 248, 256, 258, 269,
            278, 279, 282, 283, 285, 286, 291, 294, 326, 329,
            394, 395, 396, 397, 507, 513, 572, 577, 618, 636,
            685, 745, 834,
        ];
        Assert.All(auditedSidedClarityReplacementIds, exerciseId =>
            Assert.True(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));

        int[] auditedContinuousClarityReplacementIds =
        [
            15, 17, 19, 31, 107, 135, 150, 169, 176, 193, 195,
            201, 230, 251, 257, 262, 263, 265, 266,
            267, 268, 270, 275, 287, 289, 301, 314, 321,
            413, 425, 516, 615, 677, 683, 687, 884, 885,
        ];
        Assert.All(auditedContinuousClarityReplacementIds, exerciseId =>
            Assert.False(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            exercises.Single(exercise => exercise.Id == 118).SideSequence);
        Exercise externalRotation = exercises.Single(exercise => exercise.Id == 268);
        Assert.Equal(
            "Goalpost-to-T Rotations",
            externalRotation.Name);
        Assert.Equal(ExerciseMode.Repetition, externalRotation.Mode);
        Assert.Equal(ExercisePresentation.Motion, externalRotation.Presentation);
        Assert.Equal(ExerciseSideSequence.Continuous, externalRotation.SideSequence);
        Assert.Equal(0, externalRotation.HoldFramePercent);
        Exercise unsupportedSissySquat = exercises.Single(exercise => exercise.Id == 212);
        Assert.Equal(
            "Unsupported Sissy Squat",
            unsupportedSissySquat.Name);
        Assert.Equal(ExerciseMode.Repetition, unsupportedSissySquat.Mode);
        Assert.Equal(ExercisePresentation.Motion, unsupportedSissySquat.Presentation);
        Assert.Equal(0, unsupportedSissySquat.HoldFramePercent);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            unsupportedSissySquat.SideSequence);

        Dictionary<int, string> auditedCorrectedNames = new()
        {
            [31] = "Alternating Knee Raises with Two-Arm Pull-Down",
            [219] = "Alternating High-Knee Cross-Body Pull",
            [21] = "Standing-Scale Balance Hold",
            [105] = "Wide Turned-Out Squat",
            [115] = "Pistol Squat",
            [119] = "Tiptoe Walk",
            [126] = "Squat to Alternating Side Kick",
            [135] = "Overhead Squat Hold",
            [139] = "Wide-Squat Alternating Heel Raises",
            [145] = "Standing Knee-Extension Hold",
            [188] = "Narrow Turned-Out Shallow Squat",
            [195] = "Side Lunge to Knee-Up Balance",
            [197] = "Parallel Squat-to-Calf Raise",
            [198] = "Wide Squat to Feet-Together Calf Raise",
            [199] = "Wide-Stance Side-to-Side Squat",
            [211] = "Bent-Elbow Wrist-Flexion Stretch",
            [212] = "Unsupported Sissy Squat",
            [213] = "Bent-Elbow Wrist-Extension Stretch",
            [214] = "Forward Wrist Circles",
            [215] = "Forearm Pronation-Supination Flow",
            [216] = "Interlaced-Finger Palm-Out Stretch",
            [217] = "Tree Pose Hold",
            [218] = "Sequential Finger Waves",
            [223] = "Forward Controlled Wrist Circles",
            [224] = "Qigong Interlaced Wrist Rolls",
            [225] = "Opposite-Hand Fist-Down Wrist Stretch",
            [231] = "Karate Reverse Punch",
            [232] = "Extended Side Angle Hold",
            [233] = "Standing Wrist Flexion Stretch",
            [234] = "Straight Fingers to Knuckle Bend",
            [236] = "Bilateral Wrist Figure Eights",
            [237] = "Sequential Finger Curl Waves",
            [239] = "Tabletop Tendon Glide",
            [240] = "Hook Fingers to Full Fist",
            [241] = "Open Hand to Hook Fist",
            [242] = "Open Hand to Full Fist",
            [245] = "Straight-Punch to Shovel-Hook Combo",
            [246] = "Bodyweight Cuban Rotation",
            [251] = "Forward Fold to Overhead Reach",
            [256] = "Overhead Side-Stretch Hold",
            [257] = "Finger Spread to Interlace Stretch",
            [258] = "Karate Downward Block",
            [262] = "Standing Bicycle Crunches",
            [270] = "Goalpost Chest-Opener Hold",
            [282] = "Side-Step Knee Drive with Alternating Side Punches",
            [283] = "Open Hand to Straight Fist",
            [289] = "Fingertip Spider Presses",
            [290] = "Low Palm Scoop to Side Opening",
            [326] = "Rear-Hand Straight Punch",
            [338] = "Overhead Triceps Stretch with Side Bend",
            [390] = "Inhale Arms Up, Exhale Step-Touch",
            [391] = "Inhale Arms Open, Exhale High-Knee",
            [394] = "Inhale Open, Exhale Cross-Body Knee",
            [395] = "Single-Side Inhale Reach Up, Exhale Knee Lift",
            [396] = "Single-Leg Knee-Lift Balance Hold",
            [397] = "Inhale Open, Exhale Cross-Body Side Tap",
            [398] = "Inhale Arms Open, Exhale Self-Hug and Fold",
            [399] = "Inhale Chest Open, Exhale Arms Close with Shallow Squat",
            [400] = "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down",
            [401] = "Alternating Inhale-Twist, Exhale-Push",
            [409] = "Clockwise Full Neck Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track One Thumb Side to Side",
            [501] = "Keep Eyes on Thumb While Turning Head",
            [507] = "Single-Side Knee Raise with Elbow Pull",
            [508] = "Side-Step with Two-Arm Overhead Reach",
            [510] = "Clasped-Hands Chest-Opening Forward-Fold Hold",
            [996] = "Partial Pistol Squat",
            [997] = "Bottom Pistol Squat Hold",
            [998] = "Deep-Squat Thoracic Rotation",
            [999] = "Deep-Squat Walk",
            [512] = "Standing Upper-Back and Neck Hug Stretch",
            [513] = "Single-Leg Head Nods",
            [577] = "Single-Side Standing Side-Leg Raise with Side Reach",
            [618] = "Single-Side High-Knee Hold with Side Reach",
            [654] = "Single-Side Leg Lift to Overhead Knee Drive",
            [834] = "Single-Side Diagonal Knee Drive with Overhead Pull",
            [915] = "Single-Side Split-Stance Knee Drive with Overhead Reach",
            [588] = "Backward Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [611] = "Counterclockwise Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Bent-Knee Hip Abduction",
            [686] = "Standing Knee-to-Chest Glute Stretch",
            [687] = "Horse-Stance Alternating Straight Punches",
            [712] = "Standing Arms-Back Chest-Opener Hold",
            [843] = "Arm-Behind-Back Assisted Side Neck Stretch",
            [969] = "Chair-Pose Hold",
            [1000] = "Standing Forward-Fold Hold",
        };
        Assert.All(auditedCorrectedNames, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).Name));

        int[] explicitSingleSideKneeExerciseIds =
            [395, 507, 577, 618, 654, 834, 915];
        Assert.All(explicitSingleSideKneeExerciseIds, exerciseId =>
        {
            Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
            Assert.StartsWith("Single-Side ", exercise.Name);
            Assert.True(exercise.SideSequence.UsesTimedSides());
        });
        Exercise highKneeSideReach = exercises.Single(exercise => exercise.Id == 618);
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            highKneeSideReach.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            highKneeSideReach.SecondaryCanonicalGroups);

        Assert.Equal(
            CanonicalMuscleGroup.IntrinsicHand,
            exercises.Single(exercise => exercise.Id == 283).PrimaryCanonicalGroup);

        Exercise shadowBoxing = exercises.Single(exercise => exercise.Id == 591);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            shadowBoxing.PrimaryCanonicalGroup);
        Assert.Equal(ExerciseSideSequence.Alternating, shadowBoxing.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, shadowBoxing.Mode);
        Assert.Equal(ExercisePresentation.Motion, shadowBoxing.Presentation);
        Assert.Contains(
            CanonicalMuscleGroup.Chest,
            shadowBoxing.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            shadowBoxing.SecondaryCanonicalGroups);

        Exercise restoredShoulderRaise = exercises.Single(exercise => exercise.Id == 266);
        Assert.Equal("T-Arm Shoulder Hold", restoredShoulderRaise.Name);
        Assert.Equal("Standing Palms-Up Arm Raise", restoredShoulderRaise.RetiredName);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            restoredShoulderRaise.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            restoredShoulderRaise.SideSequence);
        Assert.Equal(ExerciseMode.Hold, restoredShoulderRaise.Mode);
        Assert.Equal(ExercisePresentation.Still, restoredShoulderRaise.Presentation);
        Assert.Equal(50, restoredShoulderRaise.HoldFramePercent);
        Assert.Contains(
            CanonicalMuscleGroup.ScapularGirdle,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.RotatorCuff,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.ElbowFlexors,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            restoredShoulderRaise.SecondaryCanonicalGroups);

        Assert.Contains(exercises, exercise => exercise.Silent);
        Assert.Contains(exercises, exercise => !exercise.Silent);

        Assert.All(exercises, exercise =>
        {
            Assert.InRange(exercise.Id, 1, 1000);
            Assert.True(Enum.IsDefined(exercise.PrimaryCanonicalGroup));
            Assert.Equal(
                exercise.SecondaryCanonicalGroups.Length,
                exercise.SecondaryCanonicalGroups.Distinct().Count());
            Assert.DoesNotContain(
                exercise.PrimaryCanonicalGroup,
                exercise.SecondaryCanonicalGroups);
            Assert.All(exercise.SecondaryCanonicalGroups, group =>
                Assert.True(Enum.IsDefined(group)));
            Assert.True(exercise.OnlyFeetTouchGround);
            Assert.True(exercise.ShoeAgnostic);
            Assert.InRange(exercise.MaxSpaceMeters, 1, 2);
            Assert.Equal("None", exercise.Equipment);
            Assert.False(string.IsNullOrWhiteSpace(exercise.Practice));
            Assert.False(string.IsNullOrWhiteSpace(exercise.MotionProfile));
            Assert.True(Enum.IsDefined(exercise.SideSequence));
            Assert.True(Enum.IsDefined(exercise.DirectionSequence));
            Assert.True(Enum.IsDefined(exercise.Presentation));
            Assert.Equal(0, exercise.Score);

            if (exercise.Presentation == ExercisePresentation.Still)
            {
                Assert.Equal(ExerciseMode.Hold, exercise.Mode);
            }

            if (exercise.Mode == ExerciseMode.Hold)
            {
                Assert.Matches(
                    "(?i)\\b(hold|isometric|pose|stance|stretch)\\b",
                    exercise.Name);
                Assert.InRange(exercise.HoldFramePercent, 1, 99);
                Assert.True(
                    File.Exists(Path.Combine(
                        AppContext.BaseDirectory,
                        "Assets",
                        "exercise_hold_frames",
                        $"exercise_{exercise.Id:D4}.png")),
                    $"Exercise {exercise.Id} has no reviewed hold frame.");
            }
            else
            {
                Assert.Equal(0, exercise.HoldFramePercent);
            }
        });
    }
}
