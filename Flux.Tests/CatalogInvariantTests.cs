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
        Exercise[] timedSideExercises = exercises
            .Where(exercise =>
                exercise.SideSequence != ExerciseSideSequence.Continuous)
            .ToArray();
        Assert.Equal(105, timedSideExercises.Length);
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
            [143] = ExerciseSideSequence.ScreenRightThenLeft,
            [215] = ExerciseSideSequence.ScreenLeftThenRight,
            [216] = ExerciseSideSequence.ScreenLeftThenRight,
            [217] = ExerciseSideSequence.Continuous,
            [218] = ExerciseSideSequence.Continuous,
            [237] = ExerciseSideSequence.ScreenLeftThenRight,
            [241] = ExerciseSideSequence.ScreenLeftThenRight,
            [256] = ExerciseSideSequence.Continuous,
            [257] = ExerciseSideSequence.Continuous,
            [258] = ExerciseSideSequence.Continuous,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenLeftThenRight,
            [291] = ExerciseSideSequence.Continuous,
            [292] = ExerciseSideSequence.ScreenRightThenLeft,
            [293] = ExerciseSideSequence.ScreenLeftThenRight,
            [294] = ExerciseSideSequence.Continuous,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.ScreenRightThenLeft,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.Continuous,
            [501] = ExerciseSideSequence.ScreenRightThenLeft,
            [508] = ExerciseSideSequence.ScreenLeftThenRight,
            [611] = ExerciseSideSequence.Continuous,
            [619] = ExerciseSideSequence.Continuous,
            [648] = ExerciseSideSequence.ScreenRightThenLeft,
            [649] = ExerciseSideSequence.ScreenLeftThenRight,
            [884] = ExerciseSideSequence.ScreenRightThenLeft,
            [885] = ExerciseSideSequence.ScreenRightThenLeft,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
        };
        Assert.All(auditedSideSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).SideSequence));
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
            [241] = "Straight-Hand Knuckle-Bend Flow",
            [270] = "Palm-Squeeze Forward Press",
            [290] = "Low Palm Scoop to Side Opening",
            [394] = "Standing Arms Open and Close",
            [397] = "Staggered-Stance Weight Shift",
            [409] = "Full Neck Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track One Thumb Side to Side",
            [497] = "Track Finger in Circles",
            [501] = "Single-Leg Thumb-Focus Head Turns",
            [508] = "Curl Raised Leg with One Arm",
            [588] = "Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [611] = "Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Clamshell",
            [843] = "Behind-Back Wrist-Pull Neck Stretch",
            [969] = "Chair-Pose Hold",
        };
        Assert.All(auditedCorrectedNames, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).Name));

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
