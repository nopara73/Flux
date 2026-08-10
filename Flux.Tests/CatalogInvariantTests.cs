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
        Exercise overheadBreathingHold = exercises.Single(exercise => exercise.Id == 395);
        Assert.Equal(ExerciseMode.Hold, overheadBreathingHold.Mode);
        Assert.Equal(ExercisePresentation.Still, overheadBreathingHold.Presentation);
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
        Assert.Equal(113, timedSideExercises.Length);
        Assert.DoesNotContain(timedSideExercises, exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Assert.Equal(9, timedDirectionExercises.Length);
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
            [816] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
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
            [116] = ExerciseSideSequence.Continuous,
            [117] = ExerciseSideSequence.ScreenLeftThenRight,
            [123] = ExerciseSideSequence.ScreenRightThenLeft,
            [126] = ExerciseSideSequence.Continuous,
            [135] = ExerciseSideSequence.Continuous,
            [143] = ExerciseSideSequence.ScreenRightThenLeft,
            [211] = ExerciseSideSequence.ScreenLeftThenRight,
            [212] = ExerciseSideSequence.Continuous,
            [213] = ExerciseSideSequence.ScreenLeftThenRight,
            [214] = ExerciseSideSequence.ScreenLeftThenRight,
            [215] = ExerciseSideSequence.ScreenLeftThenRight,
            [216] = ExerciseSideSequence.Continuous,
            [217] = ExerciseSideSequence.ScreenLeftThenRight,
            [218] = ExerciseSideSequence.ScreenLeftThenRight,
            [220] = ExerciseSideSequence.ScreenLeftThenRight,
            [232] = ExerciseSideSequence.ScreenLeftThenRight,
            [233] = ExerciseSideSequence.ScreenLeftThenRight,
            [234] = ExerciseSideSequence.Continuous,
            [236] = ExerciseSideSequence.ScreenLeftThenRight,
            [237] = ExerciseSideSequence.Continuous,
            [239] = ExerciseSideSequence.ScreenLeftThenRight,
            [240] = ExerciseSideSequence.Continuous,
            [241] = ExerciseSideSequence.ScreenLeftThenRight,
            [256] = ExerciseSideSequence.ScreenLeftThenRight,
            [257] = ExerciseSideSequence.ScreenLeftThenRight,
            [258] = ExerciseSideSequence.ScreenLeftThenRight,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenLeftThenRight,
            [283] = ExerciseSideSequence.ScreenLeftThenRight,
            [289] = ExerciseSideSequence.ScreenLeftThenRight,
            [291] = ExerciseSideSequence.Continuous,
            [292] = ExerciseSideSequence.ScreenRightThenLeft,
            [293] = ExerciseSideSequence.ScreenLeftThenRight,
            [294] = ExerciseSideSequence.Continuous,
            [326] = ExerciseSideSequence.ScreenRightThenLeft,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.ScreenRightThenLeft,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.Continuous,
            [501] = ExerciseSideSequence.ScreenRightThenLeft,
            [508] = ExerciseSideSequence.ScreenLeftThenRight,
            [611] = ExerciseSideSequence.Continuous,
            [617] = ExerciseSideSequence.ScreenLeftThenRight,
            [619] = ExerciseSideSequence.Continuous,
            [620] = ExerciseSideSequence.ScreenLeftThenRight,
            [648] = ExerciseSideSequence.ScreenRightThenLeft,
            [649] = ExerciseSideSequence.ScreenLeftThenRight,
            [686] = ExerciseSideSequence.ScreenLeftThenRight,
            [884] = ExerciseSideSequence.ScreenRightThenLeft,
            [885] = ExerciseSideSequence.ScreenRightThenLeft,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
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

        int[] unequalResistanceRoleExerciseIds =
        [
            211, 213, 214, 215, 218, 220, 233, 236, 239, 241,
            256, 257, 258, 269, 278, 279, 283, 285, 286, 287, 289, 508, 843,
        ];
        Assert.All(unequalResistanceRoleExerciseIds, exerciseId =>
            Assert.NotEqual(
                ExerciseSideSequence.Continuous,
                exercises.Single(exercise => exercise.Id == exerciseId).SideSequence));

        int[] symmetricResistanceExerciseIds = [229, 230, 238, 242, 248, 262, 270];
        Assert.All(symmetricResistanceExerciseIds, exerciseId =>
            Assert.Equal(
                ExerciseSideSequence.Continuous,
                exercises.Single(exercise => exercise.Id == exerciseId).SideSequence));
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            exercises.Single(exercise => exercise.Id == 118).SideSequence);
        Exercise externalRotation = exercises.Single(exercise => exercise.Id == 268);
        Assert.Equal(
            "Thumbs-Up Diagonal Arm Raises",
            externalRotation.Name);
        Assert.Equal(ExerciseMode.Repetition, externalRotation.Mode);
        Assert.Equal(ExercisePresentation.Motion, externalRotation.Presentation);
        Assert.Equal(ExerciseSideSequence.Continuous, externalRotation.SideSequence);
        Assert.Equal(0, externalRotation.HoldFramePercent);

        Dictionary<int, string> auditedCorrectedNames = new()
        {
            [21] = "Standing-Scale Balance Hold",
            [105] = "Wide Turned-Out Squat",
            [126] = "Squat to Alternating Side Kick",
            [135] = "Standing Lateral Arm Pulses",
            [145] = "Standing Knee-Extension Hold",
            [188] = "Narrow Turned-Out Shallow Squat",
            [195] = "Side Lunge to Knee-Up Balance",
            [197] = "Parallel Squat-to-Calf Raise",
            [198] = "Wide Squat to Feet-Together Calf Raise",
            [199] = "Wide-Stance Side-to-Side Squat",
            [211] = "Opposite-Hand-Resisted Wrist Extension Hold",
            [212] = "Bent-Over Triceps Pulse",
            [213] = "Opposite-Hand-Resisted Wrist Flexion Hold",
            [214] = "Opposite-Hand-Resisted Wrist Ulnar-Deviation Hold",
            [215] = "Opposite-Hand-Resisted Wrist Radial-Deviation Hold",
            [216] = "Interlaced-Finger Palm-Out Stretch",
            [217] = "Tree Pose Hold",
            [218] = "Opposite-Hand-Resisted Little-Finger Abduction Hold",
            [223] = "Self-Resisted Forearm Supination Hold",
            [224] = "Opposite-Hand-Resisted Multi-Direction Wrist Hold",
            [225] = "Clenched-Fist Wrist Extensor Stretch",
            [231] = "Step-Through Karate Reverse Punch",
            [232] = "Extended Side Angle Hold",
            [233] = "Standing Wrist Flexion Stretch",
            [234] = "Alternating Thumb-to-Palm Tucks",
            [236] = "Opposite-Hand-Resisted Thumb Extension Hold",
            [237] = "Opposed Thumb-and-Index Extension Isometric",
            [239] = "Straight-Finger Knuckle Bends",
            [240] = "Hook Fingers to Full Fist",
            [241] = "Opposite-Hand-Resisted Thumb Adduction Hold",
            [245] = "Opposite-Hand-Resisted Elbow-Flexion Hold",
            [246] = "Bodyweight Cuban Rotation",
            [256] = "Self-Resisted Overhead Pull Hold",
            [257] = "Self-Resisted Chest-Level Pull Hold",
            [258] = "Self-Resisted Low Pull Hold",
            [262] = "Standing Hands-to-Thigh Abdominal Press Hold",
            [270] = "Palm-Squeeze Forward Press",
            [283] = "Opposite-Hand-Resisted Thumb Abduction Hold",
            [289] = "Opposite-Hand-Resisted Thumb Flexion Hold",
            [290] = "Low Palm Scoop to Side Opening",
            [326] = "Staggered-Stance Jab-Cross",
            [338] = "Overhead Triceps Stretch with Side Bend",
            [394] = "Inhale Arms Open, Exhale Arms Close and Round",
            [395] = "Overhead Hold with Deep Ribcage Breaths",
            [396] = "Unsupported Single-Leg Balance Hold",
            [397] = "Exhale Forward, Inhale Back Weight Shift",
            [398] = "Inhale Arms Open, Exhale Self-Hug and Fold",
            [399] = "Inhale Chest Open, Exhale Arms Close with Shallow Squat",
            [400] = "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down",
            [401] = "Alternating Inhale-Twist, Exhale-Push",
            [409] = "Full Neck Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track One Thumb Side to Side",
            [497] = "Track Finger in Circles",
            [501] = "Single-Leg Thumb-Focus Head Turns",
            [508] = "Curl Raised Leg with One Arm",
            [510] = "Clasped-Hands Chest-Opening Forward-Fold Hold",
            [513] = "Collarbone-Anchored Diagonal Neck Stretch",
            [588] = "Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [611] = "Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Clamshell",
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
        Assert.Equal("Standing Palms-Up Arm Raise", restoredShoulderRaise.Name);
        Assert.Null(restoredShoulderRaise.RetiredName);
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
