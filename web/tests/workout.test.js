import assert from "node:assert/strict";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  APPROVED_EXERCISE_CORRECTIONS,
  CURRENT_CATALOG_REVISION,
  RESOLUTIONS,
  SUPPORTED_MINUTES,
  WorkoutSession,
  createWorkoutSchedule,
  createDefaultState,
  getCanonicalCoverage,
  getExerciseVideoPath,
  getHoldFramePath,
  getMovementPhaseState,
  getMovementPresentation,
  getSelectionKey,
  isSelectable,
  normalizeMinutes,
  parseStoredState,
} from "../workout.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const catalog = JSON.parse(
  await readFile(path.join(repositoryRoot, "Flux", "Assets", "exercises.json"), "utf8"),
);

test("duration inventory and legacy normalization match Flux", () => {
  assert.deepEqual(SUPPORTED_MINUTES, [3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
  assert.equal(normalizeMinutes(6), 7);
  assert.equal(normalizeMinutes(4), 5);
  assert.equal(normalizeMinutes(37), 30);
  assert.equal(normalizeMinutes(38), 45);
  assert.equal(normalizeMinutes(52), 45);
  assert.equal(normalizeMinutes(53), 60);
  assert.equal(normalizeMinutes(75), 90);
  assert.equal(normalizeMinutes(undefined), 10);
});

test("every resolution covers all canonical leaves once in scheduled order", () => {
  for (const [minutes, resolution] of RESOLUTIONS) {
    assert.equal(resolution.groups.length, minutes);
    assert.deepEqual(
      resolution.groups.map((group) => group.order),
      Array.from({ length: minutes }, (_, index) => index + 1),
    );
    const leaves = resolution.groups.flatMap((group) => group.canonicalGroups);
    assert.equal(leaves.length, 30);
    assert.equal(new Set(leaves).size, 30);
  }

  assert.equal(RESOLUTIONS.get(3).groups[0].id, "r3.head-neck-upper-limbs");
  assert.equal(RESOLUTIONS.get(3).groups.at(-1).id, "r3.lower-limbs");
  assert.equal(RESOLUTIONS.get(30).groups[0].id, "r30.pelvic-floor-perineum");
  assert.equal(RESOLUTIONS.get(30).groups.at(-1).id, "r30.medial-deep-knee-extensors");
});

test("the reviewed catalog satisfies every roll-up and selects distinct exercises", () => {
  assert.equal(catalog.length, 328);
  assert.equal(new Set(catalog.map((exercise) => exercise.id)).size, 328);
  assert.equal(new Set(catalog.map((exercise) => exercise.name)).size, 328);

  for (const [minutes, resolution] of RESOLUTIONS) {
    for (const group of resolution.groups) {
      const selectable = catalog.filter((exercise) => isSelectable(exercise, group));
      assert.ok(selectable.length >= 10, `${group.id} has ${selectable.length} choices`);
    }

    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(selected.length, minutes);
    assert.equal(new Set(selected.map((exercise) => exercise.id)).size, minutes);
  }

  for (const minutes of [45, 60, 90]) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(selected.length, minutes);
    assert.equal(new Set(selected.map((exercise) => exercise.id)).size, 30);
  }
});

test("long workouts repeat the thirty-minute lineup with unique round IDs", () => {
  for (const [minutes, firstHalfSets, secondHalfSets] of [
    [45, 1, 2],
    [60, 2, 2],
    [90, 3, 3],
  ]) {
    const rounds = createWorkoutSchedule(minutes);
    const selectionGroups = RESOLUTIONS.get(30).groups;
    assert.equal(rounds.length, minutes);
    assert.deepEqual(
      rounds.map((round) => round.order),
      Array.from({ length: minutes }, (_, index) => index + 1),
    );
    assert.equal(new Set(rounds.map((round) => round.id)).size, minutes);

    for (const [index, selectionGroup] of selectionGroups.entries()) {
      const groupRounds = rounds.filter(
        (round) => getSelectionKey(round) === selectionGroup.id,
      );
      assert.equal(groupRounds.length, index < 15 ? firstHalfSets : secondHalfSets);
      assert.deepEqual(
        groupRounds.map((round) => round.id),
        groupRounds.map((round, setIndex) => `${selectionGroup.id}.set${setIndex + 1}`),
      );
    }
  }
});

test("a rejected repeated round replaces its shared exercise once", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(45);
  const rounds = session.getActiveGroups();
  const target = rounds.find((round) => round.id.endsWith(".set2"));
  const selectionKey = getSelectionKey(target);
  const rejected = session.getSelectedExercise(target);

  for (const round of rounds) {
    session.recordOutcome(round, round.id !== target.id);
  }

  assert.equal(session.getScore(rejected), -1);
  assert.equal(session.state.selectedExerciseIds[selectionKey], rejected.id);
  session.acknowledgeCompletion();
  assert.notEqual(session.state.selectedExerciseIds[selectionKey], rejected.id);
  assert.equal(session.state.activeWorkoutMinutes, 0);
});

test("interrupted long workout settles a pending repeated round exactly once", () => {
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.initialize();
  started.startWorkout(45);
  const rounds = started.getActiveGroups();
  const pendingRound = rounds.find((round) => round.id.endsWith(".set2"));
  const performed = started.getSelectedExercise(pendingRound);
  for (const round of rounds.slice(0, pendingRound.order - 1)) {
    started.recordOutcome(round, true);
  }
  started.beginRest(pendingRound, Date.now() + 15_000);

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();
  assert.equal(restored.getScore(performed), -1);
  assert.equal(restored.state.activeWorkoutMinutes, 0);

  const restoredAgain = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(restored.state)),
    () => 0,
  );
  restoredAgain.initialize();
  assert.equal(restoredAgain.getScore(performed), -1);
});

test("selection requires primary ownership and ranks score before coverage", () => {
  const group = {
    id: "test",
    displayName: "Test",
    order: 1,
    canonicalGroups: ["A", "B"],
  };
  const secondaryOnly = exercise(1, "C", ["A", "B"], 99);
  const highScore = exercise(2, "A", [], 3);
  const broadLowScore = exercise(3, "A", ["B"], 2);
  const broadEqualScore = exercise(4, "A", ["B"], 3);

  assert.equal(isSelectable(secondaryOnly, group), false);
  const session = new WorkoutSession(
    [secondaryOnly, highScore, broadLowScore, broadEqualScore],
    createDefaultState(),
    () => 0,
  );
  assert.equal(session.chooseBestCandidate(group).id, broadEqualScore.id);
  assert.equal(getCanonicalCoverage(broadEqualScore, group), 2);
});

test("movement countdown uses exact continuous and 20/5/20 boundaries", () => {
  assert.deepEqual(getMovementPhaseState(45_000, false), {
    phase: "Continuous",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
  assert.equal(getMovementPhaseState(1_001, false).secondsRemaining, 2);
  assert.deepEqual(getMovementPhaseState(25_000, true), {
    phase: "ChangeSides",
    secondsRemaining: 5,
    segmentDurationSeconds: 5,
    isExercise: false,
  });
  assert.equal(getMovementPhaseState(20_000, true).phase, "SecondSide");
  assert.equal(getMovementPhaseState(19_999, true).secondsRemaining, 20);
  assert.equal(getMovementPhaseState(0, true).phase, "Complete");
});

test("side pairs mirror only phase two and direction pairs never mirror", () => {
  const side = {
    sideSequence: "ScreenRightThenLeft",
    directionSequence: "None",
  };
  const direction = {
    sideSequence: "Continuous",
    directionSequence: "ForwardThenBackward",
  };

  assert.deepEqual(getMovementPresentation(side, "FirstSide"), {
    cue: "ScreenRight",
    mirrorMedia: false,
    activeScreenSide: "Right",
  });
  assert.deepEqual(getMovementPresentation(side, "SecondSide"), {
    cue: "ScreenLeft",
    mirrorMedia: true,
    activeScreenSide: "Left",
  });
  assert.deepEqual(getMovementPresentation(direction, "SecondSide"), {
    cue: "Backward",
    mirrorMedia: false,
    activeScreenSide: null,
  });
});

test("rejection decrements once, purges saved copies, and replaces only rejected slots", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(3);
  const groups = session.getActiveGroups();
  const rejectedGroup = groups[0];
  const rejected = session.getSelectedExercise(rejectedGroup);
  const keptIds = new Map(
    groups.slice(1).map((group) => [group.id, session.getSelectedExercise(group).id]),
  );
  const canonicalGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes(rejected.primaryCanonicalGroup),
  );
  session.state.selectedExerciseIds[canonicalGroup.id] = rejected.id;

  session.recordOutcome(rejectedGroup, false);
  for (const group of groups.slice(1)) {
    session.recordOutcome(group, true);
  }
  assert.equal(session.getScore(rejected), -1);
  session.acknowledgeCompletion();

  assert.equal(Object.values(session.state.selectedExerciseIds).includes(rejected.id), false);
  for (const [groupId, exerciseId] of keptIds) {
    assert.equal(session.state.selectedExerciseIds[groupId], exerciseId);
  }
  assert.equal(session.state.activeWorkoutMinutes, 0);
});

test("interrupted movement is neutral while an unkept pending rest is settled once", () => {
  const neutral = new WorkoutSession(catalog, createDefaultState(), () => 0);
  neutral.startWorkout(3);
  const neutralGroup = neutral.getNextGroup();
  const neutralExercise = neutral.getSelectedExercise(neutralGroup);
  neutral.finishInterruptedWorkout();
  assert.equal(neutral.getScore(neutralExercise), 0);
  assert.equal(neutral.state.activeWorkoutMinutes, 0);

  const rejected = new WorkoutSession(catalog, createDefaultState(), () => 0);
  rejected.startWorkout(3);
  const rejectedGroup = rejected.getNextGroup();
  const rejectedExercise = rejected.getSelectedExercise(rejectedGroup);
  rejected.beginRest(rejectedGroup, Date.now() + 15_000);
  const serialized = JSON.stringify(rejected.state);

  const restored = new WorkoutSession(catalog, parseStoredState(serialized), () => 0);
  restored.initialize();
  assert.equal(restored.getScore(rejectedExercise), -1);
  assert.equal(restored.state.activeWorkoutMinutes, 0);

  const restoredAgain = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(restored.state)),
    () => 0,
  );
  restoredAgain.initialize();
  assert.equal(restoredAgain.getScore(rejectedExercise), -1);
});

test("pending rest survives schedule order and coverage changes for the performed exercise", () => {
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.initialize();
  started.startWorkout(3);
  const pendingGroup = started.getActiveGroups().at(-1);
  const performed = started.getSelectedExercise(pendingGroup);
  started.beginRest(pendingGroup, Date.now() + 15_000);

  const changedCatalog = catalog.map((item) =>
    item.id === performed.id
      ? { ...item, secondaryCanonicalGroups: [] }
      : item,
  );
  assert.equal(isSelectable(changedCatalog.find((item) => item.id === performed.id), pendingGroup), false);

  const restored = new WorkoutSession(
    changedCatalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.getScore(performed), -1);
  assert.equal(restored.state.activeWorkoutMinutes, 0);
});

test("catalog identity replacement clears inherited score and workout references", () => {
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.initialize();
  started.startWorkout(3);
  const group = started.getNextGroup();
  const retired = started.getSelectedExercise(group);
  started.setScore(retired, -4);
  started.beginRest(group, Date.now() + 15_000);

  const changedCatalog = catalog.map((item) =>
    item.id === retired.id
      ? { ...item, name: `${item.name} Reviewed Replacement` }
      : item,
  );
  const restored = new WorkoutSession(
    changedCatalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.state.scores[String(retired.id)], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.activeWorkoutMinutes, 0);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.equal(restored.getScore(changedCatalog.find((item) => item.id === retired.id)), 0);
});

test("approved timed-side name cleanup preserves browser memory", () => {
  const normalized = catalog.find(
    (item) =>
      item.sideSequence !== "Continuous" &&
      !item.name.startsWith("Alternating "),
  );
  const group = RESOLUTIONS.get(30).groups.find((candidate) => isSelectable(normalized, candidate));
  const priorCatalog = catalog.map((item) =>
    item.id === normalized.id
      ? { ...item, name: `Alternating ${item.name}` }
      : item,
  );
  const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
  prior.initialize();
  prior.state.selectedExerciseIds[group.id] = normalized.id;
  prior.setScore(priorCatalog.find((item) => item.id === normalized.id), -3);

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(prior.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.state.selectedExerciseIds[group.id], normalized.id);
  assert.equal(restored.getScore(normalized), -3);
});

test("approved clarity corrections preserve browser memory", () => {
  for (const [exerciseId, [previousName, currentName]] of APPROVED_EXERCISE_CORRECTIONS) {
    const currentCatalog = catalog.map((item) =>
      item.id === exerciseId ? { ...item, name: currentName } : item,
    );
    const priorCatalog = currentCatalog.map((item) =>
      item.id === exerciseId ? { ...item, name: previousName } : item,
    );
    const currentExercise = currentCatalog.find((item) => item.id === exerciseId);
    const group = RESOLUTIONS.get(30).groups.find((candidate) =>
      isSelectable(currentExercise, candidate),
    );
    const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
    prior.initialize();
    prior.state.selectedExerciseIds[group.id] = exerciseId;
    prior.setScore(priorCatalog.find((item) => item.id === exerciseId), -3);

    const restored = new WorkoutSession(
      currentCatalog,
      parseStoredState(JSON.stringify(prior.state)),
      () => 0,
    );
    restored.initialize();

    assert.equal(restored.state.selectedExerciseIds[group.id], exerciseId);
    assert.equal(restored.getScore(currentExercise), -3);
  }
});

test("catalog revision retires replaced Android exercises from browser memory", () => {
  const replacements = catalog.filter((item) =>
    typeof item.retiredName === "string" && item.retiredName,
  );
  const replacement = replacements[0];
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(replacement, candidate),
  );
  const state = createDefaultState();
  state.catalogRevision = CURRENT_CATALOG_REVISION - 1;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds[group.id] = replacement.id;
  for (const item of replacements) {
    state.scores[String(item.id)] = -4;
  }
  state.outcomes[group.id] = "tick";
  state.pendingRestGroupId = group.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 15_000;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
  for (const item of replacements) {
    assert.equal(restored.state.scores[String(item.id)], undefined);
  }
  assert.equal(restored.state.outcomes[group.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("legacy browser state without a catalog revision migrates like Android", () => {
  const restored = parseStoredState(JSON.stringify({ lastWorkoutMinutes: 10 }));
  assert.equal(restored.catalogRevision, 0);
});

test("corrupt local state resets safely", () => {
  assert.deepEqual(parseStoredState("not json"), createDefaultState());
  assert.equal(parseStoredState('{"lastWorkoutMinutes":6}').lastWorkoutMinutes, 7);
});

test("runtime media maps to MP4s and reviewed hold frames, never GIFs", async () => {
  const directionIds = [];
  const holds = [];
  for (const item of catalog) {
    const videoPath = getExerciseVideoPath(item);
    assert.match(videoPath, /\.mp4$/);
    assert.doesNotMatch(videoPath, /exercise_gifs|\.gif$/i);
    await assertFile(path.join(repositoryRoot, "Flux", "Assets", videoPath));

    if (item.directionSequence !== "None") {
      directionIds.push(item.id);
      assert.match(videoPath, /^exercise_direction_videos\//);
    }
    if (item.mode === "Hold") {
      holds.push(item);
      await assertFile(
        path.join(repositoryRoot, "Flux", "Assets", getHoldFramePath(item)),
      );
    }
  }

  assert.deepEqual(directionIds, [264, 406, 409, 497, 588, 608, 611, 743, 816]);
  assert.ok(holds.length > 0);
  assert.ok(holds.some((item) => item.presentation === "Still"));
});

test("initial document contains no exercise-media URL", async () => {
  const html = await readFile(path.join(repositoryRoot, "web", "index.html"), "utf8");
  assert.doesNotMatch(html, /exercise_(?:videos|direction_videos|hold_frames)\//);
  assert.match(html, /src="\.\/app\.js"/);
  assert.doesNotMatch(html, /settings|sign[ -]?in|learn more/i);
  const durationLabels = html.match(/id="duration-labels"[\s\S]+?<\/div>/)?.[0] ?? "";
  assert.deepEqual(
    [...durationLabels.matchAll(/<span>(\d+)<\/span>/g)].map((item) => Number(item[1])),
    SUPPORTED_MINUTES,
  );
  assert.match(html, new RegExp(`max="${SUPPORTED_MINUTES.length - 1}"`));
});

test("browser shell pauses for buffering and keeps desktop layouts bounded", async () => {
  const [javascript, stylesheet] = await Promise.all([
    readFile(path.join(repositoryRoot, "web", "app.js"), "utf8"),
    readFile(path.join(repositoryRoot, "web", "styles.css"), "utf8"),
  ]);
  assert.match(javascript, /\.onwaiting\s*=\s*\(\)\s*=>\s*handleVideoWaiting/);
  assert.match(javascript, /pauseMovement\("buffering"\)/);
  assert.match(javascript, /\.onprogress\s*=/);
  assert.match(javascript, /resumePausedMovementWhenVisible\(\)/);
  assert.match(javascript, /MEDIA_RECOVERY_TIMEOUT_MS/);
  assert.doesNotMatch(javascript, /\.onloadeddata\s*=/);
  assert.match(javascript, /navigator\.wakeLock\?\.request/);
  assert.match(stylesheet, /@media \(orientation: landscape\)\s*\{/);
  assert.doesNotMatch(stylesheet, /@media \(orientation: landscape\) and \(min-width:/);
  assert.match(stylesheet, /@media \(min-width: 1000px\)\s*\{/);
  assert.match(stylesheet, /\.duration-controls\s*\{[\s\S]*?width: min\(100%, clamp\(760px, 58vw, 1120px\)\);/);
  assert.match(stylesheet, /width: clamp\(300px, min\(20vw, 34dvh\), 420px\);/);
  assert.match(stylesheet, /width: clamp\(112px, 8vw, 144px\);/);
  assert.match(stylesheet, /width: min\(calc\(100% - 64px\), 1600px\);/);
  assert.match(stylesheet, /grid-template-columns: minmax\(360px, 500px\) minmax\(520px, 900px\);/);
  assert.match(stylesheet, /grid-column: 2;\s*grid-row: 1 \/ span 2;/);
  assert.match(stylesheet, /width: min\(100%, 78dvh, 900px\);/);
  assert.match(stylesheet, new RegExp(`grid-template-columns: repeat\\(${SUPPORTED_MINUTES.length}, 1fr\\)`));
});

function exercise(id, primaryCanonicalGroup, secondaryCanonicalGroups, score) {
  return {
    id,
    name: `Exercise ${id}`,
    video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
    primaryCanonicalGroup,
    secondaryCanonicalGroups,
    score,
    sideSequence: "Continuous",
    directionSequence: "None",
    mode: "Repetition",
    presentation: "Motion",
  };
}

async function assertFile(file) {
  const information = await stat(file);
  assert.equal(information.isFile(), true);
  assert.ok(information.size > 0);
}
