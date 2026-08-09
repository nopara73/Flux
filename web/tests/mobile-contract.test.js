import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  APPROVED_EXERCISE_CORRECTIONS,
  CURRENT_CATALOG_REVISION,
  LAST_CUMULATIVE_CATALOG_REVISION,
  MOVEMENT_DURATION_MS,
  RESOLUTIONS,
  REST_DURATION_MS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SUPPORTED_MINUTES,
} from "../workout.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const [
  sessionService,
  workoutState,
  taxonomy,
  movementSchedule,
  mainActivity,
  catalogMigrationRules,
  catalogJson,
] = await Promise.all([
  source("Flux", "Services", "ExerciseSessionService.cs"),
  source("Flux", "Models", "WorkoutState.cs"),
  source("Flux", "Services", "MassGroupingTaxonomy.cs"),
  source("Flux", "Services", "MovementPhaseSchedule.cs"),
  source("Flux", "MainActivity.cs"),
  source("Flux", "Services", "CatalogMigrationRules.cs"),
  source("Flux", "Assets", "exercises.json"),
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

test("web and mobile persist keep-first extra-set scheduling", () => {
  assert.match(workoutState, /HashSet<int> LastKeptExerciseIds/);
  assert.match(workoutState, /HashSet<string> ActiveExtraSetSelectionGroupIds/);
  assert.match(
    sessionService,
    /OrderByDescending\(group\s*=>[\s\S]*LastKeptExerciseIds\.Contains\(exerciseId\)\)[\s\S]*ThenByDescending\(group\s*=>\s*group\.Order\)/,
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
    REST_DURATION_MS / 1000,
    integerConstant(mainActivity, "RestSeconds"),
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

function scopedCatalogInvalidations(contents) {
  const start = contents.indexOf("ScopedWorkoutStateInvalidationsByRevision =");
  const end = contents.indexOf("private static readonly", start);
  assert.ok(start >= 0 && end > start, "Could not read mobile scoped catalog invalidations.");
  return [...contents.slice(start, end).matchAll(
    /\[(\d+)\]\s*=\s*new HashSet<int>\s*\{([^}]+)\}/g,
  )].map((item) => [
    Number(item[1]),
    [...item[2].matchAll(/\d+/g)].map((exerciseId) => Number(exerciseId[0])),
  ]);
}
