export const SUPPORTED_MINUTES = Object.freeze([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
export const MOVEMENT_DURATION_MS = 45_000;
export const REST_DURATION_MS = 15_000;
export const CURRENT_CATALOG_REVISION = 15;
export const LAST_CUMULATIVE_CATALOG_REVISION = 3;
export const SCOPED_CATALOG_INVALIDATIONS_BY_REVISION = new Map([
  [4, new Set([591])],
  [5, new Set([266])],
  [6, new Set([266])],
  [7, new Set([326])],
  [8, new Set([211, 212, 213, 214, 232, 233, 234, 236])],
  [9, new Set([195])],
  [10, new Set([126, 135, 338, 686])],
  [11, new Set([
    211, 213, 214, 215, 216, 217, 218, 232,
    233, 234, 236, 237, 240, 241, 283, 289,
  ])],
  [12, new Set([513, 843])],
  [13, new Set([223, 224, 225, 245, 246])],
]);
const ALTERNATING_PREFIX = "Alternating ";
const CONTINUOUS_ALTERNATION_NORMALIZATION_IDS = new Set();
export const APPROVED_EXERCISE_CORRECTIONS = new Map([
  [105, ["Plie Squat", "Wide Turned-Out Squat"]],
  [188, ["Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat"]],
  [197, ["First-Position Plie-Releve", "Parallel Squat-to-Calf Raise"]],
  [198, ["Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise"]],
  [199, ["Alternating Deep Side Lunge", "Wide-Stance Side-to-Side Squat"]],
  [255, ["Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise"]],
  [270, ["Bodyweight Svend Press", "Palm-Squeeze Forward Press"]],
  [290, ["Universe-in-Motion Qigong", "Low Palm Scoop to Side Opening"]],
  [231, ["Karate Reverse Punch", "Step-Through Karate Reverse Punch"]],
  [394, ["Standing Arms Open and Close", "Inhale Arms Open, Exhale Arms Close and Round"]],
  [395, ["Standing Overhead Arm Sweep", "Overhead Hold with Deep Ribcage Breaths"]],
  [397, ["Staggered-Stance Weight Shift", "Exhale Forward, Inhale Back Weight Shift"]],
  [398, ["Standing Hug and Arm Expansion", "Inhale Arms Open, Exhale Self-Hug and Fold"]],
  [399, ["Shallow Squat with Chest-Opening Arms", "Inhale Chest Open, Exhale Arms Close with Shallow Squat"]],
  [400, ["Shallow Squat with Overhead Arm Circle", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down"]],
  [401, ["Alternating Weight Shift with Arm Swing", "Alternating Inhale-Twist, Exhale-Push"]],
  [402, ["Shibashi Rowing-a-Boat Breathing", "Shallow Squat with Rowing Arm Circle"]],
  [403, ["Shibashi Alternating Pushing-Palms Breathing", "Alternating Weight Shift with Palm Push"]],
  [404, ["Shibashi Alternating Punch Breathing", "Wide-Stance Alternating Slow Punch"]],
  [405, ["Shibashi Flying-Wild-Goose Breathing", "Shallow Squat with Wing Arm Raise"]],
  [406, ["Shibashi Spinning-Wheels Breathing", "Standing Wheel Arm Circles"]],
  [409, ["Neck Controlled Articular Rotation", "Full Neck Circles"]],
  [425, ["Chin-Tuck Isometric", "Chin-Tuck Hold"]],
  [588, ["Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls"]],
  [617, ["Standing Side-Leg Circles", "Standing Forward Side-Leg Circles"]],
  [626, ["Sumo Stance", "Sumo Squat Hold"]],
  [969, ["Chair-Pose Core Hold", "Chair-Pose Hold"]],
]);

export const ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES = new Map([
  [394, new Set(["Standing Open-and-Close Breathing"])],
  [395, new Set(["Standing Overhead Rib-Expansion Breathing"])],
  [397, new Set(["Breath-Integrated Weight Shift"])],
  [398, new Set(["Standing Arm-Expansion Breathing"])],
  [399, new Set(["Shibashi Opening-the-Chest Breathing"])],
  [400, new Set(["Shibashi Separating-the-Clouds Breathing"])],
  [401, new Set(["Shibashi Alternating Swinging-Arms Breathing"])],
]);

const CANONICAL_GROUPS = Object.freeze([
  null,
  "MedialAndDeepKneeExtensors",
  "PosteriorThighAndKneeFlexors",
  "MajorHipAdductors",
  "LateralKneeExtensors",
  "GlutealExtensors",
  "SpinalExtensors",
  "CalfDeepPosteriorLegAndPlantarFoot",
  "Soleus",
  "ScapularGirdle",
  "ShoulderAdductorsAndExtensors",
  "AbdominalWall",
  "HipAbductors",
  "Chest",
  "ElbowExtensors",
  "HipFlexors",
  "AnteriorLateralLowerLegAndDorsalFoot",
  "DeepHipRotators",
  "ShoulderAbductors",
  "ForearmFlexorsAndPronators",
  "DeepAndIntersegmentalBack",
  "ElbowFlexors",
  "BreathingMuscles",
  "ForearmExtensorsAndSupinators",
  "RotatorCuff",
  "AccessoryHipAdductors",
  "PosteriorNeckAndSuboccipitalMuscles",
  "CranialMuscles",
  "AnteriorLateralNeckAndHyoidMuscles",
  "IntrinsicHand",
  "PelvicFloorAndPerineum",
]);

const CANONICAL_DISPLAY_NAMES = Object.freeze([
  null,
  "Medial and deep knee extensors",
  "Posterior thigh and knee flexors",
  "Major hip adductors",
  "Lateral knee extensors",
  "Gluteal extensors",
  "Spinal extensors",
  "Calf, deep posterior leg and plantar foot",
  "Soleus",
  "Scapular girdle",
  "Shoulder adductors and extensors",
  "Abdominal wall",
  "Hip abductors",
  "Chest",
  "Elbow extensors",
  "Hip flexors",
  "Anterior/lateral lower leg and dorsal foot",
  "Deep hip rotators",
  "Shoulder abductors",
  "Forearm flexors and pronators",
  "Deep and intersegmental back",
  "Elbow flexors",
  "Breathing muscles",
  "Forearm extensors and supinators",
  "Rotator cuff",
  "Accessory hip adductors",
  "Posterior neck and suboccipital muscles",
  "Cranial muscles",
  "Anterior/lateral neck and hyoid muscles",
  "Intrinsic hand",
  "Pelvic floor and perineum",
]);

const CANONICAL_KEYS = Object.freeze([
  null,
  "medial-deep-knee-extensors",
  "posterior-thigh-knee-flexors",
  "major-hip-adductors",
  "lateral-knee-extensors",
  "gluteal-extensors",
  "spinal-extensors",
  "calf-deep-posterior-leg-plantar-foot",
  "soleus",
  "scapular-girdle",
  "shoulder-adductors-extensors",
  "abdominal-wall",
  "hip-abductors",
  "chest",
  "elbow-extensors",
  "hip-flexors",
  "anterior-lateral-lower-leg-dorsal-foot",
  "deep-hip-rotators",
  "shoulder-abductors",
  "forearm-flexors-pronators",
  "deep-intersegmental-back",
  "elbow-flexors",
  "breathing-muscles",
  "forearm-extensors-supinators",
  "rotator-cuff",
  "accessory-hip-adductors",
  "posterior-neck-suboccipital",
  "cranial-muscles",
  "anterior-lateral-neck-hyoid",
  "intrinsic-hand",
  "pelvic-floor-perineum",
]);

function bucket(key, displayName, ...canonicalIds) {
  return {
    key,
    displayName,
    canonicalGroups: canonicalIds.map((id) => CANONICAL_GROUPS[id]),
  };
}

function canonicalBucket(canonicalId) {
  return bucket(
    CANONICAL_KEYS[canonicalId],
    CANONICAL_DISPLAY_NAMES[canonicalId],
    canonicalId,
  );
}

function resolution(minutes, declaredLargestToSmallest) {
  const groups = [...declaredLargestToSmallest]
    .reverse()
    .map((group, index) =>
      Object.freeze({
        id: `r${minutes}.${group.key}`,
        displayName: group.displayName,
        order: index + 1,
        canonicalGroups: Object.freeze([...group.canonicalGroups]),
      }),
    );
  return Object.freeze({ minutes, groups: Object.freeze(groups) });
}

export const RESOLUTIONS = new Map([
  [
    3,
    resolution(3, [
      bucket("lower-limbs", "Lower limbs", 1, 2, 3, 4, 5, 7, 8, 12, 15, 16, 17, 25),
      bucket("torso-pelvic-complex", "Torso and pelvic complex", 6, 11, 13, 20, 22, 30),
      bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29),
    ]),
  ],
  [
    5,
    resolution(5, [
      bucket("hips-thighs", "Hips and thighs", 1, 2, 3, 4, 5, 12, 15, 17, 25),
      bucket("torso", "Torso", 6, 11, 13, 20, 22, 30),
      bucket("lower-legs-feet", "Lower legs and feet", 7, 8, 16),
      bucket("upper-limbs", "Upper limbs", 10, 14, 18, 19, 21, 23, 24, 29),
      bucket("head-neck-shoulder-girdle", "Head, neck and shoulder girdle", 9, 26, 27, 28),
    ]),
  ],
  [
    7,
    resolution(7, [
      bucket("torso", "Torso", 6, 11, 13, 20, 22, 30),
      bucket("knee-extensors", "Knee extensors", 1, 4),
      bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29),
      bucket("lower-legs-feet", "Lower legs and feet", 7, 8, 16),
      bucket("hip-flexors-adductors", "Hip flexors and adductors", 3, 15, 25),
      bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 5, 12, 17),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
    ]),
  ],
  [
    10,
    resolution(10, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("hip-flexors-adductors", "Hip flexors and adductors", 3, 15, 25),
      bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 5, 12, 17),
      bucket("back-abdominal-pelvic-floor", "Back, abdominal wall and pelvic floor", 6, 11, 20, 30),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("head-neck-scapular-chest-breathing", "Head, neck, scapular girdle, chest and breathing", 9, 13, 22, 26, 27, 28),
      bucket("posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot", 7, 8),
      bucket("upper-limbs", "Upper limbs", 10, 14, 18, 19, 21, 23, 24, 29),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
    ]),
  ],
  [
    15,
    resolution(15, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("hip-adductors", "Hip adductors", 3, 25),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("gluteal-extensors", "Gluteal extensors", 5),
      bucket("posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot", 7, 8),
      bucket("back-spinal-stabilization", "Back and spinal stabilization", 6, 20),
      bucket("scapular-chest-breathing", "Scapular girdle, chest and breathing", 9, 13, 22),
      bucket("lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers", 12, 17),
      bucket("arm-forearm-hand", "Arm, forearm and hand", 14, 19, 21, 23, 29),
      bucket("abdominal-pelvic-floor", "Abdominal wall and pelvic floor", 11, 30),
      bucket("shoulder", "Shoulder", 10, 18, 24),
      bucket("hip-flexors", "Hip flexors", 15),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
      bucket("head-neck", "Head and neck", 26, 27, 28),
    ]),
  ],
  [
    20,
    resolution(20, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("major-hip-adductors", "Major hip adductors", 3),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("gluteal-extensors", "Gluteal extensors", 5),
      bucket("soleus", "Soleus", 8),
      bucket("back-spinal-stabilization", "Back and spinal stabilization", 6, 20),
      bucket("calf-flexors-plantar-foot", "Calf flexors and plantar foot", 7),
      bucket("scapular-girdle", "Scapular girdle", 9),
      bucket("chest-breathing", "Chest and breathing", 13, 22),
      bucket("shoulder-adduction-extension", "Shoulder adduction and extension", 10),
      bucket("abdominal-pelvic-floor", "Abdominal wall and pelvic floor", 11, 30),
      bucket("lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers", 12, 17),
      bucket("upper-arm", "Upper arm", 14, 21),
      bucket("hip-flexors", "Hip flexors", 15),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
      bucket("shoulder-abduction-rotation", "Shoulder abduction and rotation", 18, 24),
      bucket("forearm-hand", "Forearm and hand", 19, 23, 29),
      bucket("accessory-hip-adductors", "Accessory hip adductors", 25),
      bucket("head-neck", "Head and neck", 26, 27, 28),
    ]),
  ],
  [
    30,
    resolution(
      30,
      Array.from({ length: 30 }, (_, index) => canonicalBucket(index + 1)),
    ),
  ],
]);

const ALL_GROUPS = new Map(
  [...RESOLUTIONS.values()].flatMap((item) => item.groups.map((group) => [group.id, group])),
);

export function getResolution(minutes) {
  const item = RESOLUTIONS.get(minutes);
  if (!item) {
    throw new RangeError("Workout duration must be 3, 5, 7, 10, 15, 20, or 30 minutes.");
  }
  return item;
}

export function getSelectionKey(group) {
  return group.selectionGroupId ?? group.id;
}

export function createWorkoutSchedule(minutes, extraSetSelectionGroupIds = null) {
  if (!SUPPORTED_MINUTES.includes(minutes)) {
    throw new RangeError("Unsupported workout duration.");
  }

  const resolution = getResolution(minutes > 30 ? 30 : minutes);
  if (minutes <= 30) {
    return resolution.groups;
  }

  const completeSets = Math.floor(minutes / resolution.groups.length);
  const extraSets = minutes % resolution.groups.length;
  const selectedExtraSets = extraSetSelectionGroupIds instanceof Set
    ? extraSetSelectionGroupIds
    : new Set(extraSets === 0
      ? []
      : resolution.groups.slice(-extraSets).map((group) => group.id));
  const rounds = [];
  for (let groupIndex = 0; groupIndex < resolution.groups.length; groupIndex++) {
    const selectionGroup = resolution.groups[groupIndex];
    const setCount = completeSets +
      (selectedExtraSets.has(selectionGroup.id) ? 1 : 0);
    for (let setNumber = 1; setNumber <= setCount; setNumber++) {
      rounds.push(Object.freeze({
        ...selectionGroup,
        id: `${selectionGroup.id}.set${setNumber}`,
        order: rounds.length + 1,
        selectionGroupId: selectionGroup.id,
      }));
    }
  }
  return Object.freeze(rounds);
}

export function normalizeMinutes(minutes) {
  if (!Number.isFinite(Number(minutes))) {
    return 10;
  }
  return [...SUPPORTED_MINUTES].sort((left, right) => {
    const distance = Math.abs(left - Number(minutes)) - Math.abs(right - Number(minutes));
    return distance || right - left;
  })[0];
}

export function getCanonicalCoverage(exercise, group) {
  const trained = new Set([
    exercise.primaryCanonicalGroup,
    ...(exercise.secondaryCanonicalGroups ?? []),
  ]);
  return group.canonicalGroups.filter((canonicalGroup) => trained.has(canonicalGroup)).length;
}

export function getRequiredCanonicalCoverage(group) {
  return Math.ceil(group.canonicalGroups.length / 2);
}

export function isSelectable(exercise, group) {
  return (
    group.canonicalGroups.includes(exercise.primaryCanonicalGroup) &&
    getCanonicalCoverage(exercise, group) >= getRequiredCanonicalCoverage(group)
  );
}

export function usesTimedPair(exercise) {
  return exercise.sideSequence !== "Continuous" || exercise.directionSequence !== "None";
}

export function getMovementPhaseState(remainingMilliseconds, timedPair) {
  if (remainingMilliseconds <= 0) {
    return { phase: "Complete", secondsRemaining: 0, segmentDurationSeconds: 0, isExercise: false };
  }

  const bounded = Math.min(remainingMilliseconds, MOVEMENT_DURATION_MS);
  if (!timedPair) {
    return {
      phase: "Continuous",
      secondsRemaining: Math.ceil(bounded / 1000),
      segmentDurationSeconds: 45,
      isExercise: true,
    };
  }

  if (bounded > 25_000) {
    return {
      phase: "FirstSide",
      secondsRemaining: Math.ceil((bounded - 25_000) / 1000),
      segmentDurationSeconds: 20,
      isExercise: true,
    };
  }

  if (bounded > 20_000) {
    return {
      phase: "ChangeSides",
      secondsRemaining: Math.ceil((bounded - 20_000) / 1000),
      segmentDurationSeconds: 5,
      isExercise: false,
    };
  }

  return {
    phase: "SecondSide",
    secondsRemaining: Math.ceil(bounded / 1000),
    segmentDurationSeconds: 20,
    isExercise: true,
  };
}

export function getMovementPresentation(exercise, phase) {
  if (phase === "Complete") {
    return { cue: "None", mirrorMedia: false, activeScreenSide: null };
  }

  if (!usesTimedPair(exercise)) {
    if (phase !== "Continuous") {
      throw new Error(`Continuous exercise cannot use ${phase}.`);
    }
    return { cue: "Move", mirrorMedia: false, activeScreenSide: null };
  }

  if (phase === "ChangeSides") {
    return { cue: "Switch", mirrorMedia: false, activeScreenSide: null };
  }

  if (phase !== "FirstSide" && phase !== "SecondSide") {
    throw new Error(`Timed pair cannot use ${phase}.`);
  }

  const second = phase === "SecondSide";
  if (exercise.sideSequence !== "Continuous") {
    const firstCue =
      exercise.sideSequence === "ScreenLeftThenRight" ? "ScreenLeft" : "ScreenRight";
    const cue = second
      ? firstCue === "ScreenLeft"
        ? "ScreenRight"
        : "ScreenLeft"
      : firstCue;
    return {
      cue,
      mirrorMedia: second,
      activeScreenSide: cue === "ScreenLeft" ? "Left" : "Right",
    };
  }

  const pairs = {
    ForwardThenBackward: ["Forward", "Backward"],
    BackwardThenForward: ["Backward", "Forward"],
    ClockwiseThenCounterclockwise: ["Clockwise", "Counterclockwise"],
    CounterclockwiseThenClockwise: ["Counterclockwise", "Clockwise"],
    InwardThenOutward: ["Inward", "Outward"],
    OutwardThenInward: ["Outward", "Inward"],
  };
  const pair = pairs[exercise.directionSequence];
  if (!pair) {
    throw new Error(`Unknown direction sequence ${exercise.directionSequence}.`);
  }
  return { cue: pair[second ? 1 : 0], mirrorMedia: false, activeScreenSide: null };
}

export function getExerciseVideoPath(exercise) {
  return exercise.directionSequence === "None"
    ? exercise.video
    : `exercise_direction_videos/exercise_${formatExerciseId(exercise.id)}.mp4`;
}

export function getHoldFramePath(exercise) {
  return `exercise_hold_frames/exercise_${formatExerciseId(exercise.id)}.png`;
}

export function formatExerciseId(exerciseId) {
  return String(exerciseId).padStart(4, "0");
}

export function createDefaultState() {
  return {
    version: 3,
    catalogRevision: CURRENT_CATALOG_REVISION,
    catalogIdentities: {},
    selectedExerciseIds: {},
    scores: {},
    outcomes: {},
    lastKeptExerciseIds: [],
    activeExtraSetSelectionGroupIds: [],
    pendingRestGroupId: null,
    pendingRestEndsAtUnixMilliseconds: 0,
    pendingRestKept: false,
    lastWorkoutMinutes: 10,
    activeWorkoutMinutes: 0,
    workoutCompleted: false,
    completionAcknowledged: false,
  };
}

export function parseStoredState(serialized) {
  if (!serialized) {
    return createDefaultState();
  }
  try {
    return normalizeStateShape(JSON.parse(serialized));
  } catch {
    return createDefaultState();
  }
}

function normalizeStateShape(raw) {
  const state = createDefaultState();
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return state;
  }

  state.version = Number.isInteger(raw.version) ? raw.version : state.version;
  state.catalogRevision = Number.isInteger(raw.catalogRevision)
    ? raw.catalogRevision
    : 0;
  state.lastWorkoutMinutes = normalizeMinutes(raw.lastWorkoutMinutes);
  state.activeWorkoutMinutes = Number.isInteger(raw.activeWorkoutMinutes)
    ? raw.activeWorkoutMinutes
    : 0;
  state.workoutCompleted = raw.workoutCompleted === true;
  state.completionAcknowledged = raw.completionAcknowledged === true;
  state.pendingRestGroupId =
    typeof raw.pendingRestGroupId === "string" ? raw.pendingRestGroupId : null;
  state.pendingRestEndsAtUnixMilliseconds = Number.isFinite(raw.pendingRestEndsAtUnixMilliseconds)
    ? Math.trunc(raw.pendingRestEndsAtUnixMilliseconds)
    : 0;
  state.pendingRestKept = raw.pendingRestKept === true;

  for (const [groupId, exerciseId] of Object.entries(objectOrEmpty(raw.selectedExerciseIds))) {
    if (typeof groupId === "string" && Number.isInteger(exerciseId) && exerciseId > 0) {
      state.selectedExerciseIds[groupId] = exerciseId;
    }
  }
  for (const [exerciseId, score] of Object.entries(objectOrEmpty(raw.scores))) {
    if (/^\d+$/.test(exerciseId) && Number.isInteger(score)) {
      state.scores[exerciseId] = score;
    }
  }
  for (const [exerciseId, identity] of Object.entries(objectOrEmpty(raw.catalogIdentities))) {
    if (/^\d+$/.test(exerciseId) && typeof identity === "string") {
      state.catalogIdentities[exerciseId] = identity;
    }
  }
  for (const [groupId, outcome] of Object.entries(objectOrEmpty(raw.outcomes))) {
    if (outcome === "x" || outcome === "tick") {
      state.outcomes[groupId] = outcome;
    }
  }
  state.lastKeptExerciseIds = uniquePositiveIntegers(raw.lastKeptExerciseIds);
  state.activeExtraSetSelectionGroupIds = Array.isArray(raw.activeExtraSetSelectionGroupIds)
    ? [...new Set(raw.activeExtraSetSelectionGroupIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  return state;
}

function uniquePositiveIntegers(value) {
  return Array.isArray(value)
    ? [...new Set(value.filter((item) => Number.isInteger(item) && item > 0))]
    : [];
}

function objectOrEmpty(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

export class WorkoutSession {
  constructor(exercises, storedState = createDefaultState(), random = Math.random) {
    if (!Array.isArray(exercises)) {
      throw new TypeError("Exercise catalog must be an array.");
    }
    this.exercises = exercises;
    this.exercisesById = new Map(exercises.map((exercise) => [exercise.id, exercise]));
    if (this.exercisesById.size !== exercises.length) {
      throw new Error("Exercise catalog contains duplicate IDs.");
    }
    this.state = normalizeStateShape(storedState);
    this.random = random;
  }

  initialize() {
    this.reconcileCatalog();
    this.normalizeScores();
    this.normalizeSavedLineups();
    this.normalizeKeptExerciseIds();

    if (this.state.activeWorkoutMinutes === 0) {
      this.resetTransientState();
      return;
    }

    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      this.resetTransientState();
      return;
    }

    this.normalizeActiveExtraSetSelectionGroups();
    const activeGroupIds = new Set(this.getActiveGroups().map((group) => group.id));
    for (const groupId of Object.keys(this.state.outcomes)) {
      if (!activeGroupIds.has(groupId)) {
        delete this.state.outcomes[groupId];
      }
    }

    this.state.workoutCompleted = this.getActiveGroups().every(
      (group) => this.state.outcomes[group.id] !== undefined,
    );
    if (this.state.workoutCompleted) {
      if (this.state.completionAcknowledged) {
        this.prepareNextSession();
      }
      return;
    }

    this.state.completionAcknowledged = false;
    this.normalizePendingRest();
    this.repairActiveLineup();
    this.finishInterruptedWorkout();
  }

  startWorkout(minutes) {
    if (!SUPPORTED_MINUTES.includes(minutes)) {
      throw new RangeError("Unsupported workout duration.");
    }
    if (this.state.activeWorkoutMinutes !== 0) {
      throw new Error("A workout is already active.");
    }

    const previousWorkoutMinutes = normalizeMinutes(this.state.lastWorkoutMinutes);
    this.state.lastWorkoutMinutes = minutes;
    this.state.activeWorkoutMinutes = minutes;
    this.state.outcomes = {};
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingRest();
    this.carryKeptExercisesForward(previousWorkoutMinutes);
    this.repairActiveLineup();
    this.state.activeExtraSetSelectionGroupIds = this.chooseExtraSetSelectionGroups();
  }

  getActiveGroups() {
    return SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)
      ? createWorkoutSchedule(
          this.state.activeWorkoutMinutes,
          this.getEffectiveExtraSetSelectionGroups(),
        )
      : [];
  }

  getSelectionGroups() {
    return SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)
      ? getResolution(
          this.state.activeWorkoutMinutes > 30 ? 30 : this.state.activeWorkoutMinutes,
        ).groups
      : [];
  }

  getNextGroup() {
    return this.getActiveGroups().find((group) => this.state.outcomes[group.id] === undefined) ?? null;
  }

  getSelectedExercise(group) {
    const exercise = this.exercisesById.get(
      this.state.selectedExerciseIds[getSelectionKey(group)],
    );
    if (!exercise || !this.isSavedSelectionValid(exercise, group)) {
      throw new Error(`No eligible exercise selected for ${group.displayName}.`);
    }
    return exercise;
  }

  beginRest(group, endsAtUnixMilliseconds) {
    this.state.pendingRestGroupId = group.id;
    this.state.pendingRestEndsAtUnixMilliseconds = Math.trunc(endsAtUnixMilliseconds);
    this.state.pendingRestKept = false;
  }

  keepPendingRest() {
    if (!this.state.pendingRestGroupId) {
      return false;
    }
    this.state.pendingRestKept = true;
    return true;
  }

  clearPendingRest() {
    this.state.pendingRestGroupId = null;
    this.state.pendingRestEndsAtUnixMilliseconds = 0;
    this.state.pendingRestKept = false;
  }

  recordOutcome(group, keep) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }

    return this.applyOutcome(group, keep);
  }

  applyOutcome(group, keep) {
    const exercise = this.getSelectedExercise(group);
    if (!keep) {
      this.setScore(exercise, this.getScore(exercise) - 1);
    }
    this.state.outcomes[group.id] = keep ? "tick" : "x";
    this.state.workoutCompleted = this.getActiveGroups().every(
      (activeGroup) => this.state.outcomes[activeGroup.id] !== undefined,
    );
    this.state.completionAcknowledged = false;
    return exercise;
  }

  acknowledgeCompletion() {
    if (!this.state.workoutCompleted) {
      throw new Error("Workout is not complete.");
    }
    this.state.completionAcknowledged = true;
    this.prepareNextSession();
  }

  finishInterruptedWorkout() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      this.resetTransientState();
      return;
    }

    if (this.state.pendingRestGroupId) {
      const group = this.getActiveGroups().find(
        (candidate) => candidate.id === this.state.pendingRestGroupId,
      );
      if (group && this.state.outcomes[group.id] === undefined) {
        this.applyOutcome(group, this.state.pendingRestKept);
      }
      this.clearPendingRest();
    }
    this.prepareNextSession();
  }

  prepareNextSession() {
    const activeGroups = this.getActiveGroups();
    const selectionGroups = this.getSelectionGroups();
    const rejectedSelectionKeys = new Set(
      activeGroups
        .filter((group) => this.state.outcomes[group.id] === "x")
        .map(getSelectionKey),
    );
    const newlyKeptExerciseIds = new Set(
      selectionGroups
        .filter((group) => {
          const rounds = activeGroups.filter((round) => getSelectionKey(round) === group.id);
          return rounds.some((round) => this.state.outcomes[round.id] === "tick") &&
            rounds.every((round) => this.state.outcomes[round.id] !== "x");
        })
        .map((group) => this.state.selectedExerciseIds[group.id])
        .filter(Boolean),
    );
    const rejectedExerciseIds = new Set(
      [...rejectedSelectionKeys]
        .map((selectionKey) => this.state.selectedExerciseIds[selectionKey])
        .filter(Boolean),
    );
    this.state.lastKeptExerciseIds = [...new Set([
      ...this.state.lastKeptExerciseIds.filter(
        (exerciseId) => !rejectedExerciseIds.has(exerciseId),
      ),
      ...newlyKeptExerciseIds,
    ])];
    const usedExerciseIds = new Set(
      selectionGroups
        .filter((group) => !rejectedSelectionKeys.has(group.id))
        .map((group) => this.state.selectedExerciseIds[group.id])
        .filter(Boolean),
    );

    for (const group of selectionGroups.filter((candidate) =>
      rejectedSelectionKeys.has(candidate.id))) {
      const rejectedExerciseId = this.state.selectedExerciseIds[group.id];
      for (const [savedGroupId, savedExerciseId] of Object.entries(this.state.selectedExerciseIds)) {
        if (savedGroupId !== group.id && savedExerciseId === rejectedExerciseId) {
          delete this.state.selectedExerciseIds[savedGroupId];
        }
      }

      const replacement = this.chooseBestCandidate(
        group,
        new Set([...usedExerciseIds, rejectedExerciseId]),
      );
      this.state.selectedExerciseIds[group.id] = replacement.id;
      usedExerciseIds.add(replacement.id);
    }

    this.resetTransientState();
  }

  repairActiveLineup() {
    const usedExerciseIds = new Set();
    const activeGroups = this.getActiveGroups();
    for (const group of this.getSelectionGroups()) {
      const selectedId = this.state.selectedExerciseIds[group.id];
      const selected = this.exercisesById.get(selectedId);
      const valid =
        selected &&
        !usedExerciseIds.has(selectedId) &&
        this.isSavedSelectionValid(selected, group);

      let resolvedId = selectedId;
      if (!valid) {
        const excluded = new Set(usedExerciseIds);
        if (selectedId) {
          excluded.add(selectedId);
        }
        const replacement = this.chooseBestCandidate(group, excluded);
        resolvedId = replacement.id;
        this.state.selectedExerciseIds[group.id] = resolvedId;
        for (const round of activeGroups.filter((candidate) =>
          getSelectionKey(candidate) === group.id)) {
          delete this.state.outcomes[round.id];
        }
        if (this.pendingRestMatchesSelectionGroup(group.id)) {
          this.clearPendingRest();
        }
      }
      usedExerciseIds.add(resolvedId);
    }
  }

  carryKeptExercisesForward(previousWorkoutMinutes) {
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    if (keptExerciseIds.size === 0) {
      return;
    }

    const previousGroups = getResolution(
      previousWorkoutMinutes > 30 ? 30 : previousWorkoutMinutes,
    ).groups;
    const orderedKeptExerciseIds = [...new Set([
      ...previousGroups.map((group) => this.state.selectedExerciseIds[group.id]),
      ...[...keptExerciseIds].sort((left, right) => left - right),
    ].filter((exerciseId) => keptExerciseIds.has(exerciseId)))];
    const targetGroups = this.getSelectionGroups();
    const assignedTargetGroupIds = new Set();

    for (const exerciseId of orderedKeptExerciseIds) {
      const exercise = this.exercisesById.get(exerciseId);
      const targetGroup = exercise
        ? targetGroups.find((group) => isSelectable(exercise, group))
        : null;
      if (!targetGroup || assignedTargetGroupIds.has(targetGroup.id)) {
        continue;
      }

      this.state.selectedExerciseIds[targetGroup.id] = exerciseId;
      assignedTargetGroupIds.add(targetGroup.id);
    }
  }

  chooseBestCandidate(group, excludedExerciseIds = new Set()) {
    const candidates = this.exercises.filter(
      (exercise) => isSelectable(exercise, group) && !excludedExerciseIds.has(exercise.id),
    );
    if (candidates.length === 0) {
      throw new Error(`No eligible exercise exists for ${group.displayName}.`);
    }

    const highestScore = Math.max(...candidates.map((exercise) => this.getScore(exercise)));
    const highestScored = candidates.filter((exercise) => this.getScore(exercise) === highestScore);
    const widestCoverage = Math.max(
      ...highestScored.map((exercise) => getCanonicalCoverage(exercise, group)),
    );
    const finalists = highestScored.filter(
      (exercise) => getCanonicalCoverage(exercise, group) === widestCoverage,
    );
    const index = Math.min(finalists.length - 1, Math.floor(this.random() * finalists.length));
    return finalists[Math.max(0, index)];
  }

  getScore(exercise) {
    const saved = this.state.scores[String(exercise.id)];
    return Number.isInteger(saved) ? saved : Number.isInteger(exercise.score) ? exercise.score : 0;
  }

  setScore(exercise, score) {
    this.state.scores[String(exercise.id)] = Math.trunc(score);
  }

  normalizeSavedLineups() {
    for (const [groupId, exerciseId] of Object.entries(this.state.selectedExerciseIds)) {
      const group = ALL_GROUPS.get(groupId);
      const exercise = this.exercisesById.get(exerciseId);
      if (!group || !exercise || !this.isSavedSelectionValid(exercise, group)) {
        delete this.state.selectedExerciseIds[groupId];
      }
    }
  }

  normalizeScores() {
    for (const exerciseId of Object.keys(this.state.scores)) {
      if (!this.exercisesById.has(Number(exerciseId))) {
        delete this.state.scores[exerciseId];
      }
    }
  }

  normalizeKeptExerciseIds() {
    this.state.lastKeptExerciseIds = this.state.lastKeptExerciseIds.filter((exerciseId) =>
      this.exercisesById.has(exerciseId));
  }

  normalizeActiveExtraSetSelectionGroups() {
    const expectedExtraSets = this.getExtraSetCount();
    const selectionGroupIds = new Set(this.getSelectionGroups().map((group) => group.id));
    const valid = this.state.activeExtraSetSelectionGroupIds.length === expectedExtraSets &&
      this.state.activeExtraSetSelectionGroupIds.every((groupId) =>
        selectionGroupIds.has(groupId));
    if (!valid) {
      this.state.activeExtraSetSelectionGroupIds = this.chooseExtraSetSelectionGroups();
    }
  }

  getExtraSetCount() {
    if (this.state.activeWorkoutMinutes <= 30) {
      return 0;
    }
    return this.state.activeWorkoutMinutes % this.getSelectionGroups().length;
  }

  chooseExtraSetSelectionGroups() {
    const extraSets = this.getExtraSetCount();
    if (extraSets === 0) {
      return [];
    }
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    return [...this.getSelectionGroups()]
      .sort((left, right) => {
        const leftKept = keptExerciseIds.has(this.state.selectedExerciseIds[left.id]) ? 1 : 0;
        const rightKept = keptExerciseIds.has(this.state.selectedExerciseIds[right.id]) ? 1 : 0;
        return rightKept - leftKept || right.order - left.order;
      })
      .slice(0, extraSets)
      .map((group) => group.id);
  }

  getEffectiveExtraSetSelectionGroups() {
    const expectedExtraSets = this.getExtraSetCount();
    const groupIds = this.state.activeExtraSetSelectionGroupIds.length === expectedExtraSets
      ? this.state.activeExtraSetSelectionGroupIds
      : this.chooseExtraSetSelectionGroups();
    return new Set(groupIds);
  }

  normalizePendingRest() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    const pendingExercise = pendingGroup
      ? this.exercisesById.get(
          this.state.selectedExerciseIds[getSelectionKey(pendingGroup)],
        )
      : null;
    if (
      !pendingGroup ||
      !pendingExercise ||
      this.state.pendingRestEndsAtUnixMilliseconds <= 0 ||
      this.state.outcomes[pendingGroup.id] !== undefined ||
      !this.isAssignedToGroup(pendingExercise, pendingGroup)
    ) {
      this.clearPendingRest();
    }
  }

  isSavedSelectionValid(exercise, group) {
    return (
      isSelectable(exercise, group) ||
      (this.pendingRestMatchesSelectionGroup(getSelectionKey(group)) &&
        this.isAssignedToGroup(exercise, group))
    );
  }

  pendingRestMatchesSelectionGroup(selectionGroupId) {
    if (!this.state.pendingRestGroupId) {
      return false;
    }
    return this.getActiveGroups().some(
      (round) =>
        round.id === this.state.pendingRestGroupId &&
        getSelectionKey(round) === selectionGroupId,
    );
  }

  isAssignedToGroup(exercise, group) {
    return (
      group.canonicalGroups.includes(exercise.primaryCanonicalGroup) ||
      (exercise.secondaryCanonicalGroups ?? []).some((canonicalGroup) =>
        group.canonicalGroups.includes(canonicalGroup),
      )
    );
  }

  reconcileCatalog() {
    const previousIdentities = this.state.catalogIdentities;
    const currentIdentities = Object.fromEntries(
      this.exercises.map((exercise) => [String(exercise.id), catalogIdentity(exercise)]),
    );
    const changedExerciseIds = catalogInvalidationIdsSince(
      this.state.catalogRevision,
      this.exercises,
    );

    for (const [exerciseId, previousIdentity] of Object.entries(previousIdentities)) {
      const currentIdentity = currentIdentities[exerciseId];
      const currentExercise = this.exercisesById.get(Number(exerciseId));
      if (
        currentIdentity === undefined ||
        (currentIdentity !== previousIdentity &&
          !isApprovedIdentityPreservingNameChange(
            Number(exerciseId),
            previousIdentity,
            currentExercise,
          ))
      ) {
        changedExerciseIds.add(Number(exerciseId));
      }
    }

    if (changedExerciseIds.size > 0) {
      const affectedGroupIds = Object.entries(this.state.selectedExerciseIds)
        .filter(([, exerciseId]) => changedExerciseIds.has(exerciseId))
        .map(([groupId]) => groupId);
      for (const groupId of affectedGroupIds) {
        delete this.state.selectedExerciseIds[groupId];
        for (const round of this.getActiveGroups().filter((candidate) =>
          getSelectionKey(candidate) === groupId)) {
          delete this.state.outcomes[round.id];
        }
      }
      if (
        this.state.pendingRestGroupId &&
        affectedGroupIds.some((groupId) =>
          this.pendingRestMatchesSelectionGroup(groupId))
      ) {
        this.clearPendingRest();
      }
      for (const exerciseId of changedExerciseIds) {
        delete this.state.scores[String(exerciseId)];
      }
    }

    this.normalizeKeptExerciseIds();

    this.state.catalogIdentities = currentIdentities;
    this.state.catalogRevision = Math.max(
      this.state.catalogRevision,
      CURRENT_CATALOG_REVISION,
    );
    this.state.version = 3;
  }

  resetTransientState() {
    this.state.activeWorkoutMinutes = 0;
    this.state.outcomes = {};
    this.state.activeExtraSetSelectionGroupIds = [];
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingRest();
  }
}

function catalogIdentity(exercise) {
  return `${exercise.name}\u001f${exercise.video}`;
}

function catalogInvalidationIdsSince(priorRevision, exercises) {
  const invalidatedExerciseIds = new Set();
  if (priorRevision < LAST_CUMULATIVE_CATALOG_REVISION) {
    for (const exercise of exercises) {
      if (typeof exercise.retiredName === "string" && exercise.retiredName) {
        invalidatedExerciseIds.add(exercise.id);
      }
    }
  }

  for (const [revision, exerciseIds] of SCOPED_CATALOG_INVALIDATIONS_BY_REVISION) {
    if (revision > priorRevision) {
      for (const exerciseId of exerciseIds) {
        invalidatedExerciseIds.add(exerciseId);
      }
    }
  }

  return invalidatedExerciseIds;
}

function isApprovedIdentityPreservingNameChange(exerciseId, previousIdentity, currentExercise) {
  if (!currentExercise) {
    return false;
  }
  const separatorIndex = previousIdentity.indexOf("\u001f");
  if (separatorIndex < 0) {
    return false;
  }
  const previousName = previousIdentity.slice(0, separatorIndex);
  const previousVideo = previousIdentity.slice(separatorIndex + 1);
  if (previousVideo !== currentExercise.video) {
    return false;
  }

  const timedSideNormalization =
    currentExercise.sideSequence !== "Continuous" &&
    previousName.startsWith(ALTERNATING_PREFIX) &&
    previousName.slice(ALTERNATING_PREFIX.length) === currentExercise.name;
  const continuousAlternationNormalization =
    CONTINUOUS_ALTERNATION_NORMALIZATION_IDS.has(exerciseId) &&
    currentExercise.sideSequence === "Continuous" &&
    currentExercise.name.startsWith(ALTERNATING_PREFIX) &&
    previousName === currentExercise.name.slice(ALTERNATING_PREFIX.length);
  const correction = APPROVED_EXERCISE_CORRECTIONS.get(exerciseId);
  const additionalCorrectionNames =
    ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES.get(exerciseId);
  const approvedExerciseCorrection =
    correction !== undefined &&
    (previousName === correction[0] ||
      additionalCorrectionNames?.has(previousName) === true) &&
    currentExercise.name === correction[1];

  return (
    timedSideNormalization ||
    continuousAlternationNormalization ||
    approvedExerciseCorrection
  );
}
