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
        WorkoutGroup[] resolutionGroups = MassGroupingTaxonomy
            .GetResolution(minutes)
            .Groups
            .ToArray();
        Exercise[] exercises = resolutionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
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
        Exercise[] exercises = MassGroupingTaxonomy.GetResolution(30).Groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
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
        Exercise primary = QualifiedExercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            100,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        Exercise otherTorso = QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [primary, secondary, otherTorso, upper],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            primary.PrimaryCanonicalGroup);
        Assert.Equal(primary.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionUsesHighestScoreBucketAmongPrimaryCandidates()
    {
        Exercise lowerScore = QualifiedExercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -1);
        Exercise higherScore = QualifiedExercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3);
        var service = new ExerciseSessionService(
        [
            lowerScore,
            higherScore,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            higherScore.PrimaryCanonicalGroup);
        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void CoverageGateRunsBeforeScoreRanking()
    {
        Exercise highScoreBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5,
            100);
        Exercise lowerScoreQualified = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            -100);
        var service = new ExerciseSessionService(
        [
            highScoreBelowThreshold,
            lowerScoreQualified,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            lowerScoreQualified.PrimaryCanonicalGroup);
        Assert.Equal(
            lowerScoreQualified.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionPrefersBroadestCoverageWithinHighestScoreBucket()
    {
        Exercise narrow = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3);
        Exercise broad = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            narrow,
            broad,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            broad.PrimaryCanonicalGroup);
        Assert.Equal(broad.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionCountsCoverageOnlyInsideOwningRolledUpGroup()
    {
        Exercise crossBucketCoverage = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3,
        [
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.AbdominalWall,
            CanonicalMuscleGroup.ScapularGirdle,
            CanonicalMuscleGroup.ElbowExtensors,
        ]);
        Exercise inBucketCoverage = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            crossBucketCoverage,
            inBucketCoverage,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            inBucketCoverage.PrimaryCanonicalGroup);
        Assert.Equal(inBucketCoverage.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void HigherScoreStillOutranksBroaderCoverage()
    {
        Exercise higherScore = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            4);
        Exercise broaderLowerScore = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            higherScore,
            broaderLowerScore,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            higherScore.PrimaryCanonicalGroup);
        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void TwentyGroupResolutionRewardsCoverageAcrossItsThreeLeafBucket()
    {
        Exercise[] canonicalExercises = MassGroupingTaxonomy.GetResolution(20).Groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
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
        Exercise current = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            10);
        Exercise narrowReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            9);
        Exercise broadReplacement = ExerciseWithCoverage(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            9);
        var service = new ExerciseSessionService(
        [
            current,
            narrowReplacement,
            broadReplacement,
            QualifiedExercise(4, CanonicalMuscleGroup.SpinalExtensors, 10),
            QualifiedExercise(5, CanonicalMuscleGroup.ScapularGirdle, 10),
        ], new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            current.PrimaryCanonicalGroup);

        foreach (WorkoutGroup group in groups.Where(group => group.Id != lower.Id))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.RecordOutcome(state, lower, keep: false);
        service.FinishInterruptedWorkout(state);

        Assert.Equal(broadReplacement.Id, state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void SecondaryOnlyCandidatesNeverBypassCoverageGate()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise secondaryForLower = Exercise(
            1,
            CanonicalMuscleGroup.SpinalExtensors,
            10,
            lower.CanonicalGroups.Take(6).ToArray());
        var service = new ExerciseSessionService(
            [secondaryForLower],
            new Random(1));
        var state = new WorkoutState();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => service.StartWorkout(state, 3));
        Assert.Contains("primary-owned", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidSavedSelectionIsNotRerankedWithoutUserRejection()
    {
        Exercise savedNarrow = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3);
        Exercise unsavedBroad = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            savedNarrow.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            savedNarrow,
            unsavedBroad,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = savedNarrow.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(savedNarrow.Id, state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void SavedSelectionBelowCoverageThresholdIsReplaced()
    {
        Exercise savedBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5,
            100);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            savedBelowThreshold.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            savedBelowThreshold,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = savedBelowThreshold.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(
            qualifyingReplacement.Id,
            state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void PendingRestFromPreviousScheduleOrderIsSettledBeforeLineupRepair()
    {
        Exercise performedBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            performedBelowThreshold.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            performedBelowThreshold,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = performedBelowThreshold.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 123456,
        };

        service.Initialize(state);
        Exercise? penalized = service.FinishInterruptedWorkout(state);

        Assert.Same(performedBelowThreshold, penalized);
        Assert.Equal(-1, performedBelowThreshold.Score);
        Assert.Equal(qualifyingReplacement.Id, state.SelectedExerciseIds[lower.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void KeptPendingRestFromPreviousScheduleOrderRemainsKept()
    {
        Exercise performed = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        Exercise alternative = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            performed.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            performed,
            alternative,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = performed.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        service.Initialize(state);
        Exercise? penalized = service.FinishInterruptedWorkout(state);

        Assert.Null(penalized);
        Assert.Equal(0, performed.Score);
        Assert.Equal(performed.Id, state.SelectedExerciseIds[lower.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void StalePendingRestDoesNotPreserveSelectionBelowCoverageThreshold()
    {
        Exercise staleSelection = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            staleSelection.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            staleSelection,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = staleSelection.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 0,
        };

        service.Initialize(state);

        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(
            qualifyingReplacement.Id,
            state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void RejectedExerciseIsReplacedWithinGroupAndKeptSlotsRemainStable()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Dictionary<string, int> initial = groups.ToDictionary(
            group => group.Id,
            group => service.GetSelectedExercise(state, group).Id);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);

        foreach (WorkoutGroup group in groups.Where(group => group.Id != lower.Id))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        Exercise rejected = service.RecordOutcome(state, lower, keep: false);
        Exercise? pendingPenalty = service.FinishInterruptedWorkout(state);

        Assert.Null(pendingPenalty);
        Assert.Equal(9, rejected.Score);
        Assert.Equal(7, state.SelectedExerciseIds[lower.Id]);
        Assert.All(
            groups.Where(group => group.Id != lower.Id),
            group => Assert.Equal(initial[group.Id], state.SelectedExerciseIds[group.Id]));
        Assert.Contains(
            exercises.Single(exercise => exercise.Id == state.SelectedExerciseIds[lower.Id])
                .PrimaryCanonicalGroup,
            lower.CanonicalGroups);
        Assert.Equal(3, groups.Select(group => state.SelectedExerciseIds[group.Id]).Distinct().Count());
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
    }

    [Fact]
    public void InitializeRepairsDuplicateSavedSelectionsWithinActiveWorkout()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        Exercise overlapping = ExerciseWithCoverage(
            20,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            20,
        [
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.ScapularGirdle,
        ]);
        var service = new ExerciseSessionService([overlapping, .. exercises], new Random(1));
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = groups.ToDictionary(group => group.Id, _ => overlapping.Id),
        };

        service.Initialize(state);

        int[] selectedIds = groups.Select(group => state.SelectedExerciseIds[group.Id]).ToArray();
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            overlapping.PrimaryCanonicalGroup);
        Assert.Equal(overlapping.Id, state.SelectedExerciseIds[lower.Id]);
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
        Exercise selected = FullyCoveredExercise(
            50,
            CanonicalMuscleGroup.GlutealExtensors);
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
        Exercise selected = FullyCoveredExercise(
            50,
            CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService(
        [
            selected,
            FullyCoveredExercise(51, CanonicalMuscleGroup.SpinalExtensors),
            FullyCoveredExercise(52, CanonicalMuscleGroup.ScapularGirdle),
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
            QualifiedExercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 10),
            QualifiedExercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 5),
            QualifiedExercise(7, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 7),
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors, 10),
            QualifiedExercise(4, CanonicalMuscleGroup.SpinalExtensors, 5),
            QualifiedExercise(5, CanonicalMuscleGroup.ScapularGirdle, 10),
            QualifiedExercise(6, CanonicalMuscleGroup.ScapularGirdle, 5),
        ];
    }

    private static Exercise QualifiedForGroup(
        int id,
        WorkoutGroup group,
        int score = 0)
    {
        CanonicalMuscleGroup primary = group.CanonicalGroups
            .Order()
            .First();
        int minutes = MassGroupingTaxonomy.SupportedMinutes.Single(minutes =>
            MassGroupingTaxonomy.GetResolution(minutes).Groups.Contains(group));
        return ExerciseWithCoverage(
            id,
            primary,
            minutes,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group),
            score);
    }

    private static Exercise QualifiedExercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(3, primary);
        return ExerciseWithCoverage(
            id,
            primary,
            3,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group),
            score);
    }

    private static Exercise ExerciseWithCoverage(
        int id,
        CanonicalMuscleGroup primary,
        int minutes,
        int inBucketCoverage,
        int score = 0,
        params CanonicalMuscleGroup[] additionalSecondaries)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(minutes, primary);
        if (inBucketCoverage is < 1 || inBucketCoverage > group.CanonicalGroups.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(inBucketCoverage));
        }

        CanonicalMuscleGroup[] secondary = group.CanonicalGroups
            .Where(candidate => candidate != primary)
            .Order()
            .Take(inBucketCoverage - 1)
            .Concat(additionalSecondaries)
            .Where(candidate => candidate != primary)
            .Distinct()
            .ToArray();
        return Exercise(id, primary, score, secondary);
    }

    private static Exercise FullyCoveredExercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0)
    {
        return Exercise(
            id,
            primary,
            score,
            Enum.GetValues<CanonicalMuscleGroup>()
                .Where(group => group != primary)
                .ToArray());
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
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
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
