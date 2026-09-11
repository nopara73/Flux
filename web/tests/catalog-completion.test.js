import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  CURRENT_CATALOG_REVISION,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  createDefaultState,
  getCanonicalCoverage,
  getSelectionKey,
  getSessionMovementId,
  isCompatibleWithWorkoutModifiers,
  isSelectable,
} from "../workout.js";

const catalog = JSON.parse(await readFile(
  new URL("../../Flux/Assets/exercises.json", import.meta.url), "utf8"));

for (const light of [false, true]) {
  for (const shy of [false, true]) {
    test(`short workout keeps broad training: Light=${light}, Shy=${shy}`, () => {
      for (const randomValue of [0.01, 0.2, 0.4]) {
        const state = createDefaultState();
        state.scores[239] = 100;
        const profile = state.lastWorkoutModifiers |
          (light ? WORKOUT_MODIFIERS.Light : WORKOUT_MODIFIERS.None) |
          (shy ? WORKOUT_MODIFIERS.Shy : WORKOUT_MODIFIERS.None);
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
        assert.ok(getCanonicalCoverage(selectedUpper, upper) >= 6);
        assert.notEqual(selectedUpper.id, 239);
        if (light) assert.equal(selectedUpper.muscularDemand, 0);
        for (const round of rounds) session.recordOutcome(round, true);
        assert.equal(session.state.workoutCompleted, true);
      }
    });
  }
}

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
