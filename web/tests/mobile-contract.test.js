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
  FULL_SIDE_MOVEMENT_DURATION_MS,
  LAST_CUMULATIVE_CATALOG_REVISION,
  MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
  MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT,
  MINIMUM_MODIFIER_MATERIALITY_PERCENT,
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
  modifierPolicy,
  exerciseDatabase,
  durationLayout,
  androidColors,
  androidStyles,
  strings,
  webIndex,
  webStyles,
  webBuild,
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
  source("Flux", "Data", "SqliteExerciseDatabase.cs"),
  source("Flux", "Resources", "layout", "screen_duration.xml"),
  source("Flux", "Resources", "values", "colors.xml"),
  source("Flux", "Resources", "values", "styles.xml"),
  source("Flux", "Resources", "values", "strings.xml"),
  source("web", "index.html"),
  source("web", "styles.css"),
  source("web", "scripts", "build.mjs"),
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

test("web and mobile persist one combined duration and modifier selection context", () => {
  assert.equal(WORKOUT_MODIFIERS.Insect, 1);
  assert.equal(WORKOUT_MODIFIERS.Silence, 2);
  assert.equal(DEFAULT_WORKOUT_MODIFIERS, WORKOUT_MODIFIERS.Silence);
  assert.match(workoutModifiers, /Insect\s*=\s*1/);
  assert.match(workoutModifiers, /Silence\s*=\s*2/);
  assert.equal(CURRENT_WORKOUT_STATE_VERSION, 6);
  assert.match(workoutState, /public int Version[^=]*=\s*9/);
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
  assert.equal(
    MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
    integerConstant(modifierPolicy, "MinimumExercisesPerPairStatePerGroup"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
    integerConstant(modifierPolicy, "MinimumReleasedExercises"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_PERCENT,
    integerConstant(modifierPolicy, "MinimumReleasedExercisePercent"),
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
  assert.doesNotMatch(modifierPolicy, /1\s*<<\s*Rules\.Length/);
  assert.doesNotMatch(workoutModule, /1\s*<<\s*MODIFIER_RULES\.length/);
  assert.match(
    workoutModule,
    /findWorkoutProfileLineupDeficiencies[\s\S]*getMaximumDistinctLineupSize/,
  );
  assert.match(
    webApp,
    /findWorkoutModifierPairCoverageDeficiencies[\s\S]*findWorkoutModifierMaterialityDeficiencies[\s\S]*findWorkoutProfileLineupDeficiencies/,
  );
  assert.match(
    exerciseDatabase,
    /FindPairwiseCoverageDeficiencies[\s\S]*FindMaterialityDeficiencies[\s\S]*FindDistinctLineupDeficiencies[\s\S]*hasUndersizedModifierPairState/,
  );
  assert.match(exerciseModel, /ExerciseInsectCompatibility InsectCompatibility/);
  assert.ok(catalog.every((exercise) => typeof exercise.silent === "boolean"));
  assert.ok(catalog.every((exercise) =>
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible));
  assert.match(webApp, /session\.startWorkout\(selectedMinutes, selectedModifiers\)/);
  assert.match(durationLayout, /@\+id\/insect_modifier_button/);
  assert.match(durationLayout, /@\+id\/silence_modifier_button/);
  assert.match(durationLayout, /@drawable\/ic_no_clap/);
  assert.doesNotMatch(durationLayout, /silence_modifier_button[\s\S]*?foregroundTint=/);
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
  assert.match(webIndex, /Quiet exercise filter: quiet exercises only/);
  assert.match(durationLayout, /@\+id\/duration_modifier_feedback/);
  assert.match(durationLayout, /@drawable\/duration_modifier_feedback_background/);
  assert.match(webIndex, /id="modifier-feedback"[\s\S]*role="status"[\s\S]*aria-live="polite"/);
  assert.match(strings, /<string name="insect_mode_enabled_feedback">insect mode ON<\/string>/);
  assert.match(strings, /<string name="insect_mode_disabled_feedback">insect mode OFF<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_enabled_feedback">noisy exercises ENABLED<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_disabled_feedback">noisy exercises DISABLED<\/string>/);
  for (const label of [
    "insect mode ON",
    "insect mode OFF",
    "noisy exercises ENABLED",
    "noisy exercises DISABLED",
  ]) {
    assert.match(webApp, new RegExp(label));
  }
  assert.match(mainActivity, /ShowModifierFeedback\(enabled[\s\S]*insect_mode_enabled_feedback[\s\S]*insect_mode_disabled_feedback/);
  assert.match(mainActivity, /ShowModifierFeedback\(enabled[\s\S]*noisy_exercises_disabled_feedback[\s\S]*noisy_exercises_enabled_feedback/);
  assert.match(webApp, /flag === WORKOUT_MODIFIERS\.Insect[\s\S]*insectEnabled[\s\S]*insectDisabled[\s\S]*noisyDisabled[\s\S]*noisyEnabled/);
  assert.match(webStyles, /@keyframes modifier-feedback-blink[\s\S]*scale\(0\.82\)[\s\S]*scale\(1\.08\)/);
  assert.doesNotMatch(webIndex, /M20\.24 12\.24a6 6 0 0 0-8\.49-8\.49L5 10\.5V19h8\.5Z/);
  assert.doesNotMatch(webIndex, /M3\.27 2 2 3\.27/);
  assert.match(mainActivity, /WorkoutModifiers\.Insect[\s\S]*WorkoutModifiers\.Silence/);
  assert.match(webApp, /WORKOUT_MODIFIERS\.Insect[\s\S]*WORKOUT_MODIFIERS\.Silence/);
  assert.match(exerciseDatabase, /DatabaseVersion\s*=\s*56/);
  assert.match(
    exerciseDatabase,
    /oldVersion\s+is\s+not\s+\([\s\S]*\bor\s+55\)[\s\S]*newVersion\s*!=\s*DatabaseVersion/,
  );
  assert.match(exerciseDatabase, /CHECK \(silent IN \(0, 1\)\)/);
  assert.match(exerciseDatabase, /max_space_meters > 0 AND max_space_meters <= 2/);
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

test("exercise previews label only timed unilateral execution", async () => {
  const workoutLayout = await source("Flux", "Resources", "layout", "screen_workout.xml");
  assert.match(workoutLayout, /@\+id\/side_phase_preview/);
  assert.match(workoutLayout, /@\+id\/side_phase_label/);
  assert.match(workoutLayout, /@drawable\/exercise_execution_label_unilateral_background/);
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
    /ScreenLeftThenRight[\s\S]*ScreenRightThenLeft[\s\S]*if \(!isUnilateral\)[\s\S]*"UNILATERAL"/,
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
    /if \(!usesTimedSides\(exercise\)\)[\s\S]*textContent = "UNILATERAL"[\s\S]*classList\.add\("unilateral"\)/,
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
    /\.side-phase-label\.unilateral[\s\S]*border-color: var\(--move-text\)[\s\S]*background: var\(--move-accent\)/,
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

test("directions are linked exercises and precede longer side timers", () => {
  assert.match(exerciseModel, /int DirectionPartnerExerciseId/);
  assert.match(workoutState, /Dictionary<string, int> ActiveDirectionPartnerExerciseIds/);
  assert.match(
    sessionService,
    /directionPartners\.Add\(group\.Id, partner\.Id\);[\s\S]*remainingExtraMinutes[\s\S]*sidedRoundIds/,
  );
  assert.match(workoutModule, /directionPartnerExerciseIds\.set\(group\.id, partnerId\);[\s\S]*remainingExtraMinutes[\s\S]*sidedRoundIds/);
  assert.equal(catalog.filter((exercise) => exercise.directionSequence !== "None").length, 0);
  const linkedDirections = catalog.filter((exercise) =>
    exercise.directionPartnerExerciseId > 0);
  assert.equal(linkedDirections.length, 20);
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
