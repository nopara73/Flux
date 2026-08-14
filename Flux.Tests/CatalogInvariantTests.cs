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
        Exercise[] breathingExercises = exercises
            .Where(exercise =>
                exercise.PrimaryCanonicalGroup == CanonicalMuscleGroup.BreathingMuscles)
            .ToArray();
        Assert.Equal(10, breathingExercises.Length);
        Assert.All(breathingExercises, exercise =>
            Assert.Matches("(?i)\\b(inhale|exhale|breath)", exercise.Name));
        Exercise overheadBreathingFlow = exercises.Single(exercise => exercise.Id == 395);
        Assert.Equal("Inhale Reach Up, Exhale Knee Lift", overheadBreathingFlow.Name);
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
            .Where(exercise =>
                exercise.SideSequence != ExerciseSideSequence.Continuous)
            .ToArray();
        Assert.Equal(104, timedSideExercises.Length);
        Assert.DoesNotContain(timedSideExercises, exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Assert.Equal(8, timedDirectionExercises.Length);
        Assert.All(timedDirectionExercises, exercise =>
        {
            Assert.Equal(ExerciseSideSequence.Continuous, exercise.SideSequence);
            Assert.Equal(ExerciseMode.Repetition, exercise.Mode);
            Assert.Equal(ExercisePresentation.Motion, exercise.Presentation);
        });
        Dictionary<int, ExerciseDirectionSequence> auditedDirectionSequences = new()
        {
            [264] = ExerciseDirectionSequence.BackwardThenForward,
            [406] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [409] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [497] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [588] = ExerciseDirectionSequence.BackwardThenForward,
            [608] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [611] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [743] = ExerciseDirectionSequence.BackwardThenForward,
        };
        Assert.All(auditedDirectionSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key)
                    .DirectionSequence));
        int[] declaredReplacementIds = exercises
            .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
            .Select(exercise => exercise.Id)
            .Order()
            .ToArray();
        Assert.Equal(
            CatalogMigrationRules.ReplacedExerciseIds.Order(),
            declaredReplacementIds);
        Assert.All(
            exercises.Where(exercise =>
                CatalogMigrationRules.ReplacedExerciseIds.Contains(exercise.Id)),
            exercise => Assert.NotEqual(exercise.Name, exercise.RetiredName));

        Dictionary<int, ExerciseSideSequence> auditedSideSequences = new()
        {
            [58] = ExerciseSideSequence.ScreenLeftThenRight,
            [115] = ExerciseSideSequence.ScreenLeftThenRight,
            [116] = ExerciseSideSequence.Continuous,
            [117] = ExerciseSideSequence.ScreenLeftThenRight,
            [123] = ExerciseSideSequence.ScreenRightThenLeft,
            [126] = ExerciseSideSequence.Continuous,
            [135] = ExerciseSideSequence.Continuous,
            [143] = ExerciseSideSequence.ScreenRightThenLeft,
            [211] = ExerciseSideSequence.Continuous,
            [212] = ExerciseSideSequence.Continuous,
            [213] = ExerciseSideSequence.Continuous,
            [214] = ExerciseSideSequence.Continuous,
            [215] = ExerciseSideSequence.ScreenRightThenLeft,
            [216] = ExerciseSideSequence.Continuous,
            [217] = ExerciseSideSequence.ScreenLeftThenRight,
            [218] = ExerciseSideSequence.Continuous,
            [220] = ExerciseSideSequence.ScreenRightThenLeft,
            [232] = ExerciseSideSequence.ScreenLeftThenRight,
            [233] = ExerciseSideSequence.ScreenLeftThenRight,
            [234] = ExerciseSideSequence.Continuous,
            [236] = ExerciseSideSequence.Continuous,
            [237] = ExerciseSideSequence.Continuous,
            [239] = ExerciseSideSequence.ScreenRightThenLeft,
            [240] = ExerciseSideSequence.Continuous,
            [241] = ExerciseSideSequence.ScreenRightThenLeft,
            [242] = ExerciseSideSequence.ScreenRightThenLeft,
            [245] = ExerciseSideSequence.ScreenRightThenLeft,
            [256] = ExerciseSideSequence.Continuous,
            [257] = ExerciseSideSequence.ScreenRightThenLeft,
            [258] = ExerciseSideSequence.ScreenRightThenLeft,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.Continuous,
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
            [397] = ExerciseSideSequence.Continuous,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.Continuous,
            [501] = ExerciseSideSequence.ScreenRightThenLeft,
            [508] = ExerciseSideSequence.Continuous,
            [513] = ExerciseSideSequence.ScreenLeftThenRight,
            [611] = ExerciseSideSequence.Continuous,
            [617] = ExerciseSideSequence.ScreenLeftThenRight,
            [619] = ExerciseSideSequence.Continuous,
            [620] = ExerciseSideSequence.ScreenLeftThenRight,
            [648] = ExerciseSideSequence.ScreenRightThenLeft,
            [649] = ExerciseSideSequence.ScreenRightThenLeft,
            [685] = ExerciseSideSequence.ScreenRightThenLeft,
            [686] = ExerciseSideSequence.ScreenLeftThenRight,
            [884] = ExerciseSideSequence.ScreenRightThenLeft,
            [885] = ExerciseSideSequence.ScreenRightThenLeft,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
            [996] = ExerciseSideSequence.ScreenLeftThenRight,
            [997] = ExerciseSideSequence.ScreenLeftThenRight,
            [998] = ExerciseSideSequence.Continuous,
            [999] = ExerciseSideSequence.Continuous,
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
            16, 20, 47, 97, 179, 180, 220, 239, 241, 242,
            257, 258, 278, 279, 283, 285, 286, 291, 294,
            326, 329, 396, 513, 685,
        ];
        Assert.All(auditedSidedClarityReplacementIds, exerciseId =>
            Assert.NotEqual(
                ExerciseSideSequence.Continuous,
                exercises.Single(exercise => exercise.Id == exerciseId).SideSequence));

        int[] auditedContinuousClarityReplacementIds =
        [
            15, 17, 19, 31, 107, 135, 150, 169, 193, 219,
            229, 230, 248, 251, 256, 262, 266, 268, 269, 270,
            275, 282, 287, 314, 321, 390, 391, 394, 395, 397,
            425, 507, 508, 516, 572, 576, 577, 615, 618, 677,
            683, 745, 816, 834,
        ];
        Assert.All(auditedContinuousClarityReplacementIds, exerciseId =>
            Assert.Equal(
                ExerciseSideSequence.Continuous,
                exercises.Single(exercise => exercise.Id == exerciseId).SideSequence));
        Assert.Equal(
            ExerciseSideSequence.Continuous,
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
            [21] = "Standing-Scale Balance Hold",
            [105] = "Wide Turned-Out Squat",
            [115] = "Pistol Squat",
            [119] = "Tiptoe Walk",
            [126] = "Squat to Alternating Side Kick",
            [135] = "Standing Snow Angels",
            [139] = "Wide-Squat Alternating Heel Raises",
            [145] = "Standing Knee-Extension Hold",
            [188] = "Narrow Turned-Out Shallow Squat",
            [195] = "Side Lunge to Knee-Up Balance",
            [197] = "Parallel Squat-to-Calf Raise",
            [198] = "Wide Squat to Feet-Together Calf Raise",
            [199] = "Wide-Stance Side-to-Side Squat",
            [211] = "Assisted Wrist Flexion-Extension Glides",
            [212] = "Unsupported Sissy Squat",
            [213] = "Assisted Side-to-Side Wrist Glides",
            [214] = "Wrist Circles",
            [215] = "Forearm Pronation-Supination Flow",
            [216] = "Interlaced-Finger Palm-Out Stretch",
            [217] = "Tree Pose Hold",
            [218] = "Sequential Finger Waves",
            [223] = "Controlled Wrist Circles",
            [224] = "Qigong Interlaced Wrist Rolls",
            [225] = "Clenched-Fist Wrist Extensor Stretch",
            [231] = "Step-Through Karate Reverse Punch",
            [232] = "Extended Side Angle Hold",
            [233] = "Standing Wrist Flexion Stretch",
            [234] = "Alternating Thumb-to-Palm Tucks",
            [236] = "Bilateral Wrist Figure Eights",
            [237] = "Sequential Finger Curl Waves",
            [239] = "Tabletop Tendon Glide",
            [240] = "Hook Fingers to Full Fist",
            [241] = "Hook-Fist Tendon Glide",
            [242] = "Full-Fist Tendon Glide",
            [245] = "Straight-Punch to Shovel-Hook Combo",
            [246] = "Bodyweight Cuban Rotation",
            [256] = "Bent-Over Straight-Arm Lat Sweeps",
            [257] = "Karate Knife-Hand Block",
            [258] = "Karate Downward Block",
            [262] = "Standing Bicycle Crunches",
            [270] = "Goalpost Elbow Open-and-Close",
            [283] = "Straight-Fist Tendon Glide",
            [289] = "Thumb-to-Fingertip Opposition",
            [290] = "Low Palm Scoop to Side Opening",
            [326] = "Rear-Hand Straight Punch",
            [338] = "Overhead Triceps Stretch with Side Bend",
            [390] = "Inhale Arms Up, Exhale Step-Touch",
            [391] = "Inhale Arms Open, Exhale High-Knee",
            [394] = "Inhale Open, Exhale Cross-Body Knee",
            [395] = "Inhale Reach Up, Exhale Knee Lift",
            [396] = "Single-Leg Knee-Lift Balance Hold",
            [397] = "Inhale Open, Exhale Cross-Body Side Tap",
            [398] = "Inhale Arms Open, Exhale Self-Hug and Fold",
            [399] = "Inhale Chest Open, Exhale Arms Close with Shallow Squat",
            [400] = "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down",
            [401] = "Alternating Inhale-Twist, Exhale-Push",
            [409] = "Full Neck Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track One Thumb Side to Side",
            [497] = "Track Finger in Circles",
            [501] = "Single-Leg Thumb-Focus Head Turns",
            [508] = "Wide-Step Elbow Pull",
            [510] = "Clasped-Hands Chest-Opening Forward-Fold Hold",
            [996] = "Partial Pistol Squat",
            [997] = "Bottom Pistol Squat Hold",
            [998] = "Deep-Squat Thoracic Rotation",
            [999] = "Deep-Squat Walk",
            [512] = "Standing Upper-Back and Neck Hug Stretch",
            [513] = "Single-Leg Thumb-Focus Head Nods",
            [588] = "Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [611] = "Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Bent-Knee Hip Abduction",
            [686] = "Standing Knee-to-Chest Glute Stretch",
            [712] = "Standing Arms-Back Chest-Opener Hold",
            [843] = "Arm-Behind-Back Assisted Side Neck Stretch",
            [969] = "Chair-Pose Hold",
            [1000] = "Standing Forward-Fold Hold",
        };
        Assert.All(auditedCorrectedNames, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).Name));

        Assert.Equal(
            CanonicalMuscleGroup.IntrinsicHand,
            exercises.Single(exercise => exercise.Id == 283).PrimaryCanonicalGroup);

        Exercise shadowBoxing = exercises.Single(exercise => exercise.Id == 591);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            shadowBoxing.PrimaryCanonicalGroup);
        Assert.Equal(ExerciseSideSequence.Continuous, shadowBoxing.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, shadowBoxing.Mode);
        Assert.Equal(ExercisePresentation.Motion, shadowBoxing.Presentation);
        Assert.Contains(
            CanonicalMuscleGroup.Chest,
            shadowBoxing.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            shadowBoxing.SecondaryCanonicalGroups);

        Exercise restoredShoulderRaise = exercises.Single(exercise => exercise.Id == 266);
        Assert.Equal("Alternating T-Arm Lifts", restoredShoulderRaise.Name);
        Assert.Equal("Standing Palms-Up Arm Raise", restoredShoulderRaise.RetiredName);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            restoredShoulderRaise.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            restoredShoulderRaise.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, restoredShoulderRaise.Mode);
        Assert.Equal(ExercisePresentation.Motion, restoredShoulderRaise.Presentation);
        Assert.Equal(0, restoredShoulderRaise.HoldFramePercent);
        Assert.Contains(
            CanonicalMuscleGroup.ScapularGirdle,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.RotatorCuff,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.ElbowFlexors,
            restoredShoulderRaise.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AbdominalWall,
            restoredShoulderRaise.SecondaryCanonicalGroups);

        string[] deficientWorkoutGroups = MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Select(group => new
            {
                Group = group,
                SelectableCount = exercises.Count(exercise =>
                    WorkoutCoveragePolicy.IsSelectable(exercise, group)),
            })
            .Where(result =>
                result.SelectableCount <
                    WorkoutCoveragePolicy.MinimumSelectableExercisesPerGroup)
            .Select(result =>
                $"{result.Group.Id}: {result.SelectableCount} primary-owned exercises " +
                $"meeting the {WorkoutCoveragePolicy.MinimumCoveragePercent}% " +
                "coverage requirement")
            .ToArray();
        Assert.True(
            deficientWorkoutGroups.Length == 0,
            string.Join(Environment.NewLine, deficientWorkoutGroups));

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
            Assert.InRange(exercise.MaxSpaceMeters, 1, 3);
            Assert.Equal("None", exercise.Equipment);
            Assert.True(exercise.Silent);
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
