import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  APPROVED_EXERCISE_CORRECTIONS,
  CURRENT_CATALOG_REVISION,
  CURRENT_WORKOUT_STATE_VERSION,
  DEFAULT_WORKOUT_MODIFIERS,
  EXERCISE_HARD_FLOOR_COMPATIBILITY,
  EXERCISE_INSECT_COMPATIBILITY,
  EXERCISE_MIRROR_COVERAGE,
  HARD_PRIMARY_MUSCLE_LOAD_HALF_UNITS,
  HARD_SECONDARY_MUSCLE_LOAD_HALF_UNITS,
  HARD_MUSCULAR_DEMAND,
  HARD_RECOVERY_WINDOW_MS,
  HARD_ROTATION_STATUS,
  LAST_CUMULATIVE_CATALOG_REVISION,
  MAXIMUM_MUSCULAR_DEMAND,
  MODERATE_MUSCULAR_DEMAND,
  MODERATE_RECOVERY_WINDOW_MS,
  MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MIRROR_CATEGORY,
  MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
  MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT,
  MINIMUM_MODIFIER_MATERIALITY_PERCENT,
  MINIMUM_MUSCULAR_DEMAND,
  MUSCLE_BUDGET_MAX_REBALANCE_PASSES,
  MUSCLE_SESSION_BUDGET_HALF_UNITS,
  MIRROR_EQUIPMENT,
  MOVEMENT_DURATION_MS,
  PREPARATION_DURATION_MS,
  MODERATE_PRIMARY_MUSCLE_LOAD_HALF_UNITS,
  SCORE_HALF_UNITS_PER_VOTE,
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
  workoutGroup,
  taxonomy,
  movementSchedule,
  mainActivity,
  webApp,
  instantControls,
  workoutModule,
  catalogMigrationRules,
  catalogJson,
  workoutModifiers,
  exerciseModel,
  modifierPolicy,
  muscleBudgetPolicy,
  recoveryPolicy,
  exerciseDatabase,
  catalogInvariantTests,
  exerciseDatabaseVersionPolicy,
  workoutSessionLog,
  durationLayout,
  workoutLayout,
  androidColors,
  androidStyles,
  strings,
  webIndex,
  webStyles,
  webBuild,
  mirrorEquipmentModel,
  mirrorCoverageModel,
  hardFloorCompatibilityModel,
  sequenceBlockModel,
  movementPresentationPolicy,
  workoutDisplayPolicy,
  workoutTimelineView,
  compactMirrorIcon,
  tallMirrorIcon,
  hardFloorIcon,
  softFloorIcon,
  atomicSequenceLineupSolver,
  workoutSequencePolicy,
] = await Promise.all([
  source("Flux", "Services", "ExerciseSessionService.cs"),
  source("Flux", "Models", "WorkoutState.cs"),
  source("Flux", "Models", "WorkoutGroup.cs"),
  source("Flux", "Services", "MassGroupingTaxonomy.cs"),
  source("Flux", "Services", "MovementPhaseSchedule.cs"),
  source("Flux", "MainActivity.cs"),
  source("web", "app.js"),
  source("web", "instant-controls.js"),
  source("web", "workout.js"),
  source("Flux", "Services", "CatalogMigrationRules.cs"),
  source("Flux", "Assets", "exercises.json"),
  source("Flux", "Models", "WorkoutModifiers.cs"),
  source("Flux", "Models", "Exercise.cs"),
  source("Flux", "Services", "WorkoutModifierPolicy.cs"),
  source("Flux", "Services", "WorkoutMuscleBudgetPolicy.cs"),
  source("Flux", "Services", "WorkoutRecoveryPolicy.cs"),
  source("Flux", "Data", "SqliteExerciseDatabase.cs"),
  source("Flux.Tests", "CatalogInvariantTests.cs"),
  source("Flux", "Data", "ExerciseDatabaseVersionPolicy.cs"),
  source("Flux", "Models", "WorkoutSessionLog.cs"),
  source("Flux", "Resources", "layout", "screen_duration.xml"),
  source("Flux", "Resources", "layout", "screen_workout.xml"),
  source("Flux", "Resources", "values", "colors.xml"),
  source("Flux", "Resources", "values", "styles.xml"),
  source("Flux", "Resources", "values", "strings.xml"),
  source("web", "index.html"),
  source("web", "styles.css"),
  source("web", "scripts", "build.mjs"),
  source("Flux", "Models", "MirrorEquipment.cs"),
  source("Flux", "Models", "ExerciseMirrorCoverage.cs"),
  source("Flux", "Models", "ExerciseHardFloorCompatibility.cs"),
  source("Flux", "Models", "ExerciseSequenceBlock.cs"),
  source("Flux", "Services", "MovementPhasePresentationPolicy.cs"),
  source("Flux", "Services", "WorkoutDisplayPolicy.cs"),
  source("Flux", "WorkoutBlockTimelineView.cs"),
  source("Flux", "Resources", "drawable", "ic_mirror_compact.xml"),
  source("Flux", "Resources", "drawable", "ic_mirror_tall.xml"),
  binarySource("Flux", "Resources", "drawable-xxhdpi", "ic_hard_floor.png"),
  binarySource("Flux", "Resources", "drawable-xxhdpi", "ic_soft_floor.png"),
  source("Flux", "Services", "AtomicSequenceLineupSolver.cs"),
  source("Flux", "Services", "WorkoutSequencePolicy.cs"),
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

test("web and mobile persist the same complete workout audit trail", () => {
  for (const field of [
    "SessionId",
    "StartedAtUnixMilliseconds",
    "EndedAtUnixMilliseconds",
    "WorkoutMinutes",
    "Modifiers",
    "KeptExerciseIdsAtStart",
    "InitialSelections",
    "SelectionChanges",
    "Blocks",
    "Decisions",
  ]) {
    assert.match(workoutSessionLog, new RegExp(`\\b${field}\\b`));
  }
  assert.match(workoutState, /NextWorkoutSessionId[\s\S]*ActiveWorkoutSession[\s\S]*WorkoutHistory/);
  assert.match(
    sessionService,
    /RecordCompletedWorkoutBlock[\s\S]*RecordWorkoutDecision[\s\S]*FinalizeActiveWorkoutSession/,
  );
  assert.match(
    workoutModule,
    /recordCompletedWorkoutBlock[\s\S]*recordWorkoutDecision[\s\S]*finalizeActiveWorkoutSession/,
  );
  assert.match(
    workoutModule,
    /workoutHistory[\s\S]*activeWorkoutSession[\s\S]*selectionChanges[\s\S]*blocks[\s\S]*decisions/,
  );
});

test("web and mobile persist hard-first block-aware workout allocation", () => {
  assert.match(workoutState, /HashSet<int> LastKeptExerciseIds/);
  assert.match(workoutState, /HashSet<string> ActiveExtraSetSelectionGroupIds/);
  assert.match(workoutState, /Dictionary<string, int> ActiveSetCountsBySelectionGroupId/);
  assert.match(
    sessionService,
    /OrderByDescending\(placement =>[\s\S]*GetSequenceExercises\(placement\.Root\)[\s\S]*Any\(WorkoutRecoveryPolicy\.IsHardExercise\)[\s\S]*ThenByDescending\(placement => IsSequenceKept\(state, placement\.Root\)\)[\s\S]*ThenByDescending\(placement => placement\.Anchor\.Order\)/,
  );
  assert.match(
    workoutModule,
    /rightMembers\.some\(\(member\) =>[\s\S]*HARD_MUSCULAR_DEMAND[\s\S]*leftMembers\.some[\s\S]*keptExerciseIds\.has\(member\.id\)[\s\S]*right\.anchor\.order - left\.anchor\.order/,
  );
  assert.match(
    sessionService,
    /blockCostByGroup[\s\S]*SequenceBlocks\.Length[\s\S]*remainingMinutes[\s\S]*CanFill/,
  );
  assert.match(
    workoutModule,
    /blockCostByGroup[\s\S]*sequenceBlocks\.length[\s\S]*remainingMinutes[\s\S]*canFill/,
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
    /CarryKeptExercisesForward\([\s\S]*LastKeptExerciseIds[\s\S]*ChooseBestDistinctLineup\([\s\S]*preferredTieOrder: orderedKeptExerciseIds/,
  );
});

test("web and mobile apply the same temporary muscle workload budget", () => {
  assert.equal(
    MUSCLE_SESSION_BUDGET_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "MaximumLoadHalfUnits"),
  );
  assert.equal(
    MODERATE_PRIMARY_MUSCLE_LOAD_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "ModeratePrimaryLoadHalfUnits"),
  );
  assert.equal(
    HARD_PRIMARY_MUSCLE_LOAD_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "HardPrimaryLoadHalfUnits"),
  );
  assert.equal(
    HARD_SECONDARY_MUSCLE_LOAD_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "HardSecondaryLoadHalfUnits"),
  );
  assert.equal(
    SCORE_HALF_UNITS_PER_VOTE,
    integerConstant(muscleBudgetPolicy, "ScoreHalfUnitsPerVote"),
  );
  assert.equal(
    MUSCLE_BUDGET_MAX_REBALANCE_PASSES,
    integerConstant(muscleBudgetPolicy, "MaximumRebalancePasses"),
  );
  assert.match(
    sessionService,
    /RepairActiveLineup\(state\);[\s\S]*RebalanceNewExercisesByMuscleBudget\(state\);[\s\S]*SetActiveLongWorkoutAllocation\(state\);/,
  );
  assert.match(
    workoutModule,
    /this\.repairActiveLineup\(\);[\s\S]*this\.rebalanceNewExercisesByMuscleBudget\(\);[\s\S]*this\.setActiveLongWorkoutAllocation\(\);/,
  );
  assert.match(
    muscleBudgetPolicy,
    /MinimumMuscularDemand => 0[\s\S]*ModerateMuscularDemand => ModeratePrimaryLoadHalfUnits[\s\S]*MaximumMuscularDemand => HardPrimaryLoadHalfUnits/,
  );
  assert.match(
    muscleBudgetPolicy,
    /ModerateMuscularDemand => 0[\s\S]*MaximumMuscularDemand => HardSecondaryLoadHalfUnits/,
  );
  assert.match(
    workoutModule,
    /case MINIMUM_MUSCULAR_DEMAND:[\s\S]*case MODERATE_MUSCULAR_DEMAND:[\s\S]*MODERATE_PRIMARY_MUSCLE_LOAD_HALF_UNITS[\s\S]*case HARD_MUSCULAR_DEMAND:[\s\S]*HARD_PRIMARY_MUSCLE_LOAD_HALF_UNITS/,
  );
  assert.match(
    sessionService,
    /CalculateScheduledLoadHalfUnits[\s\S]*GetPrimaryLoadHalfUnits\(exercise\)[\s\S]*GetSecondaryLoadHalfUnits\(exercise\)/,
  );
  assert.match(
    workoutModule,
    /calculateScheduledLoadHalfUnits[\s\S]*getPrimaryMuscleLoadHalfUnits\(exercise\)[\s\S]*getSecondaryMuscleLoadHalfUnits\(exercise\)/,
  );
  assert.match(
    sessionService,
    /IsSequenceKept\(state, currentExercise\)[\s\S]*NextWorkoutExcludedExerciseIds\.Contains\(exercise\.Id\)/,
  );
  assert.match(
    workoutModule,
    /this\.isSequenceKept\(currentExercise\)[\s\S]*nextWorkoutExcludedExerciseIds\.includes\(exercise\.id\)/,
  );
  assert.match(workoutState, /HashSet<int> NextWorkoutExcludedExerciseIds/);
});

test("web and mobile persist one combined duration and modifier selection context", () => {
  assert.equal(WORKOUT_MODIFIERS.Insect, 1);
  assert.equal(WORKOUT_MODIFIERS.Silence, 2);
  assert.equal(WORKOUT_MODIFIERS.Mirror, 4);
  assert.equal(WORKOUT_MODIFIERS.TallMirror, 8);
  assert.equal(WORKOUT_MODIFIERS.HardFloor, 16);
  assert.deepEqual(MIRROR_EQUIPMENT, {
    None: "None",
    Compact: "Compact",
    Tall: "Tall",
  });
  assert.equal(
    DEFAULT_WORKOUT_MODIFIERS,
    WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Silence,
  );
  assert.match(workoutModifiers, /Insect\s*=\s*1/);
  assert.match(workoutModifiers, /Silence\s*=\s*2/);
  assert.match(workoutModifiers, /Mirror\s*=\s*4/);
  assert.match(workoutModifiers, /TallMirror\s*=\s*8/);
  assert.match(workoutModifiers, /HardFloor\s*=\s*16/);
  assert.match(mirrorEquipmentModel, /None[\s\S]*Compact[\s\S]*Tall/);
  assert.match(mirrorCoverageModel, /None[\s\S]*UpperBody[\s\S]*FullBody/);
  assert.deepEqual(EXERCISE_HARD_FLOOR_COMPATIBILITY, {
    Unreviewed: "Unreviewed",
    Compatible: "Compatible",
    Incompatible: "Incompatible",
  });
  assert.match(
    hardFloorCompatibilityModel,
    /Unreviewed[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.equal(CURRENT_WORKOUT_STATE_VERSION, 18);
  assert.match(workoutState, /public int Version[^=]*=\s*21/);
  assert.match(workoutState, /PendingRestMillisecondsRemaining/);
  assert.match(workoutState, /PendingRestPausedByUser/);
  assert.match(workoutModule, /pendingRestMillisecondsRemaining/);
  assert.match(workoutModule, /pendingRestPausedByUser/);
  assert.match(
    workoutState,
    /LastWorkoutModifiers[^=]*=[\s\S]*WorkoutModifiers\.HardFloor\s*\|\s*WorkoutModifiers\.Silence/,
  );
  assert.match(
    sessionService,
    /DefaultWorkoutModifiers\s*=[\s\S]*WorkoutModifiers\.HardFloor\s*\|\s*WorkoutModifiers\.Silence/,
  );
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
    /ChooseBestDistinctLineup\([\s\S]*IsWorkoutSelectionCandidate\(state, exercise, group, modifiers\)/,
  );
  assert.match(exerciseModel, /int SessionMovementId/);
  assert.match(
    exerciseModel,
    /ExerciseHardFloorCompatibility HardFloorCompatibility/,
  );
  assert.match(
    modifierPolicy,
    /WorkoutModifiers\.HardFloor[\s\S]*HardFloorCompatibility[\s\S]*Compatible/,
  );
  assert.match(
    modifierPolicy,
    /GetSessionMovementId[\s\S]*SessionMovementId > 0[\s\S]*exercise\.Id/,
  );
  assert.match(
    modifierPolicy,
    /GetMaximumDistinctLineupSize[\s\S]*WorkoutSequencePolicy\.GetPlacementOptions[\s\S]*GetSessionMovementId\(exercise\)[\s\S]*AtomicSequenceLineupSolver\.Solve/,
  );
  assert.match(
    sessionService,
    /ChooseBestDistinctLineup[\s\S]*GetSessionMovementId\(candidate\)[\s\S]*AtomicSequenceLineupSolver\.Solve/,
  );
  assert.match(
    workoutModule,
    /chooseBestDistinctLineup[\s\S]*getSessionMovementId\(candidate\)[\s\S]*solveAtomicSequenceLineup/,
  );
  assert.equal((sessionService.match(/unavailableMovementIds/g) ?? []).length, 4);
  assert.equal((workoutModule.match(/unavailableMovementIds/g) ?? []).length, 4);
  assert.match(
    modifierPolicy,
    /WorkoutCoveragePolicy\.IsSelectable\(exercise, group\)[\s\S]*IsCompatible\(exercise, profile\)/,
  );
  assert.match(
    modifierPolicy,
    /MinimumExercisesPerPairStatePerGroup\s*=\s*5[\s\S]*FindPairwiseCoverageDeficiencies[\s\S]*FindMaterialityDeficiencies/,
  );
  assert.match(
    modifierPolicy,
    /requiresMirrorRelevance[\s\S]*IsMirrorRelevant\(exercise\)/,
  );
  assert.match(
    modifierPolicy,
    /GetRuleStateProfiles[\s\S]*WorkoutModifiers\.Mirror \| WorkoutModifiers\.TallMirror/,
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
    integerConstant(modifierPolicy, "MinimumExercisesPerPairStatePerGroup"),
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_MIRROR_CATEGORY,
    integerConstant(modifierPolicy, "MinimumExercisesPerMirrorCategory"),
  );
  assert.match(
    modifierPolicy,
    /FindMirrorCategoryDeficiencies[\s\S]*MirrorOnly[\s\S]*UpperBody[\s\S]*FullBody[\s\S]*BenefitsGreatly/,
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
    integerConstant(modifierPolicy, "MinimumMaterialExercises"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_PERCENT,
    integerConstant(modifierPolicy, "MinimumMaterialExercisePercent"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT,
    integerConstant(modifierPolicy, "MinimumAffectedBucketPercent"),
  );
  assert.match(
    modifierPolicy,
    /FindDistinctLineupDeficiencies[\s\S]*GetMaximumDistinctLineupSize/,
  );
  assert.match(
    workoutModule,
    /MODIFIER_RULES[\s\S]*MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP\s*=\s*5[\s\S]*findWorkoutModifierPairCoverageDeficiencies[\s\S]*findWorkoutModifierMaterialityDeficiencies/,
  );
  assert.match(
    workoutModule,
    /matchingExerciseCount:[\s\S]*MODIFIER_RULES\.every\(\(rule\)\s*=>\s*rule\.isReviewed\(exercise\)\)/,
  );
  assert.match(
    workoutModule,
    /getModifierRuleStateProfiles[\s\S]*WORKOUT_MODIFIERS\.TallMirror[\s\S]*requiresMirrorRelevance[\s\S]*isMirrorRelevant\(exercise\)/,
  );
  assert.doesNotMatch(modifierPolicy, /1\s*<<\s*Rules\.Length/);
  assert.doesNotMatch(workoutModule, /1\s*<<\s*MODIFIER_RULES\.length/);
  assert.match(
    workoutModule,
    /findWorkoutProfileLineupDeficiencies[\s\S]*getMaximumDistinctLineupSize/,
  );
  for (const heavyCatalogInvariant of [
    "findWorkoutModifierPairCoverageDeficiencies",
    "findWorkoutModifierMaterialityDeficiencies",
    "findWorkoutProfileLineupDeficiencies",
  ]) {
    assert.match(webBuild, new RegExp(heavyCatalogInvariant));
    assert.doesNotMatch(webApp, new RegExp(heavyCatalogInvariant));
  }
  assert.match(webBuild, /failedCatalogInvariants[\s\S]*Catalog failed build-time invariants/);
  assert.match(webApp, /findMirrorCategoryDeficiencies/);
  assert.match(webApp, /isModifierMetadataComplete/);
  assert.match(webApp, /isSessionMovementMetadataValid/);
  for (const heavyCatalogInvariant of [
    "FindPairwiseCoverageDeficiencies",
    "FindMaterialityDeficiencies",
    "FindDistinctLineupDeficiencies",
  ]) {
    assert.match(catalogInvariantTests, new RegExp(heavyCatalogInvariant));
    assert.doesNotMatch(exerciseDatabase, new RegExp(heavyCatalogInvariant));
  }
  assert.match(exerciseDatabase, /FindMirrorCategoryDeficiencies/);
  assert.match(exerciseModel, /ExerciseInsectCompatibility InsectCompatibility/);
  assert.match(exerciseModel, /ExerciseMirrorRelationship MirrorRelationship/);
  assert.match(exerciseModel, /ExerciseMirrorCoverage MinimumMirrorCoverage/);
  assert.ok(catalog.every((exercise) => typeof exercise.silent === "boolean"));
  assert.ok(catalog.every((exercise) =>
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "MirrorOnly" ||
    exercise.mirrorRelationship === "BenefitsGreatly" ||
    exercise.mirrorRelationship === "Agnostic"));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "MirrorOnly"
      ? exercise.equipment === "Mirror" &&
        (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
          exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody)
      : exercise.equipment === "None"));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "Agnostic"
      ? exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.None
      : exercise.minimumMirrorCoverage !== EXERCISE_MIRROR_COVERAGE.None));
  assert.ok(catalog.every((exercise) =>
    exercise.hardFloorCompatibility === "Compatible" ||
    exercise.hardFloorCompatibility === "Incompatible"));
  assert.match(webApp, /session\.startWorkout\(selectedMinutes, selectedModifiers\)/);
  assert.match(durationLayout, /@\+id\/hard_floor_modifier_button/);
  assert.match(durationLayout, /@\+id\/insect_modifier_button/);
  assert.match(durationLayout, /@\+id\/silence_modifier_button/);
  assert.match(durationLayout, /@\+id\/mirror_modifier_button/);
  assert.match(durationLayout, /@drawable\/ic_mirror/);
  assert.ok(
    durationLayout.indexOf("@+id/hard_floor_modifier_button") <
      durationLayout.indexOf("@+id/insect_modifier_button"),
  );
  assert.ok(
    webIndex.indexOf('id="hard-floor-modifier"') <
      webIndex.indexOf('id="insect-modifier"'),
  );
  assert.match(durationLayout, /@drawable\/ic_hard_floor/);
  assert.notDeepEqual(hardFloorIcon, softFloorIcon);
  assert.deepEqual([...hardFloorIcon.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  assert.deepEqual([...softFloorIcon.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  assert.match(
    mainActivity,
    /UpdateHardFloorModifierPresentation[\s\S]*Resource\.Drawable\.ic_hard_floor[\s\S]*Resource\.Drawable\.ic_soft_floor/,
  );
  assert.match(webIndex, /class="floor-glyph floor-glyph-hard"/);
  assert.match(webIndex, /class="floor-glyph floor-glyph-soft"/);
  assert.match(webStyles, /data-hard-floor="hard"[\s\S]*floor-glyph-hard[\s\S]*data-hard-floor="soft"[\s\S]*floor-glyph-soft/);
  assert.match(webStyles, /mask-image:\s*url\("\.\/assets\/ic_hard_floor\.png"\)/);
  assert.match(webStyles, /mask-image:\s*url\("\.\/assets\/ic_soft_floor\.png"\)/);
  assert.match(webBuild, /drawable-xxhdpi[\s\S]*ic_hard_floor\.png[\s\S]*ic_soft_floor\.png/);
  assert.match(webBuild, /fingerprintedName\([\s\S]*"ic_hard_floor"[\s\S]*"ic_soft_floor"/);
  assert.match(
    durationLayout,
    /mirror_modifier_button(?:(?!\/>)[\s\S])*drawableTop="@drawable\/ic_mirror"/,
  );
  assert.doesNotMatch(
    durationLayout,
    /mirror_modifier_button(?:(?!\/>)[\s\S])*foreground="@drawable\/ic_mirror"/,
  );
  assert.match(
    mainActivity,
    /UpdateMirrorModifierPresentation[\s\S]*MirrorEquipment\.Compact\s*=>\s*Resource\.Drawable\.ic_mirror_compact[\s\S]*MirrorEquipment\.Tall\s*=>\s*Resource\.Drawable\.ic_mirror_tall[\s\S]*Resource\.Drawable\.ic_mirror/,
  );
  assert.doesNotMatch(mainActivity, /_mirrorModifierButton\.Text\s*=\s*equipment\s+switch/);
  assert.notEqual(compactMirrorIcon, tallMirrorIcon);
  assert.match(compactMirrorIcon, /pathData="M12,3\.5C7\.2,3\.5/);
  assert.match(tallMirrorIcon, /pathData="M8,2\.5H16/);
  assert.match(
    webStyles,
    /data-mirror-equipment="none"[\s\S]*mirror-glyph-none[\s\S]*data-mirror-equipment="compact"[\s\S]*mirror-glyph-compact[\s\S]*data-mirror-equipment="tall"[\s\S]*mirror-glyph-tall/,
  );
  assert.match(durationLayout, /@drawable\/ic_no_clap/);
  assert.doesNotMatch(
    durationLayout,
    /silence_modifier_button(?:(?!\/>)[\s\S])*foregroundTint=/,
  );
  assert.match(webIndex, /class="modifier-icon no-clap-icon"/);
  assert.match(webIndex, /viewBox="0 0 256 256"/);
  assert.match(webIndex, /Hands-clapping silhouette adapted from Phosphor Icons/);
  assert.match(webIndex, /class="no-clap-slash-cutout"/);
  assert.match(webIndex, /class="no-clap-slash"/);
  assert.match(webStyles, /\.no-clap-slash-cutout[\s\S]*stroke-width: 34/);
  assert.match(webStyles, /\.no-clap-slash[\s\S]*stroke-width: 18/);
  assert.doesNotMatch(webIndex, /class="modifier-icon shhh-icon"/);
  assert.doesNotMatch(durationLayout, /@drawable\/ic_volume_off/);
  assert.doesNotMatch(durationLayout, /@drawable\/ic_quiet_movement/);
  assert.match(strings, /<string name="silence_modifier_description">Quiet exercise filter<\/string>/);
  assert.match(webIndex, /id="insect-modifier"/);
  assert.match(webIndex, /id="hard-floor-modifier"/);
  assert.match(webIndex, /id="silence-modifier"/);
  assert.match(webIndex, /id="mirror-modifier"/);
  assert.match(webIndex, /class="mirror-glyph mirror-glyph-compact"/);
  assert.match(webIndex, /class="mirror-glyph mirror-glyph-tall"/);
  assert.doesNotMatch(webIndex, /mirror-mode-label/);
  assert.doesNotMatch(webApp, /mirrorModeLabel/);
  assert.match(webIndex, /Quiet exercise filter: quiet exercises only/);
  assert.match(durationLayout, /@\+id\/duration_modifier_feedback/);
  assert.match(durationLayout, /@drawable\/duration_modifier_feedback_background/);
  assert.match(webIndex, /id="modifier-feedback"[\s\S]*role="status"[\s\S]*aria-live="polite"/);
  assert.match(strings, /<string name="insect_mode_enabled_feedback">insect mode ON<\/string>/);
  assert.match(strings, /<string name="insect_mode_disabled_feedback">insect mode OFF<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_enabled_feedback">noisy exercises ENABLED<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_disabled_feedback">noisy exercises DISABLED<\/string>/);
  assert.match(strings, /<string name="compact_mirror_equipment_enabled_feedback">equipment ON: compact mirror<\/string>/);
  assert.match(strings, /<string name="tall_mirror_equipment_enabled_feedback">equipment ON: tall mirror<\/string>/);
  assert.match(strings, /<string name="mirror_equipment_disabled_feedback">equipment OFF: mirror<\/string>/);
  assert.match(strings, /<string name="hard_floor_enabled_feedback">hard floor ON<\/string>/);
  assert.match(strings, /<string name="hard_floor_disabled_feedback">hard floor OFF<\/string>/);
  for (const label of [
    "hard floor ON",
    "hard floor OFF",
    "insect mode ON",
    "insect mode OFF",
    "noisy exercises ENABLED",
    "noisy exercises DISABLED",
    "equipment ON: compact mirror",
    "equipment ON: tall mirror",
    "equipment OFF: mirror",
  ]) {
    assert.match(webApp, new RegExp(label));
  }
  assert.match(mainActivity, /button\.TooltipText = GetString\([\s\S]*GetModifierFeedbackResourceId\(modifier, enabled\)/);
  assert.match(mainActivity, /GetMirrorFeedbackResourceId[\s\S]*MirrorEquipment\.Compact[\s\S]*compact_mirror_equipment_enabled_feedback[\s\S]*MirrorEquipment\.Tall[\s\S]*tall_mirror_equipment_enabled_feedback/);
  assert.match(mainActivity, /MirrorEquipment\.None\s*=>\s*MirrorEquipment\.Compact[\s\S]*MirrorEquipment\.Compact\s*=>\s*MirrorEquipment\.Tall[\s\S]*MirrorEquipment\.Tall\s*=>\s*MirrorEquipment\.None/);
  assert.match(durationLayout, /insect_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/insect_mode_disabled_feedback"/);
  assert.match(durationLayout, /hard_floor_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/hard_floor_enabled_feedback"/);
  assert.match(durationLayout, /silence_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/noisy_exercises_disabled_feedback"/);
  assert.match(durationLayout, /mirror_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/mirror_equipment_disabled_feedback"/);
  assert.match(webApp, /showWorkoutModifierFeedback\(workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(webApp, /setAttribute\("title", workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(
    instantControls,
    /name === "hardFloor"[\s\S]*Floor surface: hard floor[\s\S]*Floor surface: soft floor/,
  );
  assert.match(webApp, /cycleMirrorEquipment[\s\S]*MIRROR_EQUIPMENT\.None[\s\S]*MIRROR_EQUIPMENT\.Compact[\s\S]*MIRROR_EQUIPMENT\.Tall/);
  assert.match(
    mainActivity,
    /ModifierFeedbackEnterDurationMilliseconds\s*=\s*140L[\s\S]*ModifierFeedbackHoldMilliseconds\s*=\s*1_200L[\s\S]*ModifierFeedbackFadeDurationMilliseconds\s*=\s*700L/,
  );
  assert.match(mainActivity, /SetDuration\(ModifierFeedbackEnterDurationMilliseconds\)/);
  assert.match(mainActivity, /SetDuration\(ModifierFeedbackFadeDurationMilliseconds\)/);
  assert.match(mainActivity, /PostDelayed\([\s\S]*ModifierFeedbackHoldMilliseconds/);
  assert.match(
    webApp,
    /MODIFIER_FEEDBACK_DURATION_MS\s*=\s*2_040[\s\S]*setTimeout\([\s\S]*MODIFIER_FEEDBACK_DURATION_MS/,
  );
  assert.match(
    webStyles,
    /\.modifier-feedback\.show[\s\S]*2040ms[\s\S]*@keyframes modifier-feedback-blink[\s\S]*7%[\s\S]*11%[\s\S]*66%[\s\S]*100%[\s\S]*opacity:\s*0[\s\S]*scale\(1\.08\)/,
  );
  assert.doesNotMatch(webIndex, /M20\.24 12\.24a6 6 0 0 0-8\.49-8\.49L5 10\.5V19h8\.5Z/);
  assert.doesNotMatch(webIndex, /M3\.27 2 2 3\.27/);
  assert.match(
    exerciseDatabase,
    /DatabaseVersion\s*=\s*ExerciseDatabaseVersionPolicy\.CurrentVersion/,
  );
  assert.match(exerciseDatabaseVersionPolicy, /CurrentVersion\s*=\s*70/);
  assert.match(
    exerciseDatabase,
    /ExerciseDatabaseVersionPolicy\.IsSupportedNonDestructiveUpgrade\([\s\S]*oldVersion,[\s\S]*newVersion/,
  );
  assert.match(exerciseDatabase, /CHECK \(silent IN \(0, 1\)\)/);
  assert.match(
    exerciseDatabase,
    /hard_floor_compatibility TEXT NOT NULL[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.match(
    exerciseDatabase,
    /muscular_demand INTEGER NOT NULL[\s\S]*CHECK \(muscular_demand BETWEEN 0 AND 2\)/,
  );
  assert.match(exerciseDatabase, /max_space_meters > 0 AND max_space_meters <= 2/);
  assert.match(exerciseDatabase, /equipment IN \('None', 'Mirror'\)/);
  assert.match(exerciseDatabase, /mirror_relationship TEXT NOT NULL/);
  assert.match(exerciseDatabase, /mirror_coverage TEXT NOT NULL/);
  assert.match(
    exerciseDatabase,
    /session_movement_id INTEGER NOT NULL DEFAULT 0[\s\S]*CHECK \(session_movement_id >= 0\)/,
  );
  assert.match(exerciseDatabase, /values\.Put\("session_movement_id"/);
  assert.match(
    exerciseDatabase,
    /ScreenLeftLeadThenRightLead[\s\S]*ScreenRightLeadThenLeftLead/,
  );
});

test("muscular demand is a separate reviewed catalog score on both platforms", () => {
  assert.equal(MINIMUM_MUSCULAR_DEMAND, 0);
  assert.equal(MAXIMUM_MUSCULAR_DEMAND, 2);
  assert.match(exerciseModel, /MinimumMuscularDemand\s*=\s*0/);
  assert.match(exerciseModel, /MaximumMuscularDemand\s*=\s*2/);
  assert.match(exerciseModel, /int MuscularDemand/);
  assert.match(exerciseModel, /int Score/);
  assert.match(workoutModule, /hasReviewedMuscularDemand/);
  assert.ok(catalog.every((exercise) =>
    Number.isInteger(exercise.muscularDemand) &&
    exercise.muscularDemand >= MINIMUM_MUSCULAR_DEMAND &&
    exercise.muscularDemand <= MAXIMUM_MUSCULAR_DEMAND));
  assert.ok(catalog.every((exercise) => exercise.score === 0));
});

test("web and mobile apply rolling muscular recovery by primary muscle", () => {
  assert.equal(MODERATE_MUSCULAR_DEMAND, 1);
  assert.equal(HARD_MUSCULAR_DEMAND, MAXIMUM_MUSCULAR_DEMAND);
  assert.equal(MODERATE_RECOVERY_WINDOW_MS, 18 * 60 * 60 * 1000);
  assert.equal(HARD_RECOVERY_WINDOW_MS, 36 * 60 * 60 * 1000);
  assert.deepEqual(HARD_ROTATION_STATUS, {
    RecoveringHard: "RecoveringHard",
    Neutral: "Neutral",
    FreshHard: "FreshHard",
  });
  assert.match(
    recoveryPolicy,
    /ModerateMuscularDemand\s*=\s*Exercise\.ModerateMuscularDemand/,
  );
  assert.match(
    recoveryPolicy,
    /HardMuscularDemand\s*=\s*Exercise\.MaximumMuscularDemand/,
  );
  assert.match(recoveryPolicy, /ModerateRecoveryWindowMilliseconds[\s\S]*18L/);
  assert.match(recoveryPolicy, /HardRecoveryWindowMilliseconds[\s\S]*36L/);
  assert.match(
    workoutState,
    /Dictionary<string, long>[\s\S]*LastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(
    workoutState,
    /Dictionary<string, long>[\s\S]*LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.doesNotMatch(workoutState, /LastKeptLocalDateByExerciseId/);
  assert.doesNotMatch(workoutState, /ActiveRecoveryExcludedExerciseIds/);
  assert.match(
    recoveryPolicy,
    /GetRotationStatus[\s\S]*IsPrimaryMuscleRecovering[\s\S]*HardExerciseRotationStatus\.FreshHard/,
  );
  assert.match(
    workoutModule,
    /getHardRotationStatus[\s\S]*isPrimaryMuscleRecovering[\s\S]*HARD_ROTATION_STATUS\.FreshHard/,
  );
  assert.match(
    recoveryPolicy,
    /IsModerateExerciseRecovering[\s\S]*IsPrimaryMuscleInModerateRecovery/,
  );
  assert.match(
    workoutModule,
    /isModerateExerciseRecovering[\s\S]*isPrimaryMuscleInModerateRecovery/,
  );
  assert.match(
    sessionService,
    /BeginRest\([\s\S]*RecordCompletedMuscularWork\([\s\S]*LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[\s\S]*LastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(
    workoutModule,
    /beginRest\([\s\S]*lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[\s\S]*lastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(mainActivity, /_sessionService\.BeginRest\(/);
  assert.match(
    sessionService,
    /mirrorPreferenceWeight[\s\S]*moderateRecoveryAvoidanceWeight[\s\S]*hardRecoveryAvoidanceWeight[\s\S]*freshHardWeight[\s\S]*scoreWeight/,
  );
  assert.match(
    workoutModule,
    /mirrorPreferenceWeight[\s\S]*moderateRecoveryAvoidanceWeight[\s\S]*hardRecoveryAvoidanceWeight[\s\S]*freshHardWeight[\s\S]*scoreWeight/,
  );
  assert.match(
    sessionService,
    /var utilities = new BigInteger\[groups\.Count, candidates\.Count\][\s\S]*utilitiesByGroup/,
  );
  assert.match(
    workoutModule,
    /const utilities = groups\.map\(\(\) => candidates\.map\(\(\) => 0n\)\)[\s\S]*utilitiesByGroup/,
  );
});

test("runtime media and the deployable web shell are content-addressed", () => {
  assert.match(mainActivity, /SHA256\.HashData/);
  assert.match(mainActivity, /assetFingerprint/);
  assert.match(webApp, /data\/exercises\.json[\s\S]*data\/asset-versions\.json/);
  assert.doesNotMatch(webApp, /cache:\s*"no-store"/);
  assert.match(webApp, /assetVersions\[path\][\s\S]*searchParams\.set\("v", fingerprint\)/);
  assert.match(webBuild, /createHash\("sha256"\)/);
  assert.match(
    webBuild,
    /catalogVersion[\s\S]*assetVersionsVersion[\s\S]*exercises\.json\?v=\$\{catalogVersion\}[\s\S]*asset-versions\.json\?v=\$\{assetVersionsVersion\}/,
  );
  assert.match(webBuild, /fingerprintedName\("workout", "js"/);
  assert.match(webBuild, /fingerprintedName\("app", "js"/);
  assert.match(webBuild, /fingerprintedName\("styles", "css"/);
  assert.match(webBuild, /from \"\.\/\$\{workoutOutputName\}\"/);
  assert.match(webBuild, /replace\('\.\/styles\.css'/);
  assert.match(webBuild, /replace\('\.\/app\.js'/);
  assert.match(
    webIndex,
    /rel="preload" href="\.\/data\/exercises\.json"[\s\S]*rel="preload" href="\.\/data\/asset-versions\.json"/,
  );
});

test("movement and rest phases use pronounced accents across the surface, media, and actions", () => {
  for (const [name, value] of [
    ["move_surface", "#F0C7CC"],
    ["move_text", "#681E27"],
    ["move_accent", "#A42E3A"],
    ["move_track", "#D9959E"],
    ["rest_surface", "#CBE1F2"],
    ["rest_text", "#194F77"],
    ["rest_accent", "#2D6F9F"],
    ["rest_track", "#98C5E3"],
  ]) {
    assert.match(androidColors, new RegExp(`<color name="${name}">${value}<\\/color>`));
    assert.match(webStyles, new RegExp(`--${name.replaceAll("_", "-")}: ${value.toLowerCase()}`));
  }

  assert.match(
    mainActivity,
    /RenderSplitWorkoutPhase[\s\S]*SetExerciseMediaPhase\(resting: false\)/,
  );
  assert.match(
    mainActivity,
    /SetExerciseMediaPhase[\s\S]*media_card_rest_background[\s\S]*media_card_move_background/,
  );
  assert.match(mainActivity, /phase_rest_chip_background/);
  assert.match(mainActivity, /phase_move_chip_background/);
  assert.match(
    androidStyles,
    /FluxKeepButton[\s\S]*@drawable\/rest_button_background/,
  );
  assert.match(
    workoutLayout,
    /<ImageButton[\s\S]*@\+id\/keep_button[\s\S]*@drawable\/ic_love[\s\S]*@color\/white/,
  );
  assert.match(
    workoutLayout,
    /<ImageButton[\s\S]*@\+id\/rest_playback_action[\s\S]*@string\/pause_rest_description[\s\S]*@drawable\/ic_phase_pause/,
  );
  assert.match(
    webIndex,
    /<button[\s\S]*id="toggle-rest"[\s\S]*data-state="playing"[\s\S]*rest-pause-icon[\s\S]*rest-play-icon/,
  );
  assert.match(mainActivity, /ToggleRestPlayback/);
  assert.match(mainActivity, /_sessionService\.PauseRest/);
  assert.match(mainActivity, /_sessionService\.ResumeRest/);
  assert.match(mainActivity, /UpdateRestPlaybackActionVisual/);
  assert.match(webApp, /toggleRestPlayback/);
  assert.match(webApp, /session\.pauseRest/);
  assert.match(webApp, /session\.resumeRest/);
  assert.match(webApp, /renderRestPlaybackToggle/);
  assert.match(
    mainActivity,
    /PendingRestKept[\s\S]*rest_button_background[\s\S]*SetColorFilter[\s\S]*Resource\.Color\.white/,
  );
  assert.match(
    webApp,
    /setFullPhaseSurface[\s\S]*setWorkoutPhaseClass\(kind\)[\s\S]*classList\.toggle\("phase-move"[\s\S]*classList\.toggle\("phase-rest"/,
  );
  assert.match(
    webStyles,
    /\.workout-screen\.phase-move \.exercise-media-card[\s\S]*\.workout-screen\.phase-rest \.exercise-media-card[\s\S]*\.move-panel\.change \.media-control[\s\S]*\.keep-button/,
  );
});

test("literal work-block timelines and logical exercise progress match", () => {
  const fullNeckCircles = catalog.find((exercise) => exercise.id === 409);
  assert.equal(fullNeckCircles.name, "Full Neck Circles");
  assert.equal(fullNeckCircles.sequenceBlocks.length, 2);
  assert.match(workoutLayout, /@\+id\/execution_signifier/);
  assert.doesNotMatch(workoutLayout, /sequence_signifier_icon|set_signifier_icon/);
  assert.match(webIndex, /id="workout-progress-text"[\s\S]*id="execution-signifier"[\s\S]*id="exercise-name"[\s\S]*id="exercise-media-card"/);
  assert.match(webIndex, /id="execution-playhead"[\s\S]*id="execution-block-track"/);
  assert.doesNotMatch(webIndex, /sequence-signifier-icon|set-signifier-icon/);
  assert.doesNotMatch(webIndex, /unilateral-signifier|bidirectional-signifier/);
  assert.match(
    mainActivity,
    /WorkoutDisplayPolicy\.GetProgress[\s\S]*RenderExecutionTimeline\(\)/,
  );
  const androidTimeline = methodBody(
    mainActivity,
    "private void RenderExecutionTimeline(",
    "private void AnimateExerciseChange(",
  );
  assert.match(
    androidTimeline,
    /WorkoutDisplayPolicy\.GetTimeline[\s\S]*SetTimeline[\s\S]*Work block/,
  );
  assert.match(
    mainActivity,
    /ShowRestPanel\(\)[\s\S]*RenderExecutionTimeline\([\s\S]*selectUpcomingBlock/,
  );
  assert.match(
    workoutDisplayPolicy,
    /Distinct\(StringComparer\.Ordinal\)[\s\S]*GetTimeline[\s\S]*UsesThreeDistinctExercisePalette/,
  );
  assert.match(
    workoutDisplayPolicy,
    /SequenceBlockCount == 3[\s\S]*ExerciseOverrideId > 0[\s\S]*GetAccent\(group\) == WorkoutBlockAccent\.Neutral[\s\S]*Distinct\(\)[\s\S]*Count\(\) == 3/,
  );
  assert.match(
    workoutDisplayPolicy,
    /ScreenRight[\s\S]*Blue[\s\S]*ScreenLeft[\s\S]*Red[\s\S]*Neutral/,
  );
  assert.match(
    workoutTimelineView,
    /for \(int index = 0; index < _blocks\.Length; index\+\+\)[\s\S]*DrawRoundRect[\s\S]*_currentBlockIndex[\s\S]*DrawPath/,
  );
  const webTimeline = methodBody(
    webApp,
    "function renderExecutionTimeline(",
    "function showReadyPanel()",
  );
  assert.match(
    webTimeline,
    /getWorkoutExecutionTimeline[\s\S]*execution-work-block[\s\S]*executionPlayhead\.style\.left/,
  );
  assert.match(
    webApp,
    /getWorkoutDisplayProgress\([\s\S]*Exercise \$\{position\} of \$\{total\}/,
  );
  assert.match(
    workoutModule,
    /getWorkoutDisplayProgress[\s\S]*getWorkoutExecutionTimeline[\s\S]*usesThreeDistinctExercisePalette[\s\S]*getThreeDistinctExerciseAccent[\s\S]*getWorkoutBlockAccent/,
  );
  assert.match(webStyles, /\.execution-block-track[\s\S]*grid-template-columns[\s\S]*\.execution-work-block\.blue[\s\S]*var\(--rest-accent\)[\s\S]*\.execution-work-block\.red[\s\S]*var\(--move-accent\)[\s\S]*\.execution-work-block\.neutral[\s\S]*var\(--chartreuse\)/);
  assert.match(webStyles, /\.execution-playhead[\s\S]*border-top: 7px solid var\(--graphite\)/);
  assert.doesNotMatch(workoutLayout, /side_phase_label|countdown_phase_icon/);
  assert.doesNotMatch(mainActivity, /_sidePhaseLabel|_countdownPhaseIcon|GetMovementCueIcon/);
  assert.doesNotMatch(webIndex, /side-phase-label|movement-cue|BIDIRECTIONAL|UNILATERAL/);
  assert.doesNotMatch(
    webApp,
    /sidePhaseLabel|elements\.movementCue|function cueSymbol|BIDIRECTIONAL|UNILATERAL/,
  );
  assert.doesNotMatch(webStyles, /\.side-phase-label|\.movement-cue/);
  assert.doesNotMatch(workoutLayout, /two_sided_badge|ic_two_sides/);
  assert.doesNotMatch(mainActivity, /_twoSidedBadge/);
  assert.doesNotMatch(webIndex, /two-sided-badge|BOTH SIDES/);
  assert.doesNotMatch(webApp, /twoSidedBadge/);
  assert.doesNotMatch(webStyles, /\.two-sided-(?:badge|icon)/);
});

test("Android device builds embed their managed assemblies", async () => {
  const project = await source("Flux", "Flux.csproj");
  assert.match(project, /<EmbedAssembliesIntoApk>true<\/EmbedAssembliesIntoApk>/);
});

test("duration controls do not wait for catalog startup on either platform", () => {
  const mobileCreate = methodBody(
    mainActivity,
    "protected override void OnCreate(Bundle? savedInstanceState)",
    "private async Task InitializeApplicationAsync()",
  );
  const mobileStart = methodBody(
    mainActivity,
    "private void StartSelectedWorkout()",
    "private static void RecoverPendingScoreUpdate(",
  );
  assert.match(
    mobileCreate,
    /_stateStore\s*=\s*new SharedPreferencesWorkoutStateStore[\s\S]*_state\s*=\s*_stateStore\.Load\(\)[\s\S]*ShowDurationSelection\(\)[\s\S]*InitializeApplicationAsync\(\)/,
  );
  assert.doesNotMatch(mobileCreate, /new SqliteExerciseDatabase|\.Exercises/);
  assert.match(
    mainActivity,
    /Task\.Run\(\(\) => InitializeApplication\(context\)\)[\s\S]*CompleteApplicationStartup/,
  );
  assert.match(
    mobileStart,
    /!_applicationStartupCompleted[\s\S]*_startWorkoutWhenReady\s*=\s*true[\s\S]*_beginWorkoutButton\.Enabled\s*=\s*false/,
  );

  assert.match(
    webIndex,
    /id="begin-workout"(?:(?!disabled)[\s\S])*?<\/button>/,
  );
  assert.match(webIndex, /<script src="\.\/instant-controls\.js"><\/script>/);
  assert.match(
    instantControls,
    /flux-controls-ready[\s\S]*durationDecrease|duration-decrease[\s\S]*requestStart/,
  );
  assert.match(
    instantControls,
    /startQueued\s*=\s*true[\s\S]*elements\.begin\.disabled\s*=\s*true[\s\S]*startRequested/,
  );
  assert.match(
    webApp,
    /startupControls\.connect[\s\S]*startRequested:\s*startWorkout[\s\S]*if \(!session\)[\s\S]*startWorkoutWhenReady\s*=\s*true/,
  );
  assert.match(
    webBuild,
    /instant-controls\.js[\s\S]*instantControlsSource[\s\S]*<script>/,
  );
});

test("Android persistence rejects malformed stored shapes without crashing launch", async () => {
  const stateStore = await source(
    "Flux",
    "Data",
    "SharedPreferencesWorkoutStateStore.cs",
  );
  assert.match(
    stateStore,
    /RootElement\.ValueKind != JsonValueKind\.Object[\s\S]*return new WorkoutState\(\)/,
  );
  assert.match(
    stateStore,
    /versionElement\.ValueKind != JsonValueKind\.Number[\s\S]*!versionElement\.TryGetInt32\(out version\)[\s\S]*return new WorkoutState\(\)/,
  );
  assert.match(stateStore, /catch \(JsonException\)/);
});

test("workout transport controls are functional and muscle labels stay hidden", async () => {
  const workoutLayout = await source("Flux", "Resources", "layout", "screen_workout.xml");
  const startControl = workoutLayout.match(
    /<ImageButton[\s\S]*?android:id="@\+id\/start_button"[\s\S]*?\/>/,
  )?.[0] ?? "";
  assert.match(startControl, /@drawable\/ic_phase_active/);
  assert.doesNotMatch(startControl, /android:text=/);
  assert.match(workoutLayout, /@\+id\/shuffle_button[\s\S]*@drawable\/ic_shuffle/);
  assert.match(workoutLayout, /@\+id\/repeat_action[\s\S]*@drawable\/ic_repeat/);
  assert.match(workoutLayout, /@\+id\/playback_action[\s\S]*@drawable\/ic_phase_pause/);
  assert.match(workoutLayout, /@\+id\/next_action[\s\S]*@drawable\/ic_next/);
  assert.doesNotMatch(workoutLayout, /workout_group_name|muscle_chip_background|skip_action/);
  assert.doesNotMatch(webIndex, /workout-group-name|skip-exercise|>\s*Start\s*</);
  assert.match(webIndex, /id="shuffle-exercise"[\s\S]*id="start-movement"/);
  assert.match(webIndex, /id="start-movement"[\s\S]*start-playback-icon/);
  assert.match(webIndex, /id="repeat-exercise"[\s\S]*id="toggle-playback"[\s\S]*id="next-exercise"/);

  const mobilePause = methodBody(
    mainActivity,
    "private void TogglePlayback()",
    "private void RepeatExercise()",
  );
  const mobileRepeat = methodBody(
    mainActivity,
    "private void RepeatExercise()",
    "private void GoToNextExercise()",
  );
  const mobileNext = methodBody(
    mainActivity,
    "private void GoToNextExercise()",
    "private void CompleteCountdown()",
  );
  assert.match(mobilePause, /ResumeCountdown\(\)/);
  assert.match(mobilePause, /_countdownPausedByUser = true[\s\S]*PauseCountdown\(\)/);
  assert.match(mobileRepeat, /StopCountdownTimer\(\)[\s\S]*StartCountdownTimer\(GetCurrentCountdownDurationMilliseconds\(\)\)/);
  assert.doesNotMatch(mobileRepeat, /FinalizeCurrentRound|RecordOutcome/);
  assert.match(
    mobileNext,
    /RejectCurrentSequenceWithScoreUpdates[\s\S]*SaveStateAndScores/,
  );
  assert.match(mobileNext, /!_countdownActive && !_countdownPaused/);
  const mobileAvailability = methodBody(
    mainActivity,
    "private void SetPlaybackControlsAvailability(bool available)",
    "private static void SetPlaybackControlAvailability(",
  );
  assert.match(mobileAvailability, /_repeatAction, available/);
  assert.match(mobileAvailability, /_playbackAction, available/);
  assert.match(
    mobileAvailability,
    /_workoutPhase == WorkoutPhase\.Move[\s\S]*_countdownActive \|\| _countdownPaused[\s\S]*_nextAction, nextAvailable/,
  );
  const mobileShuffle = methodBody(
    mainActivity,
    "private void ShuffleCurrentExercise()",
    "private void StartCountdown()",
  );
  assert.match(mobileShuffle, /ShuffleNextExercise[\s\S]*SaveStateAndScores[\s\S]*ShowNextExercise/);
  assert.doesNotMatch(mobileShuffle, /FinalizeCurrentRound|RecordOutcome/);
  assert.match(mainActivity, /_repeatAction\.Click[\s\S]*_playbackAction\.Click[\s\S]*_nextAction\.Click/);

  const webPause = methodBody(
    webApp,
    "function toggleMovementPlayback()",
    "function repeatMovement()",
  );
  const webRepeat = methodBody(
    webApp,
    "function repeatMovement()",
    "function goToNextExercise()",
  );
  const webNext = methodBody(
    webApp,
    "function goToNextExercise()",
    "function completeMovement()",
  );
  assert.match(webPause, /resumeMovement\(\)/);
  assert.match(webPause, /pauseMovement\("user"\)/);
  assert.match(webRepeat, /getMovementCountdownDurationMs\(currentGroup\)[\s\S]*setMovementDeadline\(movementRemaining\)/);
  assert.doesNotMatch(webRepeat, /recordOutcome|persistState/);
  assert.match(webNext, /rejectCurrentSequence\(currentGroup\)[\s\S]*persistState/);
  assert.match(webNext, /!movementRunning && !movementPauseReason/);
  const webAvailability = methodBody(
    webApp,
    "function setPlaybackControlsEnabled(enabled)",
    "function renderPlaybackToggle()",
  );
  assert.match(webAvailability, /repeatExercise\.disabled = !enabled/);
  assert.match(webAvailability, /playbackToggle\.disabled = !enabled/);
  assert.match(
    webAvailability,
    /nextExercise\.disabled = !movementRunning && !movementPauseReason/,
  );
  const webShuffle = methodBody(
    webApp,
    "function shuffleCurrentExercise()",
    "function showMovePanel()",
  );
  assert.match(webShuffle, /shuffleNextExercise\(currentGroup\)[\s\S]*persistState\(\)[\s\S]*showNextExercise\(\)/);
  assert.doesNotMatch(webShuffle, /recordOutcome/);
  assert.match(webApp, /repeatExercise\.addEventListener[\s\S]*playbackToggle\.addEventListener[\s\S]*nextExercise\.addEventListener/);
  assert.match(
    workoutModule,
    /shuffleNextExercise\(group\)[\s\S]*getCompatibleShuffleCandidates[\s\S]*setScore\(exercise, this\.getScore\(exercise\) - 1\)/,
  );
  assert.match(
    sessionService,
    /Shuffle\(candidates\);[\s\S]*ShuffleCandidate selected = candidates\[0\]/,
  );
  assert.match(
    workoutModule,
    /this\.shuffle\(candidates\);[\s\S]*const selected = candidates\[0\]/,
  );
  assert.match(
    sessionService,
    /ChooseLongWorkoutAllocation\([\s\S]*startedSelectionGroupIds/,
  );
  assert.match(
    workoutModule,
    /chooseLongWorkoutAllocation\(startedSelectionGroupIds\)/,
  );
  assert.doesNotMatch(mainActivity, /_workoutGroupName/);
  assert.doesNotMatch(webApp, /workoutGroupName/);
});

test("active movement checkpoints and invalid media recovery match across platforms", async () => {
  const [stateStoreContract, stateStore] = await Promise.all([
    source("Flux", "Data", "IWorkoutStateStore.cs"),
    source("Flux", "Data", "SharedPreferencesWorkoutStateStore.cs"),
  ]);
  assert.match(workoutState, /PendingMovementGroupId/);
  assert.match(workoutState, /PendingMovementMillisecondsRemaining/);
  assert.match(workoutState, /PendingMovementEndsAtUnixMilliseconds/);
  assert.match(workoutState, /PendingMovementPausedByUser/);
  assert.match(workoutModule, /pendingMovementGroupId:\s*null/);
  assert.match(workoutModule, /pendingMovementMillisecondsRemaining:\s*0/);
  assert.match(workoutModule, /pendingMovementEndsAtUnixMilliseconds:\s*0/);
  assert.match(workoutModule, /pendingMovementPausedByUser:\s*false/);

  const mobileCreate = methodBody(
    mainActivity,
    "protected override void OnCreate(Bundle? savedInstanceState)",
    "protected override void OnResume()",
  );
  const mobilePause = methodBody(
    mainActivity,
    "private void PauseCountdown()",
    "private void ResumeCountdown()",
  );
  const mobileStart = methodBody(
    mainActivity,
    "private void StartCountdownTimer(long millisecondsRemaining)",
    "private void UpdateMoveCountdown(long millisecondsRemaining)",
  );
  const mobileDirectionGuard = methodBody(
    mainActivity,
    "private void EnforceDirectionMediaSegment(MovementPhase phase)",
    "private int GetCurrentMediaSegmentStartMilliseconds()",
  );
  assert.match(
    mobileCreate,
    /GetPendingMovementGroup[\s\S]*GetPendingRestGroup[\s\S]*pendingMovementGroup is null[\s\S]*pendingRestGroup is null[\s\S]*FinishInterruptedWorkout[\s\S]*RestorePendingMovement[\s\S]*RestorePendingRest/,
  );
  assert.match(stateStoreContract, /void SaveDeferred\(WorkoutState state\)/);
  assert.match(
    stateStore,
    /SaveDeferred\(WorkoutState state\)[\s\S]*CreateEditor\(state\)\.Apply\(\)/,
  );
  assert.match(
    stateStore,
    /public void Save\(WorkoutState state\)[\s\S]*editor\.Commit\(\)/,
  );
  assert.match(mobilePause, /PauseMovement[\s\S]*_stateStore\.Save\(_state\)/);
  assert.match(
    mobileStart,
    /BeginMovement[\s\S]*_stateStore\.SaveDeferred\(_state\)/,
  );
  assert.match(
    mobileDirectionGuard,
    /CurrentPosition[\s\S]*catch \(Java\.Lang\.IllegalStateException\)[\s\S]*RecoverInvalidMediaPlayerState/,
  );

  assert.match(
    workoutModule,
    /initialize\(\)[\s\S]*normalizePendingRest\(\)[\s\S]*normalizePendingMovement\(\)[\s\S]*getPendingRestGroup\(\)[\s\S]*getPendingMovementGroup\(\)/,
  );
  assert.match(
    webApp,
    /getPendingRestGroup\(\)[\s\S]*restorePendingRest\(\)[\s\S]*getPendingMovementGroup\(\)[\s\S]*restorePendingMovement\(\)/,
  );
  assert.match(
    webApp,
    /pendingRestGroup = session\.getPendingRestGroup\(\)[\s\S]*pendingMovementGroup = session\.getPendingMovementGroup\(\)[\s\S]*activeWorkoutMinutes !== 0[\s\S]*!pendingRestGroup[\s\S]*!pendingMovementGroup[\s\S]*session\.finishInterruptedWorkout\(\)/,
  );
  assert.match(
    mainActivity,
    /RestorePendingRest\([\s\S]*ShowNextExercise\(\)[\s\S]*_restActive = true[\s\S]*ShowRestPanel\(\)[\s\S]*ResumeRestCountdown\(\)/,
  );
  assert.match(
    webApp,
    /function restorePendingRest\([\s\S]*showNextExercise\(\)[\s\S]*restActive = true[\s\S]*showRestPanel\(\)[\s\S]*startRestTimer\(\)/,
  );
  assert.match(
    webApp,
    /function setMovementDeadline[\s\S]*session\.beginMovement[\s\S]*persistState\(\)/,
  );
  assert.match(
    webApp,
    /function pauseMovement[\s\S]*session\.pauseMovement[\s\S]*persistState\(\)/,
  );
  assert.match(webApp, /visibilitychange[\s\S]*pagehide/);
});

test("lead-stance exercises use the same two-block sequence cues on mobile and web", () => {
  const expectedLeadStanceIds = [
    265, 274, 280, 287, 473, 575, 578, 583, 591, 884, 885, 886, 887,
  ];
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.sideSequence.includes("LeadThen"))
      .map((exercise) => exercise.id),
    expectedLeadStanceIds,
  );
  for (const exercise of catalog.filter((item) => expectedLeadStanceIds.includes(item.id))) {
    assert.equal(exercise.sequenceBlocks.length, 2);
    assert.deepEqual(
      exercise.sequenceBlocks.map((block) => block.sideCue),
      ["ShownLeadStance", "OppositeLeadStance"],
    );
  }
  assert.match(
    sequenceBlockModel,
    /ShownLeadStance[\s\S]*OppositeLeadStance/,
  );
  assert.match(
    movementPresentationPolicy,
    /GetPresentation\([\s\S]*ExerciseSequenceSideCue sideCue[\s\S]*ExerciseSequenceDirectionCue directionCue[\s\S]*bool mirrorMedia[\s\S]*return new MovementPhasePresentation\([\s\S]*sideCue,[\s\S]*directionCue,[\s\S]*mirrorMedia/,
  );
  assert.match(workoutModule, /ShownLeadStance/);
  assert.match(workoutModule, /OppositeLeadStance/);
  assert.match(
    mainActivity,
    /ShownLeadStance => "Shown lead stance"[\s\S]*OppositeLeadStance => "Opposite lead stance"/,
  );
  assert.match(
    webApp,
    /ShownLeadStance:[\s\S]*"Shown lead stance"[\s\S]*OppositeLeadStance:[\s\S]*"Opposite lead stance"/,
  );
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
  assert.equal(
    PREPARATION_DURATION_MS / 1000,
    integerConstant(movementSchedule, "PreparationDurationSeconds"),
  );
  assert.doesNotMatch(
    movementSchedule,
    /SideDurationSeconds|SideChangeDurationSeconds|FullSide/,
  );
  assert.match(
    mainActivity,
    /GetCountdownDurationSeconds\([\s\S]*includePreparation: !_sessionService\.IsSequenceContinuationBlock/,
  );
  assert.match(
    webApp,
    /getMovementPhaseState\([\s\S]*!session\.isSequenceContinuationBlock/,
  );
  assert.equal(
    REST_DURATION_MS / 1000,
    integerConstant(mainActivity, "RestSeconds"),
  );
});

test("web and mobile separate the exercise whistle from the final completion cue", () => {
  const mobileStart = methodBody(mainActivity, "private void StartCountdown()", "private void TogglePlayback()");
  const webStart = methodBody(webApp, "function startMovement()", "function setMovementDeadline(");
  assert.doesNotMatch(mobileStart, /PlayWhistleCue/);
  assert.doesNotMatch(webStart, /playSound/);
  assert.match(
    mainActivity,
    /previousPhase is null or MovementPhase\.Preparation[\s\S]*CueMovementRestart\(\)/,
  );
  assert.match(
    webApp,
    /previousPhase === "Preparation"[\s\S]*playSound\("start"\)/,
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
  const mobileCueBodies = [
    methodBody(mainActivity, "private void CueMovementRestart()", "private void RenderCountdownPhase("),
    methodBody(mainActivity, "private void CompleteCountdown()", "private void CancelCountdown("),
    methodBody(mainActivity, "private void FinalizeCurrentRound(bool keep)", "private void ShowCongratulations()"),
    methodBody(mainActivity, "private void PlayWhistleCue(int soundId)", "[SuppressMessage("),
  ];
  const webCueBodies = [
    methodBody(webApp, "function applyMovementPhase(phase)", "function restartMediaForPhase("),
    methodBody(webApp, "function completeMovement()", "function startRestTimer()"),
    methodBody(webApp, "function showCompletion(playCue)", "function closeCompletion()"),
    methodBody(webApp, "function playSound(name)", "function handleVisibilityChange()"),
  ];
  for (const body of mobileCueBodies) {
    assert.doesNotMatch(body, /WorkoutModifiers\.Silence|_selectedWorkoutModifiers/);
  }
  for (const body of webCueBodies) {
    assert.doesNotMatch(body, /WORKOUT_MODIFIERS\.Silence|selectedModifiers/);
  }
});

test("all bilateral, directional, linked, and repeated work uses one sequence model", () => {
  assert.match(exerciseModel, /ExerciseSequenceBlock\[\] SequenceBlocks/);
  assert.doesNotMatch(exerciseModel, /DirectionPartnerExerciseId/);
  assert.match(
    sequenceBlockModel,
    /ExerciseSequenceSideCue[\s\S]*ExerciseSequenceDirectionCue[\s\S]*ExerciseSequenceMediaSegment[\s\S]*ExerciseId[\s\S]*MirrorMedia/,
  );
  assert.match(
    workoutGroup,
    /SequenceBlockIndex[\s\S]*SequenceBlockCount[\s\S]*SetNumber[\s\S]*SetCount[\s\S]*IsFinalSequenceRound/,
  );
  assert.doesNotMatch(workoutGroup, /PairedRoundId|IsPairDecisionRound/);
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.directionSequence !== "None")
      .map((exercise) => exercise.id),
    [264, 275, 406, 409, 460, 588, 608, 611, 743],
  );
  assert.ok(catalog.every((exercise) =>
    !Object.hasOwn(exercise, "directionPartnerExerciseId")));
  const ownerByExerciseId = new Map();
  for (const root of catalog.filter((exercise) => exercise.sequenceBlocks.length > 0)) {
    for (const exerciseId of new Set(root.sequenceBlocks.map((block) => block.exerciseId))) {
      assert.equal(ownerByExerciseId.has(exerciseId), false);
      ownerByExerciseId.set(exerciseId, root.id);
    }
  }
  assert.equal(ownerByExerciseId.size, catalog.length);
  assert.deepEqual(
    catalog
      .filter((root) => new Set(root.sequenceBlocks.map((block) => block.exerciseId)).size > 1)
      .map((root) => root.id),
    [
      96, 104, 113, 115, 120, 123, 143, 160, 177, 178, 179, 180, 181,
      211, 214, 220, 223, 252, 261, 264, 285, 286, 288, 292, 327, 329,
      367, 392, 393, 414, 415, 420, 459, 465, 491, 500, 502, 566, 610, 612,
      617, 742, 784, 834, 845, 910, 948, 996,
    ],
  );
});

test("atomic sequences are adjacent units that may satisfy multiple primary slots", () => {
  assert.match(workoutState, /Dictionary<string, int> ActiveSetCountsBySelectionGroupId/);
  assert.doesNotMatch(
    sessionService,
    /state\.ActiveWorkoutMinutes <= 30 &&[\s\S]*exercise\.SequenceBlocks\.Length > 1/,
  );
  assert.doesNotMatch(
    workoutModule,
    /workoutMinutes !== null && workoutMinutes <= 30[\s\S]*exercise\.sequenceBlocks\.length > 1/,
  );
  assert.match(
    workoutSequencePolicy,
    /GetPrimaryCoverageGroups[\s\S]*member\.PrimaryCanonicalGroup[\s\S]*coveredGroupIds\.Add/,
  );
  assert.match(
    atomicSequenceLineupSolver,
    /CoverageMask[\s\S]*BlockCount[\s\S]*workoutMinutes[\s\S]*ReduceToBlockCapacity/,
  );
  assert.match(
    sessionService,
    /candidate\.SequenceBlocks\.Length \+[\s\S]*groups\.Count - placementGroups\.Length[\s\S]*state\.ActiveWorkoutMinutes/,
  );
  assert.match(
    workoutModule,
    /candidate\.sequenceBlocks\.length \+[\s\S]*groups\.length - placementGroups\.length[\s\S]*this\.state\.activeWorkoutMinutes/,
  );
  assert.match(
    sessionService,
    /OrderBy\(placement => setCounts\[placement\.Anchor\.Id\]\)[\s\S]*ThenByDescending\(placement =>[\s\S]*blockCostByGroup\[placement\.Anchor\.Id\] == 1\)/,
  );
  assert.match(
    workoutModule,
    /setCountsBySelectionGroupId\.get\(left\.anchor\.id\) -[\s\S]*setCountsBySelectionGroupId\.get\(right\.anchor\.id\) \|\|[\s\S]*blockCostByGroup\.get\(right\.anchor\.id\) === 1[\s\S]*blockCostByGroup\.get\(left\.anchor\.id\) === 1/,
  );
  assert.match(
    sessionService,
    /selectedGroupsByRootId[\s\S]*GetSequencePlacementOptions[\s\S]*CoveredGroups/,
  );
  assert.match(
    workoutModule,
    /selectedGroupsByRootId[\s\S]*getSequencePlacementOptions[\s\S]*coveredGroups/,
  );
  assert.match(
    sessionService,
    /for \(int setNumber = 1;[\s\S]*for \(int blockIndex = 0;[\s\S]*\.set\{setNumber\}\.[\s\S]*block\{blockIndex \+ 1\}/,
  );
  assert.match(
    workoutModule,
    /for \(let setNumber = 1;[\s\S]*for \(let blockIndex = 0;[\s\S]*\.set\$\{setNumber\}\.block\$\{blockIndex \+ 1\}/,
  );
  assert.match(
    sessionService,
    /if \(!group\.IsFinalSequenceRound\)[\s\S]*only be rated after its final block/,
  );
  assert.match(
    workoutModule,
    /if \(!isFinalSequenceRound\(group\)\)[\s\S]*only be rated after its final block/,
  );
  assert.match(
    sessionService,
    /ApplySequenceOutcome[\s\S]*GetSequenceExercises[\s\S]*LastKeptExerciseIds/,
  );
  assert.match(
    workoutModule,
    /applySequenceOutcome[\s\S]*getSequenceExercises[\s\S]*lastKeptExerciseIds/,
  );
  assert.match(
    mainActivity,
    /IsIntermediateSequenceBlock[\s\S]*_keepButton\.Visibility = ViewStates\.Gone/,
  );
  assert.match(
    webApp,
    /getNextSequenceBlock[\s\S]*elements\.keepExercise\.hidden = isIntermediateBlock/,
  );
  assert.match(
    webIndex,
    /id="keep-exercise"[\s\S]*aria-label="Keep exercise for the next session"[\s\S]*class="keep-love-icon"/,
  );
  assert.match(strings, /name="keep_exercise_description">Keep this exercise/);
  assert.doesNotMatch(mainActivity, /Tap to keep both|keep both directions|tap_to_keep_both/);
  assert.doesNotMatch(webApp, /Tap to keep both|keep both directions/);
  assert.doesNotMatch(strings, /tap_to_keep|Tap to keep/);
});

test("every intermediate block rests 15 seconds and continues automatically", () => {
  assert.match(
    sessionService,
    /GetNextSequenceBlock[\s\S]*activeGroups\[groupIndex \+ 1\][\s\S]*nextGroup\.SelectionKey == group\.SelectionKey/,
  );
  assert.match(
    workoutModule,
    /getNextSequenceBlock[\s\S]*activeGroups\[groupIndex \+ 1\][\s\S]*getSelectionKey\(nextGroup\)/,
  );
  assert.match(
    sessionService,
    /KeepPendingRest[\s\S]*IsIntermediateSequenceBlock[\s\S]*return false/,
  );
  assert.match(
    workoutModule,
    /keepPendingRest\(\)[\s\S]*isIntermediateSequenceBlock[\s\S]*return false/,
  );
  assert.match(
    sessionService,
    /AdvanceSequence[\s\S]*ExerciseOutcome\.Neutral[\s\S]*WorkoutCompleted = false/,
  );
  assert.match(
    workoutModule,
    /advanceSequence[\s\S]*"neutral"[\s\S]*workoutCompleted = false/,
  );
  assert.match(
    mainActivity,
    /Next block:[\s\S]*starts automatically[\s\S]*ContinueWithNextSequenceBlock[\s\S]*PauseMovement[\s\S]*RestorePendingMovement/i,
  );
  assert.match(
    webApp,
    /Next block:[\s\S]*starts automatically[\s\S]*advanceSequence[\s\S]*pauseMovement[\s\S]*restorePendingMovement/i,
  );
  assert.match(
    mainActivity,
    /ContinueWithNextSequenceBlock[\s\S]*TotalDurationSeconds \* 1_000/,
  );
  assert.match(
    webApp,
    /pauseMovement\([\s\S]*getMovementDurationMs\(nextBlock\)[\s\S]*false/,
  );
});

test("intermediate Rest previews the exact upcoming block without advancing it", () => {
  assert.match(
    mainActivity,
    /ShowRestPanel[\s\S]*GetNextSequenceBlock[\s\S]*ShowUpcomingSequenceBlockPreview/,
  );
  assert.match(
    mainActivity,
    /ShowUpcomingSequenceBlockPreview[\s\S]*GetSelectedExercise[\s\S]*RenderExerciseIdentity\(nextExercise, upcoming: true\)[\s\S]*LoadExerciseMedia\([\s\S]*nextExercise,[\s\S]*nextBlock,[\s\S]*previewingUpcomingSequenceBlock: true/,
  );
  assert.match(
    webApp,
    /showRestPanel[\s\S]*getNextSequenceBlock[\s\S]*showUpcomingSequenceBlockPreview/,
  );
  assert.match(
    webApp,
    /showUpcomingSequenceBlockPreview[\s\S]*getSelectedExercise[\s\S]*renderExerciseIdentity\(nextExercise, true\)[\s\S]*loadExerciseMedia\(nextExercise, nextBlock, true\)/,
  );
  assert.match(
    mainActivity,
    /WorkoutPhase\.Rest when _previewingUpcomingSequenceBlock[\s\S]*_mediaWorkoutGroup/,
  );
  assert.match(
    webApp,
    /restActive && previewingUpcomingSequenceBlock[\s\S]*mediaExercise\?\.presentation !== "Still"/,
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
      .filter(
        (exerciseId) =>
          !integerCollection(
            catalogMigrationRules,
            "PermanentlyRetiredExerciseIdSet",
          ).includes(exerciseId),
      )
      .sort((left, right) => left - right),
  );
});

async function source(...segments) {
  return readFile(path.join(repositoryRoot, ...segments), "utf8");
}

async function binarySource(...segments) {
  return readFile(path.join(repositoryRoot, ...segments));
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
