import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  APPROVED_EXERCISE_CORRECTIONS,
  CURRENT_CATALOG_REVISION,
  EXERCISE_INSECT_COMPATIBILITY,
  FULL_SIDE_MOVEMENT_DURATION_MS,
  LAST_CUMULATIVE_CATALOG_REVISION,
  MOVEMENT_DURATION_MS,
  PREPARATION_DURATION_MS,
  RESOLUTIONS,
  REST_DURATION_MS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS,
} from "../workout.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const [
  sessionService,
  workoutState,
  taxonomy,
  movementSchedule,
  mainActivity,
  webApp,
  workoutModule,
  catalogMigrationRules,
  catalogJson,
  workoutModifiers,
  exerciseModel,
] = await Promise.all([
  source("Flux", "Services", "ExerciseSessionService.cs"),
  source("Flux", "Models", "WorkoutState.cs"),
  source("Flux", "Services", "MassGroupingTaxonomy.cs"),
  source("Flux", "Services", "MovementPhaseSchedule.cs"),
  source("Flux", "MainActivity.cs"),
  source("web", "app.js"),
  source("web", "workout.js"),
  source("Flux", "Services", "CatalogMigrationRules.cs"),
  source("Flux", "Assets", "exercises.json"),
  source("Flux", "Models", "WorkoutModifiers.cs"),
  source("Flux", "Models", "Exercise.cs"),
]);
const catalog = JSON.parse(catalogJson);

test("web duration choices match the mobile workout contract", () => {
  assert.deepEqual(
    SUPPORTED_MINUTES,
    integerArray(sessionService, "WorkoutMinutes"),
  );
  assert.deepEqual(
    [...RESOLUTIONS.keys()],
    integerArray(taxonomy, "SupportedMinutes"),
  );
});

test("web and mobile persist keep-first long-workout allocation", () => {
  assert.match(workoutState, /HashSet<int> LastKeptExerciseIds/);
  assert.match(workoutState, /HashSet<string> ActiveExtraSetSelectionGroupIds/);
  assert.match(workoutState, /HashSet<string> ActiveFullSideSelectionGroupIds/);
  assert.match(
    sessionService,
    /OrderByDescending\(group\s*=>[\s\S]*LastKeptExerciseIds\.Contains\(exerciseId\)\)[\s\S]*ThenByDescending\(group\s*=>\s*group\.Order\)/,
  );
  assert.match(
    sessionService,
    /LastKeptExerciseIds\.ExceptWith\(rejectedExerciseIds\);[\s\S]*LastKeptExerciseIds\.UnionWith\(newlyKeptExerciseIds\);/,
  );
});

test("web and mobile carry kept exercises across workout durations", () => {
  assert.match(
    sessionService,
    /StartWorkout\([\s\S]*CarryKeptExercisesForward\([\s\S]*previousWorkoutMinutes,[\s\S]*previousWorkoutModifiers\);[\s\S]*RepairActiveLineup\(state\);/,
  );
  assert.match(
    sessionService,
    /CarryKeptExercisesForward\([\s\S]*LastKeptExerciseIds[\s\S]*IsSelectable\(/,
  );
});

test("web and mobile persist one combined duration and modifier selection context", () => {
  assert.equal(WORKOUT_MODIFIERS.Insect, 1);
  assert.match(workoutModifiers, /Insect\s*=\s*1/);
  assert.match(workoutState, /WorkoutModifiers LastWorkoutModifiers/);
  assert.match(workoutState, /WorkoutModifiers ActiveWorkoutModifiers/);
  assert.match(
    sessionService,
    /StartWorkout\([\s\S]*state\.LastWorkoutModifiers\s*=\s*modifiers;[\s\S]*state\.ActiveWorkoutModifiers\s*=\s*modifiers;/,
  );
  assert.match(
    workoutModule,
    /startWorkout\(minutes, modifiers[\s\S]*this\.state\.lastWorkoutModifiers\s*=\s*modifiers;[\s\S]*this\.state\.activeWorkoutModifiers\s*=\s*modifiers;/,
  );
  assert.match(
    sessionService,
    /ChooseBestCandidate\([\s\S]*IsSelectable\(exercise, group, modifiers\)/,
  );
  assert.match(exerciseModel, /ExerciseInsectCompatibility InsectCompatibility/);
  assert.ok(catalog.every((exercise) =>
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible));
  assert.equal(catalog.filter((exercise) =>
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible).length, 147);
  assert.match(webApp, /session\.startWorkout\(selectedMinutes, selectedModifiers\)/);
});

test("web and mobile preserve deployed keeps by catalog membership", () => {
  assert.doesNotMatch(
    catalogMigrationRules,
    /LastKeptExerciseIds\.RemoveWhere\(invalidatedExerciseIds\.Contains\)/,
  );
  assert.match(
    sessionService,
    /NormalizeKeptExerciseIds\([\s\S]*!_exercisesById\.ContainsKey\(exerciseId\)/,
  );
});

test("web and mobile finalize rejected replacements when Done is acknowledged", () => {
  assert.match(
    sessionService,
    /AcknowledgeCompletion\([\s\S]*state\.CompletionAcknowledged\s*=\s*true;[\s\S]*PrepareNextSession\(state\);/,
  );
  assert.match(
    workoutModule,
    /acknowledgeCompletion\(\)[\s\S]*this\.state\.completionAcknowledged\s*=\s*true;[\s\S]*this\.prepareNextSession\(\);/,
  );
});

test("web movement and rest timing match the mobile workout contract", () => {
  assert.equal(
    MOVEMENT_DURATION_MS / 1000,
    integerConstant(movementSchedule, "TotalDurationSeconds"),
  );
  assert.equal(integerConstant(movementSchedule, "SideDurationSeconds"), 20);
  assert.equal(integerConstant(movementSchedule, "SideChangeDurationSeconds"), 5);
  assert.equal(
    PREPARATION_DURATION_MS / 1000,
    integerConstant(movementSchedule, "PreparationDurationSeconds"),
  );
  assert.equal(
    FULL_SIDE_MOVEMENT_DURATION_MS / 1000,
    integerConstant(movementSchedule, "FullSideTotalDurationSeconds"),
  );
  assert.equal(integerConstant(movementSchedule, "FullSideDurationSeconds"), 45);
  assert.equal(integerConstant(movementSchedule, "FullSideChangeDurationSeconds"), 15);
  assert.equal(
    REST_DURATION_MS / 1000,
    integerConstant(mainActivity, "RestSeconds"),
  );
});

test("web and mobile separate the exercise whistle from the final completion cue", () => {
  const mobileStart = methodBody(mainActivity, "private void StartCountdown()", "private void SkipExercise()");
  const webStart = methodBody(webApp, "function startMovement()", "function setMovementDeadline(");
  assert.doesNotMatch(mobileStart, /PlayWhistleCue/);
  assert.doesNotMatch(webStart, /playSound/);
  assert.match(
    mainActivity,
    /previousPhase == MovementPhase\.Preparation[\s\S]*CueMovementRestart\(\)/,
  );
  assert.match(
    webApp,
    /previousPhase === "Preparation" \|\| phase === "SecondSide"[\s\S]*playSound\("start"\)/,
  );
  assert.match(
    mainActivity,
    /private void CompleteCountdown\(\)[\s\S]*PlayWhistleCue\(_restStartWhistleId\);[\s\S]*BeginRest\(\);/,
  );
  assert.match(
    mainActivity,
    /private void FinalizeCurrentRound\(bool keep\)[\s\S]*if \(_state\.WorkoutCompleted\)[\s\S]*PlayWhistleCue\(_workoutCompleteWhistleId\);[\s\S]*ShowCongratulations\(\);/,
  );
  assert.match(
    webApp,
    /function completeMovement\(\)[\s\S]*playSound\("rest"\);[\s\S]*session\.beginRest/,
  );
  assert.match(
    webApp,
    /function completeRest\(\)[\s\S]*session\.state\.workoutCompleted\)[\s\S]*showCompletion\(true\);/,
  );
});

test("web catalog migration matches the mobile workout contract", () => {
  assert.equal(
    CURRENT_CATALOG_REVISION,
    integerConstant(catalogMigrationRules, "CurrentCatalogRevision"),
  );
  assert.equal(
    LAST_CUMULATIVE_CATALOG_REVISION,
    integerConstant(catalogMigrationRules, "LastCumulativeWorkoutStateRevision"),
  );
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION].map(([revision, exerciseIds]) => [
      revision,
      [...exerciseIds],
    ]),
    scopedCatalogInvalidations(catalogMigrationRules),
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION].map(([revision, exerciseIds]) => [
      revision,
      [...exerciseIds],
    ]),
    scopedCatalogInvalidations(
      catalogMigrationRules,
      "ScopedScoreInvalidationsByRevision =",
    ),
  );
  assert.deepEqual(
    [...APPROVED_EXERCISE_CORRECTIONS],
    approvedExerciseCorrections(catalogMigrationRules),
  );
  assert.deepEqual(
    catalog
      .filter((exercise) => typeof exercise.retiredName === "string" && exercise.retiredName)
      .map((exercise) => exercise.id)
      .sort((left, right) => left - right),
    integerCollection(catalogMigrationRules, "ReplacedExerciseIdSet")
      .sort((left, right) => left - right),
  );
});

async function source(...segments) {
  return readFile(path.join(repositoryRoot, ...segments), "utf8");
}

function methodBody(contents, startMarker, endMarker) {
  const start = contents.indexOf(startMarker);
  const end = contents.indexOf(endMarker, start + startMarker.length);
  assert.ok(start >= 0 && end > start, `Could not isolate ${startMarker}.`);
  return contents.slice(start, end);
}

function integerArray(contents, name) {
  const match = contents.match(
    new RegExp(`${name}\\s*=\\s*Array\\.AsReadOnly\\(\\[([^\\]]+)\\]\\)`, "s"),
  );
  assert.ok(match, `Could not read mobile array ${name}.`);
  return [...match[1].matchAll(/\d+/g)].map((item) => Number(item[0]));
}

function integerConstant(contents, name) {
  const match = contents.match(new RegExp(`const\\s+int\\s+${name}\\s*=\\s*(\\d+)`));
  assert.ok(match, `Could not read mobile constant ${name}.`);
  return Number(match[1]);
}

function integerCollection(contents, name) {
  const match = contents.match(new RegExp(`${name}\\s*=\\s*\\[([^\\]]+)\\]`, "s"));
  assert.ok(match, `Could not read mobile collection ${name}.`);
  return [...match[1].matchAll(/\d+/g)].map((item) => Number(item[0]));
}

function approvedExerciseCorrections(contents) {
  const start = contents.indexOf("ApprovedExerciseCorrections =");
  const end = contents.indexOf("private static readonly", start);
  assert.ok(start >= 0 && end > start, "Could not read mobile approved exercise corrections.");
  return [...contents.slice(start, end).matchAll(
    /\[(\d+)\]\s*=\s*new\(\s*"([^"]+)",\s*"([^"]+)"\s*\)/g,
  )].map((item) => [Number(item[1]), [item[2], item[3]]]);
}

function scopedCatalogInvalidations(
  contents,
  name = "ScopedWorkoutStateInvalidationsByRevision =",
) {
  const start = contents.indexOf(name);
  const end = contents.indexOf("private static readonly", start);
  assert.ok(start >= 0 && end > start, "Could not read mobile scoped catalog invalidations.");
  return [...contents.slice(start, end).matchAll(
    /\[(\d+)\]\s*=\s*new HashSet<int>\s*\{([^}]+)\}/g,
  )].map((item) => [
    Number(item[1]),
    [...item[2].matchAll(/\d+/g)].map((exerciseId) => Number(exerciseId[0])),
  ]);
}
