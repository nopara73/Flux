import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { WorkoutSession, WorkoutUnavailableError, getWorkoutAvailability, getResolution,
  isSelectable, isCompatibleWithWorkoutModifiers, findCatalogContractViolations } from "../workout.js";

const cases = JSON.parse(readFileSync(new URL("../../Flux.Tests/Fixtures/workout-availability-cases.json", import.meta.url)));
function movement(move) {
  return {
    id: move.id, name: `Test ${move.id}`, video: "test.mp4", primaryCanonicalGroup: move.primary,
    secondaryCanonicalGroups: move.secondary ?? [], muscularDemand: move.demand ?? 0, score: move.score ?? 0,
    silent: move.silent ?? true, insectCompatibility: "Compatible", hardFloorCompatibility: move.hardFloor ?? "Compatible",
    upperBodyClothingRequirement: "Agnostic", shyCompatibility: "Compatible", mirrorRelationship: "Agnostic",
    minimumMirrorCoverage: "None", equipment: "None", wallRequired: move.wall ?? false, soleWallContactRequired: false,
    onlyFeetTouchGround: true, shoeAgnostic: true, maxSpaceMeters: 2,
    sideSequence: "Continuous", directionSequence: "None", mode: "Repetition", presentation: "Motion",
    sequenceBlocks: (move.blocks ?? [move.id]).map((exerciseId) => ({exerciseId,
      sideCue: "None", directionCue: "None", mirrorMedia: false, mediaSegment: "Full"})),
  };
}
for (const fixture of cases) test(fixture.name, () => {
  const catalog = fixture.moves.map(movement);
  const availability = getWorkoutAvailability(catalog, fixture.minutes, fixture.profile);
  assert.equal(availability.canStart, fixture.canStart);
  assert.equal(availability.requiresAcceptance, fixture.limited);
  assert.equal(availability.groups.length, fixture.groupCount);
  const session = new WorkoutSession(catalog);
  const initialState = structuredClone(session.state);
  if (fixture.limited || !fixture.canStart) {
    assert.throws(() => session.startWorkout(fixture.minutes, fixture.profile), WorkoutUnavailableError);
    assert.deepEqual(session.state, initialState, "unaccepted scope cannot mutate workout state");
  }
  if (!fixture.canStart) return;
  session.startWorkout(fixture.minutes, fixture.profile, fixture.limited);
  const rounds = session.getActiveGroups();
  assert.equal(rounds.length, fixture.minutes);
  for (const round of rounds) {
    const selected = session.getSelectedExercise(round);
    assert.ok(isCompatibleWithWorkoutModifiers(selected, fixture.profile), selected.name);
    assert.ok(round.canonicalGroups.includes(selected.primaryCanonicalGroup));
  }
  for (const placement of session.getSelectedSequencePlacements()) {
    const scheduled = rounds.filter((round) => round.selectionGroupId === placement.anchor.id || round.id === placement.anchor.id);
    assert.equal(scheduled.length % placement.root.sequenceBlocks.length, 0, "only complete sequences may repeat");
  }
});

test("a shoulder alone qualifies as upper-body work and no secondary claim can change its region", () => {
  const shoulder = movement({id: 1, primary: "ShoulderAbductors", secondary: ["AbdominalWall", "GlutealExtensors"]});
  for (const group of getResolution(3).groups)
    assert.equal(isSelectable(shoulder, group), group.canonicalGroups.includes("ShoulderAbductors"));
});

test("catalog improvements never create a requirement for noisy exercises", () => {
  const catalog = JSON.parse(readFileSync(new URL("../../Flux/Assets/exercises.json", import.meta.url)));
  assert.deepEqual(findCatalogContractViolations(catalog), []);
  const quiet = catalog.find((exercise) => exercise.silent && exercise.sequenceBlocks.length === 1);
  const expanded = [...catalog, ...Array.from({length: 100}, (_, index) => ({...quiet, id: 2000 + index,
    sessionMovementId: 0, sequenceBlocks: [{...quiet.sequenceBlocks[0], exerciseId: 2000 + index}]}))];
  assert.deepEqual(findCatalogContractViolations(expanded), []);
});

test("completed demand-zero primary work rotates an unkept short-workout target", () => {
  const catalog = [movement({id: 1, primary: "ShoulderAbductors"}), movement({id: 2, primary: "ElbowExtensors"}),
    movement({id: 3, primary: "AbdominalWall"}), movement({id: 4, primary: "GlutealExtensors"})];
  const session = new WorkoutSession(catalog);
  session.state.workoutHistory = [{sessionId: 1, status: "Interrupted", workoutMinutes: 3,
    blocks: [{primaryCanonicalGroup: "ShoulderAbductors", muscularDemand: 0, completedAtUnixMilliseconds: 1000}]}];
  session.startWorkout(3, 0);
  const upper = session.getActiveGroups().find((group) => group.canonicalGroups.includes("ShoulderAbductors"));
  assert.equal(session.getSelectedExercise(upper).primaryCanonicalGroup, "ElbowExtensors");
});

test("an impossible old primary placement saves completed work and requires setup review", () => {
  const catalog = [movement({id:1, primary:"ShoulderAbductors"}), movement({id:2, primary:"AbdominalWall"}), movement({id:3, primary:"GlutealExtensors"})];
  const old = new WorkoutSession(catalog);
  old.startWorkout(3, 0);
  const first = old.getNextGroup();
  old.beginRest(first, Date.now() + 15000);
  const sessionId = old.state.activeWorkoutSession.sessionId;
  const blocks = structuredClone(old.state.activeWorkoutSession.blocks);
  old.state.version = 26;
  old.state.selectedExerciseIds[first.selectionGroupId ?? first.id] = first.canonicalGroups.includes("AbdominalWall") ? 1 : 2;
  const restored = new WorkoutSession(catalog, JSON.parse(JSON.stringify(old.state)));
  restored.initialize();
  assert.equal(restored.state.workoutSetupReviewRequired, true);
  assert.equal(restored.state.activeWorkoutMinutes, 0);
  assert.equal(restored.state.workoutHistory[0].sessionId, sessionId);
  assert.equal(restored.state.workoutHistory[0].status, "Interrupted");
  assert.deepEqual(restored.state.workoutHistory[0].blocks, blocks);
});

test("a Light change cannot retain an unfinished demanding sequence", () => {
  const catalog = [movement({id:1, primary:"ShoulderAbductors", demand:2, score:100, blocks:[1,1]}),
    movement({id:2, primary:"AbdominalWall"}), movement({id:3, primary:"GlutealExtensors"}),
    movement({id:4, primary:"ShoulderAbductors"})];
  const session = new WorkoutSession(catalog);
  session.startWorkout(5, 0, true);
  const hard = session.getActiveGroups().find((group) => session.getSelectedExercise(group).id === 1);
  assert.ok(hard);
  while (session.getNextGroup().id !== hard.id) {
    const next = session.getNextGroup();
    if (session.isIntermediateSequenceBlock(next)) session.advanceSequence(next);
    else session.recordOutcome(next, true);
  }
  session.beginRest(hard, Date.now() + 15000);
  const before = structuredClone(session.state);
  assert.throws(() => session.reconfigureActiveWorkout(256, hard.id));
  assert.deepEqual(session.state, before);
});
