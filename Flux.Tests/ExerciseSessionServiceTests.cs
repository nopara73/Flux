using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class ExerciseSessionServiceTests
{
    [Fact]
    public void MuscleBudgetTemporarilyDownvotesOnlyNewOverloadedChoices()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        CanonicalMuscleGroup overloadedMuscle = groups[0].CanonicalGroups.Single();
        WorkoutGroup targetGroup = groups[10];
        var selectedByGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        var exercises = new List<Exercise>();
        for (int index = 0; index < groups.Length; index++)
        {
            CanonicalMuscleGroup primary = groups[index].CanonicalGroups.Single();
            CanonicalMuscleGroup[] secondary = index is >= 1 and <= 9 or 11
                ? [overloadedMuscle]
                : [];
            Exercise selected = Exercise(1 + index, primary, 0, secondary);
            exercises.Add(selected);
            selectedByGroup[groups[index].Id] = selected.Id;
        }

        Exercise overloadedZero = Exercise(
            selectedByGroup[targetGroup.Id],
            targetGroup.CanonicalGroups.Single(),
            0,
            overloadedMuscle);
        exercises.RemoveAll(exercise => exercise.Id == overloadedZero.Id);
        exercises.Add(overloadedZero);
        Exercise downvotedOnce = Exercise(
            1_001,
            targetGroup.CanonicalGroups.Single(),
            -1);
        Exercise downvotedTwice = Exercise(
            1_002,
            targetGroup.CanonicalGroups.Single(),
            -2);
        exercises.Add(downvotedOnce);
        exercises.Add(downvotedTwice);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 30,
            SelectedExerciseIds = selectedByGroup,
        };
        var service = new ExerciseSessionService(exercises, new Random(1));

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        Assert.Equal(downvotedOnce.Id, state.SelectedExerciseIds[targetGroup.Id]);
        Assert.Equal(0, overloadedZero.Score);
        Assert.Equal(-1, downvotedOnce.Score);
        Assert.Equal(-2, downvotedTwice.Score);

        var tieSelectedExerciseIds = new Dictionary<string, int>(
            selectedByGroup,
            StringComparer.Ordinal)
        {
            [targetGroup.Id] = overloadedZero.Id,
        };
        var tieState = new WorkoutState
        {
            LastWorkoutMinutes = 30,
            SelectedExerciseIds = tieSelectedExerciseIds,
        };
        Exercise reducedLoadExercise = Exercise(
            selectedByGroup[groups[11].Id],
            groups[11].CanonicalGroups.Single(),
            0);
        var tieService = new ExerciseSessionService(
            exercises
                .Where(exercise =>
                    exercise.Id != reducedLoadExercise.Id &&
                    exercise.Id != downvotedTwice.Id)
                .Append(reducedLoadExercise)
                .ToArray(),
            new Random(1));

        tieService.StartWorkout(tieState, 30, WorkoutModifiers.None);

        Assert.Equal(overloadedZero.Id, tieState.SelectedExerciseIds[targetGroup.Id]);
    }

    [Fact]
    public void UnreviewedCatalogCannotSilentlyTreatEnabledModifierAsOff()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.Throws<InvalidOperationException>(() =>
            service.StartWorkout(state, 3, WorkoutModifiers.Insect));
    }

    [Fact]
    public void InsectSelectionFiltersBeforeScoreAndCoverageRanking()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));

        var normal = new WorkoutState();
        service.StartWorkout(normal, 3, WorkoutModifiers.None);
        Assert.All(service.GetActiveGroups(normal), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                service.GetSelectedExercise(normal, group).InsectCompatibility));

        var insect = new WorkoutState();
        service.StartWorkout(insect, 3, WorkoutModifiers.Insect);
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.All(service.GetActiveGroups(insect), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(insect, group).InsectCompatibility));
    }

    [Fact]
    public void FullyReviewedCatalogAlwaysHonorsEnabledModifier()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups.SelectMany((group, index) => new[]
        {
            QualifiedForGroup(
                1 + index,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible),
            QualifiedForGroup(
                101 + index,
                group,
                score: 100,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
        }).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.All(service.GetActiveGroups(state), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(state, group).InsectCompatibility));
    }

    [Fact]
    public void CurrentPreDirectionStateKeepsExplicitlyRelaxedSilenceModifier()
    {
        var service = new ExerciseSessionService(ReviewedInsectCatalog(), new Random(1));
        var state = new WorkoutState
        {
            Version = 8,
            LastWorkoutModifiers = WorkoutModifiers.None,
        };

        service.Initialize(state);

        Assert.Equal(10, state.Version);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
    }

    [Fact]
    public void ModifierProfilesShareKeepsWithoutForgettingExcludedExercises()
    {
        var service = new ExerciseSessionService(
            ReviewedInsectCatalog(),
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        int[] keptIds = service.GetActiveGroups(state)
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        foreach (WorkoutGroup group in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.Equal(keptIds.Order(), state.LastKeptExerciseIds.Order());
        Assert.All(service.GetActiveGroups(state), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(state, group).InsectCompatibility));

        service.FinishInterruptedWorkout(state);
        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            keptIds.Order(),
            service.GetActiveGroups(state)
                .Select(group => service.GetSelectedExercise(state, group).Id)
                .Order());
    }

    [Fact]
    public void NeutralModifierProfileDoesNotReselectRejectedExercise()
    {
        Exercise[] exercises = ThreeGroupCatalog(
            ExerciseInsectCompatibility.Compatible);
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int rejectedExerciseId = service.GetSelectedExercise(state, groups[0]).Id;

        service.RecordOutcome(state, groups[0], keep: false);
        foreach (WorkoutGroup group in groups.Skip(1))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);

        Assert.DoesNotContain(rejectedExerciseId, state.SelectedExerciseIds.Values);
        Assert.Contains(rejectedExerciseId, state.NextWorkoutExcludedExerciseIds);
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.Empty(state.NextWorkoutExcludedExerciseIds);
        Assert.NotEqual(
            rejectedExerciseId,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void InsectProfileCarriesKeepsIntoLongWorkoutBeforeAllocatingExtraSets()
    {
        var service = new ExerciseSessionService(
            ReviewedInsectCatalog(),
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);
        int[] keptExerciseIds = service.GetActiveGroups(state)
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        foreach (WorkoutGroup group in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);

        service.StartWorkout(state, 45, WorkoutModifiers.Insect);

        WorkoutGroup[] keptGroups = service.GetActiveGroups(state)
            .GroupBy(group => group.SelectionKey, StringComparer.Ordinal)
            .Select(rounds => rounds.First())
            .Where(group => keptExerciseIds.Contains(
                service.GetSelectedExercise(state, group).Id))
            .ToArray();
        Assert.Equal(keptExerciseIds.Length, keptGroups.Length);
        Assert.All(keptGroups, group =>
            Assert.Contains(
                group.SelectionKey,
                state.ActiveExtraSetSelectionGroupIds));
        Assert.All(keptGroups, group =>
            Assert.True(state.SelectedExerciseIds.ContainsKey(
                $"p1|{group.SelectionKey}")));
    }

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

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

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

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Equal(30, groups.Length);
        Assert.Equal(30, selected.Select(exercise => exercise.Id).Distinct().Count());
        Assert.All(groups.Zip(selected), pair =>
            Assert.Contains(pair.Second.PrimaryCanonicalGroup, pair.First.CanonicalGroups));
    }

    [Theory]
    [InlineData(45, 1, 2)]
    [InlineData(60, 2, 2)]
    [InlineData(90, 3, 3)]
    public void LongWorkoutsRepeatTheThirtyMinuteLineupBySet(
        int minutes,
        int firstHalfSets,
        int secondHalfSets)
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(minutes, rounds.Length);
        Assert.Equal(Enumerable.Range(1, minutes), rounds.Select(round => round.Order));
        Assert.Equal(minutes, rounds.Select(round => round.Id).Distinct().Count());

        for (int index = 0; index < selectionGroups.Length; index++)
        {
            WorkoutGroup selectionGroup = selectionGroups[index];
            WorkoutGroup[] groupRounds = rounds
                .Where(round => round.SelectionKey == selectionGroup.Id)
                .ToArray();
            int expectedSets = index < 15 ? firstHalfSets : secondHalfSets;
            Assert.Equal(expectedSets, groupRounds.Length);
            Assert.All(groupRounds, round =>
                Assert.Equal(
                    state.SelectedExerciseIds[selectionGroup.Id],
                    service.GetSelectedExercise(state, round).Id));
        }
    }

    [Fact]
    public void FortyFiveMinutesUpgradeSidedExercisesBeforeAddingRepeatedSets()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                sideSequence: index < 12
                    ? ExerciseSideSequence.ScreenRightThenLeft
                    : ExerciseSideSequence.Continuous))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(33, rounds.Length);
        Assert.Equal(12, rounds.Count(round => round.UsesFullSideTiming));
        Assert.All(rounds.Where(round => round.UsesFullSideTiming), round =>
            Assert.NotEqual(
                ExerciseSideSequence.Continuous,
                service.GetSelectedExercise(state, round).SideSequence));
        Assert.Equal(3, state.ActiveExtraSetSelectionGroupIds.Count);
        Assert.Equal(12, state.ActiveFullSideRoundIds.Count);
        Assert.Equal(45, rounds.Sum(round => round.UsesFullSideTiming ? 2 : 1));
    }

    [Fact]
    public void FortyFiveMinutesAddLinkedDirectionsBeforeLengtheningSidedTimers()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                score: 10,
                sideSequence: ExerciseSideSequence.ScreenLeftThenRight))
            .ToArray();
        Exercise[] partners = groups
            .Take(15)
            .Select((group, index) => CloneWithDirectionPartner(
                QualifiedForGroup(
                    1001 + index,
                    group,
                    sideSequence: ExerciseSideSequence.ScreenLeftThenRight),
                baseExercises[index].Id))
            .ToArray();
        for (int index = 0; index < partners.Length; index++)
        {
            baseExercises[index] = CloneWithDirectionPartner(
                baseExercises[index],
                partners[index].Id);
        }
        Exercise[] exercises = [.. baseExercises, .. partners];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup[] directionRounds = rounds
            .Where(round => round.Id.EndsWith(".direction", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(15, directionRounds.Length);
        Assert.Equal(45, rounds.Length);
        Assert.Empty(state.ActiveFullSideRoundIds);
        Assert.Empty(state.ActiveExtraSetSelectionGroupIds);
        Assert.All(directionRounds, round =>
        {
            int baseId = state.SelectedExerciseIds[round.SelectionKey];
            Exercise selected = exercises.Single(exercise => exercise.Id == baseId);
            Assert.Equal(
                selected.DirectionPartnerExerciseId,
                service.GetSelectedExercise(state, round).Id);
        });
    }

    [Fact]
    public void LinkedDirectionAlreadyInBaseLineupIsNotDuplicated()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise first = CloneWithDirectionPartner(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.Order().First(), 10),
            2);
        Exercise second = CloneWithDirectionPartner(
            FullyCoveredExercise(2, groups[1].CanonicalGroups.Order().First(), 10),
            1);
        Exercise[] remaining = groups
            .Skip(2)
            .Select((group, index) => QualifiedForGroup(3 + index, group, 10))
            .ToArray();
        var service = new ExerciseSessionService(
            [first, second, .. remaining],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        Assert.DoesNotContain(
            service.GetActiveGroups(state),
            round => round.Id.EndsWith(".direction", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectingLinkedDirectionScoresOnlyThatDirection()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise partner = CloneWithDirectionPartner(
            QualifiedForGroup(1001, groups[0]),
            baseExercises[0].Id);
        baseExercises[0] = CloneWithDirectionPartner(baseExercises[0], partner.Id);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup directionRound = service.GetActiveGroups(state).Single(round =>
            round.Id.EndsWith(".direction", StringComparison.Ordinal));
        int baseExerciseId = state.SelectedExerciseIds[directionRound.SelectionKey];
        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .Take(directionRound.Order - 1))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }
        service.RecordOutcome(state, directionRound, keep: false);

        Assert.Equal(-1, partner.Score);
        Assert.Equal(baseExerciseId, state.SelectedExerciseIds[directionRound.SelectionKey]);
        Assert.NotEqual(baseExerciseId, partner.Id);
    }

    [Fact]
    public void VersionEightLongWorkoutRecomputesDirectionAllocationWithoutEnablingSilence()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise partner = CloneWithDirectionPartner(
            QualifiedForGroup(1001, groups[0]),
            baseExercises[0].Id);
        baseExercises[0] = CloneWithDirectionPartner(baseExercises[0], partner.Id);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        state.Version = 8;
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
        state.ActiveExtraSetSelectionGroupIds.Clear();

        service.Initialize(state);

        Assert.Equal(10, state.Version);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.ActiveWorkoutModifiers);
        Assert.Single(state.ActiveDirectionPartnerExerciseIds);
        Assert.Equal(45, service.GetActiveGroups(state).Sum(round =>
            round.UsesFullSideTiming ? 2 : 1));
    }

    [Fact]
    public void KeptSidedExercisesReceiveFullSideTimingBeforeLargerUnkeptMuscles()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                sideSequence: ExerciseSideSequence.ScreenLeftThenRight))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastKeptExerciseIds = exercises.Take(4).Select(exercise => exercise.Id).ToHashSet(),
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        string[] expected = groups.Take(4)
            .Concat(groups.TakeLast(11))
            .Select(group => $"{group.Id}.set1")
            .ToArray();
        Assert.Equal(expected.Order(), state.ActiveFullSideRoundIds.Order());
        Assert.Empty(state.ActiveExtraSetSelectionGroupIds);
        Assert.Equal(30, service.GetActiveGroups(state).Count);

        state.LastKeptExerciseIds.Clear();
        service.Initialize(state);
        Assert.Equal(expected.Order(), state.ActiveFullSideRoundIds.Order());
    }

    [Fact]
    public void FortyFiveMinuteExtraSetsPreferPreviouslyKeptExercisesThenMuscleMass()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        Exercise[] replacements = selectionGroups
            .Select((group, index) => QualifiedForGroup(1001 + index, group, -1))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);

        WorkoutGroup[] previousRounds = service.GetActiveGroups(state).ToArray();
        foreach (WorkoutGroup round in previousRounds)
        {
            service.RecordOutcome(state, round, keep: round.Order <= 10);
        }

        int[] expectedKeptExerciseIds = previousRounds
            .Take(10)
            .Select(round => state.SelectedExerciseIds[round.SelectionKey])
            .ToArray();
        service.AcknowledgeCompletion(state);
        service.Initialize(state);
        Assert.Equal(expectedKeptExerciseIds.Order(), state.LastKeptExerciseIds.Order());

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string[] extraSetGroupIds = selectionGroups
            .Where(group => rounds.Count(round => round.SelectionKey == group.Id) == 2)
            .Select(group => group.Id)
            .ToArray();
        string[] expectedExtraSetGroupIds = selectionGroups
            .Take(10)
            .Concat(selectionGroups.TakeLast(5))
            .Select(group => group.Id)
            .ToArray();
        Assert.Equal(expectedExtraSetGroupIds, extraSetGroupIds);
        Assert.Equal(
            expectedExtraSetGroupIds.Order(),
            state.ActiveExtraSetSelectionGroupIds.Order());

        service.Initialize(state);
        Assert.Equal(expectedExtraSetGroupIds, selectionGroups
            .Where(group => service.GetActiveGroups(state)
                .Count(round => round.SelectionKey == group.Id) == 2)
            .Select(group => group.Id));
    }

    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(5, 3, 3)]
    public void KeptExercisesFillCompatibleSlotsAfterWorkoutDurationChanges(
        int previousMinutes,
        int nextMinutes,
        int expectedCarriedCount)
    {
        WorkoutGroup[] previousGroups = MassGroupingTaxonomy
            .GetResolution(previousMinutes)
            .Groups
            .ToArray();
        WorkoutGroup[] nextGroups = MassGroupingTaxonomy
            .GetResolution(nextMinutes)
            .Groups
            .ToArray();
        Exercise[] keptExercises = previousGroups
            .Select((group, index) => FullyCoveredExercise(
                index + 1,
                group.CanonicalGroups.Order().First()))
            .ToArray();
        Exercise[] nextDurationAlternatives = nextGroups
            .Select((group, index) => FullyCoveredExercise(
                101 + index,
                group.CanonicalGroups.Order().First(),
                score: 10))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. keptExercises, .. nextDurationAlternatives],
            new Random(1));
        var state = new WorkoutState
        {
            SelectedExerciseIds = previousGroups
                .Zip(keptExercises)
                .ToDictionary(pair => pair.First.Id, pair => pair.Second.Id),
            LastKeptExerciseIds = keptExercises
                .Select(exercise => exercise.Id)
                .ToHashSet(),
        };

        service.StartWorkout(state, previousMinutes, WorkoutModifiers.None);
        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, round, keep: true);
        }
        service.AcknowledgeCompletion(state);
        service.Initialize(state);

        foreach ((WorkoutGroup group, Exercise alternative) in
                 nextGroups.Zip(nextDurationAlternatives))
        {
            state.SelectedExerciseIds[group.Id] = alternative.Id;
        }

        service.StartWorkout(state, nextMinutes, WorkoutModifiers.None);

        HashSet<int> keptExerciseIds = keptExercises
            .Select(exercise => exercise.Id)
            .ToHashSet();
        int[] selectedExerciseIds = nextGroups
            .Select(group => state.SelectedExerciseIds[group.Id])
            .ToArray();
        Assert.Equal(previousMinutes, state.LastKeptExerciseIds.Count);
        Assert.Equal(
            expectedCarriedCount,
            selectedExerciseIds.Count(keptExerciseIds.Contains));
        Assert.Equal(nextMinutes, selectedExerciseIds.Distinct().Count());
    }

    [Fact]
    public void InterruptedWorkoutPreservesUnreviewedKeepUntilExplicitRejection()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        Exercise kept = exercises[0];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            LastKeptExerciseIds = [kept.Id],
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        service.FinishInterruptedWorkout(state);

        Assert.Contains(kept.Id, state.LastKeptExerciseIds);

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            bool keep = service.GetSelectedExercise(state, round).Id != kept.Id;
            service.RecordOutcome(state, round, keep);
        }
        service.FinishInterruptedWorkout(state);

        Assert.DoesNotContain(kept.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void RejectedSetReplacesTheSharedExerciseOnceAfterLongWorkout()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        WorkoutGroup target = selectionGroups[^1];
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise original = baseline[^1];
        Exercise replacement = QualifiedForGroup(1000, target, 5);
        var service = new ExerciseSessionService(
            [.. baseline, replacement],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] targetRounds = service.GetActiveGroups(state)
            .Where(round => round.SelectionKey == target.Id)
            .ToArray();

        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            bool keep = round.Id != targetRounds[0].Id;
            service.RecordOutcome(state, round, keep);
        }

        Assert.Equal(9, original.Score);
        Assert.Equal(original.Id, state.SelectedExerciseIds[target.Id]);
        service.AcknowledgeCompletion(state);
        service.Initialize(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[target.Id]);
    }

    [Fact]
    public void DoneAtomicallyReplacesEveryRejectedExerciseBeforeRelaunch()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise[] replacements = groups
            .Select((group, index) => QualifiedForGroup(1001 + index, group, 0))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);
        int[] rejectedIds = service.GetActiveGroups(state)
            .Where(round => round.Order % 2 == 1)
            .Select(round => service.GetSelectedExercise(state, round).Id)
            .ToArray();

        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, round, keep: round.Order % 2 == 0);
        }

        service.AcknowledgeCompletion(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
        Assert.False(state.WorkoutCompleted);
        Assert.False(state.CompletionAcknowledged);
        Assert.All(rejectedIds, rejectedId =>
            Assert.DoesNotContain(rejectedId, state.SelectedExerciseIds.Values));

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);
        Assert.All(rejectedIds, rejectedId =>
            Assert.DoesNotContain(rejectedId, restored.SelectedExerciseIds.Values));
    }

    [Fact]
    public void InterruptedLongWorkoutSettlesPendingRepeatedRoundExactlyOnce()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        WorkoutGroup target = selectionGroups[15];
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise original = baseline[15];
        Exercise replacement = QualifiedForGroup(1000, target, 5);
        var service = new ExerciseSessionService(
            [.. baseline, replacement],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup pendingRound = service.GetActiveGroups(state)
            .First(round => round.SelectionKey == target.Id);

        foreach (WorkoutGroup round in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != pendingRound.Id))
        {
            service.RecordOutcome(state, round, keep: true);
        }
        state.PendingRestGroupId = pendingRound.Id;
        state.PendingRestEndsAtUnixMilliseconds = 123456;

        Exercise? penalty = service.FinishInterruptedWorkout(state);
        Exercise? repeated = service.FinishInterruptedWorkout(state);

        Assert.Same(original, penalty);
        Assert.Null(repeated);
        Assert.Equal(9, original.Score);
        Assert.Equal(replacement.Id, state.SelectedExerciseIds[target.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void FinalPendingRoundInLongWorkoutIsTheLastSet()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();

        foreach (WorkoutGroup round in rounds[..^1])
        {
            Assert.False(service.IsFinalPendingGroup(state, round));
            service.RecordOutcome(state, round, keep: true);
        }

        Assert.True(service.IsFinalPendingGroup(state, rounds[^1]));
        Assert.Equal(2, rounds
            .Count(round => round.SelectionKey == rounds[^1].SelectionKey));
    }

    [Fact]
    public void HigherScoringSecondaryAssignmentOutranksLowerScoringPrimary()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise primary = QualifiedExercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            100,
            lower.CanonicalGroups.ToArray());
        Exercise otherTorso = QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [primary, secondary, otherTorso, upper],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(secondary.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void PrimaryAssignmentBreaksEqualScoreTieBeforeCoverage()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise primary = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            5,
            lower.CanonicalGroups.ToArray());
        var service = new ExerciseSessionService(
        [
            primary,
            secondary,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(primary.Id, service.GetSelectedExercise(state, lower).Id);
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

        service.StartWorkout(state, 3, WorkoutModifiers.None);

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

        service.StartWorkout(state, 3, WorkoutModifiers.None);

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

        service.StartWorkout(state, 3, WorkoutModifiers.None);

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

        service.StartWorkout(state, 3, WorkoutModifiers.None);

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

        service.StartWorkout(state, 3, WorkoutModifiers.None);

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

        service.StartWorkout(state, 20, WorkoutModifiers.None);

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

        service.StartWorkout(state, 30, WorkoutModifiers.None);

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
        service.StartWorkout(state, 3, WorkoutModifiers.None);
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
    public void SecondaryOnlyCandidateIsEligibleWhenItMeetsCoverageGate()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise secondaryForLower = Exercise(
            1,
            CanonicalMuscleGroup.SpinalExtensors,
            10,
            lower.CanonicalGroups.Take(6).ToArray());
        var service = new ExerciseSessionService(
        [
            secondaryForLower,
            QualifiedExercise(2, CanonicalMuscleGroup.SpinalExtensors, 10),
            QualifiedExercise(3, CanonicalMuscleGroup.ScapularGirdle, 10),
        ],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            secondaryForLower.Id,
            state.SelectedExerciseIds[lower.Id]);
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
        service.StartWorkout(state, 3, WorkoutModifiers.None);
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
    public void InitializeUsesGlobalMatchingWhenPreservingASavedExerciseWouldDeadEnd()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise shared = FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 100);
        Exercise firstOnly = QualifiedForGroup(2, groups[0]);
        Exercise lastOnly = QualifiedForGroup(3, groups[2]);
        var service = new ExerciseSessionService(
            [shared, firstOnly, lastOnly],
            new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = shared.Id,
                [groups[2].Id] = lastOnly.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(firstOnly.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(shared.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(lastOnly.Id, state.SelectedExerciseIds[groups[2].Id]);
    }

    [Fact]
    public void CarryKeptExercisesForwardMaximizesKeptCountAcrossTheWholeLineup()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise sharedKept = FullyCoveredExercise(
            1,
            groups[0].CanonicalGroups.First(),
            100);
        Exercise firstOnlyKept = QualifiedForGroup(2, groups[0]);
        Exercise lastOnly = QualifiedForGroup(3, groups[2]);
        var service = new ExerciseSessionService(
            [sharedKept, firstOnlyKept, lastOnly],
            new Random(1));
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds = [sharedKept.Id, firstOnlyKept.Id],
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = sharedKept.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        int[] selectedIds = groups
            .Select(group => state.SelectedExerciseIds[group.Id])
            .ToArray();
        Assert.Contains(sharedKept.Id, selectedIds);
        Assert.Contains(firstOnlyKept.Id, selectedIds);
        Assert.Equal(firstOnlyKept.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(sharedKept.Id, state.SelectedExerciseIds[groups[1].Id]);
    }

    [Fact]
    public void EligibleHardKeepOutranksLowerDemandKeepForTheSameSlot()
    {
        DateOnly today = new(2026, 8, 20);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: -10),
            muscularDemand: 2);
        Exercise lowerDemandKeep = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 100),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, lowerDemandKeep, middle, last],
            new AlwaysZeroRandom(),
            () => today);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds = [hardKeep.Id, lowerDemandKeep.Id],
            LastKeptLocalDateByExerciseId = new Dictionary<int, string>
            {
                [hardKeep.Id] = "2026-08-20",
                [lowerDemandKeep.Id] = "2026-08-20",
            },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = lowerDemandKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(hardKeep.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Empty(state.ActiveRecoveryExcludedExerciseIds);
    }

    [Fact]
    public void OnlyPreviousDayHardKeepsAreTemporarilyExcluded()
    {
        DateOnly today = new(2026, 8, 20);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise yesterdayHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 100),
            muscularDemand: 2);
        Exercise yesterdayModerate = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 1);
        Exercise olderHard = CloneWithMuscularDemand(
            QualifiedForGroup(3, groups[1]),
            muscularDemand: 2);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [yesterdayHard, yesterdayModerate, olderHard, last],
            new AlwaysZeroRandom(),
            () => today);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds =
                [yesterdayHard.Id, yesterdayModerate.Id, olderHard.Id],
            LastKeptLocalDateByExerciseId = new Dictionary<int, string>
            {
                [yesterdayHard.Id] = "2026-08-19",
                [yesterdayModerate.Id] = "2026-08-19",
                [olderHard.Id] = "2026-08-18",
            },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = yesterdayHard.Id,
                [groups[1].Id] = olderHard.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal([yesterdayHard.Id], state.ActiveRecoveryExcludedExerciseIds);
        Assert.Equal(yesterdayModerate.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(olderHard.Id, state.SelectedExerciseIds[groups[1].Id]);
    }

    [Fact]
    public void CompletingKeepsRecordsTheirLocalDateForTomorrowRecovery()
    {
        DateOnly localDate = new(2026, 8, 20);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise alternative = QualifiedForGroup(2, groups[0]);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, alternative, middle, last],
            new AlwaysZeroRandom(),
            () => localDate);
        var state = new WorkoutState
        {
            LastKeptExerciseIds = [hardKeep.Id],
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, round, keep: true);
        }
        service.AcknowledgeCompletion(state);

        Assert.Equal("2026-08-20", state.LastKeptLocalDateByExerciseId[hardKeep.Id]);

        localDate = localDate.AddDays(1);
        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Contains(hardKeep.Id, state.ActiveRecoveryExcludedExerciseIds);
        Assert.Equal(alternative.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void PreparingRejectedReplacementsUsesGlobalMatchingInsteadOfGreedyOrder()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise currentFirst = QualifiedForGroup(1, groups[0], 10);
        Exercise currentMiddle = QualifiedForGroup(2, groups[1], 10);
        Exercise currentLast = QualifiedForGroup(3, groups[2], 10);
        Exercise sharedReplacement = FullyCoveredExercise(
            4,
            groups[0].CanonicalGroups.First(),
            100);
        Exercise firstOnlyReplacement = QualifiedForGroup(5, groups[0], 5);
        var service = new ExerciseSessionService(
            [
                currentFirst,
                currentMiddle,
                currentLast,
                sharedReplacement,
                firstOnlyReplacement,
            ],
            new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = currentFirst.Id,
                [groups[1].Id] = currentMiddle.Id,
                [groups[2].Id] = currentLast.Id,
            },
        };
        service.Initialize(state);

        service.RecordOutcome(state, groups[0], keep: false);
        service.RecordOutcome(state, groups[1], keep: false);
        service.RecordOutcome(state, groups[2], keep: true);
        service.AcknowledgeCompletion(state);

        Assert.Equal(firstOnlyReplacement.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(sharedReplacement.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(currentLast.Id, state.SelectedExerciseIds[groups[2].Id]);
    }

    [Fact]
    public void AbruptClosePenalizesPendingRestExactlyOnceAndRespectsCompletedOutcomes()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var stateStore = new FakeWorkoutStateStore();
        using var database = new FakeExerciseDatabase(exercises);
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
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
        Assert.Equal(10, state.Version);
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
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.Equal(3, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InitializePreservesAlreadyCompletedCurrentOutcome()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup first = service.GetActiveGroups(state)[0];
        service.RecordOutcome(state, first, keep: true);

        service.Initialize(state);

        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[first.Id]);
        Assert.Equal(service.GetActiveGroups(state)[1].Id, service.GetNextGroup(state)?.Id);
    }

    [Fact]
    public void FinalPendingGroupIsIdentifiedOnlyAfterEarlierRoundsFinish()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        IReadOnlyList<WorkoutGroup> groups = service.GetActiveGroups(state);

        Assert.False(service.IsFinalPendingGroup(state, groups[0]));
        service.RecordOutcome(state, groups[0], keep: true);
        Assert.False(service.IsFinalPendingGroup(state, groups[1]));
        service.RecordOutcome(state, groups[1], keep: true);

        Assert.True(service.IsFinalPendingGroup(state, groups[2]));
        Assert.False(service.IsFinalPendingGroup(state, groups[1]));
    }

    private static Exercise[] ThreeGroupCatalog(
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
    {
        return
        [
            QualifiedExercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 10, insectCompatibility),
            QualifiedExercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 5, insectCompatibility),
            QualifiedExercise(7, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 7, insectCompatibility),
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors, 10, insectCompatibility),
            QualifiedExercise(4, CanonicalMuscleGroup.SpinalExtensors, 5, insectCompatibility),
            QualifiedExercise(5, CanonicalMuscleGroup.ScapularGirdle, 10, insectCompatibility),
            QualifiedExercise(6, CanonicalMuscleGroup.ScapularGirdle, 5, insectCompatibility),
        ];
    }

    private static Exercise[] ReviewedInsectCatalog()
    {
        var exercises = new List<Exercise>();
        int exerciseId = 10_000;
        foreach (CanonicalMuscleGroup primary in
                 Enum.GetValues<CanonicalMuscleGroup>())
        {
            CanonicalMuscleGroup[] secondary =
                Enum.GetValues<CanonicalMuscleGroup>()
                    .Where(group => group != primary)
                    .ToArray();
            exercises.Add(Exercise(
                exerciseId++,
                primary,
                0,
                ExerciseSideSequence.Continuous,
                ExerciseInsectCompatibility.Compatible,
                secondary));

            exercises.Add(Exercise(
                exerciseId++,
                primary,
                100,
                ExerciseSideSequence.Continuous,
                ExerciseInsectCompatibility.Incompatible,
                secondary));
        }

        return exercises.ToArray();
    }

    private static Exercise QualifiedForGroup(
        int id,
        WorkoutGroup group,
        int score = 0,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
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
            score,
            sideSequence: sideSequence,
            insectCompatibility: insectCompatibility);
    }

    private static Exercise QualifiedExercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(3, primary);
        return ExerciseWithCoverage(
            id,
            primary,
            3,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group),
            score,
            insectCompatibility: insectCompatibility);
    }

    private static Exercise ExerciseWithCoverage(
        int id,
        CanonicalMuscleGroup primary,
        int minutes,
        int inBucketCoverage,
        int score = 0,
        CanonicalMuscleGroup[]? additionalSecondaries = null,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
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
            .Concat(additionalSecondaries ?? [])
            .Where(candidate => candidate != primary)
            .Distinct()
            .ToArray();
        return Exercise(
            id,
            primary,
            score,
            sideSequence,
            insectCompatibility,
            secondary);
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

    private static Exercise CloneWithDirectionPartner(
        Exercise source,
        int directionPartnerExerciseId)
    {
        return new Exercise
        {
            Id = source.Id,
            Name = source.Name,
            RetiredName = source.RetiredName,
            Video = source.Video,
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            DirectionPartnerExerciseId = directionPartnerExerciseId,
            InsectCompatibility = source.InsectCompatibility,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = source.Silent,
        };
    }

    private static Exercise CloneWithMuscularDemand(
        Exercise source,
        int muscularDemand)
    {
        return new Exercise
        {
            Id = source.Id,
            Name = source.Name,
            RetiredName = source.RetiredName,
            Video = source.Video,
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            DirectionPartnerExerciseId = source.DirectionPartnerExerciseId,
            InsectCompatibility = source.InsectCompatibility,
            MuscularDemand = muscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = source.Silent,
        };
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0,
        params CanonicalMuscleGroup[] secondary)
        => Exercise(id, primary, score, ExerciseSideSequence.Continuous, secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        params CanonicalMuscleGroup[] secondary)
        => Exercise(
            id,
            primary,
            score,
            sideSequence,
            ExerciseInsectCompatibility.Unreviewed,
            secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        ExerciseInsectCompatibility insectCompatibility,
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
            SideSequence = sideSequence,
            InsectCompatibility = insectCompatibility,
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
