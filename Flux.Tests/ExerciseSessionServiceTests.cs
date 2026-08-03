using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class ExerciseSessionServiceTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void EveryResolutionSelectsExactlyOneDistinctExercisePerScheduledGroup(
        int minutes)
    {
        Exercise[] exercises = Enum.GetValues<CanonicalMuscleGroup>()
            .Select(group => Exercise((int)group, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, minutes);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int[] exerciseIds = groups
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        Assert.Equal(minutes, groups.Length);
        Assert.Equal(minutes, exerciseIds.Length);
        Assert.Equal(minutes, exerciseIds.Distinct().Count());
    }

    [Fact]
    public void ThirtyMinuteWorkoutSelectsOneDistinctEligibleExercisePerGroup()
    {
        Exercise[] exercises = Enum.GetValues<CanonicalMuscleGroup>()
            .Select(group => Exercise((int)group, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 30);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Equal(30, groups.Length);
        Assert.Equal(30, selected.Select(exercise => exercise.Id).Distinct().Count());
        Assert.All(groups.Zip(selected), pair =>
            Assert.Contains(pair.Second.PrimaryCanonicalGroup, pair.First.CanonicalGroups));
    }

    [Fact]
    public void SelectionPrefersPrimaryAssignmentOverHigherScoringSecondary()
    {
        Exercise primary = Exercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, -5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            100,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        Exercise otherTorso = Exercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = Exercise(4, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [primary, secondary, otherTorso, upper],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(primary.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void SelectionUsesHighestScoreBucketAmongPrimaryCandidates()
    {
        Exercise lowerScore = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -1);
        Exercise higherScore = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3);
        var service = new ExerciseSessionService(
        [
            lowerScore,
            higherScore,
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void SelectionPrefersBroadestCoverageWithinHighestScoreBucket()
    {
        Exercise narrow = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3);
        Exercise broad = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            CanonicalMuscleGroup.LateralKneeExtensors,
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.Soleus);
        var service = new ExerciseSessionService(
        [
            narrow,
            broad,
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(broad.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void SelectionCountsCoverageOnlyInsideOwningRolledUpGroup()
    {
        Exercise crossBucketCoverage = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.AbdominalWall,
            CanonicalMuscleGroup.ScapularGirdle,
            CanonicalMuscleGroup.ElbowExtensors);
        Exercise inBucketCoverage = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            CanonicalMuscleGroup.LateralKneeExtensors);
        var service = new ExerciseSessionService(
        [
            crossBucketCoverage,
            inBucketCoverage,
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(inBucketCoverage.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void HigherScoreStillOutranksBroaderCoverage()
    {
        Exercise higherScore = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            4);
        Exercise broaderLowerScore = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            CanonicalMuscleGroup.LateralKneeExtensors,
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.Soleus);
        var service = new ExerciseSessionService(
        [
            higherScore,
            broaderLowerScore,
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void TwentyGroupResolutionRewardsCoverageAcrossItsThreeLeafBucket()
    {
        Exercise[] canonicalExercises = Enum.GetValues<CanonicalMuscleGroup>()
            .Select(group => Exercise((int)group, group))
            .ToArray();
        Exercise broadForearmAndHand = Exercise(
            100,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            0,
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators,
            CanonicalMuscleGroup.IntrinsicHand);
        var service = new ExerciseSessionService(
            [.. canonicalExercises, broadForearmAndHand],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 20);

        WorkoutGroup forearmAndHand = service.GetActiveGroups(state)
            .Single(group => group.Id == "r20.forearm-hand");
        Assert.Equal(
            broadForearmAndHand.Id,
            service.GetSelectedExercise(state, forearmAndHand).Id);
    }

    [Fact]
    public void ThirtyGroupResolutionTreatsOutOfLeafSecondariesAsNoExtraCoverage()
    {
        Exercise[] canonicalExercises = Enum.GetValues<CanonicalMuscleGroup>()
            .Select(group => Exercise(
                (int)group,
                group,
                0))
            .ToArray();
        Exercise broaderOutsideLeaf = Exercise(
            100,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            0,
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators,
            CanonicalMuscleGroup.IntrinsicHand);
        var service = new ExerciseSessionService(
            [.. canonicalExercises, broaderOutsideLeaf],
            new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 30);

        WorkoutGroup forearmFlexors = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators);
        Assert.Equal(
            (int)CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            service.GetSelectedExercise(state, forearmFlexors).Id);
    }

    [Fact]
    public void RejectedExerciseReplacementUsesCoverageRanking()
    {
        Exercise current = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            10);
        Exercise narrowReplacement = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            9);
        Exercise broadReplacement = Exercise(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            9,
            CanonicalMuscleGroup.LateralKneeExtensors,
            CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService(
        [
            current,
            narrowReplacement,
            broadReplacement,
            Exercise(4, CanonicalMuscleGroup.SpinalExtensors, 10),
            Exercise(5, CanonicalMuscleGroup.ScapularGirdle, 10),
        ], new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();

        service.RecordOutcome(state, groups[0], keep: false);
        service.RecordOutcome(state, groups[1], keep: true);
        service.RecordOutcome(state, groups[2], keep: true);
        service.FinishInterruptedWorkout(state);

        Assert.Equal(broadReplacement.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void SelectionUsesSecondaryOnlyWhenNoUnusedPrimaryExists()
    {
        Exercise secondaryForLower = Exercise(
            1,
            CanonicalMuscleGroup.SpinalExtensors,
            10,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        Exercise torsoPrimary = Exercise(2, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upperPrimary = Exercise(3, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [secondaryForLower, torsoPrimary, upperPrimary],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Assert.Equal(secondaryForLower.Id, service.GetSelectedExercise(state, groups[0]).Id);
        Assert.Equal(torsoPrimary.Id, service.GetSelectedExercise(state, groups[1]).Id);
        Assert.Equal(3, groups
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .Distinct()
            .Count());
    }

    [Fact]
    public void SecondaryFallbackAlsoPrefersBroadestInBucketCoverage()
    {
        Exercise narrowFallback = Exercise(
            1,
            CanonicalMuscleGroup.SpinalExtensors,
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        Exercise broadFallback = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            CanonicalMuscleGroup.LateralKneeExtensors,
            CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService(
        [
            narrowFallback,
            broadFallback,
            Exercise(3, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        Assert.Equal(broadFallback.Id,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void ValidSavedSelectionIsNotRerankedWithoutUserRejection()
    {
        Exercise savedNarrow = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3);
        Exercise unsavedBroad = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            CanonicalMuscleGroup.LateralKneeExtensors,
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.Soleus);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        var service = new ExerciseSessionService(
        [
            savedNarrow,
            unsavedBroad,
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = savedNarrow.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(savedNarrow.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void RejectedExerciseIsReplacedWithinGroupAndKeptSlotsRemainStable()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int[] initial = groups
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();

        Exercise rejected = service.RecordOutcome(state, groups[0], keep: false);
        service.RecordOutcome(state, groups[1], keep: true);
        service.RecordOutcome(state, groups[2], keep: true);
        Exercise? pendingPenalty = service.FinishInterruptedWorkout(state);

        Assert.Null(pendingPenalty);
        Assert.Equal(9, rejected.Score);
        Assert.Equal(7, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(initial[1], state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(initial[2], state.SelectedExerciseIds[groups[2].Id]);
        Assert.Contains(
            exercises.Single(exercise => exercise.Id == state.SelectedExerciseIds[groups[0].Id])
                .PrimaryCanonicalGroup,
            groups[0].CanonicalGroups);
        Assert.Equal(3, groups.Select(group => state.SelectedExerciseIds[group.Id]).Distinct().Count());
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
    }

    [Fact]
    public void InitializeRepairsDuplicateSavedSelectionsWithinActiveWorkout()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        Exercise overlapping = Exercise(
            20,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            20,
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService([overlapping, .. exercises], new Random(1));
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = groups.ToDictionary(group => group.Id, _ => overlapping.Id),
        };

        service.Initialize(state);

        int[] selectedIds = groups.Select(group => state.SelectedExerciseIds[group.Id]).ToArray();
        Assert.Equal(overlapping.Id, selectedIds[0]);
        Assert.Equal(3, selectedIds.Distinct().Count());
        Assert.All(groups, group =>
        {
            Exercise selected = service.GetSelectedExercise(state, group);
            Assert.True(
                group.CanonicalGroups.Contains(selected.PrimaryCanonicalGroup) ||
                selected.SecondaryCanonicalGroups.Any(group.CanonicalGroups.Contains));
        });
    }

    [Fact]
    public void AbruptClosePenalizesPendingRestExactlyOnceAndRespectsCompletedOutcomes()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var stateStore = new FakeWorkoutStateStore();
        using var database = new FakeExerciseDatabase(exercises);
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int[] initial = groups.Select(group => state.SelectedExerciseIds[group.Id]).ToArray();

        Exercise completedRejection = service.RecordOutcome(state, groups[0], keep: false);
        database.UpdateScore(completedRejection);
        service.RecordOutcome(state, groups[1], keep: true);
        state.PendingRestGroupId = groups[2].Id;
        state.PendingRestEndsAtUnixMilliseconds = 123456;
        state.PendingRestKept = false;
        stateStore.Save(state);

        WorkoutState restored = stateStore.Load();
        Assert.Equal(ExerciseOutcome.X, restored.Outcomes[groups[0].Id]);
        Exercise? pendingPenalty = service.FinishInterruptedWorkout(restored);
        Assert.NotNull(pendingPenalty);
        database.UpdateScore(pendingPenalty);
        stateStore.Save(restored);

        Assert.Equal(initial[2], pendingPenalty.Id);
        Assert.Equal(9, completedRejection.Score);
        Assert.Equal(9, pendingPenalty.Score);
        Assert.NotEqual(initial[0], restored.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(initial[1], restored.SelectedExerciseIds[groups[1].Id]);
        Assert.NotEqual(initial[2], restored.SelectedExerciseIds[groups[2].Id]);
        Assert.Equal(0, restored.ActiveWorkoutMinutes);
        Assert.Empty(restored.Outcomes);

        Exercise? repeated = service.FinishInterruptedWorkout(stateStore.Load());
        Assert.Null(repeated);
        Assert.Equal(9, completedRejection.Score);
        Assert.Equal(9, pendingPenalty.Score);
        Assert.Equal(2, database.Updates.Count);
    }

    [Fact]
    public void PendingScoreJournalRecoversExactValueOnlyOnceWithFakePersistence()
    {
        Exercise exercise = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            0);
        using var database = new FakeExerciseDatabase([exercise]);
        var store = new FakeWorkoutStateStore();
        var state = new WorkoutState();
        exercise.Score = -1;

        ScoreJournalProtocol.Stage(state, exercise, store);
        Assert.Equal(0, database.PersistedScore(exercise.Id));

        WorkoutState afterCrash = store.Load();
        ScoreJournalProtocol.Recover(afterCrash, store, database);
        Assert.Equal(-1, database.PersistedScore(exercise.Id));
        Assert.Equal((exercise.Id, -1), Assert.Single(database.Updates));
        Assert.Equal(0, store.Load().PendingScoreExerciseId);

        ScoreJournalProtocol.Recover(store.Load(), store, database);
        Assert.Single(database.Updates);
    }

    [Fact]
    public void InitializeMigratesLegacyKeptSelectionAcrossEveryResolution()
    {
        Exercise selected = Exercise(50, CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService([selected], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            LastWorkoutMinutes = 4,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = selected.Name,
            },
        };

        service.Initialize(state);

        Assert.Equal(5, state.LastWorkoutMinutes);
        Assert.Equal(5, state.Version);
        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            WorkoutGroup group = MassGroupingTaxonomy.GetGroup(
                minutes,
                selected.PrimaryCanonicalGroup);
            Assert.Equal(selected.Id, state.SelectedExerciseIds[group.Id]);
        }
        Assert.Empty(state.LegacySelectedExerciseNames);
    }

    [Fact]
    public void InitializeDoesNotSeedRejectedOrPendingLegacySelections()
    {
        Exercise rejected = Exercise(50, CanonicalMuscleGroup.GlutealExtensors);
        Exercise pending = Exercise(51, CanonicalMuscleGroup.AbdominalWall);
        var service = new ExerciseSessionService([rejected, pending], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = rejected.Name,
                ["Core"] = pending.Name,
            },
            LegacyOutcomes = new Dictionary<string, ExerciseOutcome>
            {
                ["Glutes"] = ExerciseOutcome.X,
            },
            LegacyPendingRestGroup = "Core",
            PendingRestKept = false,
        };

        service.Initialize(state);

        Assert.DoesNotContain(rejected.Id, state.SelectedExerciseIds.Values);
        Assert.DoesNotContain(pending.Id, state.SelectedExerciseIds.Values);
        Assert.Empty(state.LegacySelectedExerciseNames);
        Assert.Empty(state.LegacyOutcomes);
        Assert.Null(state.LegacyPendingRestGroup);
    }

    [Fact]
    public void LegacyPendingRestPenaltyIsReturnedOnlyOnce()
    {
        Exercise exercise = Exercise(50, CanonicalMuscleGroup.AbdominalWall);
        var service = new ExerciseSessionService([exercise], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            ActiveWorkoutMinutes = 10,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Core"] = exercise.Name,
            },
            LegacyPendingRestGroup = "Core",
            PendingRestKept = false,
            PendingRestEndsAtUnixMilliseconds = 123456,
        };

        service.Initialize(state);
        Exercise? penalty = service.FinishInterruptedWorkout(state);
        Exercise? repeated = service.FinishInterruptedWorkout(state);

        Assert.Same(exercise, penalty);
        Assert.Null(repeated);
        Assert.Equal(-1, exercise.Score);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.LegacySelectedExerciseNames);
        Assert.Null(state.LegacyPendingRestGroup);
    }

    [Fact]
    public void AcknowledgedLegacyCompletionReturnsToDurationSelection()
    {
        Exercise selected = Exercise(50, CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService(
        [
            selected,
            Exercise(51, CanonicalMuscleGroup.SpinalExtensors),
            Exercise(52, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            ActiveWorkoutMinutes = 10,
            WorkoutCompleted = true,
            CompletionAcknowledged = true,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = selected.Name,
            },
        };

        service.Initialize(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.False(state.WorkoutCompleted);
        Assert.False(state.CompletionAcknowledged);
        Assert.Empty(state.LegacySelectedExerciseNames);
        service.StartWorkout(state, 3);
        Assert.Equal(3, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InitializePreservesAlreadyCompletedCurrentOutcome()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup first = service.GetActiveGroups(state)[0];
        service.RecordOutcome(state, first, keep: true);

        service.Initialize(state);

        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[first.Id]);
        Assert.Equal(service.GetActiveGroups(state)[1].Id, service.GetNextGroup(state)?.Id);
    }

    private static Exercise[] ThreeGroupCatalog()
    {
        return
        [
            Exercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 10),
            Exercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 5),
            Exercise(7, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 7),
            Exercise(3, CanonicalMuscleGroup.SpinalExtensors, 10),
            Exercise(4, CanonicalMuscleGroup.SpinalExtensors, 5),
            Exercise(5, CanonicalMuscleGroup.ScapularGirdle, 10),
            Exercise(6, CanonicalMuscleGroup.ScapularGirdle, 5),
        ];
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0,
        params CanonicalMuscleGroup[] secondary)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = secondary,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            HoldFramePercent = 0,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }

    private sealed class AlwaysZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
