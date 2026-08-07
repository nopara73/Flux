import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  MOVEMENT_DURATION_MS,
  RESOLUTIONS,
  REST_DURATION_MS,
  SUPPORTED_MINUTES,
} from "../workout.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const [sessionService, taxonomy, movementSchedule, mainActivity] = await Promise.all([
  source("Flux", "Services", "ExerciseSessionService.cs"),
  source("Flux", "Services", "MassGroupingTaxonomy.cs"),
  source("Flux", "Services", "MovementPhaseSchedule.cs"),
  source("Flux", "MainActivity.cs"),
]);

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
