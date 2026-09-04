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
        Assert.All(document.RootElement.EnumerateArray(), element =>
        {
            Assert.True(element.TryGetProperty("muscularDemand", out JsonElement value));
            Assert.Equal(JsonValueKind.Number, value.ValueKind);
            Assert.InRange(
                value.GetInt32(),
                Exercise.MinimumMuscularDemand,
                Exercise.MaximumMuscularDemand);
            Assert.True(element.TryGetProperty(
                "wallRequired",
                out JsonElement wallRequired));
            Assert.Contains(
                wallRequired.ValueKind,
                new[] { JsonValueKind.True, JsonValueKind.False });
            Assert.True(element.TryGetProperty(
                "soleWallContactRequired",
                out JsonElement soleWallContactRequired));
            Assert.Contains(
                soleWallContactRequired.ValueKind,
                new[] { JsonValueKind.True, JsonValueKind.False });
            Assert.False(
                soleWallContactRequired.GetBoolean() &&
                !wallRequired.GetBoolean());
        });
        Assert.Equal(121, exercises.Count(exercise => exercise.MuscularDemand == 0));
        Assert.Equal(240, exercises.Count(exercise => exercise.MuscularDemand == 1));
        Assert.Equal(153, exercises.Count(exercise => exercise.MuscularDemand == 2));
        Assert.All(Enum.GetValues<CanonicalMuscleGroup>(), canonicalGroup =>
            Assert.Contains(exercises, exercise =>
                exercise.PrimaryCanonicalGroup == canonicalGroup));
        HashSet<int> reviewedAbdominalSecondaryIds =
        [
            17, 21, 59, 124, 125, 132, 174, 176, 177, 182, 183, 186, 219,
            227, 292, 305, 394, 395, 408, 449, 470, 524, 525, 526, 527,
            542, 547, 548, 570, 577, 618, 625, 790, 801, 804, 825, 884,
            885, 905, 917, 973, 998,
        ];
        Assert.True(reviewedAbdominalSecondaryIds.SetEquals(exercises
            .Where(exercise => exercise.SecondaryCanonicalGroups.Contains(
                CanonicalMuscleGroup.AbdominalWall))
            .Select(exercise => exercise.Id)));
        Assert.All(new[] { 266, 287, 591, 603, 701 }, exerciseId =>
            Assert.DoesNotContain(
                CanonicalMuscleGroup.AbdominalWall,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SecondaryCanonicalGroups));

        Exercise squatObliqueCrunch = exercises.Single(exercise =>
            exercise.Id == 132);
        Assert.Equal(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            squatObliqueCrunch.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.MaximumMuscularDemand,
            squatObliqueCrunch.MuscularDemand);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            squatObliqueCrunch.SecondaryCanonicalGroups);
        Exercise widePlieSideBend = exercises.Single(exercise =>
            exercise.Id == 905);
        Assert.Equal(
            CanonicalMuscleGroup.MajorHipAdductors,
            widePlieSideBend.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.MaximumMuscularDemand,
            widePlieSideBend.MuscularDemand);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            widePlieSideBend.SecondaryCanonicalGroups);

        Assert.All(new[] { 910, 948, 954 }, exerciseId => Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            exercises.Single(exercise => exercise.Id == exerciseId)
                .PrimaryCanonicalGroup));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.SecondaryCanonicalGroups.Contains(
                CanonicalMuscleGroup.PelvicFloorAndPerineum));
        Exercise pelvicFloorSlowSqueeze = exercises.Single(exercise =>
            exercise.Id == 918);
        Assert.Equal(
            "Standing Pelvic-Floor Slow Squeeze and Release",
            pelvicFloorSlowSqueeze.Name);
        Assert.Equal(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            pelvicFloorSlowSqueeze.PrimaryCanonicalGroup);
        Assert.Empty(pelvicFloorSlowSqueeze.SecondaryCanonicalGroups);
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            pelvicFloorSlowSqueeze.MuscularDemand);
        Assert.True(pelvicFloorSlowSqueeze.Silent);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            pelvicFloorSlowSqueeze.HardFloorCompatibility);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            pelvicFloorSlowSqueeze.SideSequence);
        Exercise fixedGazeHeadTurnMarch = exercises.Single(exercise =>
            exercise.Id == 919);
        Assert.Equal(
            "March in Place with Fixed-Gaze Head Turns",
            fixedGazeHeadTurnMarch.Name);
        Assert.Equal(
            CanonicalMuscleGroup.CranialMuscles,
            fixedGazeHeadTurnMarch.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
                CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
            },
            fixedGazeHeadTurnMarch.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            fixedGazeHeadTurnMarch.MuscularDemand);
        Assert.True(fixedGazeHeadTurnMarch.Silent);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            fixedGazeHeadTurnMarch.HardFloorCompatibility);
        Assert.Equal(
            ExerciseInsectCompatibility.Compatible,
            fixedGazeHeadTurnMarch.InsectCompatibility);
        Assert.Equal(
            ExerciseShyCompatibility.Compatible,
            fixedGazeHeadTurnMarch.ShyCompatibility);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            fixedGazeHeadTurnMarch.SideSequence);
        Assert.Single(fixedGazeHeadTurnMarch.SequenceBlocks);
        Exercise wallSidePlankKneeDrive = exercises.Single(exercise =>
            exercise.Id == 911);
        Assert.Equal(
            "Single-Side Wall Side-Plank Knee Drive",
            wallSidePlankKneeDrive.Name);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            wallSidePlankKneeDrive.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.MaximumMuscularDemand,
            wallSidePlankKneeDrive.MuscularDemand);
        Assert.True(wallSidePlankKneeDrive.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            wallSidePlankKneeDrive.HardFloorCompatibility);
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftThenRight,
            wallSidePlankKneeDrive.SideSequence);

        Exercise verticalDeadBug = exercises.Single(exercise =>
            exercise.Id == 913);
        Assert.Equal("Wall-Supported Vertical Dead Bug", verticalDeadBug.Name);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            verticalDeadBug.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            verticalDeadBug.MuscularDemand);
        Assert.True(verticalDeadBug.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            verticalDeadBug.HardFloorCompatibility);
        Assert.Equal(
            ExerciseUpperBodyClothingRequirement.ClothingRequired,
            verticalDeadBug.UpperBodyClothingRequirement);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            verticalDeadBug.SideSequence);
        Exercise standingPelvicTilt = exercises.Single(exercise =>
            exercise.Id == 916);
        Assert.Equal(
            "Standing Pelvic-Tilt Repetitions",
            standingPelvicTilt.Name);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            standingPelvicTilt.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.DeepAndIntersegmentalBack,
                CanonicalMuscleGroup.SpinalExtensors,
            },
            standingPelvicTilt.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.MinimumMuscularDemand,
            standingPelvicTilt.MuscularDemand);
        Assert.False(standingPelvicTilt.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            standingPelvicTilt.HardFloorCompatibility);
        Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            standingPelvicTilt.MirrorRelationship);
        Assert.Equal(
            ExerciseMirrorCoverage.FullBody,
            standingPelvicTilt.MinimumMirrorCoverage);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            standingPelvicTilt.SideSequence);
        Exercise standingSpinalWave = exercises.Single(exercise =>
            exercise.Id == 917);
        Assert.Equal("Standing Spinal Wave", standingSpinalWave.Name);
        Assert.Equal(
            CanonicalMuscleGroup.DeepAndIntersegmentalBack,
            standingSpinalWave.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.AbdominalWall,
                CanonicalMuscleGroup.SpinalExtensors,
            },
            standingSpinalWave.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.MinimumMuscularDemand,
            standingSpinalWave.MuscularDemand);
        Assert.False(standingSpinalWave.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            standingSpinalWave.HardFloorCompatibility);
        Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            standingSpinalWave.MirrorRelationship);
        Assert.Equal(
            ExerciseMirrorCoverage.UpperBody,
            standingSpinalWave.MinimumMirrorCoverage);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            standingSpinalWave.SideSequence);
        Dictionary<int, int[]> expectedSessionMovements = new()
        {
            [104] = [104, 136, 626],
            [113] = [113, 135],
            [115] = [115, 996, 997],
            [117] = [117, 123],
            [120] = [120, 184],
            [124] = [124, 636],
            [125] = [125, 973],
            [159] = [159, 649],
            [177] = [177, 186],
            [214] = [214, 223],
            [231] = [231, 685],
            [256] = [256, 845],
            [261] = [261, 677],
            [262] = [262, 507],
            [514] = [514, 521],
            [755] = [755, 756],
        };
        Dictionary<int, int[]> actualSessionMovements = exercises
            .Where(exercise => exercise.SessionMovementId > 0)
            .GroupBy(exercise => exercise.SessionMovementId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(exercise => exercise.Id).Order().ToArray());
        Assert.Equal(expectedSessionMovements.Keys.Order(), actualSessionMovements.Keys.Order());
        Assert.All(expectedSessionMovements, expected => Assert.Equal(
            expected.Value,
            actualSessionMovements[expected.Key]));
        Assert.All(exercises, exercise => Assert.Equal(0, exercise.Score));
        Assert.Equal(0, exercises.Single(exercise => exercise.Id == 211).MuscularDemand);
        Assert.Equal(1, exercises.Single(exercise => exercise.Id == 264).MuscularDemand);
        Assert.Equal(2, exercises.Single(exercise => exercise.Id == 101).MuscularDemand);
        Exercise miniSquatCalfRaise = exercises.Single(exercise => exercise.Id == 565);
        Assert.Equal("Mini-Squat Calf Raises with Forward Reach", miniSquatCalfRaise.Name);
        Assert.Equal(CanonicalMuscleGroup.Soleus, miniSquatCalfRaise.PrimaryCanonicalGroup);
        Assert.Equal(Exercise.MaximumMuscularDemand, miniSquatCalfRaise.MuscularDemand);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            miniSquatCalfRaise.HardFloorCompatibility);
        Assert.Contains(
            CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot,
            miniSquatCalfRaise.SecondaryCanonicalGroups);
        Exercise forwardMarchingArmCircles = exercises.Single(exercise =>
            exercise.Id == 302);
        Assert.Equal("Marching Forward Arm Circles", forwardMarchingArmCircles.Name);
        Assert.Equal(
            new[] { 302, 304 },
            forwardMarchingArmCircles.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 302, 304 },
            exerciseId => Assert.Equal(
                1,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .MuscularDemand));
        Exercise standingMarchTwist = exercises.Single(exercise => exercise.Id == 305);
        Assert.Equal("Standing March with Torso Twist", standingMarchTwist.Name);
        Assert.Equal(ExerciseSideSequence.Alternating, standingMarchTwist.SideSequence);
        Assert.Equal(Exercise.MinimumMuscularDemand, standingMarchTwist.MuscularDemand);
        Exercise neckFlexion = exercises.Single(exercise => exercise.Id == 307);
        Assert.Equal(
            new[] { 307, 310 },
            neckFlexion.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 307, 308, 309, 310 },
            exerciseId =>
            {
                Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
                Assert.Equal("Self-resistance", exercise.Practice);
                Assert.Equal(ExerciseMode.Hold, exercise.Mode);
                Assert.Equal(Exercise.MaximumMuscularDemand, exercise.MuscularDemand);
                Assert.Equal(
                    ExerciseInsectCompatibility.Incompatible,
                    exercise.InsectCompatibility);
                Assert.Equal(
                    ExerciseHardFloorCompatibility.Compatible,
                    exercise.HardFloorCompatibility);
            });
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftThenRight,
            exercises.Single(exercise => exercise.Id == 308).SideSequence);
        Assert.Equal(
            ExerciseSideSequence.ScreenRightThenLeft,
            exercises.Single(exercise => exercise.Id == 309).SideSequence);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.InsectCompatibility == ExerciseInsectCompatibility.Unreviewed);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Unreviewed);
        Assert.Equal(310, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Compatible));
        Assert.Equal(204, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Incompatible));
        Assert.All(
            new[] { 37, 610, 326 },
            exerciseId => Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .HardFloorCompatibility));
        Assert.All(
            new[] { 101, 167, 367 },
            exerciseId => Assert.Equal(
                ExerciseHardFloorCompatibility.Compatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .HardFloorCompatibility));
        var pogoHeadMovements = new Dictionary<int, string>
        {
            [439] = "Pogo Bounces with Fixed-Gaze Head Turns",
            [442] = "Pogo Bounces with Fixed-Gaze Head Nods",
            [444] = "Pogo Bounces with Fixed-Gaze Head Tilts",
        };
        Assert.All(pogoHeadMovements, expected =>
        {
            Exercise exercise = exercises.Single(candidate =>
                candidate.Id == expected.Key);
            Assert.Equal(expected.Value, exercise.Name);
            Assert.StartsWith("PogoHead", exercise.MotionProfile);
            Assert.Equal(1, exercise.MuscularDemand);
            Assert.False(exercise.Silent);
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                exercise.InsectCompatibility);
            Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                exercise.HardFloorCompatibility);
            Assert.Contains(
                CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot,
                exercise.SecondaryCanonicalGroups);
        });
        Exercise[] airborneImpactExercises = exercises.Where(exercise =>
            System.Text.RegularExpressions.Regex.IsMatch(
                exercise.Name,
                @"(?i)\b(?:jump(?:ing|s)?|hop(?:ping|s)?|pogo|bounce(?:s)?|jack(?:s)?|bound(?:s|ing)?)\b") ||
            System.Text.RegularExpressions.Regex.IsMatch(
                exercise.MotionProfile,
                @"(?:Jump|Hop|Pogo|Bounce|Jack|Bound)"))
            .ToArray();
        Assert.NotEmpty(airborneImpactExercises);
        Assert.All(airborneImpactExercises, exercise => Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            exercise.HardFloorCompatibility));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.UpperBodyClothingRequirement ==
                ExerciseUpperBodyClothingRequirement.Unreviewed);
        Assert.Equal(
            new HashSet<int> { 134, 137, 175, 579, 580, 801, 913 },
            exercises.Where(exercise =>
                    exercise.UpperBodyClothingRequirement ==
                        ExerciseUpperBodyClothingRequirement.ClothingRequired)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            new HashSet<int> { 524, 525, 526, 527, 528, 790, 993 },
            exercises.Where(exercise =>
                    exercise.UpperBodyClothingRequirement ==
                        ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            500,
            exercises.Count(exercise =>
                exercise.UpperBodyClothingRequirement ==
                    ExerciseUpperBodyClothingRequirement.Agnostic));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.ShyCompatibility == ExerciseShyCompatibility.Unreviewed);
        HashSet<int> shyIncompatibleExerciseIds =
        [
            20, 58, 92, 93, 160, 178, 180, 181, 182, 183, 242, 276,
            285, 286, 291, 294, 327, 408, 411, 412, 413, 414, 415,
            416, 418, 419, 442, 444, 449, 478, 480, 484, 485, 486, 489,
            490, 491, 492, 493, 495, 497, 499, 500,
            501, 505, 506, 511, 513, 514, 515, 517, 518, 519, 520, 521,
            522, 523, 524, 525, 526, 527, 528, 533, 534, 535, 536, 541,
            545, 546, 556, 560, 561, 562, 588, 603, 614, 619, 666, 681,
            684, 790, 916, 917, 993,
        ];
        Assert.Equal(
            shyIncompatibleExerciseIds,
            exercises.Where(exercise =>
                    exercise.ShyCompatibility ==
                        ExerciseShyCompatibility.Incompatible)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(430, exercises.Count(exercise =>
            exercise.ShyCompatibility == ExerciseShyCompatibility.Compatible));
        Assert.Equal(
            ExerciseShyCompatibility.Compatible,
            exercises.Single(exercise => exercise.Id == 439).ShyCompatibility);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Unreviewed);
        Assert.Equal(
            83,
            exercises.Count(exercise =>
                exercise.MirrorRelationship ==
                    ExerciseMirrorRelationship.BenefitsGreatly));
        Assert.Equal(
            419,
            exercises.Count(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic));
        Assert.Equal(
            12,
            exercises.Count(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly));
        Assert.Equal(6, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(6, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(32, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(51, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(419, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None));
        Assert.DoesNotContain(exercises, exercise => exercise.Id == 90);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Name.StartsWith("Mirror-Guided ", StringComparison.Ordinal));
        Assert.Equal(
            new HashSet<int> { 515, 520, 521, 522, 523, 993 },
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            new HashSet<int> { 524, 525, 526, 527, 528, 790 },
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Exercise mostMuscularPose = exercises.Single(exercise => exercise.Id == 790);
        Assert.Equal("Mirror Most-Muscular Posing", mostMuscularPose.Name);
        Assert.Equal(CanonicalMuscleGroup.ScapularGirdle,
            mostMuscularPose.PrimaryCanonicalGroup);
        Assert.Equal(ExerciseMode.Repetition, mostMuscularPose.Mode);
        Assert.Equal(ExercisePresentation.Motion, mostMuscularPose.Presentation);
        Assert.Equal(Exercise.MaximumMuscularDemand, mostMuscularPose.MuscularDemand);
        Assert.Equal(ExerciseHardFloorCompatibility.Compatible,
            mostMuscularPose.HardFloorCompatibility);
        int[] bodybuildingPosingIds = [524, 525, 526, 527, 528, 790];
        Assert.All(bodybuildingPosingIds, exerciseId =>
        {
            Exercise pose = exercises.Single(exercise => exercise.Id == exerciseId);
            Assert.EndsWith(" Posing", pose.Name);
            Assert.Equal(ExerciseMode.Repetition, pose.Mode);
            Assert.Equal(ExercisePresentation.Motion, pose.Presentation);
            Assert.Equal(0, pose.HoldFramePercent);
        });
        Exercise standingVacuum = exercises.Single(exercise => exercise.Id == 993);
        Assert.Equal("Mirror Standing Vacuum Repetitions", standingVacuum.Name);
        Assert.Equal(CanonicalMuscleGroup.AbdominalWall,
            standingVacuum.PrimaryCanonicalGroup);
        Assert.Contains(CanonicalMuscleGroup.BreathingMuscles,
            standingVacuum.SecondaryCanonicalGroups);
        Assert.Equal(ExerciseMode.Repetition, standingVacuum.Mode);
        Assert.Equal(Exercise.ModerateMuscularDemand, standingVacuum.MuscularDemand);
        Assert.All(
            exercises.Where(exercise =>
                new HashSet<int> { 94, 95, 99, 100, 497, 498, 500, 511, 514 }
                    .Contains(exercise.Id)),
            exercise =>
            {
                Assert.Equal(ExerciseMirrorRelationship.Agnostic, exercise.MirrorRelationship);
                Assert.Equal("None", exercise.Equipment);
            });
        Assert.All(
            exercises.Where(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly),
            exercise => Assert.Equal("Mirror", exercise.Equipment));
        Assert.All(
            exercises.Where(exercise =>
                exercise.MirrorRelationship != ExerciseMirrorRelationship.MirrorOnly),
            exercise => Assert.Equal("None", exercise.Equipment));
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.Empty(
            WorkoutModifierPolicy.FindMirrorCategoryDeficiencies(exercises));
        Exercise[] wallRequired = exercises
            .Where(exercise => exercise.WallRequired)
            .ToArray();
        Exercise[] baseWallRequired = wallRequired
            .Where(exercise => !exercise.SoleWallContactRequired)
            .ToArray();
        Exercise[] soleWallRequired = wallRequired
            .Where(exercise => exercise.SoleWallContactRequired)
            .ToArray();
        Assert.Equal(32, wallRequired.Length);
        Assert.Equal(27, baseWallRequired.Length);
        Assert.Equal(
            27,
            baseWallRequired
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
        Assert.Equal(5, soleWallRequired.Length);
        Assert.Equal(
            new HashSet<int> { 563, 564, 567, 568, 574 },
            soleWallRequired.Select(exercise => exercise.Id).ToHashSet());
        Assert.Equal(
            5,
            soleWallRequired
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
        Assert.Empty(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));
        Assert.Empty(WorkoutModifierPolicy
            .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.All(wallRequired, exercise =>
        {
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.None));
        });
        Assert.All(baseWallRequired, exercise =>
        {
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Wall |
                    WorkoutModifiers.UpperBodyClothing));
        });
        WorkoutModifiers soleWallProfile =
            WorkoutModifierPolicy.WithWallEquipment(
                WorkoutModifiers.UpperBodyClothing,
                WallEquipment.SolesMayTouch);
        Assert.All(soleWallRequired, exercise =>
        {
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Wall));
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                soleWallProfile));
        });
        Assert.All(
            exercises.Where(exercise => exercise.Mode == ExerciseMode.Hold),
            exercise => Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                exercise.InsectCompatibility));
        WorkoutModifierPairCoverageDeficiency[] pairwiseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises).ToArray();
        Assert.Empty(pairwiseDeficiencies);
        Assert.Equal(
            5,
            WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(3));
        Assert.Equal(
            1,
            WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(30));
        WorkoutHardFloorCategoryCoverageDeficiency[] hardFloorCategoryDeficiencies =
            WorkoutModifierPolicy
                .FindHardFloorCategoryCoverageDeficiencies(exercises)
                .ToArray();
        Assert.Empty(hardFloorCategoryDeficiencies);
        WorkoutMuscularDemandCoverageDeficiency[] muscularDemandDeficiencies =
            WorkoutModifierPolicy
                .FindMuscularDemandCoverageDeficiencies(exercises)
                .ToArray();
        Assert.Empty(muscularDemandDeficiencies);
        WorkoutModifierMaterialityDeficiency[] materialityDeficiencies =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises).ToArray();
        Assert.Empty(materialityDeficiencies);
        WorkoutProfileLineupDeficiency[] lineupDeficiencies =
            WorkoutModifierPolicy.FindDistinctLineupDeficiencies(exercises).ToArray();
        Assert.Empty(lineupDeficiencies);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        IReadOnlyDictionary<int, Exercise> sequenceRootByExerciseId = exercises
            .Where(root => root.SequenceBlocks.Length > 0)
            .SelectMany(root => root.SequenceBlocks
                .Select(block => (block.ExerciseId, Root: root)))
            .DistinctBy(entry => entry.ExerciseId)
            .ToDictionary(entry => entry.ExerciseId, entry => entry.Root);
        Parallel.ForEach(
            WorkoutModifierPolicy.ValidationProfiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            },
            profile =>
            {
                // Profiles are independent. Give each one a deterministic
                // random stream and service so exhaustive validation can use
                // separate cores without sharing mutable session state.
                    var profileService = new ExerciseSessionService(
                        exercises,
                        new Random(1));
                foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
                {
                    var profileState = new WorkoutState();
                    try
                    {
                        profileService.StartWorkout(profileState, minutes, profile);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"Workout generation failed for {minutes} minutes " +
                            $"with modifier profile {profile}.",
                            exception);
                    }
                    WorkoutGroup[] activeGroups = profileService
                        .GetActiveGroups(profileState)
                        .ToArray();
                    Exercise[] baseSelections = activeGroups
                        .GroupBy(group => group.SelectionKey, StringComparer.Ordinal)
                        .Select(rounds => profileService.GetSelectedExercise(
                            profileState,
                            rounds.First()))
                        .ToArray();
                    Assert.Equal(
                        baseSelections.Length,
                        baseSelections
                            .Select(WorkoutModifierPolicy.GetSessionMovementId)
                            .Distinct()
                            .Count());
                    Assert.All(profileService.GetActiveGroups(profileState), group =>
                        Assert.True(WorkoutModifierPolicy.IsCompatible(
                            profileService.GetSelectedExercise(profileState, group),
                            profile)));
                    IReadOnlyList<WorkoutGroup> resolutionGroups =
                        MassGroupingTaxonomy.GetResolution(
                            minutes > 30 ? 30 : minutes).Groups;
                    Assert.All(
                        activeGroups.GroupBy(
                            group => group.SelectionKey,
                            StringComparer.Ordinal),
                        rounds =>
                        {
                            Exercise selectedMember =
                                profileService.GetSelectedExercise(
                                    profileState,
                                    rounds.First());
                            Exercise root =
                                sequenceRootByExerciseId[selectedMember.Id];
                            Assert.Contains(
                                WorkoutSequencePolicy.GetPlacementOptions(
                                    root,
                                    exercisesById,
                                    resolutionGroups),
                                placement => placement.Any(group =>
                                    group.Id == rounds.Key));
                        });
                    Assert.Equal(minutes, activeGroups.Length);
                }
            });
        var profileService = new ExerciseSessionService(exercises, new Random(1));
        WorkoutModifiers allModifiers = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence |
            WorkoutModifiers.Mirror;
        foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
        {
            var profileState = new WorkoutState();
            profileService.StartWorkout(profileState, minutes, allModifiers);
            Assert.Equal(allModifiers, profileState.ActiveWorkoutModifiers);
            WorkoutGroup[] activeGroups = profileService
                .GetActiveGroups(profileState)
                .ToArray();
            Assert.All(activeGroups, group =>
                Assert.True(WorkoutModifierPolicy.IsCompatible(
                    profileService.GetSelectedExercise(profileState, group),
                    allModifiers)));
            IReadOnlyList<WorkoutGroup> resolutionGroups =
                MassGroupingTaxonomy.GetResolution(
                    minutes > 30 ? 30 : minutes).Groups;
            Assert.All(
                activeGroups.GroupBy(group => group.SelectionKey, StringComparer.Ordinal),
                rounds =>
                {
                    Exercise selectedMember = profileService.GetSelectedExercise(
                        profileState,
                        rounds.First());
                    Exercise root = sequenceRootByExerciseId[selectedMember.Id];
                    Assert.Contains(
                        WorkoutSequencePolicy.GetPlacementOptions(
                            root,
                            exercisesById,
                            resolutionGroups),
                        placement => placement.Any(group => group.Id == rounds.Key));
                });
        }
        Exercise[] breathingExercises = exercises
            .Where(exercise =>
                exercise.PrimaryCanonicalGroup == CanonicalMuscleGroup.BreathingMuscles)
            .ToArray();
        Assert.Equal(2, breathingExercises.Length);
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
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            overheadBreathingFlow.PrimaryCanonicalGroup);
        Assert.Contains(
            CanonicalMuscleGroup.BreathingMuscles,
            overheadBreathingFlow.SecondaryCanonicalGroups);
        Exercise alternatingSideTap = exercises.Single(exercise => exercise.Id == 397);
        Assert.Equal(
            "Alternating Side Tap with Diagonal Arm Sweep",
            alternatingSideTap.Name);
        Assert.Equal(ExerciseSideSequence.Alternating, alternatingSideTap.SideSequence);
        ExerciseSequenceBlock alternatingSideTapBlock = Assert.Single(
            alternatingSideTap.SequenceBlocks);
        Assert.Equal(alternatingSideTap.Id, alternatingSideTapBlock.ExerciseId);
        Assert.Equal(ExerciseSequenceSideCue.None, alternatingSideTapBlock.SideCue);
        Assert.False(alternatingSideTapBlock.MirrorMedia);
        Assert.Equal(CanonicalMuscleGroup.HipAbductors,
            alternatingSideTap.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.BreathingMuscles,
            alternatingSideTap.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.AccessoryHipAdductors,
            alternatingSideTap.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.ScapularGirdle,
            alternatingSideTap.SecondaryCanonicalGroups);
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
        Assert.Equal(158, timedSideExercises.Length);
        Assert.DoesNotContain(
            timedSideExercises.Where(exercise =>
                !exercise.SideSequence.UsesTimedLeadStances()),
            exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] alternatingExercises = exercises
            .Where(exercise =>
                exercise.SideSequence == ExerciseSideSequence.Alternating)
            .ToArray();
        Assert.Equal(161, alternatingExercises.Length);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 219);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 15);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 429);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 398);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 515);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 919);
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Dictionary<int, ExerciseDirectionSequence> auditedDirectionSequences = new()
        {
            [264] = ExerciseDirectionSequence.BackwardThenForward,
            [275] = ExerciseDirectionSequence.BackwardThenForward,
            [406] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [409] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [460] = ExerciseDirectionSequence.ForwardThenBackward,
            [588] = ExerciseDirectionSequence.BackwardThenForward,
            [608] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [611] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [743] = ExerciseDirectionSequence.BackwardThenForward,
        };
        Assert.Equal(
            auditedDirectionSequences.Keys.ToHashSet(),
            timedDirectionExercises.Select(exercise => exercise.Id).ToHashSet());
        Assert.All(auditedDirectionSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key)
                    .DirectionSequence));
        Dictionary<int, int[]> auditedMultiMemberSequences = new()
        {
            [96] = [96, 540],
            [115] = [115, 532],
            [143] = [143, 538],
            [160] = [160, 533],
            [178] = [178, 535],
            [179] = [179, 539],
            [180] = [180, 534],
            [181] = [181, 536],
            [211] = [211, 213],
            [214] = [214, 755],
            [220] = [220, 543],
            [223] = [223, 756],
            [252] = [252, 253, 254],
            [264] = [264, 406],
            [285] = [285, 541],
            [286] = [286, 545],
            [288] = [288, 758],
            [291] = [291, 294],
            [292] = [292, 542],
            [302] = [302, 304],
            [307] = [307, 310],
            [327] = [327, 546],
            [329] = [329, 531],
            [367] = [367, 529],
            [392] = [392, 399, 400],
            [393] = [393, 537],
            [414] = [414, 418],
            [415] = [415, 416],
            [420] = [420, 421, 426],
            [459] = [459, 468, 469],
            [465] = [465, 445],
            [491] = [491, 501],
            [500] = [500, 505, 506],
            [502] = [502, 503],
            [566] = [566, 581, 582],
            [610] = [610, 232],
            [612] = [612, 530],
            [617] = [617, 620],
            [742] = [742, 338],
            [784] = [784, 969, 1000],
            [834] = [834, 914],
            [910] = [910, 962],
            [948] = [948, 949],
        };
        int[] actualMultiMemberRoots = exercises
            .Where(root => root.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Count() > 1)
            .Select(root => root.Id)
            .Order()
            .ToArray();
        Assert.Equal(auditedMultiMemberSequences.Keys.Order(), actualMultiMemberRoots);
        Assert.All(auditedMultiMemberSequences, expected =>
        {
            Exercise root = exercises.Single(exercise => exercise.Id == expected.Key);
            int[] expectedBlocks = expected.Value
                .SelectMany(memberId =>
                {
                    Exercise member = exercises.Single(exercise => exercise.Id == memberId);
                    int sideBlocks = member.SideSequence.UsesTimedSides() ? 2 : 1;
                    int directionBlocks = member.DirectionSequence ==
                            ExerciseDirectionSequence.None
                        ? 1
                        : 2;
                    return Enumerable.Repeat(memberId, sideBlocks * directionBlocks);
                })
                .ToArray();
            Assert.Equal(
                expectedBlocks,
                root.SequenceBlocks.Select(block => block.ExerciseId).ToArray());
            Assert.Single(root.SequenceBlocks
                .Select(block => exercises.Single(exercise =>
                    exercise.Id == block.ExerciseId).Mode)
                .Distinct());
        });
        Dictionary<int, int> expectedSequenceBlockDistribution = new()
        {
            [1] = 297,
            [2] = 128,
            [3] = 28,
            [4] = 11,
        };
        Dictionary<int, int> actualSequenceBlockDistribution = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .GroupBy(exercise => exercise.SequenceBlocks.Length)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(
            expectedSequenceBlockDistribution.Keys.Order(),
            actualSequenceBlockDistribution.Keys.Order());
        Assert.All(expectedSequenceBlockDistribution, expected => Assert.Equal(
            expected.Value,
            actualSequenceBlockDistribution[expected.Key]));
        var sequenceOwners = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .SelectMany(root => root.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Select(memberId => (RootId: root.Id, MemberId: memberId)))
            .ToArray();
        Assert.Equal(exercises.Length, sequenceOwners.Length);
        Assert.All(
            sequenceOwners.GroupBy(owner => owner.MemberId),
            owners => Assert.Single(owners));
        Assert.Equal(
            exercises.Select(exercise => exercise.Id).Order(),
            sequenceOwners.Select(owner => owner.MemberId).Order());
        Assert.All(
            exercises.Where(exercise => exercise.SequenceBlocks.Length > 0),
            root => Assert.Contains(
                root.SequenceBlocks,
                block => block.ExerciseId == root.Id));
        string[] oneWayCircleTerms =
        [
            "Clockwise", "Counterclockwise", "Forward", "Backward",
            "Inward", "Outward",
        ];
        HashSet<int> sequenceMemberIds = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .SelectMany(exercise => exercise.SequenceBlocks
                .Select(block => block.ExerciseId))
            .ToHashSet();
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Name.EndsWith("Circles", StringComparison.Ordinal) &&
            oneWayCircleTerms.Any(term => exercise.Name.Contains(
                term,
                StringComparison.Ordinal)) &&
            !sequenceMemberIds.Contains(exercise.Id));
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
            [234] = ExerciseSideSequence.Continuous,
            [236] = ExerciseSideSequence.Continuous,
            [237] = ExerciseSideSequence.Continuous,
            [239] = ExerciseSideSequence.Continuous,
            [240] = ExerciseSideSequence.Continuous,
            [241] = ExerciseSideSequence.Continuous,
            [242] = ExerciseSideSequence.Continuous,
            [245] = ExerciseSideSequence.ScreenRightThenLeft,
            [256] = ExerciseSideSequence.ScreenRightThenLeft,
            [257] = ExerciseSideSequence.Continuous,
            [258] = ExerciseSideSequence.ScreenRightThenLeft,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenRightThenLeft,
            [283] = ExerciseSideSequence.Alternating,
            [289] = ExerciseSideSequence.Continuous,
            [291] = ExerciseSideSequence.Alternating,
            [292] = ExerciseSideSequence.ScreenRightThenLeft,
            [293] = ExerciseSideSequence.ScreenLeftThenRight,
            [294] = ExerciseSideSequence.Alternating,
            [305] = ExerciseSideSequence.Alternating,
            [308] = ExerciseSideSequence.ScreenLeftThenRight,
            [309] = ExerciseSideSequence.ScreenRightThenLeft,
            [326] = ExerciseSideSequence.ScreenRightThenLeft,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [31] = ExerciseSideSequence.Alternating,
            [176] = ExerciseSideSequence.Alternating,
            [195] = ExerciseSideSequence.Alternating,
            [198] = ExerciseSideSequence.Alternating,
            [219] = ExerciseSideSequence.Alternating,
            [248] = ExerciseSideSequence.Alternating,
            [265] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [274] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [280] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [287] = ExerciseSideSequence.Alternating,
            [282] = ExerciseSideSequence.ScreenLeftThenRight,
            [390] = ExerciseSideSequence.Alternating,
            [391] = ExerciseSideSequence.Alternating,
            [394] = ExerciseSideSequence.Alternating,
            [395] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.Alternating,
            [398] = ExerciseSideSequence.Alternating,
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
            [421] = ExerciseSideSequence.Continuous,
            [427] = ExerciseSideSequence.Alternating,
            [468] = ExerciseSideSequence.Alternating,
            [473] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.Continuous,
            [507] = ExerciseSideSequence.Alternating,
            [508] = ExerciseSideSequence.Alternating,
            [512] = ExerciseSideSequence.ScreenRightThenLeft,
            [513] = ExerciseSideSequence.ScreenLeftThenRight,
            [515] = ExerciseSideSequence.Alternating,
            [576] = ExerciseSideSequence.Alternating,
            [577] = ExerciseSideSequence.ScreenRightThenLeft,
            [575] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [578] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [583] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [563] = ExerciseSideSequence.ScreenRightThenLeft,
            [564] = ExerciseSideSequence.ScreenRightThenLeft,
            [567] = ExerciseSideSequence.ScreenRightThenLeft,
            [568] = ExerciseSideSequence.ScreenRightThenLeft,
            [574] = ExerciseSideSequence.ScreenLeftThenRight,
            [591] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
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
            [884] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [885] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [886] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [887] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
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

        int[] leadStanceExerciseIds =
        [
            265, 274, 280, 473, 575, 578, 583, 591, 884, 885, 886, 887,
        ];
        Assert.Equal(
            leadStanceExerciseIds,
            exercises
                .Where(exercise => exercise.SideSequence.UsesTimedLeadStances())
                .Select(exercise => exercise.Id)
                .Order()
                .ToArray());
        Assert.All(
            leadStanceExerciseIds,
            exerciseId => Assert.True(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));

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
            213, 220, 225, 256, 258, 269,
            278, 279, 282, 285, 286, 326, 329,
            395, 396, 512, 513, 572, 577, 618, 636,
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
            198, 201, 218, 230, 234, 237, 239, 240, 241, 242, 248,
            251, 257, 262, 263, 266, 268, 270, 275, 283, 289, 291,
            294, 301, 314, 321, 556,
            394, 397, 413, 421, 425, 427, 468, 507, 516, 615, 677, 683, 687,
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
            [193] = "Wide-Stance Floor-to-Overhead Reach",
            [195] = "Side Lunge to Knee-Up Balance",
            [197] = "Parallel Squat-to-Calf Raise",
            [198] = "Wide Squat to Feet-Together Calf Raise",
            [199] = "Wide-Stance Side-to-Side Squat",
            [211] = "Bent-Elbow Wrist-Flexion Stretch",
            [212] = "Unsupported Sissy Squat",
            [213] = "Bent-Elbow Wrist-Extension Stretch",
            [214] = "Inward Wrist Circles",
            [215] = "Forearm Pronation-Supination Flow",
            [216] = "Interlaced-Finger Palm-Out Stretch",
            [217] = "Tree Pose Hold",
            [218] = "Fingertip Wall Push-Ups",
            [223] = "Inward Controlled Wrist Circles",
            [224] = "Qigong Interlaced Wrist Rolls",
            [225] = "Opposite-Hand Fist-Down Wrist Stretch",
            [231] = "Karate Reverse Punch",
            [232] = "Extended Side Angle Hold",
            [233] = "Standing Wrist Flexion Stretch",
            [234] = "Standing W Extensions",
            [236] = "Bilateral Wrist Figure Eights",
            [237] = "Standing Overhead Elbow Extensions",
            [239] = "Standing Reverse Prayer Stretch",
            [240] = "Grapevine Step",
            [241] = "Isometric Palm Press Hold",
            [242] = "Jazz Square",
            [245] = "Straight-Punch to Shovel-Hook Combo",
            [246] = "Bodyweight Cuban Rotation",
            [248] = "Alternating Side-Tap Palm Pushes",
            [251] = "Forward Fold to Overhead Reach",
            [256] = "Overhead Side-Stretch Hold",
            [257] = "Finger Spread to Interlace Stretch",
            [258] = "Karate Downward Block",
            [262] = "Standing Bicycle Crunches",
            [270] = "Goalpost Chest-Opener Hold",
            [282] = "Side-Step Knee Drive with Alternating Side Punches",
            [283] = "Alternating Palm Strikes",
            [288] = "Forward Knee-and-Ankle Circles",
            [289] = "Fingertip Spider Presses",
            [290] = "Low Palm Scoop to Side Opening",
            [291] = "Inward Knife-Hand Strikes",
            [294] = "Outward Knife-Hand Strikes",
            [326] = "Rear-Hand Straight Punch",
            [338] = "Overhead Triceps Stretch with Side Bend",
            [390] = "Inhale Arms Up, Exhale Step-Touch",
            [391] = "Inhale Arms Open, Exhale High-Knee",
            [394] = "Inhale Open, Exhale Cross-Body Knee",
            [395] = "Single-Side Inhale Reach Up, Exhale Knee Lift",
            [396] = "Single-Leg Knee-Lift Balance Hold",
            [397] = "Alternating Side Tap with Diagonal Arm Sweep",
            [398] = "Inhale Arms Open, Exhale Self-Hug and Fold",
            [399] = "Inhale Chest Open, Exhale Arms Close with Shallow Squat",
            [400] = "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down",
            [401] = "Alternating Inhale-Twist, Exhale-Push",
            [264] = "Standing Arm Circles",
            [275] = "Small Arm Circles",
            [406] = "Standing Wheel Arm Circles",
            [409] = "Full Neck Circles",
            [417] = "Narrow-Stance Overhead-to-Floor Reach",
            [460] = "Jogging in Place with Arm Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track One Thumb Side to Side",
            [501] = "Keep Eyes on Thumb While Turning Head",
            [507] = "Alternating Cross-Body Knee-to-Elbow Crunch",
            [508] = "Side-Step with Two-Arm Overhead Reach",
            [510] = "Clasped-Hands Chest-Opening Forward-Fold Hold",
            [996] = "Partial Pistol Squat",
            [997] = "Bottom Pistol Squat Hold",
            [998] = "Deep-Squat Thoracic Rotation",
            [999] = "Deep-Squat Walk",
            [512] = "Standing Upper-Back and Neck Hug Stretch",
            [513] = "Single-Leg Head Nods",
            [95] = "Single-Leg Knee-Raise Hold",
            [556] = "Alternating Backfists",
            [561] = "Tiptoe Running Steps with Head Spot",
            [562] = "Ballet Calf Raises with Arm Sweeps",
            [563] = "Hip Airplane with Back Foot on Wall",
            [564] = "Standing Foot-to-Wall Press Hold",
            [565] = "Mini-Squat Calf Raises with Forward Reach",
            [566] = "Parallel Calf Raises",
            [567] = "Rear-Foot-on-Wall Split Squat",
            [568] = "Toes-on-Wall Calf Stretch",
            [574] = "Wall Toe Taps",
            [581] = "Toes-In Calf Raises",
            [582] = "Toes-Out Calf Raises",
            [615] = "Alternating Hamstring Curls with Prayer Hands",
            [577] = "Single-Side Standing Side-Leg Raise with Side Reach",
            [618] = "Single-Side High-Knee Hold with Side Reach",
            [654] = "Single-Side Leg Lift to Overhead Knee Drive",
            [834] = "Single-Side Diagonal Knee Drive with Overhead Pull",
            [915] = "Single-Side Split-Stance Knee Drive with Overhead Reach",
            [588] = "Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [608] = "Hip Circles",
            [611] = "Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Bent-Knee Hip Abduction",
            [686] = "Standing Knee-to-Chest Glute Stretch",
            [687] = "Horse-Stance Alternating Straight Punches",
            [712] = "Standing Arms-Back Chest-Opener Hold",
            [755] = "Outward Wrist Circles",
            [756] = "Outward Controlled Wrist Circles",
            [758] = "Backward Knee-and-Ankle Circles",
            [743] = "Standing Large Arm Circles",
            [843] = "Arm-Behind-Back Assisted Side Neck Stretch",
            [969] = "Chair-Pose Hold",
            [1000] = "Standing Forward-Fold Hold",
        };
        Assert.All(auditedCorrectedNames, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).Name));
        string[] retiredFillerNames =
        [
            "Cumbia Two-Step",
            "Merengue Six-Count Step",
            "Salsa Front-and-Back Basic",
            "Reggaeton Single-Single-Double Step",
            "Basic Mambo Step",
            "Cha-Cha Basic Step",
            "Bachata Side-to-Side Basic",
            "Five-Position Tendon Glide",
            "Pony Step",
        ];
        Assert.DoesNotContain(exercises, exercise =>
            retiredFillerNames.Contains(exercise.Name, StringComparer.Ordinal));
        Exercise fingertipWallPushUps = exercises.Single(exercise => exercise.Id == 218);
        Assert.True(fingertipWallPushUps.WallRequired);
        Assert.Equal(
            CanonicalMuscleGroup.IntrinsicHand,
            fingertipWallPushUps.PrimaryCanonicalGroup);
        Assert.Equal(Exercise.MaximumMuscularDemand, fingertipWallPushUps.MuscularDemand);
        Exercise knifeHandSequence = exercises.Single(exercise => exercise.Id == 291);
        Assert.Equal(
            new[] { 291, 294 },
            knifeHandSequence.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 283, 291, 294, 556 },
            exerciseId =>
            {
                Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
                Assert.Equal(ExerciseSideSequence.Alternating, exercise.SideSequence);
                Assert.Equal(
                    ExerciseMirrorRelationship.BenefitsGreatly,
                    exercise.MirrorRelationship);
            });

        Exercise wideStanceReach = exercises.Single(exercise => exercise.Id == 193);
        Assert.Equal(1, wideStanceReach.MuscularDemand);
        Assert.Equal(
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
            wideStanceReach.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            wideStanceReach.SecondaryCanonicalGroups);

        Exercise narrowStanceReach = exercises.Single(exercise => exercise.Id == 417);
        Assert.Equal(1, narrowStanceReach.MuscularDemand);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.CranialMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);

        Exercise kneeRaiseHold = exercises.Single(exercise => exercise.Id == 95);
        Assert.Equal(ExerciseMode.Hold, kneeRaiseHold.Mode);
        Assert.Equal(ExercisePresentation.Still, kneeRaiseHold.Presentation);
        Assert.Equal(60, kneeRaiseHold.HoldFramePercent);
        Assert.Equal(
            ExerciseInsectCompatibility.Incompatible,
            kneeRaiseHold.InsectCompatibility);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Id is 267 or 553 or 558 or 559);

        int[] explicitSingleSideKneeExerciseIds =
            [395, 577, 618, 654, 834, 915];
        Assert.All(explicitSingleSideKneeExerciseIds, exerciseId =>
        {
            Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
            Assert.StartsWith("Single-Side ", exercise.Name);
            Assert.True(exercise.SideSequence.UsesTimedSides());
        });
        Exercise alternatingKneeCrunch =
            exercises.Single(exercise => exercise.Id == 507);
        Assert.Equal(
            "Alternating Cross-Body Knee-to-Elbow Crunch",
            alternatingKneeCrunch.Name);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            alternatingKneeCrunch.SideSequence);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            alternatingKneeCrunch.PrimaryCanonicalGroup);
        Assert.Contains(
            CanonicalMuscleGroup.HipFlexors,
            alternatingKneeCrunch.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.ScapularGirdle,
            alternatingKneeCrunch.SecondaryCanonicalGroups);
        Assert.Single(alternatingKneeCrunch.SequenceBlocks);
        Assert.Equal(262, alternatingKneeCrunch.SessionMovementId);
        Exercise highKneeSideReach = exercises.Single(exercise => exercise.Id == 618);
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            highKneeSideReach.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            highKneeSideReach.SecondaryCanonicalGroups);

        Assert.Equal(
            CanonicalMuscleGroup.ElbowExtensors,
            exercises.Single(exercise => exercise.Id == 283).PrimaryCanonicalGroup);

        Exercise shadowBoxing = exercises.Single(exercise => exercise.Id == 591);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            shadowBoxing.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            shadowBoxing.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, shadowBoxing.Mode);
        Assert.Equal(ExercisePresentation.Motion, shadowBoxing.Presentation);
        Assert.Contains(
            CanonicalMuscleGroup.Chest,
            shadowBoxing.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AbdominalWall,
            shadowBoxing.SecondaryCanonicalGroups);

        Exercise alternatingUppercuts = exercises.Single(exercise => exercise.Id == 287);
        Assert.Equal(
            "Wide-Stance Alternating Uppercuts",
            alternatingUppercuts.Name);
        Assert.Equal(
            CanonicalMuscleGroup.ElbowFlexors,
            alternatingUppercuts.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            alternatingUppercuts.SideSequence);
        Assert.Single(alternatingUppercuts.SequenceBlocks);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            alternatingUppercuts.HardFloorCompatibility);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AbdominalWall,
            alternatingUppercuts.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            alternatingUppercuts.SecondaryCanonicalGroups);

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
        Assert.DoesNotContain(
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
            Assert.Equal(
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                    ? "Mirror"
                    : "None",
                exercise.Equipment);
            Assert.Equal(
                exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic,
                exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None);
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
                    "(?i)\\b(hold|isometric|pose|stance|stretch|sit)\\b",
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
