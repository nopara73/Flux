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
        });
        Assert.Equal(129, exercises.Count(exercise => exercise.MuscularDemand == 0));
        Assert.Equal(227, exercises.Count(exercise => exercise.MuscularDemand == 1));
        Assert.Equal(143, exercises.Count(exercise => exercise.MuscularDemand == 2));
        Dictionary<int, int[]> expectedSessionMovements = new()
        {
            [104] = [104, 136, 626],
            [113] = [113, 135],
            [115] = [115, 997],
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
        Assert.DoesNotContain(exercises, exercise =>
            exercise.InsectCompatibility == ExerciseInsectCompatibility.Unreviewed);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Unreviewed);
        Assert.Equal(406, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Compatible));
        Assert.Equal(93, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Incompatible));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Unreviewed);
        Assert.Equal(
            77,
            exercises.Count(exercise =>
                exercise.MirrorRelationship ==
                    ExerciseMirrorRelationship.BenefitsGreatly));
        Assert.Equal(
            412,
            exercises.Count(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic));
        Assert.Equal(
            10,
            exercises.Count(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly));
        Assert.Equal(5, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(5, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(27, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(50, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(412, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None));
        Assert.DoesNotContain(exercises, exercise => exercise.Id == 90);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Name.StartsWith("Mirror-Guided ", StringComparison.Ordinal));
        Assert.Equal(
            new HashSet<int> { 515, 520, 521, 522, 523 },
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            new HashSet<int> { 524, 525, 526, 527, 528 },
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
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
        Assert.Equal(24, wallRequired.Length);
        Assert.Equal(
            24,
            wallRequired
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
        Assert.Empty(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));
        Assert.All(wallRequired, exercise =>
        {
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.None));
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Wall));
        });
        Assert.All(
            exercises.Where(exercise => exercise.Mode == ExerciseMode.Hold),
            exercise => Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                exercise.InsectCompatibility));
        WorkoutModifierPairCoverageDeficiency[] pairwiseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises).ToArray();
        Assert.Equal(178, pairwiseDeficiencies.Length);
        Assert.Equal(
            new Dictionary<int, int>
            {
                [3] = 2,
                [5] = 13,
                [7] = 3,
                [10] = 26,
                [15] = 29,
                [20] = 31,
                [30] = 74,
            },
            pairwiseDeficiencies
                .GroupBy(deficiency => deficiency.Minutes)
                .ToDictionary(group => group.Key, group => group.Count()));
        Assert.Equal(
            19,
            pairwiseDeficiencies.Select(deficiency => deficiency.GroupId).Distinct().Count());
        WorkoutHardFloorCategoryCoverageDeficiency[] hardFloorCategoryDeficiencies =
            WorkoutModifierPolicy
                .FindHardFloorCategoryCoverageDeficiencies(exercises)
                .ToArray();
        Assert.Equal(112, hardFloorCategoryDeficiencies.Length);
        Assert.Equal(
            new Dictionary<int, int>
            {
                [3] = 6,
                [5] = 12,
                [7] = 10,
                [10] = 13,
                [15] = 14,
                [20] = 15,
                [30] = 42,
            },
            hardFloorCategoryDeficiencies
                .GroupBy(deficiency => deficiency.Minutes)
                .ToDictionary(group => group.Key, group => group.Count()));
        Assert.Equal(
            44,
            hardFloorCategoryDeficiencies
                .Select(deficiency => deficiency.GroupId)
                .Distinct()
                .Count());
        WorkoutModifierMaterialityDeficiency[] materialityDeficiencies =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises).ToArray();
        WorkoutModifierMaterialityDeficiency materialityDeficiency =
            Assert.Single(materialityDeficiencies);
        Assert.Equal(16, materialityDeficiency.MaterialExerciseCount);
        Assert.Equal(17, materialityDeficiency.RequiredMaterialExerciseCount);
        Assert.Equal(19, materialityDeficiency.AffectedBucketCount);
        Assert.Equal(3, materialityDeficiency.RequiredAffectedBucketCount);
        WorkoutProfileLineupDeficiency[] lineupDeficiencies =
            WorkoutModifierPolicy.FindDistinctLineupDeficiencies(exercises).ToArray();
        Assert.Empty(lineupDeficiencies);
        var profileService = new ExerciseSessionService(exercises, new Random(1));
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        IReadOnlyDictionary<int, Exercise> sequenceRootByExerciseId = exercises
            .Where(root => root.SequenceBlocks.Length > 0)
            .SelectMany(root => root.SequenceBlocks
                .Select(block => (block.ExerciseId, Root: root)))
            .DistinctBy(entry => entry.ExerciseId)
            .ToDictionary(entry => entry.ExerciseId, entry => entry.Root);
        foreach (WorkoutModifiers profile in WorkoutModifierPolicy.ValidationProfiles)
        {
            foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
            {
                var profileState = new WorkoutState();
                profileService.StartWorkout(profileState, minutes, profile);
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
                            placement => placement.Any(group =>
                                group.Id == rounds.Key));
                    });
                Assert.Equal(minutes, activeGroups.Length);
            }
        }
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
        Assert.Equal(161, timedSideExercises.Length);
        Assert.DoesNotContain(
            timedSideExercises.Where(exercise =>
                !exercise.SideSequence.UsesTimedLeadStances()),
            exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] alternatingExercises = exercises
            .Where(exercise =>
                exercise.SideSequence == ExerciseSideSequence.Alternating)
            .ToArray();
        Assert.Equal(152, alternatingExercises.Length);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 219);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 15);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 429);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 398);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 515);
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
            [104] = [104, 626],
            [113] = [113, 135],
            [115] = [115, 532],
            [120] = [120, 184],
            [123] = [123, 117, 199],
            [143] = [143, 538],
            [160] = [160, 533],
            [177] = [177, 186],
            [178] = [178, 535],
            [179] = [179, 539],
            [180] = [180, 534],
            [181] = [181, 536],
            [211] = [211, 213],
            [214] = [214, 755],
            [220] = [220, 543],
            [223] = [223, 756],
            [252] = [252, 253, 254],
            [261] = [261, 677],
            [264] = [264, 406],
            [285] = [285, 541],
            [286] = [286, 545],
            [288] = [288, 758],
            [292] = [292, 542],
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
            [845] = [845, 256],
            [910] = [910, 962],
            [948] = [948, 949],
            [996] = [996, 997],
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
        });
        Dictionary<int, int> expectedSequenceBlockDistribution = new()
        {
            [1] = 278,
            [2] = 121,
            [3] = 28,
            [4] = 15,
            [5] = 1,
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
            [198] = ExerciseSideSequence.Alternating,
            [219] = ExerciseSideSequence.Alternating,
            [248] = ExerciseSideSequence.Alternating,
            [265] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [274] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [280] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [287] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [282] = ExerciseSideSequence.ScreenLeftThenRight,
            [390] = ExerciseSideSequence.Alternating,
            [391] = ExerciseSideSequence.Alternating,
            [394] = ExerciseSideSequence.Alternating,
            [395] = ExerciseSideSequence.ScreenLeftThenRight,
            [397] = ExerciseSideSequence.ScreenRightThenLeft,
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
            [507] = ExerciseSideSequence.ScreenRightThenLeft,
            [508] = ExerciseSideSequence.Alternating,
            [512] = ExerciseSideSequence.ScreenRightThenLeft,
            [513] = ExerciseSideSequence.ScreenLeftThenRight,
            [515] = ExerciseSideSequence.Alternating,
            [576] = ExerciseSideSequence.Alternating,
            [577] = ExerciseSideSequence.ScreenRightThenLeft,
            [575] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [578] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [583] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
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
            265, 274, 280, 287, 473, 575, 578, 583, 591, 884, 885, 886, 887,
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
            213, 220, 225, 234, 239, 241, 242, 256, 258, 269,
            278, 279, 282, 283, 285, 286, 291, 294, 326, 329,
            395, 396, 397, 507, 512, 513, 572, 577, 618, 636,
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
            198, 201, 230, 248, 251, 257, 262, 263, 266,
            268, 270, 275, 289, 301, 314, 321,
            394, 413, 421, 425, 427, 468, 516, 615, 677, 683, 687,
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
            [218] = "Sequential Finger Waves",
            [223] = "Inward Controlled Wrist Circles",
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
            [248] = "Alternating Side-Tap Palm Pushes",
            [251] = "Forward Fold to Overhead Reach",
            [256] = "Overhead Side-Stretch Hold",
            [257] = "Finger Spread to Interlace Stretch",
            [258] = "Karate Downward Block",
            [262] = "Standing Bicycle Crunches",
            [270] = "Goalpost Chest-Opener Hold",
            [282] = "Side-Step Knee Drive with Alternating Side Punches",
            [283] = "Open Hand to Straight Fist",
            [288] = "Forward Knee-and-Ankle Circles",
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
            [264] = "Standing Arm Circles",
            [275] = "Small Arm Circles",
            [406] = "Standing Wheel Arm Circles",
            [409] = "Full Neck Circles",
            [417] = "Narrow-Stance Overhead-to-Floor Reach",
            [460] = "Jogging in Place with Arm Circles",
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
            [95] = "Single-Leg Knee-Raise Hold",
            [556] = "Standing Fist Clench and Release",
            [561] = "Tiptoe Running Steps with Head Spot",
            [562] = "Ballet Calf Raises with Arm Sweeps",
            [564] = "Parallel Calf Raises with Hands on Hips",
            [565] = "Mini Squat with Forward Reach",
            [566] = "Parallel Calf Raises",
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
        Assert.Contains(
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);
        Assert.Contains(
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
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            shadowBoxing.SideSequence);
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
