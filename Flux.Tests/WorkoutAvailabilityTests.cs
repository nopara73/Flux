using System.Text.Json;
using Flux.Models;
using Flux.Services;

namespace Flux.Tests;

public sealed class WorkoutAvailabilityTests
{
    [Fact]
    public void SharedScenariosEnforceScopeConsentPhysicalConstraintsAndCompleteSequences()
    {
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "workout-availability-cases.json")));
        foreach (JsonElement fixture in fixtures.RootElement.EnumerateArray())
        {
            Exercise[] catalog = fixture.GetProperty("moves").EnumerateArray().Select(Movement).ToArray();
            int minutes = fixture.GetProperty("minutes").GetInt32();
            var modifiers = (WorkoutModifiers)fixture.GetProperty("profile").GetInt32();
            bool limited = fixture.GetProperty("limited").GetBoolean();
            WorkoutAvailability availability = WorkoutAvailabilityPolicy.Evaluate(catalog, minutes, modifiers);
            Assert.Equal(fixture.GetProperty("canStart").GetBoolean(), availability.CanStart);
            Assert.Equal(limited, availability.RequiresAcceptance);
            Assert.Equal(fixture.GetProperty("groupCount").GetInt32(), availability.Groups.Count);
            var service = new ExerciseSessionService(catalog);
            var state = new WorkoutState();
            string before = JsonSerializer.Serialize(state);
            if (limited || !availability.CanStart)
            {
                Assert.Throws<WorkoutUnavailableException>(() => service.StartWorkout(state, minutes, modifiers));
                Assert.Equal(before, JsonSerializer.Serialize(state));
            }
            if (!availability.CanStart) continue;
            service.StartWorkout(state, minutes, modifiers, acceptLimitedCoverage: limited);
            IReadOnlyList<WorkoutGroup> rounds = service.GetActiveGroups(state);
            Assert.Equal(minutes, rounds.Count);
            foreach (WorkoutGroup round in rounds)
            {
                Exercise selected = service.GetSelectedExercise(state, round);
                Assert.True(WorkoutModifierPolicy.IsCompatible(selected, modifiers));
                Assert.Contains(selected.PrimaryCanonicalGroup, round.CanonicalGroups);
            }
            foreach (var selection in rounds.GroupBy(round => round.SelectionKey))
                Assert.Equal(0, selection.Count() % selection.First().SequenceBlockCount);
        }
    }

    [Fact]
    public void EasyPrimaryWorkRotatesTowardAnUntrainedTarget()
    {
        using JsonDocument data = JsonDocument.Parse("""
            [{"id":1,"primary":"ShoulderAbductors"},{"id":2,"primary":"ElbowExtensors"},
             {"id":3,"primary":"AbdominalWall"},{"id":4,"primary":"GlutealExtensors"}]
            """);
        Exercise[] catalog = data.RootElement.EnumerateArray().Select(Movement).ToArray();
        var service = new ExerciseSessionService(catalog);
        var state = new WorkoutState { WorkoutHistory = [new WorkoutSessionLog
        {
            SessionId = 1, WorkoutMinutes = 3, Status = WorkoutSessionStatus.Interrupted,
            Blocks = [new WorkoutBlockLog { PrimaryCanonicalGroup = CanonicalMuscleGroup.ShoulderAbductors,
            MuscularDemand = 0, CompletedAtUnixMilliseconds = 1000 }],
        }] };
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup upper = service.GetActiveGroups(state).Single(group => group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors));
        Assert.Equal(CanonicalMuscleGroup.ElbowExtensors, service.GetSelectedExercise(state, upper).PrimaryCanonicalGroup);
    }

    [Fact]
    public void ImpossibleOldPrimaryPlacementPreservesHistoryAndRequiresSetupReview()
    {
        using JsonDocument data = JsonDocument.Parse("""
            [{"id":1,"primary":"ShoulderAbductors"},{"id":2,"primary":"AbdominalWall"},{"id":3,"primary":"GlutealExtensors"}]
            """);
        Exercise[] catalog = data.RootElement.EnumerateArray().Select(Movement).ToArray();
        var service = new ExerciseSessionService(catalog);
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup first = service.GetNextGroup(state)!;
        service.BeginRest(state, first, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks);
        state.Version = 29;
        state.SelectedExerciseIds[first.SelectionKey] = first.CanonicalGroups.Contains(catalog[1].PrimaryCanonicalGroup) ? 1 : 2;
        service.Initialize(state);
        Assert.True(state.WorkoutSetupReviewRequired);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        WorkoutSessionLog saved = Assert.Single(state.WorkoutHistory);
        Assert.Equal(sessionId, saved.SessionId);
        Assert.Equal(WorkoutSessionStatus.Interrupted, saved.Status);
        Assert.Equal(blocks, JsonSerializer.Serialize(saved.Blocks));
    }

    [Fact]
    public void LightChangeCannotRetainAnUnfinishedDemandingSequence()
    {
        using JsonDocument data = JsonDocument.Parse("""
            [{"id":1,"primary":"ShoulderAbductors","demand":2,"score":100,"blocks":[1,1]},
             {"id":2,"primary":"AbdominalWall"},{"id":3,"primary":"GlutealExtensors"},
             {"id":4,"primary":"ShoulderAbductors"}]
            """);
        Exercise[] catalog = data.RootElement.EnumerateArray().Select(Movement).ToArray();
        var service = new ExerciseSessionService(catalog);
        var state = new WorkoutState();
        service.StartWorkout(state, 5, WorkoutModifiers.None, acceptLimitedCoverage: true);
        WorkoutGroup hard = service.GetActiveGroups(state).First(group => service.GetSelectedExercise(state, group).Id == 1);
        while (service.GetNextGroup(state)!.Id != hard.Id)
        {
            WorkoutGroup next = service.GetNextGroup(state)!;
            if (service.IsIntermediateSequenceBlock(state, next)) service.AdvanceSequence(state, next);
            else service.RecordOutcome(state, next, keep: true);
        }
        service.BeginRest(state, hard, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
        string before = JsonSerializer.Serialize(state);
        Assert.ThrowsAny<InvalidOperationException>(() => service.ReconfigureActiveWorkout(state, WorkoutModifiers.Light, hard.Id));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void UpgradeNormalizesNullableCollectionsBeforeInspectingOldPlacements()
    {
        using JsonDocument data = JsonDocument.Parse("""
            [{"id":1,"primary":"ShoulderAbductors"},{"id":2,"primary":"AbdominalWall"},{"id":3,"primary":"GlutealExtensors"}]
            """);
        var service = new ExerciseSessionService(data.RootElement.EnumerateArray().Select(Movement).ToArray());
        var state = new WorkoutState { Version = 29, ActiveWorkoutMinutes = 3,
            ActiveWorkoutSession = new WorkoutSessionLog { SessionId = 1, WorkoutMinutes = 3 },
            SelectedExerciseIds = null!, Outcomes = null!, WorkoutHistory = null! };
        service.Initialize(state);
        Assert.NotNull(state.SelectedExerciseIds);
        Assert.NotNull(state.Outcomes);
        Assert.NotNull(state.WorkoutHistory);
    }

    private static Exercise Movement(JsonElement move)
    {
        int id = move.GetProperty("id").GetInt32();
        return new Exercise
        {
            Id = id, Name = $"Test {id}", Video = "test.mp4", Practice = "Test", MotionProfile = "Test",
            PrimaryCanonicalGroup = Enum.Parse<CanonicalMuscleGroup>(move.GetProperty("primary").GetString()!),
            SecondaryCanonicalGroups = move.TryGetProperty("secondary", out JsonElement secondary)
                ? secondary.EnumerateArray().Select(value => Enum.Parse<CanonicalMuscleGroup>(value.GetString()!)).ToArray() : [],
            Score = move.TryGetProperty("score", out JsonElement score) ? score.GetInt32() : 0,
            MuscularDemand = move.TryGetProperty("demand", out JsonElement demand) ? demand.GetInt32() : 0,
            Silent = !move.TryGetProperty("silent", out JsonElement silent) || silent.GetBoolean(),
            WallRequired = move.TryGetProperty("wall", out JsonElement wall) && wall.GetBoolean(),
            InsectCompatibility = ExerciseInsectCompatibility.Compatible,
            HardFloorCompatibility = move.TryGetProperty("hardFloor", out JsonElement floor)
                ? Enum.Parse<ExerciseHardFloorCompatibility>(floor.GetString()!) : ExerciseHardFloorCompatibility.Compatible,
            UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            Mode = ExerciseMode.Repetition, Presentation = ExercisePresentation.Motion, HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous, OnlyFeetTouchGround = true, ShoeAgnostic = true,
            MaxSpaceMeters = 2, Equipment = "None",
            SequenceBlocks = (move.TryGetProperty("blocks", out JsonElement blocks)
                ? blocks.EnumerateArray().Select(value => value.GetInt32()).ToArray() : [id])
                .Select(memberId => new ExerciseSequenceBlock { ExerciseId = memberId, MirrorMedia = false }).ToArray(),
        };
    }
}
