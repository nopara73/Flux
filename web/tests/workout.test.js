import assert from "node:assert/strict";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES,
  APPROVED_EXERCISE_CORRECTIONS,
  CURRENT_CATALOG_REVISION,
  EXERCISE_INSECT_COMPATIBILITY,
  EXERCISE_MIRROR_COVERAGE,
  EXERCISE_MIRROR_RELATIONSHIP,
  HARD_MUSCULAR_DEMAND,
  MAXIMUM_MUSCULAR_DEMAND,
  MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MIRROR_CATEGORY,
  MINIMUM_MUSCULAR_DEMAND,
  MUSCLE_SESSION_BUDGET_HALF_UNITS,
  PRIMARY_MUSCLE_LOAD_HALF_UNITS,
  SCORE_HALF_UNITS_PER_VOTE,
  SECONDARY_MUSCLE_LOAD_HALF_UNITS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  RESOLUTIONS,
  MIRROR_EQUIPMENT,
  WORKOUT_MODIFIER_VALIDATION_PROFILES,
  SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  calculateMuscleLoadHalfUnits,
  createWorkoutSchedule,
  createDefaultState,
  findWorkoutModifierMaterialityDeficiencies,
  findWorkoutModifierPairCoverageDeficiencies,
  findMirrorCategoryDeficiencies,
  findWorkoutProfileLineupDeficiencies,
  getCanonicalCoverage,
  getMaximumDistinctLineupSize,
  getMirrorEquipment,
  getExerciseVideoPath,
  getHoldFramePath,
  getMovementCountdownDurationMs,
  getMovementDurationMs,
  getMovementPhaseState,
  getMovementPresentation,
  getAdjustedScoreHalfUnits,
  getMuscleBudgetTemporaryDownvoteHalfUnits,
  getPreviousDayHardKeptExerciseIds,
  getSelectionKey,
  hasReviewedMuscularDemand,
  isValidLocalDateKey,
  isSelectable,
  isSelectableForWorkoutProfile,
  isCompatibleWithWorkoutModifiers,
  isModifierMetadataComplete,
  isMirrorPreferred,
  normalizeWorkoutModifiers,
  normalizeMinutes,
  parseStoredState,
  usesTimedLeadStances,
  usesTimedSides,
  withMirrorEquipment,
} from "../workout.js";

test("muscle budget counts unilateral phases once and repeated rounds again", () => {
  const unilateral = exercise(
    1,
    "HipAbductors",
    ["GlutealExtensors"],
    0,
  );
  unilateral.sideSequence = "ScreenLeftThenRight";

  const oneRound = calculateMuscleLoadHalfUnits([unilateral]);
  const twoRounds = calculateMuscleLoadHalfUnits([unilateral, unilateral]);

  assert.equal(MUSCLE_SESSION_BUDGET_HALF_UNITS, 10);
  assert.equal(PRIMARY_MUSCLE_LOAD_HALF_UNITS, 2);
  assert.equal(SECONDARY_MUSCLE_LOAD_HALF_UNITS, 1);
  assert.equal(oneRound.get("HipAbductors"), 2);
  assert.equal(oneRound.get("GlutealExtensors"), 1);
  assert.equal(twoRounds.get("HipAbductors"), 4);
  assert.equal(twoRounds.get("GlutealExtensors"), 2);
});

test("every overloaded half unit adds one temporary downvote half unit", () => {
  const loadHalfUnits = new Map([
    ["AbdominalWall", 13],
    ["GlutealExtensors", 11],
    ["HipFlexors", 10],
  ]);

  const temporaryDownvoteHalfUnits = getMuscleBudgetTemporaryDownvoteHalfUnits(
    loadHalfUnits,
    ["AbdominalWall", "GlutealExtensors", "HipFlexors", "AbdominalWall"],
  );

  assert.equal(SCORE_HALF_UNITS_PER_VOTE, 2);
  assert.equal(temporaryDownvoteHalfUnits, 4);
  assert.equal(getAdjustedScoreHalfUnits(0, 1), -1);
  assert.equal(getAdjustedScoreHalfUnits(-1, 0), -2);
  assert.equal(getAdjustedScoreHalfUnits(0, temporaryDownvoteHalfUnits), -4);
  assert.equal(getAdjustedScoreHalfUnits(-1, temporaryDownvoteHalfUnits), -6);
});

test("muscle budget prefers a once-downvoted alternative to an overloaded zero", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const overloadedMuscle = groups[0].canonicalGroups[0];
  const targetGroup = groups[10];
  const exercises = groups.map((group, index) => exercise(
    1 + index,
    group.canonicalGroups[0],
    (index >= 1 && index <= 9) || index === 11 ? [overloadedMuscle] : [],
    0,
  ));
  const overloadedZero = exercises[10];
  overloadedZero.secondaryCanonicalGroups = [overloadedMuscle];
  const downvotedOnce = exercise(
    1_001,
    targetGroup.canonicalGroups[0],
    [],
    -1,
  );
  const downvotedTwice = exercise(
    1_002,
    targetGroup.canonicalGroups[0],
    [],
    -2,
  );
  exercises.push(downvotedOnce, downvotedTwice);
  const state = createDefaultState();
  state.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = exercises[index].id;
  }
  const session = new WorkoutSession(exercises, state, () => 0);

  session.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[targetGroup.id], downvotedOnce.id);
  assert.deepEqual(session.state.scores, {});

  const tieState = createDefaultState();
  tieState.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    tieState.selectedExerciseIds[groups[index].id] = exercises[index].id;
  }
  const reducedLoadExercise = exercise(
    exercises[11].id,
    groups[11].canonicalGroups[0],
    [],
    0,
  );
  const tieSession = new WorkoutSession(
    exercises
      .filter((item) =>
        item.id !== reducedLoadExercise.id && item.id !== downvotedTwice.id)
      .concat(reducedLoadExercise),
    tieState,
    () => 0,
  );

  tieSession.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(tieSession.state.selectedExerciseIds[targetGroup.id], overloadedZero.id);
});

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const catalog = JSON.parse(
  await readFile(path.join(repositoryRoot, "Flux", "Assets", "exercises.json"), "utf8"),
);

test("muscular demand is fully reviewed and independent of user scores", () => {
  assert.equal(MINIMUM_MUSCULAR_DEMAND, 0);
  assert.equal(MAXIMUM_MUSCULAR_DEMAND, 2);
  assert.deepEqual(
    [0, 1, 2].map((rating) =>
      catalog.filter((exercise) => exercise.muscularDemand === rating).length),
    [116, 186, 129],
  );
  assert.ok(catalog.every(hasReviewedMuscularDemand));
  assert.ok(catalog.every((exercise) => exercise.score === 0));
  assert.equal(catalog.find((exercise) => exercise.id === 211).muscularDemand, 0);
  assert.equal(catalog.find((exercise) => exercise.id === 264).muscularDemand, 1);
  assert.equal(catalog.find((exercise) => exercise.id === 101).muscularDemand, 2);
  assert.equal(hasReviewedMuscularDemand({ muscularDemand: -1 }), false);
  assert.equal(hasReviewedMuscularDemand({ muscularDemand: 3 }), false);
  assert.equal(hasReviewedMuscularDemand({}), false);

  for (const exercise of catalog.filter((item) => item.directionPartnerExerciseId > 0)) {
    const partner = catalog.find((item) => item.id === exercise.directionPartnerExerciseId);
    assert.equal(exercise.muscularDemand, partner?.muscularDemand);
  }
});

test("previous-day recovery requires keep date and hardness two together", () => {
  const yesterdayHard = exercise(1, "GlutealExtensors", [], 0, undefined, true, 2);
  const yesterdayModerate = exercise(2, "GlutealExtensors", [], 0, undefined, true, 1);
  const hardButNotKept = exercise(3, "GlutealExtensors", [], 0, undefined, true, 2);
  const olderHard = exercise(4, "GlutealExtensors", [], 0, undefined, true, 2);
  const todayHard = exercise(5, "GlutealExtensors", [], 0, undefined, true, 2);

  const excluded = getPreviousDayHardKeptExerciseIds(
    [yesterdayHard, yesterdayModerate, hardButNotKept, olderHard, todayHard],
    new Set([yesterdayHard.id, yesterdayModerate.id, olderHard.id, todayHard.id]),
    {
      [yesterdayHard.id]: "2026-08-19",
      [yesterdayModerate.id]: "2026-08-19",
      [hardButNotKept.id]: "2026-08-19",
      [olderHard.id]: "2026-08-18",
      [todayHard.id]: "2026-08-20",
    },
    "2026-08-20",
  );

  assert.deepEqual([...excluded], [yesterdayHard.id]);
  assert.equal(HARD_MUSCULAR_DEMAND, 2);
  assert.equal(isValidLocalDateKey("2024-02-29"), true);
  assert.equal(isValidLocalDateKey("2026-02-29"), false);
  assert.equal(isValidLocalDateKey("2026-8-20"), false);

  const restored = parseStoredState(JSON.stringify({
    lastKeptExerciseIds: [1, 2],
    lastKeptLocalDateByExerciseId: {
      1: "2026-08-19",
      2: "invalid",
    },
    activeRecoveryExcludedExerciseIds: [1],
  }));
  assert.deepEqual(restored.lastKeptLocalDateByExerciseId, { 1: "2026-08-19" });
  assert.deepEqual(restored.activeRecoveryExcludedExerciseIds, [1]);
});

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

test("pre-silence modifier state migrates to quiet by default", () => {
  const state = parseStoredState(JSON.stringify({
    version: 4,
    lastWorkoutMinutes: 10,
    activeWorkoutMinutes: 0,
  }));
  assert.equal(state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  assert.equal(state.activeWorkoutModifiers, WORKOUT_MODIFIERS.None);
});

test("versionless stored state also migrates to quiet without losing its lineup", () => {
  const state = parseStoredState(JSON.stringify({
    lastWorkoutMinutes: 3,
    activeWorkoutMinutes: 0,
    selectedExerciseIds: { "r3.lower-limbs": 101 },
  }));

  assert.equal(state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  assert.equal(state.selectedExerciseIds["r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p2|r3.lower-limbs"], 101);
});

test("fresh workouts use silence unless the caller explicitly relaxes it", () => {
  const session = new WorkoutSession(reviewedInsectCatalog(), createDefaultState(), () => 0);

  session.startWorkout(3);

  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  for (const group of session.getActiveGroups()) {
    assert.ok(session.state.selectedExerciseIds[`p2|${getSelectionKey(group)}`]);
  }
});

test("version four active workout migrates to silence without losing progress", () => {
  const exercises = reviewedInsectCatalog();
  const groups = RESOLUTIONS.get(3).groups;
  const selectedExerciseIds = {};
  for (const group of groups) {
    const selected = exercises.find((item) =>
      isSelectableForWorkoutProfile(item, group, WORKOUT_MODIFIERS.Insect));
    selectedExerciseIds[`p1|${getSelectionKey(group)}`] = selected.id;
  }
  const pendingEnd = Date.now() + 60_000;
  const state = parseStoredState(JSON.stringify({
    version: 4,
    lastWorkoutMinutes: 3,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.Insect,
    activeWorkoutMinutes: 3,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.Insect,
    selectedExerciseIds,
    outcomes: { [groups[0].id]: "tick" },
    pendingRestGroupId: groups[1].id,
    pendingRestEndsAtUnixMilliseconds: pendingEnd,
    pendingRestKept: true,
  }));
  const session = new WorkoutSession(exercises, state, () => 0);

  assert.equal(
    session.state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(
    session.state.activeWorkoutModifiers,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(session.state.outcomes[groups[0].id], "tick");
  assert.equal(session.state.pendingRestGroupId, groups[1].id);
  assert.equal(session.state.pendingRestKept, true);
  for (const group of groups) {
    assert.ok(session.state.selectedExerciseIds[`p1|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p3|${getSelectionKey(group)}`]);
  }
});

test("current pre-direction state keeps an explicitly relaxed silence modifier", () => {
  const state = parseStoredState(JSON.stringify({
    version: 5,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.None,
    activeWorkoutMinutes: 0,
  }));

  assert.equal(state.version, 10);
  assert.equal(state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
});

test("binary mirror state does not guess mirror height during migration", () => {
  const state = parseStoredState(JSON.stringify({
    version: 8,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Mirror,
    activeWorkoutMinutes: 0,
    selectedExerciseIds: {
      "r3.lower-limbs": 101,
      "p4|r3.lower-limbs": 102,
      "p5|r3.lower-limbs": 103,
    },
  }));

  assert.equal(state.version, 10);
  assert.equal(state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Insect);
  assert.equal(getMirrorEquipment(state.lastWorkoutModifiers), MIRROR_EQUIPMENT.None);
  assert.equal(state.selectedExerciseIds["r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p4|r3.lower-limbs"], undefined);
  assert.equal(state.selectedExerciseIds["p5|r3.lower-limbs"], undefined);
});

test("unreviewed catalog cannot silently treat an enabled modifier as off", () => {
  const exercises = RESOLUTIONS.get(3).groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  assert.equal(isModifierMetadataComplete(exercises), false);
  assert.throws(
    () => session.startWorkout(3, WORKOUT_MODIFIERS.Insect),
    /No distinct exercise lineup/,
  );
});

test("neutral profile includes both compatible and explicitly excluded exercises", () => {
  const compatible = exercise(
    1,
    RESOLUTIONS.get(30).groups[0].canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const excluded = exercise(
    2,
    RESOLUTIONS.get(30).groups[0].canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Incompatible,
  );

  assert.equal(
    isCompatibleWithWorkoutModifiers(compatible, WORKOUT_MODIFIERS.None),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(excluded, WORKOUT_MODIFIERS.None),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(compatible, WORKOUT_MODIFIERS.Insect),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(excluded, WORKOUT_MODIFIERS.Insect),
    false,
  );
});

test("silence and insect compose as independent positive requirements", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const quietBug = exercise(
    1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const noisyBug = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    silent: false,
  };
  const quietNoBug = exercise(
    3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible,
  );
  const noisyNoBug = {
    ...exercise(4, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible),
    silent: false,
  };
  const candidates = [quietBug, noisyBug, quietNoBug, noisyNoBug];
  const idsFor = (profile) => candidates
    .filter((item) => isCompatibleWithWorkoutModifiers(item, profile))
    .map((item) => item.id);

  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.None), [1, 2, 3, 4]);
  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.Insect), [1, 2]);
  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.Silence), [1, 3]);
  assert.deepEqual(
    idsFor(WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence),
    [1],
  );
});

test("compact and tall mirrors apply coverage without filtering ordinary exercises", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const mirrorOnly = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
    equipment: "Mirror",
  };
  const fullBodyMirrorOnly = {
    ...mirrorOnly,
    id: 2,
    name: "Exercise 2",
    video: "exercise_videos/exercise_0002.mp4",
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const benefitsGreatly = {
    ...exercise(3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
  };
  const fullBodyBenefitsGreatly = {
    ...exercise(4, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const agnostic = exercise(
    5,
    primary,
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );

  assert.equal(isCompatibleWithWorkoutModifiers(mirrorOnly, WORKOUT_MODIFIERS.None), false);
  assert.equal(isCompatibleWithWorkoutModifiers(benefitsGreatly, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(agnostic, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    fullBodyMirrorOnly, WORKOUT_MODIFIERS.Mirror), false);
  assert.ok([mirrorOnly, benefitsGreatly, fullBodyBenefitsGreatly, agnostic].every((item) =>
    isCompatibleWithWorkoutModifiers(item, WORKOUT_MODIFIERS.Mirror)));
  const tallMirror = withMirrorEquipment(
    WORKOUT_MODIFIERS.None,
    MIRROR_EQUIPMENT.Tall,
  );
  assert.ok([
    mirrorOnly,
    fullBodyMirrorOnly,
    benefitsGreatly,
    fullBodyBenefitsGreatly,
    agnostic,
  ].every((item) => isCompatibleWithWorkoutModifiers(item, tallMirror)));
  assert.equal(isMirrorPreferred(mirrorOnly, WORKOUT_MODIFIERS.Mirror), true);
  assert.equal(isMirrorPreferred(benefitsGreatly, WORKOUT_MODIFIERS.Mirror), true);
  assert.equal(isMirrorPreferred(fullBodyBenefitsGreatly, WORKOUT_MODIFIERS.Mirror), false);
  assert.equal(isMirrorPreferred(fullBodyBenefitsGreatly, tallMirror), true);
  assert.equal(isMirrorPreferred(agnostic, WORKOUT_MODIFIERS.Mirror), false);
});

test("mirror equipment round-trips and discards an orphan tall qualifier", () => {
  const context = WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence;
  assert.equal(getMirrorEquipment(WORKOUT_MODIFIERS.TallMirror), MIRROR_EQUIPMENT.None);
  assert.equal(normalizeWorkoutModifiers(WORKOUT_MODIFIERS.TallMirror), WORKOUT_MODIFIERS.None);

  const compact = withMirrorEquipment(context, MIRROR_EQUIPMENT.Compact);
  assert.equal(getMirrorEquipment(compact), MIRROR_EQUIPMENT.Compact);
  assert.equal(compact, context | WORKOUT_MODIFIERS.Mirror);

  const tall = withMirrorEquipment(compact, MIRROR_EQUIPMENT.Tall);
  assert.equal(getMirrorEquipment(tall), MIRROR_EQUIPMENT.Tall);
  assert.equal(tall, context | WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror);
  assert.equal(withMirrorEquipment(tall, MIRROR_EQUIPMENT.None), context);
});

test("mirror category floor requires five in every relationship and coverage cell", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const categories = [
    [EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly, EXERCISE_MIRROR_COVERAGE.UpperBody],
    [EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly, EXERCISE_MIRROR_COVERAGE.FullBody],
    [EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
      EXERCISE_MIRROR_COVERAGE.UpperBody],
    [EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
      EXERCISE_MIRROR_COVERAGE.FullBody],
    [EXERCISE_MIRROR_RELATIONSHIP.Agnostic, EXERCISE_MIRROR_COVERAGE.None],
  ];
  let nextId = 1;
  const exercises = categories.flatMap(([relationship, coverage]) =>
    Array.from({ length: MINIMUM_EXERCISES_PER_MIRROR_CATEGORY }, () => exercise(
      nextId++,
      primary,
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
      0,
      relationship,
      coverage,
    )));

  assert.deepEqual(findMirrorCategoryDeficiencies(exercises), []);
  exercises.splice(exercises.findIndex((candidate) =>
    candidate.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    candidate.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody), 1);
  assert.deepEqual(findMirrorCategoryDeficiencies(exercises), [{
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
    matchingExerciseCount: 4,
    requiredExerciseCount: MINIMUM_EXERCISES_PER_MIRROR_CATEGORY,
  }]);
});

test("mirror metadata is complete only when relationship matches equipment", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const agnostic = exercise(
    1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const benefitsGreatly = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const mirrorOnly = {
    ...exercise(3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
    equipment: "Mirror",
  };

  assert.equal(isModifierMetadataComplete([agnostic, benefitsGreatly, mirrorOnly]), true);
  assert.equal(isModifierMetadataComplete([{
    ...mirrorOnly,
    equipment: "None",
  }]), false);
  assert.equal(isModifierMetadataComplete([{
    ...benefitsGreatly,
    equipment: "Mirror",
  }]), false);
  assert.equal(isModifierMetadataComplete([{
    ...agnostic,
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.Unreviewed,
  }]), false);
});

test("mirror relevance breaks score ties but never overrides a real vote", () => {
  const group = RESOLUTIONS.get(30).groups[0];
  const agnostic = exercise(
    1,
    group.canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const benefitsGreatly = {
    ...exercise(
      2,
      group.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
  };
  const tied = new WorkoutSession([agnostic, benefitsGreatly], createDefaultState(), () => 0);
  assert.equal(tied.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Mirror).id, 2);

  agnostic.score = 1;
  const voted = new WorkoutSession([agnostic, benefitsGreatly], createDefaultState(), () => 0);
  assert.equal(voted.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Mirror).id, 1);
});

test("validation profiles remain pairwise with compact and tall mirror states", () => {
  assert.equal(WORKOUT_MODIFIER_VALIDATION_PROFILES.length, 10);
  assert.equal(
    new Set(WORKOUT_MODIFIER_VALIDATION_PROFILES).size,
    WORKOUT_MODIFIER_VALIDATION_PROFILES.length,
  );
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.None));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.Insect));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.Mirror));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(
    WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Mirror |
      WORKOUT_MODIFIERS.TallMirror,
  ));
});

test("insect selection is composed with score and coverage instead of post-filtered", () => {
  const exercises = reviewedInsectCatalog();

  const normal = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
  );
  normal.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.ok(normal.getActiveGroups().every((group) =>
    normal.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Incompatible));

  const insect = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
  );
  insect.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  assert.equal(isModifierMetadataComplete(exercises), true);
  assert.ok(insect.getActiveGroups().every((group) =>
    insect.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));
});

test("fully reviewed catalog always honors an enabled modifier", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(
      index + 1,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    exercise(
      101 + index,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      100,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    ),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);

  assert.ok(session.getActiveGroups().every((group) =>
    session.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));
});

test("modifier profiles share keeps without forgetting excluded exercises", () => {
  const session = new WorkoutSession(
    reviewedInsectCatalog(),
    createDefaultState(),
    () => 0,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const keptIds = session.getActiveGroups().map((group) =>
    session.getSelectedExercise(group).id);
  for (const group of session.getActiveGroups()) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);

  assert.deepEqual([...session.state.lastKeptExerciseIds].sort(), [...keptIds].sort());
  assert.ok(session.getActiveGroups().every((group) =>
    session.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));

  session.finishInterruptedWorkout();
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.deepEqual(
    session.getActiveGroups().map((group) => session.getSelectedExercise(group).id).sort(),
    [...keptIds].sort(),
  );
});

test("neutral modifier profile does not reselect a rejected exercise", () => {
  const exercises = RESOLUTIONS.get(3).groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 7,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 5,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  const groups = session.getActiveGroups();
  const rejectedId = session.getSelectedExercise(groups[0]).id;

  session.recordOutcome(groups[0], false);
  for (const group of groups.slice(1)) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();

  assert.equal(
    Object.values(session.state.selectedExerciseIds).includes(rejectedId),
    false,
  );
  assert.deepEqual(session.state.nextWorkoutExcludedExerciseIds, [rejectedId]);
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  assert.deepEqual(session.state.nextWorkoutExcludedExerciseIds, []);
  assert.notEqual(session.getSelectedExercise(session.getActiveGroups()[0]).id, rejectedId);
});

test("insect profile carries keeps into a long workout", () => {
  const session = new WorkoutSession(
    reviewedInsectCatalog(),
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  const keptExerciseIds = session.getActiveGroups().map((group) =>
    session.getSelectedExercise(group).id);
  for (const group of session.getActiveGroups()) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();

  session.startWorkout(45, WORKOUT_MODIFIERS.Insect);

  const keptGroups = session.getSelectionGroups().filter((group) =>
    keptExerciseIds.includes(session.getSelectedExercise(group).id));
  assert.equal(keptGroups.length, keptExerciseIds.length);
  assert.ok(keptGroups.every((group) =>
    session.state.selectedExerciseIds[`p1|${group.id}`] !== undefined));
});

test("reviewed production catalog satisfies every muscle and modifier combination", () => {
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly).length, 58);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic).length, 363);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly).length, 10);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody).length, 5);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody).length, 5);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody).length, 22);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody).length, 36);
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody)
    .map((exercise) => exercise.id)), new Set([515, 520, 521, 522, 523]));
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody)
    .map((exercise) => exercise.id)), new Set([524, 525, 526, 527, 528]));
  assert.equal(catalog.some((exercise) => exercise.id === 90), false);
  assert.equal(catalog.some((exercise) => exercise.name.startsWith("Mirror-Guided ")), false);
  for (const exerciseId of [94, 95, 99, 100, 497, 498, 500, 511, 514]) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.equal(exercise.mirrorRelationship, EXERCISE_MIRROR_RELATIONSHIP.Agnostic);
    assert.equal(exercise.equipment, "None");
  }
  assert.equal(isModifierMetadataComplete(catalog), true);
  assert.deepEqual(findMirrorCategoryDeficiencies(catalog), []);
  assert.deepEqual(findWorkoutModifierPairCoverageDeficiencies(catalog), []);
  assert.deepEqual(findWorkoutModifierMaterialityDeficiencies(catalog), []);
  assert.deepEqual(findWorkoutProfileLineupDeficiencies(catalog), []);
  for (const profile of WORKOUT_MODIFIER_VALIDATION_PROFILES) {
    for (const minutes of SUPPORTED_MINUTES) {
      const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
      session.startWorkout(minutes, profile);
      assert.ok(session.getActiveGroups().every((group) =>
        isSelectableForWorkoutProfile(
          session.getSelectedExercise(group),
          group,
          profile,
        )));
      if (minutes <= 30) {
        assert.ok(session.getActiveGroups().every((group) =>
          session.getSelectedExercise(group).directionPartnerExerciseId === 0));
      }
    }
  }
  const allModifiers = WORKOUT_MODIFIERS.Insect |
    WORKOUT_MODIFIERS.Silence |
    WORKOUT_MODIFIERS.Mirror;
  for (const minutes of SUPPORTED_MINUTES) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, allModifiers);
    assert.equal(session.state.activeWorkoutModifiers, allModifiers);
    assert.ok(session.getActiveGroups().every((group) =>
      isSelectableForWorkoutProfile(
        session.getSelectedExercise(group),
        group,
        allModifiers,
      )));
  }
});

test("pairwise floor counts the four relaxed UI toggle states", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const targetGroup = groups[0];
  const exercises = [
    ...Array.from({ length: 4 }, (_, index) => exercise(
      index + 1,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    exercise(5, targetGroup.canonicalGroups[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, false),
    exercise(6, targetGroup.canonicalGroups[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible, true),
    exercise(7, targetGroup.canonicalGroups[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible, false),
  ];

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(exercises)
    .filter((result) =>
      result.minutes === 30 &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence);

  assert.equal(deficiencies.length, 1);
  assert.equal(deficiencies[0].firstModifier, WORKOUT_MODIFIERS.Insect);
  assert.equal(deficiencies[0].firstModifierEnabled, true);
  assert.equal(deficiencies[0].secondModifier, WORKOUT_MODIFIERS.Silence);
  assert.equal(deficiencies[0].secondModifierEnabled, true);
  assert.equal(deficiencies[0].matchingExerciseCount, 4);
  assert.equal(
    deficiencies[0].requiredExerciseCount,
    MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
  );

  exercises.push(exercise(
    8,
    targetGroup.canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
  ));
  assert.deepEqual(
    findWorkoutModifierPairCoverageDeficiencies(exercises).filter((result) =>
      result.minutes === 30 && result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence),
    [],
  );
});

test("mirror-on pairwise floors require mirror-relevant relationships", () => {
  const targetGroup = RESOLUTIONS.get(30).groups[0];
  const agnosticExercises = Array.from({ length: 5 }, (_, index) => ({
    ...exercise(
      index + 1,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
  }));

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(
    agnosticExercises,
  )
    .filter((result) =>
      result.minutes === 30 &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Mirror &&
      result.secondModifierEnabled);

  assert.equal(deficiencies.length, 4);
  assert.ok(deficiencies.every((result) => result.matchingExerciseCount === 0));

  assert.deepEqual(
    findWorkoutModifierPairCoverageDeficiencies(agnosticExercises)
      .filter((result) =>
        result.minutes === 30 &&
        result.groupId === targetGroup.id &&
        result.firstModifier === WORKOUT_MODIFIERS.Insect &&
        result.secondModifier === WORKOUT_MODIFIERS.Mirror &&
        !result.secondModifierEnabled),
    [],
  );

  const greatlyBenefitedExercises = Array.from({ length: 5 }, (_, index) => ({
    ...exercise(
      6 + index,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
  }));
  assert.deepEqual(
    findWorkoutModifierPairCoverageDeficiencies([
      ...agnosticExercises,
      ...greatlyBenefitedExercises,
    ]).filter((result) =>
      result.minutes === 30 &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Mirror),
    [],
  );
});

test("pairwise floor never counts unreviewed modifier metadata", () => {
  const targetGroup = RESOLUTIONS.get(30).groups[0];
  const exercises = [
    ...Array.from({ length: 4 }, (_, index) => exercise(
      index + 1,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    exercise(
      5,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
      true,
    ),
  ];

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(exercises)
    .filter((result) =>
      result.minutes === 30 && result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence);

  assert.equal(deficiencies.length, 4);
  assert.ok(deficiencies.every((deficiency) =>
    deficiency.matchingExerciseCount === 4));
});

test("modifier materiality rejects token and pairwise-redundant filters", () => {
  const canonicalGroups = RESOLUTIONS.get(30).groups.map(
    (group) => group.canonicalGroups[0],
  );
  const coversEveryGroup = (id, insectCompatibility, silent) => exercise(
    id,
    canonicalGroups[0],
    canonicalGroups.slice(1),
    0,
    insectCompatibility,
    silent,
  );
  const tokenCatalog = Array.from({ length: 20 }, (_, index) =>
    coversEveryGroup(
      index + 1,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    ));
  const tokenDeficiencies = findWorkoutModifierMaterialityDeficiencies(tokenCatalog);
  assert.equal(tokenDeficiencies.length, 14);
  assert.ok(tokenDeficiencies.every((deficiency) =>
    deficiency.materialExerciseCount === 0));

  const pairwiseRedundantCatalog = [
    ...Array.from({ length: 20 }, (_, index) => coversEveryGroup(
      index + 1,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    ...Array.from({ length: 10 }, (_, index) => coversEveryGroup(
      101 + index,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
      false,
    )),
  ];
  const pairwiseDeficiencies = findWorkoutModifierMaterialityDeficiencies(
    pairwiseRedundantCatalog,
  );
  assert.ok(pairwiseDeficiencies.some((deficiency) =>
    deficiency.baseProfile === WORKOUT_MODIFIERS.Silence &&
    deficiency.enabledModifier === WORKOUT_MODIFIERS.Insect &&
    deficiency.materialExerciseCount === 0));
  assert.ok(pairwiseDeficiencies.some((deficiency) =>
    deficiency.baseProfile === WORKOUT_MODIFIERS.Insect &&
    deficiency.enabledModifier === WORKOUT_MODIFIERS.Silence &&
    deficiency.materialExerciseCount === 0));
});

test("modifier materiality never credits unreviewed metadata", () => {
  const canonicalGroups = RESOLUTIONS.get(30).groups
    .slice(0, 3)
    .map((group) => group.canonicalGroups[0]);
  const exercises = [
    ...Array.from({ length: 5 }, (_, index) => exercise(
      index + 1,
      canonicalGroups[index % canonicalGroups.length],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    ...Array.from({ length: 5 }, (_, index) => exercise(
      index + 6,
      canonicalGroups[index % canonicalGroups.length],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
      true,
    )),
  ];

  const deficiency = findWorkoutModifierMaterialityDeficiencies(exercises)
    .find((result) =>
      result.baseProfile === WORKOUT_MODIFIERS.None &&
      result.enabledModifier === WORKOUT_MODIFIERS.Insect);

  assert.equal(deficiency.materialExerciseCount, 0);
  assert.equal(deficiency.affectedGroupCount, 0);
});

test("distinct-lineup matching reroutes shared exercises instead of using greedy counts", () => {
  const groups = [
    { id: "a", displayName: "A", canonicalGroups: ["A"] },
    { id: "b", displayName: "B", canonicalGroups: ["B"] },
    { id: "c", displayName: "C", canonicalGroups: ["C"] },
  ];
  const exercises = [
    exercise(1, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2, "A", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3, "B", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ];

  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.Insect),
    3,
  );
});

test("distinct-lineup matching detects a Hall deficit after modifier filtering", () => {
  const groups = [
    { id: "a", displayName: "A", canonicalGroups: ["A"] },
    { id: "b", displayName: "B", canonicalGroups: ["B"] },
    { id: "c", displayName: "C", canonicalGroups: ["C"] },
  ];
  const exercises = [
    exercise(1, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3, "C", [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible),
  ];

  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.None),
    3,
  );
  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.Insect),
    2,
  );
});

test("lineup repair reroutes a saved exercise when preserving it would dead-end", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const shared = exercise(
    1,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstOnly = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const lastOnly = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds = {
    [groups[0].id]: shared.id,
    [groups[2].id]: lastOnly.id,
  };
  const session = new WorkoutSession([shared, firstOnly, lastOnly], state, () => 0);

  session.repairActiveLineup();

  assert.equal(session.state.selectedExerciseIds[groups[0].id], firstOnly.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], shared.id);
  assert.equal(session.state.selectedExerciseIds[groups[2].id], lastOnly.id);
});

test("carrying keeps maximizes kept count across the whole lineup", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const sharedKept = exercise(
    1,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstOnlyKept = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const lastOnly = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.lastKeptExerciseIds = [sharedKept.id, firstOnlyKept.id];
  state.selectedExerciseIds = { [groups[0].id]: sharedKept.id };
  const session = new WorkoutSession(
    [sharedKept, firstOnlyKept, lastOnly],
    state,
    () => 0,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  const selectedIds = groups.map((group) =>
    session.state.selectedExerciseIds[group.id]);
  assert.ok(selectedIds.includes(sharedKept.id));
  assert.ok(selectedIds.includes(firstOnlyKept.id));
  assert.equal(session.state.selectedExerciseIds[groups[0].id], firstOnlyKept.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], sharedKept.id);
});

test("eligible hardness-two keep outranks a lower-demand keep for one slot", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const hardKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    -10,
    undefined,
    true,
    2,
  );
  const lowerDemandKeep = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    100,
    undefined,
    true,
    1,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.lastKeptExerciseIds = [hardKeep.id, lowerDemandKeep.id];
  state.lastKeptLocalDateByExerciseId = {
    [hardKeep.id]: "2026-08-20",
    [lowerDemandKeep.id]: "2026-08-20",
  };
  state.selectedExerciseIds = { [groups[0].id]: lowerDemandKeep.id };
  const session = new WorkoutSession(
    [hardKeep, lowerDemandKeep, middle, last],
    state,
    () => 0,
    () => "2026-08-20",
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], hardKeep.id);
  assert.deepEqual(session.state.activeRecoveryExcludedExerciseIds, []);
});

test("only previous-day hardness-two keeps are excluded from the lineup", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const yesterdayHard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    100,
    undefined,
    true,
    2,
  );
  const yesterdayModerate = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const olderHard = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.lastKeptExerciseIds = [
    yesterdayHard.id,
    yesterdayModerate.id,
    olderHard.id,
  ];
  state.lastKeptLocalDateByExerciseId = {
    [yesterdayHard.id]: "2026-08-19",
    [yesterdayModerate.id]: "2026-08-19",
    [olderHard.id]: "2026-08-18",
  };
  state.selectedExerciseIds = {
    [groups[0].id]: yesterdayHard.id,
    [groups[1].id]: olderHard.id,
  };
  const session = new WorkoutSession(
    [yesterdayHard, yesterdayModerate, olderHard, last],
    state,
    () => 0,
    () => "2026-08-20",
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.deepEqual(session.state.activeRecoveryExcludedExerciseIds, [yesterdayHard.id]);
  assert.equal(session.state.selectedExerciseIds[groups[0].id], yesterdayModerate.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], olderHard.id);
});

test("completed keeps persist their local date and rest the next day", () => {
  let localDate = "2026-08-20";
  const groups = RESOLUTIONS.get(3).groups;
  const hardKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const alternative = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastKeptExerciseIds = [hardKeep.id];
  const session = new WorkoutSession(
    [hardKeep, alternative, middle, last],
    state,
    () => 0,
    () => localDate,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  for (const round of session.getActiveGroups()) {
    session.recordOutcome(round, true);
  }
  session.acknowledgeCompletion();

  assert.equal(
    session.state.lastKeptLocalDateByExerciseId[String(hardKeep.id)],
    "2026-08-20",
  );

  localDate = "2026-08-21";
  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.ok(session.state.activeRecoveryExcludedExerciseIds.includes(hardKeep.id));
  assert.equal(session.state.selectedExerciseIds[groups[0].id], alternative.id);
});

test("rejected replacements use global matching instead of greedy group order", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const currentFirst = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    10,
  );
  const currentMiddle = exercise(
    2,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    10,
  );
  const currentLast = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    10,
  );
  const sharedReplacement = exercise(
    4,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstOnlyReplacement = exercise(
    5,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    5,
  );
  const state = createDefaultState();
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds = {
    [groups[0].id]: currentFirst.id,
    [groups[1].id]: currentMiddle.id,
    [groups[2].id]: currentLast.id,
  };
  const session = new WorkoutSession(
    [
      currentFirst,
      currentMiddle,
      currentLast,
      sharedReplacement,
      firstOnlyReplacement,
    ],
    state,
    () => 0,
  );
  session.repairActiveLineup();

  const activeGroups = session.getActiveGroups();
  session.recordOutcome(activeGroups[0], false);
  session.recordOutcome(activeGroups[1], false);
  session.recordOutcome(activeGroups[2], true);
  session.acknowledgeCompletion();

  assert.equal(
    session.state.selectedExerciseIds[groups[0].id],
    firstOnlyReplacement.id,
  );
  assert.equal(
    session.state.selectedExerciseIds[groups[1].id],
    sharedReplacement.id,
  );
  assert.equal(session.state.selectedExerciseIds[groups[2].id], currentLast.id);
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
  assert.equal(catalog.length, 431);
  assert.equal(new Set(catalog.map((exercise) => exercise.id)).size, 431);
  assert.equal(new Set(catalog.map((exercise) => exercise.name)).size, 431);
  const breathingExercises = catalog.filter(
    (exercise) => exercise.primaryCanonicalGroup === "BreathingMuscles",
  );
  assert.equal(breathingExercises.length, 15);
  for (const exercise of breathingExercises) {
    assert.match(exercise.name, /\b(?:inhale|exhale|breath|laugh|laughter)/i);
  }
  const overheadBreathingFlow = catalog.find((exercise) => exercise.id === 395);
  assert.equal(
    overheadBreathingFlow.name,
    "Single-Side Inhale Reach Up, Exhale Knee Lift",
  );
  assert.equal(overheadBreathingFlow.mode, "Repetition");
  assert.equal(overheadBreathingFlow.presentation, "Motion");
  const standingKneeExtensionHold = catalog.find((exercise) => exercise.id === 145);
  assert.equal(standingKneeExtensionHold.name, "Standing Knee-Extension Hold");
  assert.equal(standingKneeExtensionHold.mode, "Hold");
  assert.equal(standingKneeExtensionHold.presentation, "Still");
  assert.equal(standingKneeExtensionHold.holdFramePercent, 90);
  assert.equal(standingKneeExtensionHold.sideSequence, "ScreenRightThenLeft");
  const forwardSideLegCircles = catalog.find((exercise) => exercise.id === 617);
  const backwardSideLegCircles = catalog.find((exercise) => exercise.id === 620);
  assert.equal(forwardSideLegCircles.name, "Standing Forward Side-Leg Circles");
  assert.equal(backwardSideLegCircles.name, "Standing Backward Side-Leg Circles");
  assert.equal(backwardSideLegCircles.sideSequence, "ScreenLeftThenRight");
  assert.equal(backwardSideLegCircles.primaryCanonicalGroup, "HipAbductors");
  assert.notEqual(forwardSideLegCircles.video, backwardSideLegCircles.video);

  for (const exerciseId of [395, 507, 577, 618, 654, 834, 915]) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.match(exercise.name, /^Single-Side /);
    assert.equal(usesTimedSides(exercise), true);
  }
  const alternatingHighKneePull = catalog.find((exercise) => exercise.id === 219);
  assert.equal(alternatingHighKneePull.name, "Alternating High-Knee Cross-Body Pull");
  assert.equal(alternatingHighKneePull.sideSequence, "Alternating");
  const highKneeSideReach = catalog.find((exercise) => exercise.id === 618);
  assert.equal(highKneeSideReach.primaryCanonicalGroup, "HipFlexors");
  assert.equal(
    highKneeSideReach.secondaryCanonicalGroups.includes("PelvicFloorAndPerineum"),
    false,
  );

  for (const [minutes, resolution] of RESOLUTIONS) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(selected.length, minutes);
    assert.equal(new Set(selected.map((exercise) => exercise.id)).size, minutes);
  }

  for (const minutes of [45, 60, 90]) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(
      session.getActiveGroups().reduce(
        (total, group) => total + (group.usesFullSideTiming ? 2 : 1),
        0,
      ),
      minutes,
    );
    assert.equal(
      new Set(selected.map((exercise) => exercise.id)).size,
      30 + Object.keys(session.state.activeDirectionPartnerExerciseIds).length,
    );
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

test("long workouts spend extra minutes on full timed pairs before repeated sets", () => {
  const selectionGroups = RESOLUTIONS.get(30).groups;
  const exercises = selectionGroups.map((group, index) => ({
    ...exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0),
    sideSequence: index < 6 ? "ScreenRightThenLeft" : "Continuous",
    directionSequence: index >= 6 && index < 12
      ? "ClockwiseThenCounterclockwise"
      : "None",
  }));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  const rounds = session.getActiveGroups();
  assert.equal(rounds.length, 33);
  assert.equal(rounds.filter((round) => round.usesFullSideTiming).length, 12);
  assert.equal(rounds.filter((round) =>
    round.usesFullSideTiming &&
    session.getSelectedExercise(round).directionSequence !== "None").length, 6);
  assert.equal(session.state.activeFullSideRoundIds.length, 12);
  assert.equal(session.state.activeExtraSetSelectionGroupIds.length, 3);
  assert.equal(
    rounds.reduce((total, round) => total + (round.usesFullSideTiming ? 2 : 1), 0),
    45,
  );
});

test("forty-five minutes add linked directions before lengthening sided timers", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const baseExercises = groups.map((group, index) => ({
    ...exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10),
    sideSequence: "ScreenLeftThenRight",
  }));
  const partners = groups.slice(0, 15).map((group, index) => ({
    ...exercise(1001 + index, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0),
    sideSequence: "ScreenLeftThenRight",
    directionPartnerExerciseId: baseExercises[index].id,
  }));
  for (let index = 0; index < partners.length; index += 1) {
    baseExercises[index].directionPartnerExerciseId = partners[index].id;
  }
  const exercises = [...baseExercises, ...partners];
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  const rounds = session.getActiveGroups();
  const directionRounds = rounds.filter((round) => round.id.endsWith(".direction1"));
  assert.equal(directionRounds.length, 15);
  assert.equal(rounds.length, 45);
  assert.equal(session.state.activeFullSideRoundIds.length, 0);
  assert.equal(session.state.activeExtraSetSelectionGroupIds.length, 0);
  for (const round of directionRounds) {
    const baseId = session.state.selectedExerciseIds[getSelectionKey(round)];
    assert.equal(
      session.getSelectedExercise(round).id,
      exercises.find((item) => item.id === baseId).directionPartnerExerciseId,
    );
  }
});

for (const minutes of [3, 5, 7, 10, 15, 20, 30]) {
  test(`direction pairs are entirely excluded from ${minutes}-minute workouts`, () => {
    const session = new WorkoutSession(
      directionPairCatalog(),
      createDefaultState(),
      () => 0,
    );

    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);

    assert.ok(session.getActiveGroups().every((group) =>
      session.getSelectedExercise(group).directionPartnerExerciseId === 0));
    assert.deepEqual(session.state.activeDirectionPartnerExerciseIds, {});
  });
}

test("an idle pair selection is kept for long workouts but replaced at thirty minutes", () => {
  const exercises = directionPairCatalog();
  const group = RESOLUTIONS.get(30).groups[0];
  const shortState = createDefaultState();
  shortState.catalogRevision = CURRENT_CATALOG_REVISION;
  shortState.selectedExerciseIds[group.id] = 1;
  const shortSession = new WorkoutSession(exercises, shortState, () => 0);
  shortSession.initialize();
  assert.equal(shortSession.state.selectedExerciseIds[group.id], 1);

  shortSession.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.notEqual(shortSession.state.selectedExerciseIds[group.id], 1);
  assert.equal(
    shortSession.getSelectedExercise(group).directionPartnerExerciseId,
    0,
  );

  const longState = createDefaultState();
  longState.catalogRevision = CURRENT_CATALOG_REVISION;
  longState.selectedExerciseIds[group.id] = 1;
  const longSession = new WorkoutSession(exercises, longState, () => 0);
  longSession.initialize();
  longSession.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(longSession.state.selectedExerciseIds[group.id], 1);
  assert.equal(longSession.state.activeDirectionPartnerExerciseIds[group.id], 2);
});

test("every repeated direction pair remains adjacent in ninety minutes", () => {
  const session = new WorkoutSession(
    directionPairCatalog(),
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(90, WORKOUT_MODIFIERS.None);

  const rounds = session.getActiveGroups();
  const pairLeads = rounds.filter((round) =>
    round.pairedRoundId && !round.isPairDecisionRound);
  assert.ok(pairLeads.length >= 2);
  assert.equal(rounds.reduce(
    (total, round) => total + (round.usesFullSideTiming ? 2 : 1),
    0,
  ), 90);
  for (const lead of pairLeads) {
    const decision = rounds[lead.order];
    assert.equal(decision.isPairDecisionRound, true);
    assert.equal(lead.pairedRoundId, decision.id);
    assert.equal(decision.pairedRoundId, lead.id);
    assert.equal(getSelectionKey(lead), getSelectionKey(decision));
    assert.equal(
      session.getSelectedExercise(lead).directionPartnerExerciseId,
      session.getSelectedExercise(decision).id,
    );
  }
});

test("shuffle rejects the current exercise and replaces only its slot", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 7),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 5),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const activeGroups = session.getActiveGroups();
  const current = activeGroups[0];
  const originalId = session.getSelectedExercise(current).id;
  const otherSelections = new Map(activeGroups.slice(1).map((group) => [
    group.id,
    session.getSelectedExercise(group).id,
  ]));
  const originalScores = new Map(exercises.map((item) => [item.id, item.score]));
  session.state.lastKeptExerciseIds = [originalId];
  session.state.lastKeptLocalDateByExerciseId[String(originalId)] = "2026-08-21";
  session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Insect}|${getSelectionKey(current)}`
  ] = originalId;

  assert.equal(session.canShuffleNextExercise(current), true);
  const result = session.shuffleNextExercise(current);

  assert.ok(result);
  assert.equal(result.rejectedExercise.id, originalId);
  assert.notEqual(result.replacementExercise.id, originalId);
  assert.equal(
    session.getSelectedExercise(current).id,
    result.replacementExercise.id,
  );
  assert.deepEqual(session.state.outcomes, {});
  assert.deepEqual(result.scoreUpdates.map((item) => item.id), [originalId]);
  assert.equal(session.state.scores[originalId], originalScores.get(originalId) - 1);
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(originalId));
  assert.equal(session.state.lastKeptExerciseIds.includes(originalId), false);
  assert.equal(
    session.state.lastKeptLocalDateByExerciseId[String(originalId)],
    undefined,
  );
  assert.equal(
    Object.values(session.state.selectedExerciseIds).includes(originalId),
    false,
  );
  assert.ok(exercises
    .filter((item) => item.id !== originalId)
    .every((item) => item.score === originalScores.get(item.id)));
  assert.ok(activeGroups.slice(1).every((group) =>
    session.getSelectedExercise(group).id === otherSelections.get(group.id)));
});

test("shuffle visits every eligible alternative without repeating", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 7),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 5),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = session.getNextGroup();
  const visitedExerciseIds = new Set([session.getSelectedExercise(group).id]);

  let shuffleCount = 0;
  while (session.canShuffleNextExercise(group)) {
    const result = session.shuffleNextExercise(group);
    assert.equal(visitedExerciseIds.has(result.replacementExercise.id), false);
    visitedExerciseIds.add(result.replacementExercise.id);
    assert.deepEqual(session.state.outcomes, {});
    shuffleCount += 1;
    assert.ok(shuffleCount < 10);
  }

  assert.deepEqual([...visitedExerciseIds].sort((a, b) => a - b), [1, 2, 3]);
  const currentExerciseId = session.getSelectedExercise(group).id;
  assert.deepEqual(
    [...visitedExerciseIds]
      .filter((id) => id !== currentExerciseId)
      .sort((a, b) => a - b),
    session.state.nextWorkoutExcludedExerciseIds
      .filter((id) => visitedExerciseIds.has(id))
      .sort((a, b) => a - b),
  );
});

test("direction pair shuffle rejects both and is unavailable after direction one", () => {
  const exercises = directionPairCatalog();
  const canonicalGroups = [...new Set(RESOLUTIONS.get(30).groups.flatMap(
    (group) => group.canonicalGroups,
  ))];
  const third = {
    ...exercise(3, canonicalGroups[0], canonicalGroups.slice(1), -100),
    directionPartnerExerciseId: 4,
  };
  const fourth = {
    ...exercise(4, canonicalGroups[0], canonicalGroups.slice(1), -100),
    directionPartnerExerciseId: 3,
  };
  const session = new WorkoutSession(
    [...exercises, third, fourth],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const lead = session.getActiveGroups().find((round) =>
    round.pairedRoundId &&
    !round.isPairDecisionRound &&
    session.getSelectedExercise(round).id === 1);
  assert.ok(lead);
  for (const priorRound of session.getActiveGroups()) {
    if (priorRound.id === lead.id) {
      break;
    }
    session.recordOutcome(priorRound, true);
  }

  assert.equal(session.canShuffleNextExercise(lead), true);
  const rejectedLead = session.getSelectedExercise(lead);
  const rejectedPartner = exercises.find((exercise) =>
    exercise.id === rejectedLead.directionPartnerExerciseId);
  const rejectedLeadScore = rejectedLead.score;
  const rejectedPartnerScore = rejectedPartner.score;
  const result = session.shuffleNextExercise(lead);

  assert.equal(
    [rejectedLead.id, rejectedPartner.id].includes(result.replacementExercise.id),
    false,
  );
  assert.deepEqual(
    result.scoreUpdates.map((exercise) => exercise.id),
    [rejectedLead.id, rejectedPartner.id],
  );
  assert.equal(session.state.scores[rejectedLead.id], rejectedLeadScore - 1);
  assert.equal(session.state.scores[rejectedPartner.id], rejectedPartnerScore - 1);
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(rejectedLead.id));
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(rejectedPartner.id));
  const replacementLead = session.getNextGroup();
  assert.equal(
    session.getSelectedExercise(replacementLead).id,
    result.replacementExercise.id,
  );
  assert.equal(
    session.getActiveGroups().reduce(
      (minutes, round) => minutes + (round.usesFullSideTiming ? 2 : 1),
      0,
    ),
    45,
  );
  assert.equal(session.state.scores[rejectedLead.id], rejectedLeadScore - 1);
  assert.equal(session.state.scores[rejectedPartner.id], rejectedPartnerScore - 1);
  assert.equal(session.state.scores[third.id], undefined);
  assert.equal(session.state.scores[fourth.id], undefined);

  const secondSession = new WorkoutSession(
    directionPairCatalog(),
    createDefaultState(),
    () => 0,
  );
  secondSession.startWorkout(45, WORKOUT_MODIFIERS.None);
  const secondLead = secondSession.getActiveGroups().find((round) =>
    round.pairedRoundId && !round.isPairDecisionRound);
  for (const priorRound of secondSession.getActiveGroups()) {
    if (priorRound.id === secondLead.id) {
      break;
    }
    secondSession.recordOutcome(priorRound, true);
  }
  secondSession.advanceDirectionPair(secondLead);
  const secondDirection = secondSession.getNextGroup();

  assert.equal(secondDirection.isPairDecisionRound, true);
  assert.equal(secondSession.canShuffleNextExercise(secondDirection), false);
  assert.equal(secondSession.shuffleNextExercise(secondDirection), null);
});

test("a direction pair can only be kept after its second direction", () => {
  const session = new WorkoutSession(
    directionPairCatalog(),
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const lead = session.getActiveGroups().find((round) =>
    round.pairedRoundId && !round.isPairDecisionRound);
  const decision = session.getActiveGroups().find((round) =>
    round.id === lead.pairedRoundId);
  for (const priorRound of session.getActiveGroups().slice(0, lead.order - 1)) {
    session.recordOutcome(priorRound, true);
  }

  assert.throws(
    () => session.recordOutcome(lead, true),
    /only be kept after its second direction/,
  );
  assert.equal(session.state.outcomes[lead.id], undefined);
  session.advanceDirectionPair(lead);
  assert.equal(session.state.outcomes[lead.id], "neutral");
  assert.equal(session.getNextGroup().id, decision.id);
  session.recordOutcome(decision, true);

  assert.equal(session.state.outcomes[lead.id], "tick");
  assert.equal(session.state.outcomes[decision.id], "tick");
  assert.equal(session.getScore(session.getSelectedExercise(lead)), 100);
  assert.equal(session.getScore(session.getSelectedExercise(decision)), 100);
});

test("a linked direction partner is never selected as another base unit", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const allCanonicalGroups = [...new Set(groups.flatMap((group) =>
    group.canonicalGroups))];
  const first = {
    ...exercise(
      1,
      groups[0].canonicalGroups[0],
      allCanonicalGroups.filter((group) => group !== groups[0].canonicalGroups[0]),
      10,
    ),
    directionPartnerExerciseId: 2,
  };
  const second = {
    ...exercise(
      2,
      groups[1].canonicalGroups[0],
      allCanonicalGroups.filter((group) => group !== groups[1].canonicalGroups[0]),
      10,
    ),
    directionPartnerExerciseId: 1,
  };
  const remaining = groups.slice(2).map((group, index) =>
    exercise(3 + index, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10));
  const filler = exercise(
    100,
    groups[1].canonicalGroups[0],
    allCanonicalGroups.filter((group) => group !== groups[1].canonicalGroups[0]),
    10,
  );
  const session = new WorkoutSession(
    [first, second, filler, ...remaining],
    createDefaultState(),
    () => 0,
  );

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(Object.keys(session.state.activeDirectionPartnerExerciseIds).length, 1);
  assert.equal(
    Object.values(session.state.selectedExerciseIds).includes(second.id),
    false,
  );
  assert.equal(session.getActiveGroups().filter((round) =>
    round.id.endsWith(".direction1") &&
    session.getSelectedExercise(round).id === second.id).length, 1);
});

test("rejecting linked directions applies one shared decision to both", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const baseExercises = groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10));
  const partner = {
    ...exercise(1001, groups[0].canonicalGroups[0], groups[0].canonicalGroups.slice(1), 0),
    directionPartnerExerciseId: baseExercises[0].id,
  };
  baseExercises[0].directionPartnerExerciseId = partner.id;
  const session = new WorkoutSession(
    [...baseExercises, partner],
    createDefaultState(),
    () => 0,
  );

  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const directionRound = session.getActiveGroups().find((round) =>
    round.id.endsWith(".direction1"));
  const pairLead = session.getActiveGroups().find((round) =>
    round.pairedRoundId === directionRound.id);
  const baseExerciseId = session.state.selectedExerciseIds[getSelectionKey(directionRound)];
  for (const priorRound of session.getActiveGroups().slice(0, pairLead.order - 1)) {
    session.recordOutcome(priorRound, true);
  }
  session.advanceDirectionPair(pairLead);
  session.recordOutcome(directionRound, false);

  assert.equal(session.getScore(partner), -1);
  assert.equal(session.getScore(baseExercises.find((item) =>
    item.id === baseExerciseId)), 9);
  assert.equal(session.state.outcomes[pairLead.id], "x");
  assert.equal(session.state.outcomes[directionRound.id], "x");
  assert.equal(
    session.state.selectedExerciseIds[getSelectionKey(directionRound)],
    baseExerciseId,
  );
  assert.notEqual(baseExerciseId, partner.id);
});

test("version five long workout recomputes direction allocation without enabling silence", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const baseExercises = groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10));
  const partner = {
    ...exercise(1001, groups[0].canonicalGroups[0], groups[0].canonicalGroups.slice(1), 0),
    directionPartnerExerciseId: baseExercises[0].id,
  };
  baseExercises[0].directionPartnerExerciseId = partner.id;
  const exercises = [...baseExercises, partner];
  const started = new WorkoutSession(exercises, createDefaultState(), () => 0);
  started.startWorkout(45, WORKOUT_MODIFIERS.None);
  const stored = JSON.parse(JSON.stringify(started.state));
  stored.version = 5;
  delete stored.activeDirectionPartnerExerciseIds;
  delete stored.activeFullSideRoundIds;
  delete stored.activeExtraSetSelectionGroupIds;
  delete stored.activeSetCountsBySelectionGroupId;

  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(stored)),
    () => 0,
  );
  restored.normalizeActiveLongWorkoutAllocation();

  assert.equal(restored.state.version, 10);
  assert.equal(restored.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(restored.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(Object.keys(restored.state.activeDirectionPartnerExerciseIds).length, 1);
  assert.equal(
    restored.getActiveGroups().reduce(
      (total, round) => total + (round.usesFullSideTiming ? 2 : 1),
      0,
    ),
    45,
  );
});

test("a rejected repeated round replaces its shared exercise once", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(90, WORKOUT_MODIFIERS.None);
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
  started.startWorkout(90, WORKOUT_MODIFIERS.None);
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

test("persisted pending direction rest cannot recurse through allocation validation", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const baseExercises = groups.map((group, index) =>
    exercise(
      index + 1,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      10,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ));
  const partner = {
    ...exercise(
      1001,
      groups[0].canonicalGroups[0],
      groups[0].canonicalGroups.slice(1),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      false,
    ),
    directionPartnerExerciseId: baseExercises[0].id,
  };
  baseExercises[0].directionPartnerExerciseId = partner.id;
  const allCanonicalGroups = [...new Set(groups.flatMap((group) =>
    group.canonicalGroups))];
  const filler = exercise(
    2001,
    groups[0].canonicalGroups[0],
    allCanonicalGroups.filter((group) => group !== groups[0].canonicalGroups[0]),
    10,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const session = new WorkoutSession(
    [...baseExercises, partner, filler],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(45, WORKOUT_MODIFIERS.Silence);
  assert.deepEqual(session.state.activeDirectionPartnerExerciseIds, {});
  session.state.activeDirectionPartnerExerciseIds[groups[0].id] = partner.id;
  session.state.pendingRestGroupId = `${groups[0].id}.direction1`;
  session.state.pendingRestEndsAtUnixMilliseconds = 123456;

  session.initialize();

  assert.equal(session.state.activeWorkoutMinutes, 0);
  assert.equal(session.state.pendingRestGroupId, null);
  assert.equal(session.getScore(partner), 0);
});

test("selection uses truthful associations and ranks score, primary, then coverage", () => {
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

  assert.equal(isSelectable(secondaryOnly, group), true);
  const session = new WorkoutSession(
    [secondaryOnly, highScore, broadLowScore, broadEqualScore],
    createDefaultState(),
    () => 0,
  );
  assert.equal(session.chooseBestCandidate(group).id, secondaryOnly.id);
  session.setScore(secondaryOnly, 3);
  assert.equal(session.chooseBestCandidate(group).id, broadEqualScore.id);
  assert.equal(getCanonicalCoverage(broadEqualScore, group), 2);
});

test("movement countdown uses exact continuous and 20/5/20 boundaries", () => {
  assert.deepEqual(getMovementPhaseState(50_000, false), {
    phase: "Preparation",
    secondsRemaining: 5,
    segmentDurationSeconds: 5,
    isExercise: false,
  });
  assert.equal(getMovementPhaseState(45_001, false).secondsRemaining, 1);
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
  const leadStance = {
    sideSequence: "ScreenRightLeadThenLeftLead",
    directionSequence: "None",
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
  assert.deepEqual(getMovementPresentation(leadStance, "FirstSide"), {
    cue: "ShownLeadStance",
    mirrorMedia: false,
    activeScreenSide: "Right",
  });
  assert.deepEqual(getMovementPresentation(leadStance, "SecondSide"), {
    cue: "OppositeLeadStance",
    mirrorMedia: true,
    activeScreenSide: "Left",
  });
});

test("full-side rounds use exact 45/15/45 boundaries", () => {
  assert.equal(getMovementDurationMs({ usesFullSideTiming: true }), 105_000);
  assert.equal(getMovementCountdownDurationMs({ usesFullSideTiming: true }), 110_000);
  assert.deepEqual(getMovementPhaseState(110_000, true, true), {
    phase: "Preparation",
    secondsRemaining: 5,
    segmentDurationSeconds: 5,
    isExercise: false,
  });
  assert.deepEqual(getMovementPhaseState(105_000, true, true), {
    phase: "FirstSide",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
  assert.deepEqual(getMovementPhaseState(60_000, true, true), {
    phase: "ChangeSides",
    secondsRemaining: 15,
    segmentDurationSeconds: 15,
    isExercise: false,
  });
  assert.deepEqual(getMovementPhaseState(45_000, true, true), {
    phase: "SecondSide",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
});

test("reviewed sided movements always receive a timed side swap", () => {
  const sidedIds = [
    16, 20, 47, 97, 117, 179, 180, 184, 186, 211,
    213, 220, 225, 234, 239, 241, 242, 248, 256, 258, 269,
    278, 279, 282, 283, 285, 286, 291, 294, 326, 329,
    198, 394, 395, 396, 397, 421, 427, 468, 507, 512, 513, 572, 577, 618, 636,
    685, 745, 834,
  ];
  for (const exerciseId of sidedIds) {
    assert.equal(
      usesTimedSides(catalog.find((item) => item.id === exerciseId)),
      true,
      `exercise ${exerciseId} must receive separate side phases`,
    );
  }

  const correctedOneSideMedia = new Map([
    [198, "ScreenLeftThenRight"],
    [248, "ScreenRightThenLeft"],
    [282, "ScreenLeftThenRight"],
    [394, "ScreenLeftThenRight"],
    [395, "ScreenLeftThenRight"],
    [397, "ScreenRightThenLeft"],
    [421, "ScreenLeftThenRight"],
    [427, "ScreenLeftLeadThenRightLead"],
    [468, "ScreenLeftThenRight"],
    [507, "ScreenRightThenLeft"],
    [512, "ScreenRightThenLeft"],
    [577, "ScreenRightThenLeft"],
    [618, "ScreenLeftThenRight"],
    [834, "ScreenLeftThenRight"],
    [685, "ScreenLeftThenRight"],
  ]);
  for (const [exerciseId, sideSequence] of correctedOneSideMedia) {
    assert.equal(catalog.find((item) => item.id === exerciseId).sideSequence, sideSequence);
  }

  const continuousIds = [
    15, 17, 19, 31, 107, 135, 150, 169, 176, 193, 195,
    201, 230, 251, 257, 262, 263, 266,
    267, 268, 270, 275, 289, 301, 314, 321,
    391, 413, 425, 516, 615, 677, 683, 687,
  ];
  for (const exerciseId of continuousIds) {
    assert.equal(
      usesTimedSides(catalog.find((item) => item.id === exerciseId)),
      false,
      `exercise ${exerciseId} should remain one uninterrupted movement phase`,
    );
  }

  const alternating = catalog.filter((item) => item.sideSequence === "Alternating");
  assert.equal(alternating.length, 123);
  for (const exerciseId of [31, 98, 176, 195, 219, 390, 391, 398, 413, 508, 515, 576, 816]) {
    assert.equal(catalog.find((item) => item.id === exerciseId).sideSequence, "Alternating");
  }
  assert.equal(catalog.find((item) => item.id === 15).sideSequence, "Alternating");
  assert.equal(catalog.find((item) => item.id === 429).sideSequence, "Alternating");
  assert.equal(getMovementDurationMs(alternating[0]), 45_000);
  assert.deepEqual(getMovementPhaseState(45_000, usesTimedSides(alternating[0])), {
    phase: "Continuous",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });

  const leadStanceSequences = new Map([
    [265, "ScreenLeftLeadThenRightLead"],
    [274, "ScreenLeftLeadThenRightLead"],
    [280, "ScreenLeftLeadThenRightLead"],
    [287, "ScreenRightLeadThenLeftLead"],
    [427, "ScreenLeftLeadThenRightLead"],
    [473, "ScreenLeftLeadThenRightLead"],
    [591, "ScreenLeftLeadThenRightLead"],
    [884, "ScreenRightLeadThenLeftLead"],
    [885, "ScreenRightLeadThenLeftLead"],
    [886, "ScreenRightLeadThenLeftLead"],
    [887, "ScreenRightLeadThenLeftLead"],
  ]);
  assert.deepEqual(
    catalog.filter(usesTimedLeadStances).map((exercise) => exercise.id),
    [...leadStanceSequences.keys()],
  );
  for (const [exerciseId, sideSequence] of leadStanceSequences) {
    const exercise = catalog.find((item) => item.id === exerciseId);
    assert.equal(exercise.sideSequence, sideSequence);
    assert.equal(usesTimedSides(exercise), true);
  }
});

test("forty-five-minute direction and side allocation remains fixed after start", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(30, WORKOUT_MODIFIERS.None);
  const previousRounds = session.getActiveGroups();
  for (const round of previousRounds) {
    session.recordOutcome(round, round.order <= 10);
  }
  const expectedKeptExerciseIds = previousRounds
    .slice(0, 10)
    .map((round) => session.state.selectedExerciseIds[getSelectionKey(round)]);
  session.acknowledgeCompletion();
  assert.deepEqual(
    [...session.state.lastKeptExerciseIds].sort((left, right) => left - right),
    [...expectedKeptExerciseIds].sort((left, right) => left - right),
  );

  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const rounds = session.getActiveGroups();
  const expectedDirectionPartners = {
    ...session.state.activeDirectionPartnerExerciseIds,
  };
  const expectedFullSideRoundIds = [...session.state.activeFullSideRoundIds];
  const expectedExtraSetGroupIds = [...session.state.activeExtraSetSelectionGroupIds];
  assert.equal(
    Object.keys(expectedDirectionPartners).length +
      expectedFullSideRoundIds.length +
      expectedExtraSetGroupIds.length,
    15,
  );
  assert.equal(
    rounds.reduce((total, round) => total + (round.usesFullSideTiming ? 2 : 1),
    0),
    45,
  );

  session.state.lastKeptExerciseIds = [];
  assert.deepEqual(session.state.activeDirectionPartnerExerciseIds, expectedDirectionPartners);
  assert.deepEqual(session.state.activeFullSideRoundIds, expectedFullSideRoundIds);
  assert.deepEqual(session.state.activeExtraSetSelectionGroupIds, expectedExtraSetGroupIds);
});

test("kept exercises fill compatible slots after workout duration changes", () => {
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);

  for (const [previousMinutes, nextMinutes, expectedCarriedCount] of [
    [3, 5, 3],
    [5, 3, 3],
  ]) {
    const previousGroups = RESOLUTIONS.get(previousMinutes).groups;
    const nextGroups = RESOLUTIONS.get(nextMinutes).groups;
    const keptExercises = previousGroups.map((group, index) => {
      const primary = group.canonicalGroups[0];
      return exercise(
        index + 1,
        primary,
        allCanonicalGroups.filter((candidate) => candidate !== primary),
        0,
      );
    });
    const nextDurationAlternatives = nextGroups.map((group, index) => {
      const primary = group.canonicalGroups[0];
      return exercise(
        101 + index,
        primary,
        allCanonicalGroups.filter((candidate) => candidate !== primary),
        10,
      );
    });
    const state = createDefaultState();
    for (const [index, group] of previousGroups.entries()) {
      state.selectedExerciseIds[group.id] = keptExercises[index].id;
    }
    state.lastKeptExerciseIds = keptExercises.map((item) => item.id);
    const session = new WorkoutSession(
      [...keptExercises, ...nextDurationAlternatives],
      state,
      () => 0,
    );

    session.startWorkout(previousMinutes, WORKOUT_MODIFIERS.None);
    for (const round of session.getActiveGroups()) {
      session.recordOutcome(round, true);
    }
    session.acknowledgeCompletion();

    for (const [index, group] of nextGroups.entries()) {
      session.state.selectedExerciseIds[group.id] = nextDurationAlternatives[index].id;
    }
    session.startWorkout(nextMinutes, WORKOUT_MODIFIERS.None);

    const keptExerciseIds = new Set(keptExercises.map((item) => item.id));
    const selectedExerciseIds = nextGroups.map(
      (group) => session.state.selectedExerciseIds[group.id],
    );
    assert.equal(session.state.lastKeptExerciseIds.length, previousMinutes);
    assert.equal(
      selectedExerciseIds.filter((exerciseId) => keptExerciseIds.has(exerciseId)).length,
      expectedCarriedCount,
    );
    assert.equal(new Set(selectedExerciseIds).size, nextMinutes);
  }
});

test("an interrupted workout preserves unreviewed keeps until explicit rejection", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const kept = session.getSelectedExercise(session.getActiveGroups().at(-1));
  session.state.lastKeptExerciseIds = [kept.id];

  session.finishInterruptedWorkout();

  assert.equal(session.state.lastKeptExerciseIds.includes(kept.id), true);

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  for (const round of session.getActiveGroups()) {
    session.recordOutcome(round, session.getSelectedExercise(round).id !== kept.id);
  }
  session.acknowledgeCompletion();

  assert.equal(session.state.lastKeptExerciseIds.includes(kept.id), false);
});

test("rejection decrements once, purges saved copies, and replaces only rejected slots", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
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
  neutral.startWorkout(3, WORKOUT_MODIFIERS.None);
  const neutralGroup = neutral.getNextGroup();
  const neutralExercise = neutral.getSelectedExercise(neutralGroup);
  neutral.finishInterruptedWorkout();
  assert.equal(neutral.getScore(neutralExercise), 0);
  assert.equal(neutral.state.activeWorkoutMinutes, 0);

  const rejected = new WorkoutSession(catalog, createDefaultState(), () => 0);
  rejected.startWorkout(3, WORKOUT_MODIFIERS.None);
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
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
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
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = started.getNextGroup();
  const retired = started.getSelectedExercise(group);
  started.setScore(retired, -4);
  started.beginRest(group, Date.now() + 15_000);
  started.state.lastKeptExerciseIds = [retired.id];

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
  assert.deepEqual(restored.state.lastKeptExerciseIds, [retired.id]);
  assert.equal(restored.getScore(changedCatalog.find((item) => item.id === retired.id)), 0);
});

test("approved timed-side name cleanup preserves browser memory", () => {
  const normalized = catalog.find(
    (item) =>
      usesTimedSides(item) &&
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
    if (exerciseId === 500) {
      continue;
    }
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

    const isDirectionPartnerOnly =
      currentExercise.directionPartnerExerciseId > 0 &&
      currentExercise.id > currentExercise.directionPartnerExerciseId;
    assert.equal(
      restored.state.selectedExerciseIds[group.id],
      isDirectionPartnerOnly ? undefined : exerciseId,
    );
    assert.equal(restored.getScore(currentExercise), -3);
  }
});

test("second clarity corrections preserve earlier browser memory", () => {
  for (const [exerciseId, previousNames] of
    ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES) {
    const currentName = APPROVED_EXERCISE_CORRECTIONS.get(exerciseId)[1];
    const currentCatalog = catalog.map((item) =>
      item.id === exerciseId ? { ...item, name: currentName } : item,
    );
    const currentExercise = currentCatalog.find((item) => item.id === exerciseId);
    const group = RESOLUTIONS.get(30).groups.find((candidate) =>
      isSelectable(currentExercise, candidate),
    );

    for (const previousName of previousNames) {
      const priorCatalog = currentCatalog.map((item) =>
        item.id === exerciseId ? { ...item, name: previousName } : item,
      );
      const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
      prior.initialize();
      prior.state.selectedExerciseIds[group.id] = exerciseId;
      prior.setScore(priorCatalog.find((item) => item.id === exerciseId), -4);

      const restored = new WorkoutSession(
        currentCatalog,
        parseStoredState(JSON.stringify(prior.state)),
        () => 0,
      );
      restored.initialize();

      assert.equal(restored.state.selectedExerciseIds[group.id], exerciseId);
      assert.equal(restored.getScore(currentExercise), -4);
    }
  }
});

test("catalog revision retires only exercises changed by that revision", () => {
  const latestReplacementIds = new Set(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION]
      .filter(([revision]) => revision > 12)
      .flatMap(([, exerciseIds]) => [...exerciseIds]),
  );
  const replacements = catalog.filter((item) =>
    typeof item.retiredName === "string" && item.retiredName,
  );
  const replacement = catalog.find((item) => item.id === 223);
  const historicalReplacement = replacements.find((item) => item.id === 591);
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(replacement, candidate),
  );
  const state = createDefaultState();
  state.catalogRevision = 12;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds[group.id] = replacement.id;
  for (const item of replacements) {
    state.scores[String(item.id)] = -4;
  }
  state.scores[String(replacement.id)] = -4;
  state.outcomes[group.id] = "tick";
  state.pendingRestGroupId = group.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 15_000;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
  for (const item of replacements) {
    assert.equal(
      restored.state.scores[String(item.id)],
      latestReplacementIds.has(item.id) ? undefined : -4,
    );
  }
  assert.equal(restored.state.scores[String(replacement.id)], undefined);
  assert.equal(restored.state.scores[String(historicalReplacement.id)], -4);
  assert.equal(restored.state.outcomes[group.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("revision workout invalidations preserve scores for unchanged exercise identities", () => {
  const workoutOnlyIds = [119, 140, 340];
  const state = createDefaultState();
  state.catalogRevision = 17;

  for (const exerciseId of workoutOnlyIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of workoutOnlyIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(restored.state.scores[String(exerciseId)], -4);
  }
});

test("revision semantic replacements reset scores", () => {
  const semanticReplacementIds = [115, 212, 260, 512, 649];
  const state = createDefaultState();
  state.catalogRevision = 17;

  for (const exerciseId of semanticReplacementIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of semanticReplacementIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("unilateral timing revision rebuilds workouts without resetting scores", () => {
  const timingCorrectionIds = [
    31, 219, 248, 282, 390, 394, 395,
    397, 508, 576, 577, 618, 816, 834,
  ];
  const state = createDefaultState();
  state.catalogRevision = 24;

  for (const exerciseId of timingCorrectionIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(25), false);
  const laterSemanticCorrections = new Set([31, 219, 395, 577, 618, 834]);
  for (const exerciseId of timingCorrectionIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(
      restored.state.scores[String(exerciseId)],
      laterSemanticCorrections.has(exerciseId) ? undefined : -4,
    );
  }
});

test("illustration correction revision rebuilds workouts without resetting scores", () => {
  const correctedIds = [31, 282, 391, 507, 508, 577];
  const state = createDefaultState();
  state.catalogRevision = 25;

  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(26), false);
  const laterSemanticCorrections = new Set([31, 507, 577]);
  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(
      restored.state.scores[String(exerciseId)],
      laterSemanticCorrections.has(exerciseId) ? undefined : -4,
    );
  }
});

test("karate demonstration correction rebuilds workouts and resets only the replacement score", () => {
  const correctedIds = [231, 685, 687];
  const state = createDefaultState();
  state.catalogRevision = 26;

  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.deepEqual([...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(27)], [687]);
  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
  }
  assert.equal(restored.state.scores["231"], -4);
  assert.equal(restored.state.scores["685"], -4);
  assert.equal(restored.state.scores["687"], undefined);
});

test("forward-fold replacement revision rebuilds workouts and resets its score", () => {
  const state = createDefaultState();
  state.catalogRevision = 27;
  state.selectedExerciseIds["changed.251"] = 251;
  state.scores["251"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.deepEqual([...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(28)], [251]);
  assert.equal(restored.state.selectedExerciseIds["changed.251"], undefined);
  assert.equal(restored.state.scores["251"], undefined);
});

test("reactivated replacement revision drops only changed progress and scores", () => {
  const changedIds = [
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ];
  const changedIdSet = new Set(changedIds);
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(29)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(29)],
    changedIds,
  );

  const groups = RESOLUTIONS.get(30).groups;
  const changedExercise = catalog.find((exercise) => exercise.id === changedIds[0]);
  const changedGroup = groups.find((group) => isSelectable(changedExercise, group));
  const retainedGroup = groups.find((group) =>
    group.id !== changedGroup.id && catalog.some((exercise) =>
      !changedIdSet.has(exercise.id) && isSelectable(exercise, group)));
  const retainedExercise = catalog.find((exercise) =>
    !changedIdSet.has(exercise.id) && isSelectable(exercise, retainedGroup));
  const changedSelectionKey = getSelectionKey(changedGroup);
  const retainedSelectionKey = getSelectionKey(retainedGroup);
  const state = createDefaultState();
  state.catalogRevision = 28;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds[changedSelectionKey] = changedExercise.id;
  state.selectedExerciseIds[retainedSelectionKey] = retainedExercise.id;
  state.outcomes[changedGroup.id] = "x";
  state.outcomes[retainedGroup.id] = "tick";
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 15_000;
  state.pendingRestKept = true;
  state.lastKeptExerciseIds = [changedExercise.id, retainedExercise.id];
  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }
  state.scores[String(retainedExercise.id)] = -7;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedSelectionKey], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(
    restored.state.selectedExerciseIds[retainedSelectionKey],
    retainedExercise.id,
  );
  assert.equal(restored.state.outcomes[retainedGroup.id], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(restored.state.pendingRestKept, false);
  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.scores[String(retainedExercise.id)], -7);
  assert.deepEqual(
    restored.state.lastKeptExerciseIds,
    [changedExercise.id, retainedExercise.id],
  );
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("media repair revision retires invalid assets without resetting media-only scores", () => {
  const changedIds = [
    229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
    501, 504, 513, 516,
  ];
  const semanticIds = [229, 497, 501, 504, 513];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(30)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(30)],
    semanticIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 29;
  state.selectedExerciseIds["changed.media"] = 467;
  state.selectedExerciseIds["changed.retired"] = 229;
  state.scores["467"] = -7;
  state.scores["501"] = -6;
  state.scores["229"] = -5;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds["changed.media"], undefined);
  assert.equal(restored.state.selectedExerciseIds["changed.retired"], undefined);
  assert.equal(restored.state.scores["467"], -7);
  assert.equal(restored.state.scores["501"], undefined);
  assert.equal(restored.state.scores["229"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.equal(catalog.some((exercise) => exercise.id === 229), false);
  assert.equal(catalog.find((exercise) => exercise.id === 497)?.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.Agnostic);
});

test("heel illustration correction revision resets changed workout and scores", () => {
  const changedIds = [414, 415, 416, 418, 419];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(31)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(31)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 30;
  state.activeWorkoutMinutes = 30;
  const changedExercise = catalog.find((exercise) => exercise.id === 414);
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(changedExercise, group));
  state.selectedExerciseIds[changedGroup.id] = changedExercise.id;
  state.outcomes[changedGroup.id] = "x";
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = 123456;
  state.pendingRestKept = true;
  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -exerciseId;
  }
  const retainedExercise = catalog.find((exercise) => !changedIds.includes(exercise.id));
  state.scores[String(retainedExercise.id)] = -7;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(restored.state.pendingRestKept, false);
  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.scores[String(retainedExercise.id)], -7);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("single-side clarity revision resets corrected replacements", () => {
  const correctedReplacementIds = [31, 219, 395, 507, 577, 618, 654, 834];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(32)],
    correctedReplacementIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(32)],
    correctedReplacementIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 31;
  state.selectedExerciseIds["changed.knee-pull"] = 31;
  state.selectedExerciseIds["changed.high-knee-reach"] = 618;
  state.selectedExerciseIds["name-only"] = 915;
  state.scores["31"] = -3;
  state.scores["618"] = -2;
  state.scores["915"] = -1;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds["changed.knee-pull"], undefined);
  assert.equal(
    restored.state.selectedExerciseIds["changed.high-knee-reach"],
    undefined,
  );
  assert.equal(restored.state.selectedExerciseIds["name-only"], 915);
  assert.equal(restored.state.scores["31"], undefined);
  assert.equal(restored.state.scores["618"], undefined);
  assert.equal(restored.state.scores["915"], -1);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("direction split revision resets every linked identity", () => {
  const linkedDirectionIds = [
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(33)],
    linkedDirectionIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(33)],
    linkedDirectionIds,
  );
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 32;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 264;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  for (const exerciseId of linkedDirectionIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  for (const exerciseId of linkedDirectionIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("alternating correction rebuilds workout without resetting scores", () => {
  const correctedIds = [98, 390, 508, 576, 816];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(34)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(34), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 33;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 576;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["576"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["576"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("high-knee alternation correction rebuilds workout without resetting scores", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(35)],
    [219],
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(35), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 34;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 219;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["219"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["219"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("vague elbow-strike replacement rebuilds workout and resets its score", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(36)],
    [684],
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(36)],
    [684],
  );
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 35;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 684;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["684"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["684"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("alternating loop corrections rebuild workouts without resetting scores", () => {
  const correctedIds = [31, 176, 195, 391, 413, 884, 885];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(37)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(37), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 36;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 884;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["884"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["884"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("direction name correction preserves workout state and scores", () => {
  assert.equal(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.has(38), false);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(38), false);
  assert.equal(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.has(40), false);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(40), false);
  const state = createDefaultState();
  const groupId = RESOLUTIONS.get(3).groups[0].id;
  state.catalogRevision = 37;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[groupId] = 223;
  state.outcomes[groupId] = "tick";
  state.pendingRestGroupId = groupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["223"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[groupId], 223);
  assert.equal(restored.state.outcomes[groupId], "tick");
  assert.equal(restored.state.pendingRestGroupId, groupId);
  assert.equal(restored.state.pendingRestKept, true);
  assert.equal(restored.state.scores["223"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("mirror-only correction drops stale selection but preserves score", () => {
  assert.deepEqual([...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(41)], [500]);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(41), false);
  const state = createDefaultState();
  const groupId = RESOLUTIONS.get(3).groups[0].id;
  state.catalogRevision = 40;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[groupId] = 500;
  state.outcomes[groupId] = "tick";
  state.pendingRestGroupId = groupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["500"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[groupId], undefined);
  assert.equal(restored.state.outcomes[groupId], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["500"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("mirror relationship and muscle corrections rebuild workout without resetting scores", () => {
  const correctedIds = [105, 107, 108, 245, 280, 591, 884, 885, 905];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(42)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(42), false);
  const state = createDefaultState();
  const changedGroup = RESOLUTIONS.get(3).groups[0].id;
  const retainedGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 41;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 884;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["884"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["884"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("genuine mirror practice revision retires duplicate and preserves corrected scores", () => {
  const changedIds = [90, 94, 95, 99, 100, 497, 498, 500, 511, 514];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(44)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(44)],
    [90],
  );

  const state = createDefaultState();
  const correctedGroup = RESOLUTIONS.get(3).groups[0].id;
  const retiredGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 43;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[correctedGroup] = 94;
  state.selectedExerciseIds[retiredGroup] = 90;
  state.outcomes[correctedGroup] = "tick";
  state.outcomes[retiredGroup] = "x";
  state.pendingRestGroupId = correctedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["94"] = -4;
  state.scores["90"] = -6;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[correctedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retiredGroup], undefined);
  assert.equal(restored.state.outcomes[correctedGroup], undefined);
  assert.equal(restored.state.outcomes[retiredGroup], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["94"], -4);
  assert.equal(restored.state.scores["90"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("directional circles are complete sequences or explicit side-direction partners", () => {
  const expectedDirectionSequences = new Map([
    [264, "BackwardThenForward"],
    [275, "BackwardThenForward"],
    [406, "ClockwiseThenCounterclockwise"],
    [409, "ClockwiseThenCounterclockwise"],
    [460, "ForwardThenBackward"],
    [588, "BackwardThenForward"],
    [608, "CounterclockwiseThenClockwise"],
    [611, "CounterclockwiseThenClockwise"],
    [743, "BackwardThenForward"],
  ]);
  const expectedNames = new Map([
    [264, "Standing Arm Circles"],
    [275, "Small Arm Circles"],
    [406, "Standing Wheel Arm Circles"],
    [409, "Full Neck Circles"],
    [460, "Jogging in Place with Arm Circles"],
    [588, "Belly-Dance Alternating Shoulder Rolls"],
    [608, "Hip Circles"],
    [611, "Wide-Stance Hip Circles"],
    [743, "Standing Large Arm Circles"],
    [214, "Inward Wrist Circles"],
    [223, "Inward Controlled Wrist Circles"],
    [288, "Forward Knee-and-Ankle Circles"],
    [755, "Outward Wrist Circles"],
    [756, "Outward Controlled Wrist Circles"],
    [758, "Backward Knee-and-Ankle Circles"],
  ]);

  for (const [exerciseId, expectedName] of expectedNames) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)?.name,
      expectedName,
    );
  }
  for (const [exerciseId, expectedSequence] of expectedDirectionSequences) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)?.directionSequence,
      expectedSequence,
    );
  }
  const oneWayCircleName = /\b(?:clockwise|counterclockwise|forward|backward|inward|outward)\b.*\bcircles\b/i;
  assert.ok(catalog.every((exercise) =>
    !oneWayCircleName.test(exercise.name) || exercise.directionPartnerExerciseId > 0));
});

test("complete-direction revision retires duplicates and preserves side-leg scores", () => {
  const workoutIds = [
    264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
    757, 759, 760, 761, 762, 763, 764,
  ];
  const scoreIds = [
    264, 275, 406, 409, 460, 588, 608, 611, 743,
    757, 759, 760, 761, 762, 763, 764,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(45)],
    workoutIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(45)],
    scoreIds,
  );

  const state = createDefaultState();
  const changedGroup = RESOLUTIONS.get(3).groups[0].id;
  const relinkedGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 44;
  state.selectedExerciseIds[changedGroup] = 409;
  state.selectedExerciseIds[relinkedGroup] = 617;
  state.outcomes[changedGroup] = "tick";
  state.outcomes[relinkedGroup] = "tick";
  state.scores["409"] = -4;
  state.scores["617"] = -2;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[relinkedGroup], undefined);
  assert.equal(restored.state.scores["409"], undefined);
  assert.equal(restored.state.scores["617"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("lead-stance timing revision rebuilds workouts without resetting scores", () => {
  const leadStanceIds = [
    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(46)],
    leadStanceIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(46), false);

  const state = createDefaultState();
  state.catalogRevision = 45;
  for (const exerciseId of leadStanceIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of leadStanceIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(restored.state.scores[String(exerciseId)], -4);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("unilateral setup correction rebuilds workouts without resetting scores", () => {
  const correctedIds = [198, 398, 421, 427, 468, 512, 515];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(47)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(47), false);

  const state = createDefaultState();
  state.catalogRevision = 46;
  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(restored.state.scores[String(exerciseId)], -4);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("unclear exercise replacement revision resets every changed score", () => {
  const changedIds = [
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ];
  const state = createDefaultState();
  state.catalogRevision = 19;

  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("catalog clarity reset revision resets every replaced identity", () => {
  const changedIds = [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(21)];
  assert.equal(changedIds.length, 68);
  const state = createDefaultState();
  state.catalogRevision = 20;

  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("deployment migration preserves present keeps and drops missing exercises", () => {
  const present = catalog.find((item) => item.id === 223);
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(present, candidate),
  );
  const state = createDefaultState();
  state.catalogRevision = 12;
  state.selectedExerciseIds[group.id] = present.id;
  state.lastKeptExerciseIds = [present.id, 999999];

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(state)),
    () => 0,
  );
  restored.initialize();

  assert.deepEqual(
    restored.state.lastKeptExerciseIds,
    [present.id, present.directionPartnerExerciseId],
  );
  assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);

  restored.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(restored.state.selectedExerciseIds[group.id], present.id);
  assert.equal(
    restored.state.activeDirectionPartnerExerciseIds[group.id],
    present.directionPartnerExerciseId,
  );
});

test("legacy catalog revision still retires every historical replacement", () => {
  const replacements = catalog.filter((item) =>
    typeof item.retiredName === "string" && item.retiredName,
  );
  const state = createDefaultState();
  state.catalogRevision = 2;
  for (const item of replacements) {
    state.scores[String(item.id)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const item of replacements) {
    assert.equal(restored.state.scores[String(item.id)], undefined);
  }
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
      assert.match(item.name, /\b(?:hold|isometric|pose|stance|stretch)\b/i);
      await assertFile(
        path.join(repositoryRoot, "Flux", "Assets", getHoldFramePath(item)),
      );
    }
  }

  assert.deepEqual(directionIds, [264, 275, 406, 409, 460, 588, 608, 611, 743]);
  const linkedDirections = catalog.filter((item) => item.directionPartnerExerciseId > 0);
  assert.equal(linkedDirections.length, 8);
  for (const item of linkedDirections) {
    const partner = catalog.find((candidate) =>
      candidate.id === item.directionPartnerExerciseId);
    assert.ok(partner);
    assert.equal(partner.directionPartnerExerciseId, item.id);
  }
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

function exercise(
  id,
  primaryCanonicalGroup,
  secondaryCanonicalGroups,
  score,
  insectCompatibility = EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
  silent = true,
  muscularDemand = 0,
  mirrorRelationship = EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
  minimumMirrorCoverage = mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic
    ? EXERCISE_MIRROR_COVERAGE.None
    : EXERCISE_MIRROR_COVERAGE.UpperBody,
) {
  return {
    id,
    name: `Exercise ${id}`,
    video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
    primaryCanonicalGroup,
    secondaryCanonicalGroups,
    score,
    muscularDemand,
    insectCompatibility,
    mirrorRelationship,
    minimumMirrorCoverage,
    equipment: mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly
      ? "Mirror"
      : "None",
    silent,
    sideSequence: "Continuous",
    directionSequence: "None",
    directionPartnerExerciseId: 0,
    mode: "Repetition",
    presentation: "Motion",
  };
}

function reviewedInsectCatalog() {
  const canonicalGroups = RESOLUTIONS.get(30).groups.map(
    (group) => group.canonicalGroups[0],
  );
  const exercises = [];
  let exerciseId = 10_000;
  for (const primary of canonicalGroups) {
    const secondary = canonicalGroups.filter((group) => group !== primary);
    exercises.push(exercise(
      exerciseId++,
      primary,
      secondary,
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ));
    exercises.push(exercise(
      exerciseId++,
      primary,
      secondary,
      100,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    ));
  }
  return exercises;
}

function directionPairCatalog() {
  const canonicalGroups = [...new Set(RESOLUTIONS.get(30).groups.flatMap(
    (group) => group.canonicalGroups,
  ))];
  const first = {
    ...exercise(1, canonicalGroups[0], canonicalGroups.slice(1), 100),
    directionPartnerExerciseId: 2,
  };
  const second = {
    ...exercise(2, canonicalGroups[0], canonicalGroups.slice(1), 100),
    directionPartnerExerciseId: 1,
  };
  const fillers = Array.from({ length: 30 }, (_, index) => {
    const primary = canonicalGroups[index % canonicalGroups.length];
    return exercise(
      100 + index,
      primary,
      canonicalGroups.filter((group) => group !== primary),
      0,
    );
  });
  return [first, second, ...fillers];
}

async function assertFile(file) {
  const information = await stat(file);
  assert.equal(information.isFile(), true);
  assert.ok(information.size > 0);
}
