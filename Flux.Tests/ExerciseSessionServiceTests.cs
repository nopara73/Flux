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
    public void MirrorPreferenceBreaksTiesButNeverOverridesARealVote()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] agnostic = groups
            .Select((group, index) => QualifiedForGroup(
                20_000 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] benefitsGreatly = agnostic
            .Select(exercise => CloneWithMirrorRelationship(
                exercise,
                exercise.Id + 1,
                ExerciseMirrorRelationship.BenefitsGreatly))
            .ToArray();
        Exercise[] exercises = agnostic.Concat(benefitsGreatly).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var tiedState = new WorkoutState();

        service.StartWorkout(tiedState, 3, WorkoutModifiers.Mirror);

        Assert.All(service.GetActiveGroups(tiedState), group => Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            service.GetSelectedExercise(tiedState, group).MirrorRelationship));

        foreach (Exercise exercise in agnostic)
        {
            exercise.Score = 1;
        }
        var votedState = new WorkoutState();
        service.StartWorkout(votedState, 3, WorkoutModifiers.Mirror);

        Assert.All(service.GetActiveGroups(votedState), group => Assert.Equal(
            ExerciseMirrorRelationship.Agnostic,
            service.GetSelectedExercise(votedState, group).MirrorRelationship));
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

        Assert.Equal(16, state.Version);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
    }

    [Fact]
    public void BinaryMirrorStateDoesNotGuessMirrorHeightDuringMigration()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(3).Groups[0];
        Exercise selected = exercises.First(exercise =>
            WorkoutCoveragePolicy.IsSelectable(exercise, group));
        var state = new WorkoutState
        {
            Version = 11,
            LastWorkoutModifiers = WorkoutModifiers.Insect |
                WorkoutModifiers.Mirror,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [group.Id] = selected.Id,
                [$"p4|{group.Id}"] = selected.Id,
            },
        };
        var service = new ExerciseSessionService(exercises, new Random(1));

        service.Initialize(state);

        Assert.Equal(16, state.Version);
        Assert.Equal(WorkoutModifiers.Insect, state.LastWorkoutModifiers);
        Assert.Equal(MirrorEquipment.None,
            WorkoutModifierPolicy.GetMirrorEquipment(state.LastWorkoutModifiers));
        Assert.DoesNotContain(state.SelectedExerciseIds.Keys,
            key => key.StartsWith("p4|", StringComparison.Ordinal));
        Assert.Equal(selected.Id, state.SelectedExerciseIds[group.Id]);
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

    [Fact]
    public void GlobalLineupUsesAtMostOneAliasOfTheSameSessionMovement()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise root = CloneWithMuscularDemand(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            FullyCoveredExercise(2, groups[0].CanonicalGroups.First(), 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [root, alias, middle, last],
            new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Single(selected, exercise => exercise.Id is 1 or 2);
        Assert.Equal(
            selected.Length,
            selected
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
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
    public void FortyFiveMinutesUpgradeTimedPairsBeforeAddingRepeatedSets()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                sideSequence: index < 6
                    ? ExerciseSideSequence.ScreenRightThenLeft
                    : ExerciseSideSequence.Continuous,
                directionSequence: index is >= 6 and < 12
                    ? ExerciseDirectionSequence.ClockwiseThenCounterclockwise
                    : ExerciseDirectionSequence.None))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(33, rounds.Length);
        Assert.Equal(12, rounds.Count(round => round.UsesFullSideTiming));
        Assert.All(rounds.Where(round => round.UsesFullSideTiming), round =>
            Assert.True(MovementPhasePresentationPolicy.UsesTimedPair(
                service.GetSelectedExercise(state, round).SideSequence,
                service.GetSelectedExercise(state, round).DirectionSequence)));
        Assert.Equal(
            6,
            rounds.Count(round => round.UsesFullSideTiming &&
                service.GetSelectedExercise(state, round).DirectionSequence !=
                    ExerciseDirectionSequence.None));
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
            .Where(round => round.Id.EndsWith(".direction1", StringComparison.Ordinal))
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

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void DirectionPairsAreEntirelyExcludedFromShortWorkouts(int minutes)
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

        Assert.All(service.GetActiveGroups(state), group => Assert.Equal(
            0,
            service.GetSelectedExercise(state, group).DirectionPartnerExerciseId));
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
    }

    [Fact]
    public void IdlePairSelectionIsPreservedForLongWorkoutsButReplacedForThirtyMinutes()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        var shortState = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [group.Id] = 1,
            },
        };
        service.Initialize(shortState);
        Assert.Equal(1, shortState.SelectedExerciseIds[group.Id]);

        service.StartWorkout(shortState, 30, WorkoutModifiers.None);

        Assert.NotEqual(1, shortState.SelectedExerciseIds[group.Id]);
        Assert.Equal(
            0,
            service.GetSelectedExercise(shortState, group)
                .DirectionPartnerExerciseId);

        var longState = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [group.Id] = 1,
            },
        };
        service.Initialize(longState);
        service.StartWorkout(longState, 45, WorkoutModifiers.None);

        Assert.Equal(1, longState.SelectedExerciseIds[group.Id]);
        Assert.Equal(2, longState.ActiveDirectionPartnerExerciseIds[group.Id]);
    }

    [Fact]
    public void EveryRepeatedDirectionPairRemainsAdjacentInNinetyMinutes()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 90, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup[] pairLeads = rounds
            .Where(round => round.IsDirectionPairLead)
            .ToArray();
        Assert.True(pairLeads.Length >= 2);
        Assert.Equal(90, rounds.Sum(round => round.UsesFullSideTiming ? 2 : 1));
        Assert.All(pairLeads, lead =>
        {
            WorkoutGroup decision = rounds[lead.Order];
            Assert.True(decision.IsPairDecisionRound);
            Assert.Equal(lead.PairedRoundId, decision.Id);
            Assert.Equal(lead.Id, decision.PairedRoundId);
            Assert.Equal(lead.SelectionKey, decision.SelectionKey);
            Assert.Equal(
                service.GetSelectedExercise(state, lead).DirectionPartnerExerciseId,
                service.GetSelectedExercise(state, decision).Id);
        });
    }

    [Fact]
    public void ShuffleRejectsTheCurrentExerciseAndReplacesOnlyItsSlot()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        IReadOnlyList<WorkoutGroup> groups = service.GetActiveGroups(state);
        WorkoutGroup current = groups[0];
        int originalExerciseId = service.GetSelectedExercise(state, current).Id;
        Dictionary<string, int> otherSelections = groups
            .Skip(1)
            .ToDictionary(
                group => group.Id,
                group => service.GetSelectedExercise(state, group).Id,
                StringComparer.Ordinal);
        Dictionary<int, int> originalScores = exercises.ToDictionary(
            exercise => exercise.Id,
            exercise => exercise.Score);
        state.LastKeptExerciseIds.Add(originalExerciseId);
        state.SelectedExerciseIds[
            $"p{(int)WorkoutModifiers.Insect}|{current.SelectionKey}"] =
            originalExerciseId;

        Assert.True(service.CanShuffleNextExercise(state, current));
        ShuffledExerciseResult? result = service.ShuffleNextExercise(state, current);

        Assert.NotNull(result);
        Assert.Equal(originalExerciseId, result.RejectedExercise.Id);
        Assert.NotEqual(originalExerciseId, result.ReplacementExercise.Id);
        Assert.Same(
            result.ReplacementExercise,
            service.GetSelectedExercise(state, current));
        Assert.Empty(state.Outcomes);
        Assert.Equal(
            [originalExerciseId],
            result.ScoreUpdates.Select(exercise => exercise.Id));
        Assert.Equal(
            originalScores[originalExerciseId] - 1,
            result.RejectedExercise.Score);
        Assert.Contains(originalExerciseId, state.NextWorkoutExcludedExerciseIds);
        Assert.DoesNotContain(originalExerciseId, state.LastKeptExerciseIds);
        Assert.DoesNotContain(originalExerciseId, state.SelectedExerciseIds.Values);
        Assert.All(exercises.Where(exercise => exercise.Id != originalExerciseId),
            exercise => Assert.Equal(originalScores[exercise.Id], exercise.Score));
        Assert.All(groups.Skip(1), group =>
            Assert.Equal(
                otherSelections[group.Id],
                service.GetSelectedExercise(state, group).Id));
    }

    [Fact]
    public void ShuffleDoesNotOfferAnotherAliasOfTheRejectedMovement()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise current = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 50),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alternative = QualifiedForGroup(3, groups[0]);
        Exercise middle = QualifiedForGroup(4, groups[1]);
        Exercise last = QualifiedForGroup(5, groups[2]);
        var service = new ExerciseSessionService(
            [current, alias, alternative, middle, last],
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup currentGroup = service.GetActiveGroups(state)[0];

        ShuffledExerciseResult? result =
            service.ShuffleNextExercise(state, currentGroup);

        Assert.NotNull(result);
        Assert.Equal(current.Id, result.RejectedExercise.Id);
        Assert.Equal(alternative.Id, result.ReplacementExercise.Id);
    }

    [Fact]
    public void ShuffleVisitsEveryEligibleAlternativeWithoutRepeating()
    {
        Exercise[] exercises =
        [
            .. ThreeGroupCatalog(),
            QualifiedExercise(8, CanonicalMuscleGroup.ScapularGirdle, 3),
        ];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)!;
        var visitedExerciseIds = new HashSet<int>
        {
            service.GetSelectedExercise(state, group).Id,
        };

        int shuffleCount = 0;
        while (service.CanShuffleNextExercise(state, group))
        {
            ShuffledExerciseResult result =
                service.ShuffleNextExercise(state, group)!;
            Assert.True(visitedExerciseIds.Add(result.ReplacementExercise.Id));
            Assert.Empty(state.Outcomes);
            Assert.True(++shuffleCount < 10);
        }

        Assert.Equal([5, 6, 8], visitedExerciseIds.Order());
        int currentExerciseId = service.GetSelectedExercise(state, group).Id;
        Assert.Equal(
            visitedExerciseIds.Where(id => id != currentExerciseId).Order(),
            state.NextWorkoutExcludedExerciseIds
                .Where(visitedExerciseIds.Contains)
                .Order());
    }

    [Fact]
    public void DirectionPairShuffleRejectsBothAndIsUnavailableAfterDirectionOne()
    {
        const CanonicalMuscleGroup primary =
            CanonicalMuscleGroup.ForearmFlexorsAndPronators;
        Exercise third = CloneWithDirectionPartner(
            FullyCoveredExercise(3, primary, -100),
            4);
        Exercise fourth = CloneWithDirectionPartner(
            FullyCoveredExercise(4, primary, -100),
            3);
        Exercise[] exercises = [.. DirectionPairCatalog(), third, fourth];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup lead = service.GetActiveGroups(state).Single(round =>
            round.IsDirectionPairLead &&
            service.GetSelectedExercise(state, round).Id == 1);
        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != lead.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }

        Assert.True(service.CanShuffleNextExercise(state, lead));
        Exercise rejectedLead = service.GetSelectedExercise(state, lead);
        Exercise rejectedPartner = exercises.Single(exercise =>
            exercise.Id == rejectedLead.DirectionPartnerExerciseId);
        int rejectedLeadScore = rejectedLead.Score;
        int rejectedPartnerScore = rejectedPartner.Score;
        ShuffledExerciseResult? result = service.ShuffleNextExercise(state, lead);

        Assert.NotNull(result);
        Assert.DoesNotContain(
            result.ReplacementExercise.Id,
            new[] { rejectedLead.Id, rejectedPartner.Id });
        Assert.Equal(
            [rejectedLead.Id, rejectedPartner.Id],
            result.ScoreUpdates.Select(exercise => exercise.Id));
        Assert.Equal(rejectedLeadScore - 1, rejectedLead.Score);
        Assert.Equal(rejectedPartnerScore - 1, rejectedPartner.Score);
        Assert.Contains(rejectedLead.Id, state.NextWorkoutExcludedExerciseIds);
        Assert.Contains(rejectedPartner.Id, state.NextWorkoutExcludedExerciseIds);
        WorkoutGroup replacementLead = service.GetNextGroup(state)!;
        Assert.Same(
            result.ReplacementExercise,
            service.GetSelectedExercise(state, replacementLead));
        Assert.Equal(
            45,
            service.GetActiveGroups(state).Sum(round =>
                round.UsesFullSideTiming ? 2 : 1));
        Assert.Equal(-100, third.Score);
        Assert.Equal(-100, fourth.Score);

        Exercise[] secondExercises = DirectionPairCatalog();
        var secondService = new ExerciseSessionService(
            secondExercises,
            new Random(1));
        var secondState = new WorkoutState();
        secondService.StartWorkout(secondState, 45, WorkoutModifiers.None);
        WorkoutGroup secondLead = secondService.GetActiveGroups(secondState)
            .Single(round => round.IsDirectionPairLead);
        foreach (WorkoutGroup priorRound in secondService.GetActiveGroups(secondState)
                     .TakeWhile(round => round.Id != secondLead.Id))
        {
            secondService.RecordOutcome(secondState, priorRound, keep: true);
        }
        secondService.AdvanceDirectionPair(secondState, secondLead);
        WorkoutGroup secondDirection = secondService.GetNextGroup(secondState)!;

        Assert.True(secondDirection.IsPairDecisionRound);
        Assert.False(secondService.CanShuffleNextExercise(
            secondState,
            secondDirection));
        Assert.Null(secondService.ShuffleNextExercise(
            secondState,
            secondDirection));
    }

    [Fact]
    public void DirectionPairCanOnlyBeKeptAfterItsSecondDirection()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup lead = service.GetActiveGroups(state).Single(round =>
            round.IsDirectionPairLead);
        WorkoutGroup decision = service.GetActiveGroups(state).Single(round =>
            round.Id == lead.PairedRoundId);
        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != lead.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }

        Assert.Throws<InvalidOperationException>(() =>
            service.RecordOutcome(state, lead, keep: true));
        Assert.DoesNotContain(lead.Id, state.Outcomes.Keys);

        service.AdvanceDirectionPair(state, lead);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[lead.Id]);
        Assert.Equal(decision.Id, service.GetNextGroup(state)?.Id);
        RecordedWorkoutOutcome result = service.RecordOutcomeWithScoreUpdates(
            state,
            decision,
            keep: true);

        Assert.Empty(result.ScoreUpdates);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[lead.Id]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[decision.Id]);
        Assert.Equal(100, service.GetSelectedExercise(state, lead).Score);
        Assert.Equal(100, service.GetSelectedExercise(state, decision).Score);
    }

    [Fact]
    public void IntermediateRepeatedSetHasNoKeepAndRestoresAsAutomaticContinuation()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] repeatedRounds = service.GetActiveGroups(state)
            .GroupBy(round => round.SelectionKey)
            .First(rounds => rounds.Count() > 1)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup firstSet = repeatedRounds[0];

        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != firstSet.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }

        service.BeginRest(state, firstSet, 123456);

        Assert.True(service.IsIntermediateSetCompletion(state, firstSet));
        Assert.False(service.KeepPendingRest(state));
        Assert.False(state.PendingRestKept);

        service.AdvanceRepeatedSet(state, firstSet);
        service.ClearPendingRest(state);
        WorkoutGroup nextSet = service.GetNextGroup(state)!;
        int movementDurationMilliseconds =
            (nextSet.UsesFullSideTiming
                ? MovementPhaseSchedule.FullSideTotalDurationSeconds
                : MovementPhaseSchedule.TotalDurationSeconds) * 1_000;
        service.PauseMovement(
            state,
            nextSet,
            movementDurationMilliseconds,
            pausedByUser: false);

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        Assert.Equal(ExerciseOutcome.Neutral, restored.Outcomes[firstSet.Id]);
        Assert.Equal(firstSet.SelectionKey, nextSet.SelectionKey);
        Assert.True(service.IsSetContinuationRound(restored, nextSet));
        Assert.Equal(nextSet.Id, service.GetPendingMovementGroup(restored)?.Id);
        Assert.Equal(
            movementDurationMilliseconds,
            service.GetPendingMovementMillisecondsRemaining(restored, 999999));
        Assert.Equal(10, service.GetSelectedExercise(restored, nextSet).Score);

        service.BeginRest(restored, nextSet, 234567);
        Assert.False(service.IsIntermediateSetCompletion(restored, nextSet));
        Assert.True(service.KeepPendingRest(restored));
    }

    [Fact]
    public void RepeatedDirectionPairDefersItsSharedKeepUntilTheFinalSet()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 90, WorkoutModifiers.None);
        WorkoutGroup firstLead = service.GetActiveGroups(state)
            .Where(round => round.IsDirectionPairLead)
            .GroupBy(round => round.SelectionKey)
            .First(rounds => rounds.Count() > 1)
            .OrderBy(round => round.Order)
            .First();
        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != firstLead.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }

        service.AdvanceDirectionPair(state, firstLead);
        WorkoutGroup firstDecision = service.GetNextGroup(state)!;
        service.BeginRest(state, firstDecision, 123456);

        Assert.True(firstDecision.IsPairDecisionRound);
        Assert.True(service.IsIntermediateSetCompletion(state, firstDecision));
        Assert.False(service.KeepPendingRest(state));

        service.AdvanceRepeatedSet(state, firstDecision);
        service.ClearPendingRest(state);
        WorkoutGroup nextSet = service.GetNextGroup(state)!;

        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[firstLead.Id]);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[firstDecision.Id]);
        Assert.Equal(firstLead.SelectionKey, nextSet.SelectionKey);
        Assert.True(service.IsSetContinuationRound(state, nextSet));
    }

    [Fact]
    public void LinkedDirectionPartnerIsNeverSelectedAsAnotherBaseUnit()
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
        Exercise filler = FullyCoveredExercise(
            100,
            groups[1].CanonicalGroups.Order().First(),
            10);
        var service = new ExerciseSessionService(
            [first, second, filler, .. remaining],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Single(state.ActiveDirectionPartnerExerciseIds);
        Assert.DoesNotContain(
            state.SelectedExerciseIds.Values,
            exerciseId => exerciseId == second.Id);
        Assert.Single(service.GetActiveGroups(state), round =>
            round.Id.EndsWith(".direction1", StringComparison.Ordinal) &&
            service.GetSelectedExercise(state, round).Id == second.Id);
    }

    [Fact]
    public void RejectingLinkedDirectionsAppliesOneSharedDecisionToBoth()
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
            round.Id.EndsWith(".direction1", StringComparison.Ordinal));
        WorkoutGroup pairLead = service.GetActiveGroups(state).Single(round =>
            round.PairedRoundId == directionRound.Id);
        int baseExerciseId = state.SelectedExerciseIds[directionRound.SelectionKey];
        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != pairLead.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }
        service.AdvanceDirectionPair(state, pairLead);
        RecordedWorkoutOutcome result = service.RecordOutcomeWithScoreUpdates(
            state,
            directionRound,
            keep: false);

        Assert.Equal(-1, partner.Score);
        Assert.Equal(9, baseExercises.Single(exercise =>
            exercise.Id == baseExerciseId).Score);
        Assert.Equal(
            new[] { baseExerciseId, partner.Id }.Order(),
            result.ScoreUpdates.Select(exercise => exercise.Id).Order());
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[pairLead.Id]);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[directionRound.Id]);
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
        state.ActiveSetCountsBySelectionGroupId.Clear();

        service.Initialize(state);

        Assert.Equal(16, state.Version);
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
        Assert.True(
            expected.Order().SequenceEqual(state.ActiveFullSideRoundIds.Order()),
            $"Expected: {string.Join(",", expected.Order())}; " +
            $"actual: {string.Join(",", state.ActiveFullSideRoundIds.Order())}");
        Assert.Empty(state.ActiveExtraSetSelectionGroupIds);
        Assert.Equal(30, service.GetActiveGroups(state).Count);

        state.LastKeptExerciseIds.Clear();
        service.Initialize(state);
        Assert.Equal(expected.Order(), state.ActiveFullSideRoundIds.Order());
    }

    [Fact]
    public void HardSidedExercisesReceiveFullSideTimingBeforeNonHardKeeps()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                sideSequence: ExerciseSideSequence.ScreenLeftThenRight))
            .Select((exercise, index) => index is >= 10 and < 25
                ? CloneWithMuscularDemand(
                    exercise,
                    WorkoutRecoveryPolicy.HardMuscularDemand)
                : exercise)
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastKeptExerciseIds = exercises.Take(10)
                .Select(exercise => exercise.Id)
                .ToHashSet(),
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        string[] expected = groups.Skip(10)
            .Take(15)
            .Select(group => $"{group.Id}.set1")
            .ToArray();
        Assert.Equal(expected.Order(), state.ActiveFullSideRoundIds.Order());
        Assert.Empty(state.ActiveExtraSetSelectionGroupIds);
        Assert.DoesNotContain(
            $"{groups[0].Id}.set1",
            state.ActiveFullSideRoundIds);

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

    [Fact]
    public void FortyFiveMinuteExtraSetsPreferHardExercisesBeforeNonHardKeeps()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .Select((exercise, index) => index is >= 10 and < 25
                ? CloneWithMuscularDemand(
                    exercise,
                    WorkoutRecoveryPolicy.HardMuscularDemand)
                : exercise)
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastKeptExerciseIds = exercises.Take(10)
                .Select(exercise => exercise.Id)
                .ToHashSet(),
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        string[] expected = groups.Skip(10)
            .Take(15)
            .Select(group => group.Id)
            .ToArray();
        Assert.Equal(
            expected.Order(),
            state.ActiveExtraSetSelectionGroupIds.Order());
        Assert.All(groups.Take(10), group => Assert.Single(
            service.GetActiveGroups(state),
            round => round.SelectionKey == group.Id));
        Assert.All(groups.Skip(10).Take(15), group => Assert.Equal(
            2,
            service.GetActiveGroups(state)
                .Count(round => round.SelectionKey == group.Id)));
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
    public void InterruptedIntermediateSetRestDoesNotCreateAHiddenDownvote()
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
        var service = new ExerciseSessionService(
            baseline,
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
        Assert.True(service.IsIntermediateSetCompletion(state, pendingRound));

        Exercise? firstResult = service.FinishInterruptedWorkout(state);
        Exercise? repeatedResult = service.FinishInterruptedWorkout(state);

        Assert.Null(firstResult);
        Assert.Null(repeatedResult);
        Assert.Equal(10, original.Score);
        Assert.Equal(original.Id, state.SelectedExerciseIds[target.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InterruptedFinalRepeatedSetRestStillSettlesExactlyOnce()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise[] replacements = groups
            .Select((group, index) => QualifiedForGroup(1001 + index, group, 5))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 90, WorkoutModifiers.None);
        WorkoutGroup pendingRound = service.GetActiveGroups(state)
            .First(round =>
                service.IsSetContinuationRound(state, round) &&
                !service.IsIntermediateSetCompletion(state, round) &&
                !round.IsDirectionPairRound);
        Exercise original = service.GetSelectedExercise(state, pendingRound);

        foreach (WorkoutGroup priorRound in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != pendingRound.Id))
        {
            service.RecordOutcome(state, priorRound, keep: true);
        }
        service.BeginRest(state, pendingRound, 123456);

        Exercise? firstResult = service.FinishInterruptedWorkout(state);
        Exercise? repeatedResult = service.FinishInterruptedWorkout(state);

        Assert.Same(original, firstResult);
        Assert.Null(repeatedResult);
        Assert.Equal(9, original.Score);
        Assert.NotEqual(
            original.Id,
            state.SelectedExerciseIds[pendingRound.SelectionKey]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void PersistedPendingDirectionRestCannotRecurseThroughAllocationValidation()
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
            baseExercises[0].Id,
            silent: false);
        baseExercises[0] = CloneWithDirectionPartner(
            baseExercises[0],
            partner.Id);
        Exercise filler = FullyCoveredExercise(
            2001,
            groups[0].CanonicalGroups.Order().First(),
            10);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner, filler],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.Silence);
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        state.ActiveDirectionPartnerExerciseIds[groups[0].Id] = partner.Id;
        state.PendingRestGroupId = $"{groups[0].Id}.direction1";
        state.PendingRestEndsAtUnixMilliseconds = 123456;

        service.Initialize(state);

        Assert.Equal(45, state.ActiveWorkoutMinutes);
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, partner.Score);
        Assert.Null(service.FinishInterruptedWorkout(state));
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
    public void InitializeRepairsSavedAliasesAcrossDifferentMuscleSlots()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise root = CloneWithMuscularDemand(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 20),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            FullyCoveredExercise(2, groups[0].CanonicalGroups.First(), 20),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise[] fillers = groups
            .Select((group, index) => QualifiedForGroup(10 + index, group))
            .ToArray();
        var service = new ExerciseSessionService(
            [root, alias, .. fillers],
            new AlwaysZeroRandom());
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = root.Id,
                [groups[1].Id] = alias.Id,
                [groups[2].Id] = fillers[2].Id,
            },
        };

        service.Initialize(state);

        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Single(selected, exercise => exercise.Id is 1 or 2);
        Assert.Equal(
            selected.Length,
            selected
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
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
    public void FreshHardCandidateOutranksNonHardKeepAndMirrorPreference()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMirrorRelationship(
            CloneWithMuscularDemand(
                QualifiedForGroup(2, groups[0]),
                muscularDemand: 1),
            id: 2,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hard, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds = [nonHardKeep.Id],
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = nonHardKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.Mirror);

        Assert.Equal(hard.Id, state.SelectedExerciseIds[
            $"p{(int)WorkoutModifiers.Mirror}|{groups[0].Id}"]);
        Assert.Contains(nonHardKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void FreshHardKeepGetsAnOpportunityDespiteLowerSavedScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: -1),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastKeptExerciseIds = [hardKeep.Id, nonHardKeep.Id],
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(hardKeep.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void RecoveringHardKeepYieldsToNonHardKeepWithoutBeingForgotten()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds = [hardKeep.Id, nonHardKeep.Id],
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [hardKeep.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = hardKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(nonHardKeep.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Contains(hardKeep.Id, state.LastKeptExerciseIds);
        Assert.Contains(nonHardKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void RecoveringModerateKeepYieldsToEasyWorkWithoutBeingForgotten()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise moderateKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 1);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [moderateKeep, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            LastKeptExerciseIds = [moderateKeep.Id],
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [moderateKeep.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = moderateKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(easy.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Contains(moderateKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void HardRotationNeverOverridesAHigherPersistedUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise rejectedHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: -1),
            muscularDemand: 2);
        Exercise nonHard = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [rejectedHard, nonHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(nonHard.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(-1, rejectedHard.Score);
        Assert.Equal(0, nonHard.Score);
    }

    [Fact]
    public void RecoveryRemainsSoftWhenHardExerciseHasAHigherUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise recoveringHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 1),
            muscularDemand: 2);
        Exercise nonHard = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recoveringHard, nonHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recoveringHard.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(recoveringHard.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void ModerateRecoveryRemainsSoftWhenExerciseHasAHigherUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise recoveringModerate = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 1),
            muscularDemand: 1);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recoveringModerate, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recoveringModerate.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            recoveringModerate.Id,
            state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void EquivalentFreshHardCandidatesFavorTheLongestRestedPrimaryMuscle()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        CanonicalMuscleGroup recentlyWorked = groups[0].CanonicalGroups.First();
        CanonicalMuscleGroup longestRested = groups[0].CanonicalGroups.Skip(1).First();
        int requiredCoverage = WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(
            groups[0]);
        Exercise recentHard = CloneWithMuscularDemand(
            ExerciseWithCoverage(1, recentlyWorked, 3, requiredCoverage),
            muscularDemand: 2);
        Exercise restedHard = CloneWithMuscularDemand(
            ExerciseWithCoverage(2, longestRested, 3, requiredCoverage),
            muscularDemand: 2);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recentHard, restedHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recentlyWorked.ToString()] =
                        now.AddHours(-40).ToUnixTimeMilliseconds(),
                    [longestRested.ToString()] =
                        now.AddHours(-72).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(restedHard.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void CompletingHardExerciseStartsBothRecoveryWindowsButSkippingDoesNot()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 10),
            muscularDemand: 2);
        Exercise alternative = QualifiedForGroup(2, groups[0]);
        Exercise middle = QualifiedForGroup(3, groups[1], score: 10);
        Exercise last = QualifiedForGroup(4, groups[2], score: 10);
        var service = new ExerciseSessionService(
            [hard, alternative, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var completedState = new WorkoutState();
        service.StartWorkout(completedState, 3, WorkoutModifiers.None);
        WorkoutGroup completedGroup = service.GetNextGroup(completedState)!;

        service.BeginRest(
            completedState,
            completedGroup,
            now.AddSeconds(15).ToUnixTimeMilliseconds());
        var stateStore = new FakeWorkoutStateStore();
        stateStore.Save(completedState);
        WorkoutState restored = stateStore.Load();

        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            restored.LastHardWorkUnixMillisecondsByPrimaryMuscle[
                hard.PrimaryCanonicalGroup.ToString()]);
        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            restored.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
                hard.PrimaryCanonicalGroup.ToString()]);
        Assert.Equal(10, hard.Score);

        var skippedState = new WorkoutState();
        service.StartWorkout(skippedState, 3, WorkoutModifiers.None);
        WorkoutGroup skippedGroup = service.GetNextGroup(skippedState)!;
        service.RecordOutcome(skippedState, skippedGroup, keep: false);

        Assert.Empty(skippedState.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        Assert.Empty(skippedState.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
    }

    [Fact]
    public void CompletingModerateExerciseStartsOnlyMeaningfulRecovery()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise moderate = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 10),
            muscularDemand: 1);
        Exercise alternative = QualifiedForGroup(2, groups[0]);
        Exercise middle = QualifiedForGroup(3, groups[1], score: 10);
        Exercise last = QualifiedForGroup(4, groups[2], score: 10);
        var service = new ExerciseSessionService(
            [moderate, alternative, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup completedGroup = service.GetNextGroup(state)!;

        service.BeginRest(
            state,
            completedGroup,
            now.AddSeconds(15).ToUnixTimeMilliseconds());

        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
                moderate.PrimaryCanonicalGroup.ToString()]);
        Assert.Empty(state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        Assert.Equal(10, moderate.Score);
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
    public void InProgressMovementSurvivesPersistenceWithExactPausedTime()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var stateStore = new FakeWorkoutStateStore();
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = new DateTimeOffset(
            2026,
            8,
            23,
            2,
            0,
            0,
            TimeSpan.Zero).ToUnixTimeMilliseconds();

        service.BeginMovement(
            state,
            group,
            millisecondsRemaining: 42_000,
            endsAtUnixMilliseconds: now + 42_000);
        stateStore.Save(state);

        WorkoutState restoredRunning = stateStore.Load();
        service.Initialize(restoredRunning);
        Assert.Equal(3, restoredRunning.ActiveWorkoutMinutes);
        Assert.Equal(
            group.Id,
            service.GetPendingMovementGroup(restoredRunning)?.Id);
        Assert.Equal(
            40_000,
            service.GetPendingMovementMillisecondsRemaining(
                restoredRunning,
                now + 2_000));
        Assert.Equal(
            42_000,
            service.GetPendingMovementMillisecondsRemaining(
                restoredRunning,
                now + (long)TimeSpan.FromMinutes(2).TotalMilliseconds));

        service.PauseMovement(
            restoredRunning,
            group,
            millisecondsRemaining: 31_123,
            pausedByUser: false);
        stateStore.Save(restoredRunning);

        WorkoutState restoredPaused = stateStore.Load();
        service.Initialize(restoredPaused);
        Assert.Equal(3, restoredPaused.ActiveWorkoutMinutes);
        Assert.Equal(group.Id, restoredPaused.PendingMovementGroupId);
        Assert.Equal(31_123, restoredPaused.PendingMovementMillisecondsRemaining);
        Assert.Equal(0, restoredPaused.PendingMovementEndsAtUnixMilliseconds);
        Assert.False(restoredPaused.PendingMovementPausedByUser);
        Assert.Equal(
            31_123,
            service.GetPendingMovementMillisecondsRemaining(
                restoredPaused,
                now + (long)TimeSpan.FromHours(2).TotalMilliseconds));
    }

    [Fact]
    public void PendingRestSurvivesInitializationAndIdentifiesTheNextRound()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long restEndsAt = DateTimeOffset.UtcNow.AddSeconds(15)
            .ToUnixTimeMilliseconds();
        service.BeginRest(state, group, restEndsAt);

        service.Initialize(state);

        Assert.Equal(group.Id, service.GetPendingRestGroup(state)?.Id);
        Assert.Equal(restEndsAt, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(3, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void CompletingMovementClearsResumeCheckpointBeforeRest()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        service.BeginMovement(state, group, 50_000, now + 50_000);

        service.BeginRest(state, group, now + 15_000);

        Assert.Null(state.PendingMovementGroupId);
        Assert.Equal(0, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(0, state.PendingMovementEndsAtUnixMilliseconds);
        Assert.False(state.PendingMovementPausedByUser);
        Assert.Equal(group.Id, state.PendingRestGroupId);
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
        Assert.Equal(16, state.Version);
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

    private static Exercise[] DirectionPairCatalog()
    {
        Exercise first = CloneWithDirectionPartner(
            FullyCoveredExercise(
                1,
                CanonicalMuscleGroup.ForearmFlexorsAndPronators,
                100),
            2);
        Exercise second = CloneWithDirectionPartner(
            FullyCoveredExercise(
                2,
                CanonicalMuscleGroup.ForearmFlexorsAndPronators,
                100),
            1);
        CanonicalMuscleGroup[] muscleGroups =
            Enum.GetValues<CanonicalMuscleGroup>();
        Exercise[] fillers = Enumerable.Range(0, 30)
            .Select(index => FullyCoveredExercise(
                100 + index,
                muscleGroups[index % muscleGroups.Length]))
            .ToArray();
        return [first, second, .. fillers];
    }

    private static Exercise QualifiedForGroup(
        int id,
        WorkoutGroup group,
        int score = 0,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed,
        ExerciseDirectionSequence directionSequence = ExerciseDirectionSequence.None)
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
            insectCompatibility: insectCompatibility,
            directionSequence: directionSequence);
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
            ExerciseInsectCompatibility.Unreviewed,
        ExerciseDirectionSequence directionSequence = ExerciseDirectionSequence.None)
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
            directionSequence,
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
        int directionPartnerExerciseId,
        bool? silent = null)
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
            SessionMovementId = source.SessionMovementId,
            InsectCompatibility = source.InsectCompatibility,
            MirrorRelationship = source.MirrorRelationship,
            MinimumMirrorCoverage = source.MinimumMirrorCoverage,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = silent ?? source.Silent,
        };
    }

    private static Exercise CloneWithMuscularDemand(
        Exercise source,
        int muscularDemand,
        int? sessionMovementId = null)
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
            SessionMovementId = sessionMovementId ?? source.SessionMovementId,
            InsectCompatibility = source.InsectCompatibility,
            MirrorRelationship = source.MirrorRelationship,
            MinimumMirrorCoverage = source.MinimumMirrorCoverage,
            MuscularDemand = muscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = source.Silent,
        };
    }

    private static Exercise CloneWithMirrorRelationship(
        Exercise source,
        int id,
        ExerciseMirrorRelationship mirrorRelationship)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            RetiredName = source.RetiredName,
            Video = $"exercise_{id:D4}.mp4",
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
            MirrorRelationship = mirrorRelationship,
            MinimumMirrorCoverage = mirrorRelationship is
                ExerciseMirrorRelationship.MirrorOnly or
                ExerciseMirrorRelationship.BenefitsGreatly
                ? ExerciseMirrorCoverage.UpperBody
                : ExerciseMirrorCoverage.None,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = mirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                ? "Mirror"
                : "None",
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
        => Exercise(
            id,
            primary,
            score,
            sideSequence,
            insectCompatibility,
            ExerciseDirectionSequence.None,
            secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        ExerciseInsectCompatibility insectCompatibility,
        ExerciseDirectionSequence directionSequence,
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
            DirectionSequence = directionSequence,
            InsectCompatibility = insectCompatibility,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
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
