import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  WORKOUT_MODIFIER_VALIDATION_PROFILES,
  SUPPORTED_MINUTES,
  WorkoutSession,
  createDefaultState,
  isSelectableForWorkoutProfile,
} from "../workout.js";

const catalog = JSON.parse(readFileSync(
  new URL("../../Flux/Assets/exercises.json", import.meta.url),
  "utf8",
));

export function registerProductionCatalogProfileShard(shardIndex, shardCount) {
  test(`production catalog workout profiles ${shardIndex + 1}/${shardCount}`, () => {
    const profiles = WORKOUT_MODIFIER_VALIDATION_PROFILES.filter(
      (_, profileIndex) => profileIndex % shardCount === shardIndex,
    );
    assert.ok(profiles.length > 0);
    for (const profile of profiles) {
      for (const minutes of SUPPORTED_MINUTES) {
        const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
        session.startWorkout(minutes, profile);
        assert.ok(session.getActiveGroups().every((group) => {
          const selected = session.getSelectedExercise(group);
          return isSelectableForWorkoutProfile(
            session.getSequenceSelectionExerciseForGroup(selected, group),
            group,
            profile,
          );
        }));
      }
    }
  });
}
