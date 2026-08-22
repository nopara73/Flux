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
  EXERCISE_INSECT_COMPATIBILITY,
  EXERCISE_MIRROR_COVERAGE,
  FULL_SIDE_MOVEMENT_DURATION_MS,
  HARD_MUSCULAR_DEMAND,
  LAST_CUMULATIVE_CATALOG_REVISION,
  MAXIMUM_MUSCULAR_DEMAND,
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
  PRIMARY_MUSCLE_LOAD_HALF_UNITS,
  SCORE_HALF_UNITS_PER_VOTE,
  RESOLUTIONS,
  REST_DURATION_MS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  SECONDARY_MUSCLE_LOAD_HALF_UNITS,
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
  modifierPolicy,
  muscleBudgetPolicy,
  recoveryPolicy,
  exerciseDatabase,
  durationLayout,
  androidColors,
  androidStyles,
  strings,
  webIndex,
  webStyles,
  webBuild,
  mirrorEquipmentModel,
  mirrorCoverageModel,
  sideSequenceModel,
  movementPresentationPolicy,
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
  source("Flux", "Services", "WorkoutModifierPolicy.cs"),
  source("Flux", "Services", "WorkoutMuscleBudgetPolicy.cs"),
  source("Flux", "Services", "WorkoutRecoveryPolicy.cs"),
  source("Flux", "Data", "SqliteExerciseDatabase.cs"),
  source("Flux", "Resources", "layout", "screen_duration.xml"),
  source("Flux", "Resources", "values", "colors.xml"),
  source("Flux", "Resources", "values", "styles.xml"),
  source("Flux", "Resources", "values", "strings.xml"),
  source("web", "index.html"),
  source("web", "styles.css"),
  source("web", "scripts", "build.mjs"),
  source("Flux", "Models", "MirrorEquipment.cs"),
  source("Flux", "Models", "ExerciseMirrorCoverage.cs"),
  source("Flux", "Models", "ExerciseSideSequence.cs"),
  source("Flux", "Services", "MovementPhasePresentationPolicy.cs"),
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
  assert.match(workoutState, /Dictionary<string, int> ActiveDirectionPartnerExerciseIds/);
  assert.match(workoutState, /HashSet<string> ActiveFullSideRoundIds/);
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

test("web and mobile apply the same temporary muscle workload budget", () => {
  assert.equal(
    MUSCLE_SESSION_BUDGET_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "MaximumLoadHalfUnits"),
  );
  assert.equal(
    PRIMARY_MUSCLE_LOAD_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "PrimaryLoadHalfUnits"),
  );
  assert.equal(
    SECONDARY_MUSCLE_LOAD_HALF_UNITS,
    integerConstant(muscleBudgetPolicy, "SecondaryLoadHalfUnits"),
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
    /SecondaryCanonicalGroups\.Distinct\(\)[\s\S]*GetTemporaryDownvoteHalfUnits[\s\S]*MaximumLoadHalfUnits/,
  );
  assert.match(
    sessionService,
    /LastKeptExerciseIds\.Contains\(currentExerciseId\)[\s\S]*NextWorkoutExcludedExerciseIds\.Contains\(exercise\.Id\)/,
  );
  assert.match(
    workoutModule,
    /keptExerciseIds\.has\(currentExerciseId\)[\s\S]*nextWorkoutExcludedExerciseIds\.includes\(exercise\.id\)/,
  );
  assert.match(workoutState, /HashSet<int> NextWorkoutExcludedExerciseIds/);
});

test("web and mobile persist one combined duration and modifier selection context", () => {
  assert.equal(WORKOUT_MODIFIERS.Insect, 1);
  assert.equal(WORKOUT_MODIFIERS.Silence, 2);
  assert.equal(WORKOUT_MODIFIERS.Mirror, 4);
  assert.equal(WORKOUT_MODIFIERS.TallMirror, 8);
  assert.deepEqual(MIRROR_EQUIPMENT, {
    None: "None",
    Compact: "Compact",
    Tall: "Tall",
  });
  assert.equal(DEFAULT_WORKOUT_MODIFIERS, WORKOUT_MODIFIERS.Silence);
  assert.match(workoutModifiers, /Insect\s*=\s*1/);
  assert.match(workoutModifiers, /Silence\s*=\s*2/);
  assert.match(workoutModifiers, /Mirror\s*=\s*4/);
  assert.match(workoutModifiers, /TallMirror\s*=\s*8/);
  assert.match(mirrorEquipmentModel, /None[\s\S]*Compact[\s\S]*Tall/);
  assert.match(mirrorCoverageModel, /None[\s\S]*UpperBody[\s\S]*FullBody/);
  assert.equal(CURRENT_WORKOUT_STATE_VERSION, 9);
  assert.match(workoutState, /public int Version[^=]*=\s*12/);
  assert.match(workoutState, /LastWorkoutModifiers[^=]*=\s*WorkoutModifiers\.Silence/);
  assert.match(sessionService, /DefaultWorkoutModifiers\s*=\s*WorkoutModifiers\.Silence/);
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
    /ChooseBestDistinctLineup\([\s\S]*IsSelectable\(exercise, group, modifiers\)/,
  );
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
  assert.match(
    webApp,
    /findWorkoutModifierPairCoverageDeficiencies[\s\S]*findWorkoutModifierMaterialityDeficiencies[\s\S]*findMirrorCategoryDeficiencies[\s\S]*findWorkoutProfileLineupDeficiencies/,
  );
  assert.match(
    exerciseDatabase,
    /FindPairwiseCoverageDeficiencies[\s\S]*FindMaterialityDeficiencies[\s\S]*FindDistinctLineupDeficiencies[\s\S]*hasUndersizedModifierPairState/,
  );
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
  assert.match(webApp, /session\.startWorkout\(selectedMinutes, selectedModifiers\)/);
  assert.match(durationLayout, /@\+id\/insect_modifier_button/);
  assert.match(durationLayout, /@\+id\/silence_modifier_button/);
  assert.match(durationLayout, /@\+id\/mirror_modifier_button/);
  assert.match(durationLayout, /@drawable\/ic_mirror/);
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
    /UpdateMirrorModifierPresentation[\s\S]*SetCompoundDrawablesWithIntrinsicBounds\([\s\S]*Resource\.Drawable\.ic_mirror[\s\S]*MirrorEquipment\.None[\s\S]*ComplexUnitType\.Sp,[\s\S]*0f[\s\S]*ComplexUnitType\.Sp,[\s\S]*8f/,
  );
  assert.match(
    webStyles,
    /data-mirror-equipment="compact"[\s\S]*data-mirror-equipment="tall"[\s\S]*\.modifier-icon[\s\S]*scale\(0\.88\)/,
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
  assert.match(webIndex, /id="silence-modifier"/);
  assert.match(webIndex, /id="mirror-modifier"/);
  assert.match(webIndex, /id="mirror-mode-label"/);
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
  for (const label of [
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
  assert.match(durationLayout, /silence_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/noisy_exercises_disabled_feedback"/);
  assert.match(durationLayout, /mirror_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/mirror_equipment_disabled_feedback"/);
  assert.match(webApp, /showWorkoutModifierFeedback\(workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(webApp, /setAttribute\("title", workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(webApp, /cycleMirrorEquipment[\s\S]*MIRROR_EQUIPMENT\.None[\s\S]*MIRROR_EQUIPMENT\.Compact[\s\S]*MIRROR_EQUIPMENT\.Tall/);
  assert.match(webStyles, /@keyframes modifier-feedback-blink[\s\S]*scale\(0\.82\)[\s\S]*scale\(1\.08\)/);
  assert.doesNotMatch(webIndex, /M20\.24 12\.24a6 6 0 0 0-8\.49-8\.49L5 10\.5V19h8\.5Z/);
  assert.doesNotMatch(webIndex, /M3\.27 2 2 3\.27/);
  assert.match(exerciseDatabase, /DatabaseVersion\s*=\s*63/);
  assert.match(
    exerciseDatabase,
    /oldVersion\s+is\s+not\s+\([\s\S]*\bor\s+62\)[\s\S]*newVersion\s*!=\s*DatabaseVersion/,
  );
  assert.match(exerciseDatabase, /CHECK \(silent IN \(0, 1\)\)/);
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

test("web and mobile rest exactly yesterday's kept hardness-two exercises", () => {
  assert.equal(HARD_MUSCULAR_DEMAND, MAXIMUM_MUSCULAR_DEMAND);
  assert.match(
    recoveryPolicy,
    /HardMuscularDemand\s*=\s*Exercise\.MaximumMuscularDemand/,
  );
  assert.match(workoutState, /Dictionary<int, string> LastKeptLocalDateByExerciseId/);
  assert.match(workoutState, /HashSet<int> ActiveRecoveryExcludedExerciseIds/);
  assert.match(
    recoveryPolicy,
    /previousLocalDateKey[\s\S]*keptExerciseIds[\s\S]*lastKeptLocalDateByExerciseId[\s\S]*MuscularDemand\s*==\s*HardMuscularDemand/,
  );
  assert.match(
    workoutModule,
    /previousLocalDateKey[\s\S]*keptExerciseIds[\s\S]*lastKeptLocalDateByExerciseId[\s\S]*muscularDemand\s*===\s*HARD_MUSCULAR_DEMAND/,
  );
  assert.match(
    sessionService,
    /StartWorkout\([\s\S]*ActiveRecoveryExcludedExerciseIds\s*=\s*[\s\S]*GetPreviousDayHardKeptExerciseIds/,
  );
  assert.match(
    workoutModule,
    /startWorkout\([\s\S]*activeRecoveryExcludedExerciseIds\s*=\s*\[[\s\S]*getPreviousDayHardKeptExerciseIds/,
  );
  assert.match(sessionService, /hardPreferredExerciseWeight[\s\S]*preferredExerciseWeight/);
  assert.match(workoutModule, /hardPreferredExerciseWeight[\s\S]*preferredExerciseWeight/);
});

test("runtime media and the deployable web shell are content-addressed", () => {
  assert.match(mainActivity, /SHA256\.HashData/);
  assert.match(mainActivity, /assetFingerprint/);
  assert.match(webApp, /data\/asset-versions\.json[\s\S]*cache:\s*"no-store"/);
  assert.match(webApp, /assetVersions\[path\][\s\S]*searchParams\.set\("v", fingerprint\)/);
  assert.match(webBuild, /createHash\("sha256"\)/);
  assert.match(webBuild, /asset-versions\.json/);
  assert.match(webBuild, /fingerprintedName\("workout", "js"/);
  assert.match(webBuild, /fingerprintedName\("app", "js"/);
  assert.match(webBuild, /fingerprintedName\("styles", "css"/);
  assert.match(webBuild, /from \"\.\/\$\{workoutOutputName\}\"/);
  assert.match(webBuild, /replace\('\.\/styles\.css'/);
  assert.match(webBuild, /replace\('\.\/app\.js'/);
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
  assert.match(
    mainActivity,
    /phase_rest_chip_background[\s\S]*phase_move_chip_background/,
  );
  assert.match(
    androidStyles,
    /FluxKeepButton[\s\S]*@drawable\/rest_button_background[\s\S]*@color\/white/,
  );
  assert.match(
    mainActivity,
    /PendingRestKept[\s\S]*rest_button_background[\s\S]*Resource\.Color\.white/,
  );
  assert.match(
    webApp,
    /setFullPhaseSurface[\s\S]*setWorkoutPhaseClass\(kind\)[\s\S]*classList\.toggle\("phase-move"[\s\S]*classList\.toggle\("phase-rest"/,
  );
  assert.match(
    webStyles,
    /\.workout-screen\.phase-move \.exercise-media-card[\s\S]*\.workout-screen\.phase-rest \.exercise-media-card[\s\S]*\.move-panel\.change \.skip-action[\s\S]*\.keep-button/,
  );
});

test("exercise previews label every timed side or direction execution", async () => {
  const fullNeckCircles = catalog.find((exercise) => exercise.id === 409);
  assert.equal(fullNeckCircles.name, "Full Neck Circles");
  assert.equal(
    fullNeckCircles.directionSequence,
    "ClockwiseThenCounterclockwise",
  );
  const workoutLayout = await source("Flux", "Resources", "layout", "screen_workout.xml");
  assert.match(workoutLayout, /@\+id\/side_phase_preview/);
  assert.match(workoutLayout, /@\+id\/side_phase_label/);
  assert.match(workoutLayout, /@drawable\/exercise_execution_label_timed_pair_background/);
  assert.match(workoutLayout, /android:textColor="@color\/white"/);
  assert.match(webIndex, /id="exercise-name"[\s\S]*id="side-phase-preview"[\s\S]*id="exercise-media-card"/);
  assert.match(webIndex, /id="side-phase-label"/);
  assert.match(
    mainActivity,
    /RenderSidePhasePreview\(exercise\)/,
  );
  assert.match(
    methodBody(
      mainActivity,
      "private void RenderSidePhasePreview(",
      "private void AnimateExerciseChange(",
    ),
    /SideSequence\.UsesTimedSides\(\)[\s\S]*isBidirectional[\s\S]*if \(!isUnilateral && !isBidirectional\)[\s\S]*"UNILATERAL"[\s\S]*"BIDIRECTIONAL"/,
  );
  assert.doesNotMatch(
    methodBody(
      mainActivity,
      "private void RenderSidePhasePreview(",
      "private void AnimateExerciseChange(",
    ),
    /ALTERNATING|exercise_execution_label_alternating_background/,
  );
  assert.match(
    methodBody(
      webApp,
      "function renderSidePhasePreview(",
      "function showReadyPanel()",
    ),
    /isUnilateral = usesTimedSides\(exercise\)[\s\S]*isBidirectional[\s\S]*if \(!isUnilateral && !isBidirectional\)[\s\S]*"UNILATERAL"[\s\S]*"BIDIRECTIONAL"[\s\S]*classList\.add\("timed-pair"\)/,
  );
  assert.doesNotMatch(
    methodBody(
      webApp,
      "function renderSidePhasePreview(",
      "function showReadyPanel()",
    ),
    /ALTERNATING|classList\.(?:add|toggle)\("alternating"/,
  );
  assert.match(
    webStyles,
    /\.side-phase-label\.timed-pair[\s\S]*border-color: var\(--move-text\)[\s\S]*background: var\(--move-accent\)/,
  );
  assert.doesNotMatch(webStyles, /\.side-phase-label\.alternating/);
  assert.doesNotMatch(workoutLayout, /side_phase_(?:first|change|second)/);
  assert.doesNotMatch(webIndex, /side-phase-(?:first|change|second)/);
  assert.doesNotMatch(workoutLayout, /two_sided_badge|ic_two_sides/);
  assert.doesNotMatch(mainActivity, /_twoSidedBadge/);
  assert.doesNotMatch(webIndex, /two-sided-badge|BOTH SIDES/);
  assert.doesNotMatch(webApp, /twoSidedBadge/);
  assert.doesNotMatch(webStyles, /\.two-sided-(?:badge|icon)/);
});

test("lead-stance timing and cues are identical on mobile and web", () => {
  const expectedLeadStanceIds = [
    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
  ];
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.sideSequence.includes("LeadThen"))
      .map((exercise) => exercise.id),
    expectedLeadStanceIds,
  );
  assert.match(
    sideSequenceModel,
    /ScreenLeftLeadThenRightLead[\s\S]*ScreenRightLeadThenLeftLead[\s\S]*UsesTimedLeadStances/,
  );
  assert.match(
    movementPresentationPolicy,
    /ShownLeadStance[\s\S]*OppositeLeadStance[\s\S]*MirrorMedia: secondDirection/,
  );
  assert.match(
    workoutModule,
    /ScreenLeftLeadThenRightLead[\s\S]*ScreenRightLeadThenLeftLead[\s\S]*usesTimedLeadStances/,
  );
  assert.match(workoutModule, /ShownLeadStance/);
  assert.match(workoutModule, /OppositeLeadStance/);
  assert.match(
    mainActivity,
    /UsesTimedLeadStances[\s\S]*Change stance[\s\S]*Shown lead stance[\s\S]*Opposite lead stance/,
  );
  assert.match(
    webApp,
    /usesTimedLeadStances[\s\S]*Change stance[\s\S]*Shown lead stance[\s\S]*Opposite lead stance/,
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
  assert.match(
    mainActivity,
    /timedPairWorkSeconds[\s\S]*FullSideDurationSeconds[\s\S]*timedPairChangeSeconds[\s\S]*FullSideChangeDurationSeconds/,
  );
  assert.match(
    webApp,
    /changeSeconds\s*=\s*currentGroup\?\.usesFullSideTiming\s*\?\s*15\s*:\s*5[\s\S]*Change direction, \$\{changeSeconds\} seconds/,
  );
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
  const mobileCueBodies = [
    methodBody(mainActivity, "private void CueSideChange()", "private void CueMovementRestart()"),
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

test("complete direction sequences coexist with linked side-direction exercises", () => {
  assert.match(exerciseModel, /int DirectionPartnerExerciseId/);
  assert.match(workoutState, /Dictionary<string, int> ActiveDirectionPartnerExerciseIds/);
  assert.match(
    sessionService,
    /directionPartners\.Add\(group\.Id, partner\.Id\);[\s\S]*remainingExtraMinutes[\s\S]*timedPairRoundIds[\s\S]*UsesTimedPair/,
  );
  assert.match(workoutModule, /directionPartnerExerciseIds\.set\(group\.id, partnerId\);[\s\S]*remainingExtraMinutes[\s\S]*timedPairRoundIds[\s\S]*usesTimedPair/);
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.directionSequence !== "None")
      .map((exercise) => exercise.id),
    [264, 275, 406, 409, 460, 588, 608, 611, 743],
  );
  const linkedDirections = catalog.filter((exercise) =>
    exercise.directionPartnerExerciseId > 0);
  assert.equal(linkedDirections.length, 8);
  for (const exercise of linkedDirections) {
    const partner = catalog.find((candidate) =>
      candidate.id === exercise.directionPartnerExerciseId);
    assert.ok(partner);
    assert.equal(partner.directionPartnerExerciseId, exercise.id);
  }
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
