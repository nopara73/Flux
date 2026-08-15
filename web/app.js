import {
  REST_DURATION_MS,
  SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  findWorkoutModifierExclusionDeficiencies,
  findWorkoutProfileCoverageDeficiencies,
  findWorkoutProfileLineupDeficiencies,
  getExerciseVideoPath,
  getHoldFramePath,
  getMovementCountdownDurationMs,
  getMovementPhaseState,
  getMovementPresentation,
  isModifierMetadataComplete,
  parseStoredState,
  usesTimedPair,
} from "./workout.js";

const STORAGE_KEY = "flux.workout.state.v1";
const TIMER_INTERVAL_MS = 100;
const MEDIA_RECOVERY_TIMEOUT_MS = 12_000;

const elements = {
  durationScreen: byId("duration-screen"),
  durationDial: byId("duration-dial"),
  durationValue: byId("duration-value"),
  durationDecrease: byId("duration-decrease"),
  durationIncrease: byId("duration-increase"),
  durationRange: byId("duration-range"),
  durationLabels: [...byId("duration-labels").children],
  beginWorkout: byId("begin-workout"),
  insectModifier: byId("insect-modifier"),
  workoutScreen: byId("workout-screen"),
  phaseSurface: byId("phase-surface"),
  phaseLeft: byId("phase-left"),
  phaseRight: byId("phase-right"),
  workoutHeader: byId("workout-header"),
  workoutProgressText: byId("workout-progress-text"),
  workoutProgressFill: byId("workout-progress-fill"),
  workoutGroupName: byId("workout-group-name"),
  exerciseName: byId("exercise-name"),
  mediaCard: byId("exercise-media-card"),
  video: byId("exercise-video"),
  holdFrame: byId("hold-frame"),
  holdBadge: byId("hold-badge"),
  mediaScrim: byId("media-scrim"),
  mediaError: byId("media-error"),
  mediaRetry: byId("media-retry"),
  readyPanel: byId("ready-panel"),
  startMovement: byId("start-movement"),
  movePanel: byId("move-panel"),
  movementCue: byId("movement-cue"),
  skipExercise: byId("skip-exercise"),
  movementCountdown: byId("movement-countdown"),
  movementProgressFill: byId("movement-progress-fill"),
  restPanel: byId("rest-panel"),
  restCountdown: byId("rest-countdown"),
  restProgressFill: byId("rest-progress-fill"),
  keepExercise: byId("keep-exercise"),
  completionScreen: byId("completion-screen"),
  doneButton: byId("done-button"),
  status: byId("status"),
};

const sounds = Object.fromEntries(
  ["start", "side_change", "rest", "complete"].map((name) => {
    const audio = new Audio(new URL(`audio/whistle_${name}.ogg`, document.baseURI));
    audio.preload = "none";
    return [name, audio];
  }),
);

let session = null;
let selectedMinutes = 10;
let selectedModifiers = WORKOUT_MODIFIERS.None;
let currentGroup = null;
let currentExercise = null;
let mediaGeneration = 0;
let mediaReady = false;
let mediaRecoveryTimer = null;
let lastMovementPhase = null;
let movementTimer = null;
let movementEndsAt = 0;
let movementRemaining = 0;
let movementRunning = false;
let movementPauseReason = null;
let restTimer = null;
let restActive = false;
let wakeLock = null;
let wakeLockRequestPending = false;
let wakeLockGeneration = 0;

bindEvents();
renderDuration(selectedMinutes, false);
bootstrap();

async function bootstrap() {
  try {
    const response = await fetch(new URL("data/exercises.json", document.baseURI));
    if (!response.ok) {
      throw new Error(`Catalog request failed with ${response.status}.`);
    }
    const exercises = await response.json();
    const coverageDeficiencies =
      findWorkoutProfileCoverageDeficiencies(exercises);
    const lineupDeficiencies =
      findWorkoutProfileLineupDeficiencies(exercises);
    const exclusionDeficiencies =
      findWorkoutModifierExclusionDeficiencies(exercises);
    if (!isModifierMetadataComplete(exercises) ||
        coverageDeficiencies.length > 0 ||
        lineupDeficiencies.length > 0 ||
        exclusionDeficiencies.length > 0) {
      throw new Error("Catalog does not satisfy workout modifier coverage.");
    }
    session = new WorkoutSession(exercises, loadState());
    session.initialize();
    persistState();
    selectedMinutes = session.state.lastWorkoutMinutes;
    selectedModifiers = session.state.lastWorkoutModifiers;
    renderDuration(selectedMinutes, false);
    renderInsectModifier();

    if (session.state.workoutCompleted && !session.state.completionAcknowledged) {
      showCompletion(false);
    } else {
      showDuration();
    }
  } catch (error) {
    console.error(error);
    elements.beginWorkout.disabled = true;
    elements.status.textContent = "Flux is unavailable.";
  }
}

function bindEvents() {
  elements.durationDecrease.addEventListener("click", () => stepDuration(-1));
  elements.durationIncrease.addEventListener("click", () => stepDuration(1));
  elements.durationRange.addEventListener("input", () => {
    selectDurationByIndex(Number(elements.durationRange.value), true);
  });
  elements.beginWorkout.addEventListener("click", startWorkout);
  elements.insectModifier.addEventListener("click", toggleInsectModifier);
  elements.startMovement.addEventListener("click", startMovement);
  elements.skipExercise.addEventListener("click", skipExercise);
  elements.keepExercise.addEventListener("click", keepExercise);
  elements.mediaRetry.addEventListener("click", retryMedia);
  elements.doneButton.addEventListener("click", closeCompletion);
  document.addEventListener("visibilitychange", handleVisibilityChange);
}

function byId(id) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing element #${id}.`);
  }
  return element;
}

function loadState() {
  try {
    return parseStoredState(localStorage.getItem(STORAGE_KEY));
  } catch {
    return parseStoredState(null);
  }
}

function persistState() {
  if (!session) {
    return;
  }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session.state));
  } catch (error) {
    console.error("Unable to save Flux state.", error);
  }
}

function stepDuration(direction) {
  const currentIndex = SUPPORTED_MINUTES.indexOf(selectedMinutes);
  selectDurationByIndex(
    Math.max(0, Math.min(SUPPORTED_MINUTES.length - 1, currentIndex + direction)),
    true,
  );
}

function selectDurationByIndex(index, userInitiated) {
  const minutes = SUPPORTED_MINUTES[index];
  if (minutes !== undefined) {
    renderDuration(minutes, userInitiated);
  }
}

function renderDuration(minutes, userInitiated) {
  const previousMinutes = selectedMinutes;
  selectedMinutes = minutes;
  const index = SUPPORTED_MINUTES.indexOf(minutes);
  const progress = `${(index / (SUPPORTED_MINUTES.length - 1)) * 100}%`;

  elements.durationValue.value = String(minutes);
  elements.durationValue.textContent = String(minutes);
  elements.durationValue.setAttribute("aria-label", `${minutes} minutes selected`);
  elements.durationRange.value = String(index);
  elements.durationRange.style.setProperty("--range-progress", progress);
  elements.durationRange.setAttribute(
    "aria-valuetext",
    `${minutes} minutes. Options: 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90 minutes`,
  );
  elements.durationDecrease.disabled = index === 0;
  elements.durationIncrease.disabled = index === SUPPORTED_MINUTES.length - 1;
  elements.beginWorkout.setAttribute("aria-label", `Start a ${minutes} minute workout`);

  elements.durationLabels.forEach((label, labelIndex) => {
    label.classList.toggle("selected", labelIndex === index);
  });

  if (userInitiated && previousMinutes !== minutes) {
    elements.durationDial.classList.remove("pulse");
    requestAnimationFrame(() => elements.durationDial.classList.add("pulse"));
  }
}

function toggleInsectModifier() {
  selectedModifiers ^= WORKOUT_MODIFIERS.Insect;
  renderInsectModifier();
}

function renderInsectModifier() {
  const enabled = (selectedModifiers & WORKOUT_MODIFIERS.Insect) !== 0;
  elements.insectModifier.setAttribute("aria-pressed", String(enabled));
}

function showScreen(screen) {
  elements.durationScreen.hidden = screen !== "duration";
  elements.workoutScreen.hidden = screen !== "workout";
  elements.completionScreen.hidden = screen !== "completion";
}

function showDuration() {
  releaseWakeLock();
  stopRuntimeTimers();
  clearExerciseMedia();
  resetMovementVisuals();
  currentGroup = null;
  currentExercise = null;
  selectedMinutes = session?.state.lastWorkoutMinutes ?? selectedMinutes;
  selectedModifiers = session?.state.lastWorkoutModifiers ?? selectedModifiers;
  renderDuration(selectedMinutes, false);
  renderInsectModifier();
  elements.beginWorkout.disabled = !session;
  showScreen("duration");
}

function startWorkout() {
  if (!session || elements.beginWorkout.disabled) {
    return;
  }
  elements.beginWorkout.disabled = true;
  try {
    session.startWorkout(selectedMinutes, selectedModifiers);
    persistState();
    showNextExercise();
  } catch (error) {
    console.error(error);
    elements.beginWorkout.disabled = false;
  }
}

function showNextExercise() {
  if (!session) {
    return;
  }
  const nextGroup = session.getNextGroup();
  if (!nextGroup) {
    showCompletion(true);
    return;
  }

  currentGroup = nextGroup;
  currentExercise = session.getSelectedExercise(nextGroup);
  const total = session.getActiveGroups().length;
  const position = nextGroup.order;

  elements.workoutProgressText.textContent =
    `${String(position).padStart(2, "0")}  /  ${String(total).padStart(2, "0")}`;
  elements.workoutProgressText.setAttribute("aria-label", `Round ${position} of ${total}`);
  elements.workoutProgressFill.style.transform = `scaleX(${position / total})`;
  elements.workoutGroupName.textContent = nextGroup.displayName;
  elements.exerciseName.textContent = currentExercise.name;
  elements.holdBadge.hidden = currentExercise.mode !== "Hold";
  elements.status.textContent =
    `Round ${position} of ${total}. ${nextGroup.displayName}. ${currentExercise.name}.`;

  stopRuntimeTimers();
  resetMovementVisuals();
  showReadyPanel();
  showScreen("workout");
  requestWakeLock();
  loadExerciseMedia();
}

function showReadyPanel() {
  elements.readyPanel.hidden = false;
  elements.movePanel.hidden = true;
  elements.restPanel.hidden = true;
  elements.startMovement.disabled = !mediaReady;
}

function showMovePanel() {
  elements.readyPanel.hidden = true;
  elements.movePanel.hidden = false;
  elements.restPanel.hidden = true;
}

function showRestPanel() {
  elements.readyPanel.hidden = true;
  elements.movePanel.hidden = true;
  elements.restPanel.hidden = false;
}

function assetUrl(path) {
  return new URL(`assets/${path}`, document.baseURI).href;
}

function loadExerciseMedia() {
  if (!currentExercise) {
    return;
  }

  const generation = ++mediaGeneration;
  clearMediaRecoveryTimer();
  mediaReady = false;
  elements.startMovement.disabled = true;
  elements.skipExercise.disabled = true;
  elements.mediaError.hidden = true;
  elements.mediaScrim.hidden = false;
  elements.mediaScrim.classList.remove("revealed");
  elements.holdFrame.hidden = true;
  elements.holdFrame.removeAttribute("src");
  resetVideoElement();

  if (currentExercise.presentation === "Still") {
    elements.video.hidden = true;
    elements.holdFrame.onload = () => markMediaReady(generation);
    elements.holdFrame.onerror = () => showMediaError(generation);
    elements.holdFrame.src = assetUrl(getHoldFramePath(currentExercise));
    elements.holdFrame.hidden = false;
    return;
  }

  elements.video.hidden = false;
  elements.video.preload = "auto";
  elements.video.loop = true;
  elements.video.oncanplay = () => markMediaReady(generation, false);
  elements.video.onplaying = () => markMediaReady(generation, true);
  elements.video.onwaiting = () => handleVideoWaiting(generation);
  elements.video.onstalled = () => {
    if (!elements.video.paused && elements.video.readyState < 3) {
      handleVideoWaiting(generation);
    }
  };
  elements.video.onprogress = () => {
    if (!mediaReady && elements.video.readyState >= 3) {
      markMediaReady(generation, false);
    }
  };
  elements.video.onerror = () => showMediaError(generation);
  elements.video.onended = handleVideoEnded;
  elements.video.src = assetUrl(getExerciseVideoPath(currentExercise));
  elements.video.load();
}

function resetVideoElement() {
  elements.video.oncanplay = null;
  elements.video.onplaying = null;
  elements.video.onwaiting = null;
  elements.video.onstalled = null;
  elements.video.onprogress = null;
  elements.video.onerror = null;
  elements.video.onended = null;
  elements.video.pause();
  elements.video.removeAttribute("src");
  elements.video.load();
  elements.video.loop = false;
  elements.video.style.setProperty("--media-scale-x", "1");
}

function markMediaReady(generation, playbackConfirmed = false) {
  if (generation !== mediaGeneration) {
    return;
  }
  const newlyReady = !mediaReady;
  mediaReady = true;
  elements.mediaError.hidden = true;
  elements.mediaScrim.classList.add("revealed");
  elements.startMovement.disabled = false;
  elements.skipExercise.disabled = !movementRunning;

  if (playbackConfirmed || !movementPauseReason) {
    clearMediaRecoveryTimer();
  }

  if (movementPauseReason && !document.hidden) {
    if (playbackConfirmed) {
      resumeMovement();
    } else {
      resumePausedMovementWhenVisible();
    }
    return;
  }

  if (
    newlyReady &&
    !document.hidden &&
    !elements.readyPanel.hidden &&
    currentExercise?.presentation !== "Still"
  ) {
    playVideo();
  }
}

function handleVideoWaiting(generation) {
  if (generation !== mediaGeneration || currentExercise?.presentation === "Still") {
    return;
  }
  mediaReady = false;
  elements.startMovement.disabled = true;
  elements.skipExercise.disabled = true;
  elements.mediaError.hidden = true;
  elements.mediaScrim.hidden = false;
  elements.mediaScrim.classList.remove("revealed");
  if (movementRunning) {
    pauseMovement("buffering");
  }
  scheduleMediaRecoveryFailure(generation);
}

function showMediaError(generation = mediaGeneration) {
  if (generation !== mediaGeneration) {
    return;
  }
  clearMediaRecoveryTimer();
  mediaReady = false;
  elements.video.pause();
  if (movementRunning) {
    pauseMovement("media");
  } else if (movementPauseReason) {
    movementPauseReason = "media";
  }
  if (movementPauseReason === "media") {
    lastMovementPhase = null;
  }
  elements.startMovement.disabled = true;
  elements.skipExercise.disabled = true;
  elements.mediaScrim.classList.remove("revealed");
  elements.mediaScrim.hidden = true;
  elements.mediaError.hidden = false;
  elements.status.textContent = "Demonstration unavailable.";
}

function retryMedia() {
  if (!currentExercise) {
    return;
  }
  loadExerciseMedia();
}

function clearExerciseMedia() {
  clearMediaRecoveryTimer();
  mediaReady = false;
  mediaGeneration++;
  resetVideoElement();
  elements.holdFrame.onload = null;
  elements.holdFrame.onerror = null;
  elements.holdFrame.hidden = true;
  elements.holdFrame.removeAttribute("src");
  elements.mediaError.hidden = true;
}

function startMovement() {
  if (!currentExercise || !currentGroup || !mediaReady || movementRunning) {
    return;
  }
  requestWakeLock();
  movementRemaining = getMovementCountdownDurationMs(currentGroup);
  movementPauseReason = null;
  lastMovementPhase = null;
  showMovePanel();
  elements.skipExercise.disabled = false;
  setMovementDeadline(movementRemaining);
}

function setMovementDeadline(remainingMilliseconds) {
  clearInterval(movementTimer);
  movementRemaining = remainingMilliseconds;
  movementEndsAt = performance.now() + remainingMilliseconds;
  movementRunning = true;
  movementPauseReason = null;
  updateMovement();
  movementTimer = setInterval(updateMovement, TIMER_INTERVAL_MS);
}

function updateMovement() {
  if (!movementRunning || !currentExercise) {
    return;
  }
  movementRemaining = Math.max(0, movementEndsAt - performance.now());
  const movementDuration = getMovementCountdownDurationMs(currentGroup);
  const state = getMovementPhaseState(
    movementRemaining,
    usesTimedPair(currentExercise),
    currentGroup?.usesFullSideTiming === true,
  );
  elements.movementCountdown.value = String(state.secondsRemaining);
  elements.movementCountdown.textContent = String(state.secondsRemaining);
  elements.movementProgressFill.style.transform =
    `scaleX(${movementRemaining / movementDuration})`;

  if (state.phase !== lastMovementPhase && state.phase !== "Complete") {
    applyMovementPhase(state.phase);
  }
  if (movementRemaining <= 0) {
    completeMovement();
  }
}

function applyMovementPhase(phase) {
  if (!currentExercise) {
    return;
  }
  const previousPhase = lastMovementPhase;
  lastMovementPhase = phase;
  elements.movePanel.classList.toggle(
    "change",
    phase === "Preparation" || phase === "ChangeSides",
  );

  if (phase === "Preparation") {
    elements.movementCue.textContent = cueSymbol("Move");
    setMediaMirrored(false);
    setFullPhaseSurface("rest");
    elements.mediaCard.classList.add("resting");
    elements.video.pause();
    elements.status.textContent = "Prepare, 5 seconds.";
    return;
  }

  const presentation = getMovementPresentation(currentExercise, phase);
  const description = movementCueDescription(presentation.cue);
  elements.movementCue.textContent = cueSymbol(presentation.cue);

  if (phase === "ChangeSides") {
    setMediaMirrored(false);
    setFullPhaseSurface("rest");
    elements.mediaCard.classList.add("resting");
    elements.video.pause();
    playSound("side_change");
    elements.status.textContent =
      currentExercise.directionSequence === "None"
        ? `Change sides, ${currentGroup?.usesFullSideTiming ? 15 : 5} seconds.`
        : "Change direction, 5 seconds.";
    return;
  }

  elements.mediaCard.classList.remove("resting");
  setMediaMirrored(presentation.mirrorMedia);
  if (presentation.activeScreenSide) {
    setSplitPhaseSurface(presentation.activeScreenSide);
  } else {
    setFullPhaseSurface("move");
  }
  restartMediaForPhase(phase);

  const segmentSeconds = currentGroup?.usesFullSideTiming ? 45 : 20;
  elements.status.textContent =
    phase === "Continuous" ? "Move, 45 seconds." : `${description}, ${segmentSeconds} seconds.`;

  if (previousPhase === "Preparation" || phase === "SecondSide") {
    playSound("start");
  }
}

function restartMediaForPhase(phase) {
  if (!currentExercise) {
    return;
  }
  if (currentExercise.presentation === "Still") {
    elements.holdFrame.hidden = false;
    return;
  }

  elements.holdFrame.hidden = true;
  elements.video.hidden = false;
  elements.video.loop = currentExercise.mode !== "Hold";
  try {
    elements.video.currentTime =
      currentExercise.directionSequence !== "None" && phase === "SecondSide" ? 20 : 0;
  } catch {
    // The loaded media will seek once its metadata is available.
  }
  playVideo();
}

function handleVideoEnded() {
  if (currentExercise?.mode === "Hold" && !elements.movePanel.hidden) {
    showReviewedHoldFrame();
  }
}

function showReviewedHoldFrame() {
  if (!currentExercise || currentExercise.mode !== "Hold") {
    return;
  }
  const generation = mediaGeneration;
  elements.holdFrame.onload = () => {
    if (generation === mediaGeneration) {
      elements.holdFrame.hidden = false;
      elements.video.pause();
    }
  };
  elements.holdFrame.onerror = () => showMediaError(generation);
  if (!elements.holdFrame.src) {
    elements.holdFrame.src = assetUrl(getHoldFramePath(currentExercise));
  } else if (elements.holdFrame.complete && elements.holdFrame.naturalWidth > 0) {
    elements.holdFrame.hidden = false;
    elements.video.pause();
  }
}

function pauseMovement(reason) {
  if (!movementRunning) {
    return;
  }
  movementRemaining = Math.max(1, movementEndsAt - performance.now());
  clearInterval(movementTimer);
  movementTimer = null;
  movementRunning = false;
  movementPauseReason = reason;
  elements.video.pause();
  elements.skipExercise.disabled = true;
}

function resumeMovement() {
  if (!movementPauseReason || movementRemaining <= 0 || !mediaReady || document.hidden) {
    return;
  }
  elements.skipExercise.disabled = false;
  setMovementDeadline(movementRemaining);
}

function resumePausedMovementWhenVisible() {
  if (!movementPauseReason || !currentExercise || document.hidden) {
    return;
  }

  const phaseState = getMovementPhaseState(
    movementRemaining,
    usesTimedPair(currentExercise),
    currentGroup?.usesFullSideTiming === true,
  );
  if (
    currentExercise.presentation === "Still" ||
    phaseState.phase === "Preparation" ||
    phaseState.phase === "ChangeSides"
  ) {
    resumeMovement();
    return;
  }

  scheduleMediaRecoveryFailure(mediaGeneration);
  if (movementPauseReason === "media" && lastMovementPhase !== phaseState.phase) {
    restorePausedMovementMedia(phaseState.phase);
  } else {
    playVideo();
  }
}

function restorePausedMovementMedia(phase) {
  const presentation = getMovementPresentation(currentExercise, phase);
  lastMovementPhase = phase;
  elements.movePanel.classList.toggle("change", phase === "ChangeSides");
  elements.mediaCard.classList.remove("resting");
  setMediaMirrored(presentation.mirrorMedia);
  if (presentation.activeScreenSide) {
    setSplitPhaseSurface(presentation.activeScreenSide);
  } else {
    setFullPhaseSurface("move");
  }
  restartMediaForPhase(phase);
}

function skipExercise() {
  if (!movementRunning || !session || !currentGroup) {
    return;
  }
  stopMovementTimer();
  session.recordOutcome(currentGroup, false);
  persistState();
  if (session.state.workoutCompleted) {
    showCompletion(true);
  } else {
    showNextExercise();
  }
}

function completeMovement() {
  if (!movementRunning || !session || !currentGroup) {
    return;
  }
  stopMovementTimer();
  elements.movementCountdown.value = "0";
  elements.movementCountdown.textContent = "0";
  elements.movementProgressFill.style.transform = "scaleX(0)";
  if (currentExercise?.mode === "Hold") {
    showReviewedHoldFrame();
  }
  playSound("rest");
  session.beginRest(currentGroup, Date.now() + REST_DURATION_MS);
  persistState();
  restActive = true;
  elements.video.pause();
  setMediaMirrored(false);
  setFullPhaseSurface("rest");
  elements.mediaCard.classList.add("resting");
  showRestPanel();
  elements.status.textContent = "Rest, 15 seconds. Tap to keep this exercise.";
  startRestTimer();
}

function startRestTimer() {
  clearInterval(restTimer);
  updateRest();
  if (restActive) {
    restTimer = setInterval(updateRest, TIMER_INTERVAL_MS);
  }
}

function updateRest() {
  if (!restActive || !session) {
    return;
  }
  const remaining = Math.max(
    0,
    session.state.pendingRestEndsAtUnixMilliseconds - Date.now(),
  );
  const seconds = Math.ceil(remaining / 1000);
  elements.restCountdown.value = String(seconds);
  elements.restCountdown.textContent = String(seconds);
  elements.restProgressFill.style.transform = `scaleX(${remaining / REST_DURATION_MS})`;
  if (remaining <= 0) {
    completeRest();
  }
}

function keepExercise() {
  if (!restActive || !session) {
    return;
  }
  if (session.keepPendingRest()) {
    persistState();
    completeRest();
  }
}

function completeRest() {
  if (!restActive || !session || !currentGroup) {
    return;
  }
  restActive = false;
  clearInterval(restTimer);
  restTimer = null;
  const keep = session.state.pendingRestKept;
  session.recordOutcome(currentGroup, keep);
  session.clearPendingRest();
  persistState();
  if (session.state.workoutCompleted) {
    showCompletion(true);
  } else {
    showNextExercise();
  }
}

function showCompletion(playCue) {
  releaseWakeLock();
  stopRuntimeTimers();
  clearExerciseMedia();
  resetMovementVisuals();
  currentGroup = null;
  currentExercise = null;
  showScreen("completion");
  if (playCue) {
    playSound("complete");
  }
  elements.status.textContent = "Workout complete.";
}

function closeCompletion() {
  if (!session?.state.workoutCompleted) {
    return;
  }
  session.acknowledgeCompletion();
  persistState();
  showDuration();
}

function stopMovementTimer() {
  clearInterval(movementTimer);
  movementTimer = null;
  movementRunning = false;
  movementPauseReason = null;
  movementEndsAt = 0;
  movementRemaining = 0;
  elements.skipExercise.disabled = true;
}

function stopRuntimeTimers() {
  stopMovementTimer();
  restActive = false;
  clearInterval(restTimer);
  restTimer = null;
}

function resetMovementVisuals() {
  lastMovementPhase = null;
  elements.phaseSurface.classList.remove("visible");
  elements.phaseLeft.style.backgroundColor = "";
  elements.phaseRight.style.backgroundColor = "";
  elements.movePanel.classList.remove("change");
  elements.mediaCard.classList.remove("resting");
  elements.movementCountdown.value = "45";
  elements.movementCountdown.textContent = "45";
  elements.movementProgressFill.style.transform = "scaleX(1)";
  elements.restCountdown.value = "15";
  elements.restCountdown.textContent = "15";
  elements.restProgressFill.style.transform = "scaleX(1)";
  setMediaMirrored(false);
}

function setFullPhaseSurface(kind) {
  const color = kind === "move" ? "var(--move-surface)" : "var(--rest-surface)";
  elements.phaseLeft.style.backgroundColor = color;
  elements.phaseRight.style.backgroundColor = color;
  elements.phaseSurface.classList.add("visible");
}

function setSplitPhaseSurface(activeScreenSide) {
  elements.phaseLeft.style.backgroundColor =
    activeScreenSide === "Left" ? "var(--move-surface)" : "var(--rest-surface)";
  elements.phaseRight.style.backgroundColor =
    activeScreenSide === "Right" ? "var(--move-surface)" : "var(--rest-surface)";
  elements.phaseSurface.classList.add("visible");
}

function setMediaMirrored(mirrored) {
  const scale = mirrored ? "-1" : "1";
  elements.video.style.setProperty("--media-scale-x", scale);
  elements.holdFrame.style.setProperty("--media-scale-x", scale);
}

function cueSymbol(cue) {
  return {
    Move: "▶",
    Switch: "⇄",
    ScreenLeft: "▶",
    ScreenRight: "▶",
    Forward: "↓",
    Backward: "↑",
    Clockwise: "↻",
    Counterclockwise: "↺",
    Inward: "⇥",
    Outward: "⇤",
  }[cue] ?? "▶";
}

function movementCueDescription(cue) {
  return {
    Move: "Move",
    Switch: "Change",
    ScreenLeft: "Left side",
    ScreenRight: "Right side",
    Forward: "Forward",
    Backward: "Backward",
    Clockwise: "Clockwise",
    Counterclockwise: "Counterclockwise",
    Inward: "Inward",
    Outward: "Outward",
  }[cue] ?? "Move";
}

function playVideo() {
  const playback = elements.video.play();
  if (playback && typeof playback.catch === "function") {
    playback.catch(() => {
      if (movementRunning) {
        pauseMovement("buffering");
      }
      if (movementPauseReason) {
        scheduleMediaRecoveryFailure(mediaGeneration);
      }
    });
  }
}

function playSound(name) {
  const audio = sounds[name];
  if (!audio) {
    return;
  }
  try {
    audio.currentTime = 0;
    const playback = audio.play();
    if (playback && typeof playback.catch === "function") {
      playback.catch(() => {});
    }
  } catch {
    // Cues are best-effort and never block a workout.
  }
}

function handleVisibilityChange() {
  if (document.hidden) {
    clearMediaRecoveryTimer();
    if (movementRunning) {
      pauseMovement("visibility");
    }
    clearInterval(restTimer);
    restTimer = null;
    elements.video.pause();
    return;
  }

  if (restActive) {
    startRestTimer();
  } else if (movementPauseReason) {
    resumePausedMovementWhenVisible();
  } else if (!elements.readyPanel.hidden && mediaReady && currentExercise?.presentation !== "Still") {
    playVideo();
  }
  if (!elements.workoutScreen.hidden) {
    requestWakeLock();
  }
}

function scheduleMediaRecoveryFailure(generation) {
  if (document.hidden || mediaRecoveryTimer) {
    return;
  }
  mediaRecoveryTimer = setTimeout(() => {
    mediaRecoveryTimer = null;
    if (generation === mediaGeneration && !document.hidden && !movementRunning) {
      showMediaError(generation);
    }
  }, MEDIA_RECOVERY_TIMEOUT_MS);
}

function clearMediaRecoveryTimer() {
  clearTimeout(mediaRecoveryTimer);
  mediaRecoveryTimer = null;
}

async function requestWakeLock() {
  if (
    wakeLock ||
    wakeLockRequestPending ||
    document.hidden ||
    elements.workoutScreen.hidden ||
    !navigator.wakeLock?.request
  ) {
    return;
  }

  const generation = wakeLockGeneration;
  wakeLockRequestPending = true;
  try {
    const requestedLock = await navigator.wakeLock.request("screen");
    if (generation !== wakeLockGeneration || elements.workoutScreen.hidden) {
      await requestedLock.release();
      return;
    }
    wakeLock = requestedLock;
    requestedLock.addEventListener("release", () => {
      if (wakeLock === requestedLock) {
        wakeLock = null;
      }
    });
  } catch {
    // Unsupported or denied wake locks do not block a workout.
  } finally {
    wakeLockRequestPending = false;
  }
}

function releaseWakeLock() {
  wakeLockGeneration++;
  const activeLock = wakeLock;
  wakeLock = null;
  if (activeLock) {
    activeLock.release().catch(() => {});
  }
}
