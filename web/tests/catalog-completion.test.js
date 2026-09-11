import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  CURRENT_CATALOG_REVISION,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  createDefaultState,
  getSelectionKey,
  getSessionMovementId,
  isCompatibleWithWorkoutModifiers,
  isSelectable,
} from "../workout.js";

const catalog = JSON.parse(await readFile(
  new URL("../../Flux/Assets/exercises.json", import.meta.url), "utf8"));

for (const insect of [false, true]) {
  for (const light of [false, true]) {
    for (const shy of [false, true]) {
      test(`short workout keeps broad training: Light=${light}, Shy=${shy}, Insect=${insect}`, () => {
        for (const randomValue of [0.01, 0.2, 0.4]) {
          const state = createDefaultState();
          state.scores[239] = 100;
          const profile = state.lastWorkoutModifiers |
            (light ? WORKOUT_MODIFIERS.Light : WORKOUT_MODIFIERS.None) |
            (shy ? WORKOUT_MODIFIERS.Shy : WORKOUT_MODIFIERS.None) |
            (insect ? WORKOUT_MODIFIERS.Insect : WORKOUT_MODIFIERS.None);
          const session = new WorkoutSession(catalog, state, () => randomValue);
          session.startWorkout(3, profile);

          const rounds = session.getActiveGroups();
          assert.equal(rounds.length, 3);
          assert.equal(new Set(rounds.map((round) =>
            getSessionMovementId(session.getSelectedExercise(round)))).size, 3);
          for (const round of rounds) {
            const selected = session.getSelectedExercise(round);
            assert.equal(isSelectable(selected, round), true);
            assert.equal(isCompatibleWithWorkoutModifiers(selected, profile), true);
            assert.equal(selected.sequenceBlocks.length, 1);
          }
          const upper = rounds.find((round) => getSelectionKey(round) === "r3.head-neck-upper-limbs");
          const selectedUpper = session.getSelectedExercise(upper);
          assert.equal(isSelectable(selectedUpper, upper), true);
          assert.notEqual(selectedUpper.id, 239);
          if (light && !insect) assert.equal(selectedUpper.muscularDemand, 0);
          for (const round of rounds) session.recordOutcome(round, true);
          assert.equal(session.state.workoutCompleted, true);
        }
      });
    }
  }

}

test("compound workout feedback and history survive reload", () => {
  const state = createDefaultState();
  const profile = state.lastWorkoutModifiers | WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Shy;
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  session.startWorkout(3, profile);
  const rounds = session.getActiveGroups();
  const upper = rounds.find((round) => getSelectionKey(round) === "r3.head-neck-upper-limbs");
  assert.equal(session.getSelectedExercise(upper).id, 248);
  for (const round of rounds) {
    session.beginRest(round, Date.now() + 15_000);
    assert.equal(session.keepPendingRest(), true);
    session.recordOutcome(round, true);
    session.clearPendingRest();
  }
  assert.equal(session.state.workoutCompleted, true);
  const saved = JSON.parse(JSON.stringify(session.state));
  const resumed = new WorkoutSession(catalog, JSON.parse(JSON.stringify(saved)), () => 0.5);
  resumed.initialize();
  for (const key of ["workoutHistory", "keptExerciseRootIdsBySelectionGroupId", "scores",
    "lastHardWorkUnixMillisecondsByPrimaryMuscle", "lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle",
    "version", "catalogRevision"])
    assert.deepEqual(resumed.state[key], saved[key], key);
  assert.ok(resumed.state.workoutHistory[0].decisions.some((decision) =>
    decision.selectionGroupId === getSelectionKey(upper) &&
    decision.rootExerciseId === 248 && decision.outcome === "tick"));

  // Done applies the recorded decisions to preferences for the next workout.
  resumed.acknowledgeCompletion();
  assert.ok(resumed.state.keptExerciseRootIdsBySelectionGroupId[getSelectionKey(upper)].includes(248));
  const acknowledged = new WorkoutSession(catalog,
    JSON.parse(JSON.stringify(resumed.state)), () => 0.25);
  acknowledged.initialize();
  assert.deepEqual(acknowledged.state.keptExerciseRootIdsBySelectionGroupId,
    resumed.state.keptExerciseRootIdsBySelectionGroupId);
  for (const key of ["workoutHistory", "scores", "lastHardWorkUnixMillisecondsByPrimaryMuscle",
    "lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle"])
    assert.deepEqual(acknowledged.state[key], saved[key], key);
});

test("planted teacup admission preserves existing catalog feedback", () => {
  const state = createDefaultState();
  state.catalogRevision = 73;
  state.scores = Object.fromEntries(catalog.filter((item) => item.id !== 1027)
    .map((item) => [item.id, item.id % 41 - 20]));
  state.catalogIdentities = Object.fromEntries(catalog.filter((item) => item.id !== 1027)
    .map((item) => [item.id, `${item.name}\u001f${item.video}`]));
  state.keptExerciseRootIdsBySelectionGroupId = { "r30.rotator-cuff": [1026] };
  state.lastKeptExerciseIds = [1026];
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = { RotatorCuff: 123456 };
  const scores = structuredClone(state.scores);
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  for (const [id, score] of Object.entries(scores))
    assert.equal(session.state.scores[id], score, `score for ${id}`);
  assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId,
    { "r30.rotator-cuff": [1026] });
  assert.deepEqual(session.state.lastKeptExerciseIds, [1026]);
  assert.equal(session.state.lastHardWorkUnixMillisecondsByPrimaryMuscle.RotatorCuff, 123456);
  assert.equal(session.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.equal(isCompatibleWithWorkoutModifiers(
    catalog.find((item) => item.id === 1027), WORKOUT_MODIFIERS.Insect), false);
});
