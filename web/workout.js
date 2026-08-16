export const SUPPORTED_MINUTES = Object.freeze([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
export const WORKOUT_MODIFIERS = Object.freeze({
  None: 0,
  Insect: 1,
  Silence: 2,
});
export const EXERCISE_INSECT_COMPATIBILITY = Object.freeze({
  Unreviewed: "Unreviewed",
  Compatible: "Compatible",
  Incompatible: "Incompatible",
});
const MODIFIER_RULES = Object.freeze([
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Insect,
    isReviewed: (exercise) =>
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    isCompatible: (exercise) =>
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible,
    requiresExclusionFloor: true,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Silence,
    isReviewed: (exercise) => typeof exercise.silent === "boolean",
    isCompatible: (exercise) => exercise.silent === true,
    requiresExclusionFloor: false,
  }),
]);
export const SUPPORTED_WORKOUT_MODIFIER_MASK = MODIFIER_RULES.reduce(
  (mask, rule) => mask | rule.flag,
  WORKOUT_MODIFIERS.None,
);
export const SUPPORTED_WORKOUT_MODIFIER_PROFILES = Object.freeze(
  Array.from({ length: 1 << MODIFIER_RULES.length }, (_, profileIndex) =>
    MODIFIER_RULES.reduce(
      (profile, rule, ruleIndex) =>
        (profileIndex & (1 << ruleIndex)) !== 0
          ? profile | rule.flag
          : profile,
      WORKOUT_MODIFIERS.None,
    )),
);
const SELECTION_PROFILE_PREFIX = "p";
const SELECTION_PROFILE_SEPARATOR = "|";
const MINIMUM_CANONICAL_COVERAGE_PERCENT = 50;
export const MINIMUM_EXCLUDED_EXERCISES_PER_GROUP = 5;
export const DEFAULT_WORKOUT_MODIFIERS = WORKOUT_MODIFIERS.Silence;
export const CURRENT_WORKOUT_STATE_VERSION = 5;
export const MOVEMENT_DURATION_MS = 45_000;
export const FULL_SIDE_MOVEMENT_DURATION_MS = 105_000;
export const PREPARATION_DURATION_MS = 5_000;
export const REST_DURATION_MS = 15_000;
export const CURRENT_CATALOG_REVISION = 25;
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
  [16, new Set([234, 239, 240])],
  [18, new Set([115, 119, 140, 212, 260, 326, 340, 512, 649])],
  [20, new Set([
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ])],
  [21, new Set([
    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
    615, 618, 677, 683, 685, 745, 816, 834,
  ])],
  [22, new Set([
    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
  ])],
  [23, new Set([
    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
  ])],
  [24, new Set([
    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
  ])],
  [25, new Set([
    31, 219, 248, 282, 390, 394, 395,
    397, 508, 576, 577, 618, 816, 834,
  ])],
]);
export const SCOPED_SCORE_INVALIDATIONS_BY_REVISION = new Map([
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
  [16, new Set([234, 239, 240])],
  [18, new Set([115, 212, 260, 512, 649])],
  [20, new Set([
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ])],
  [21, new Set([
    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
    615, 618, 677, 683, 685, 745, 816, 834,
  ])],
  [22, new Set([
    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
  ])],
  [23, new Set([
    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
  ])],
  [24, new Set([
    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
  ])],
]);
const ALTERNATING_PREFIX = "Alternating ";
const CONTINUOUS_ALTERNATION_NORMALIZATION_IDS = new Set();
export const APPROVED_EXERCISE_CORRECTIONS = new Map([
  [21, ["Standing-Scale Balance", "Standing-Scale Balance Hold"]],
  [105, ["Plie Squat", "Wide Turned-Out Squat"]],
  [119, ["Squat to Calf Raise", "Tiptoe Walk"]],
  [139, ["Wide-Squat Heel Raise", "Wide-Squat Alternating Heel Raises"]],
  [188, ["Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat"]],
  [197, ["First-Position Plie-Releve", "Parallel Squat-to-Calf Raise"]],
  [198, ["Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise"]],
  [199, ["Alternating Deep Side Lunge", "Wide-Stance Side-to-Side Squat"]],
  [255, ["Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise"]],
  [145, ["Standing Knee Extension", "Standing Knee-Extension Hold"]],
  [256, ["Self-Resisted Overhead Pull", "Self-Resisted Overhead Pull Hold"]],
  [257, ["Self-Resisted Chest-Level Pull", "Self-Resisted Chest-Level Pull Hold"]],
  [258, ["Self-Resisted Low Pull", "Self-Resisted Low Pull Hold"]],
  [262, ["Standing Hands-to-Thigh Abdominal Press", "Standing Hands-to-Thigh Abdominal Press Hold"]],
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
  [396, ["Unsupported Single-Leg Balance", "Unsupported Single-Leg Balance Hold"]],
  [510, ["Clasped-Hands Chest-Opening Forward Fold", "Clasped-Hands Chest-Opening Forward-Fold Hold"]],
  [588, ["Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls"]],
  [617, ["Standing Side-Leg Circles", "Standing Forward Side-Leg Circles"]],
  [626, ["Sumo Stance", "Sumo Squat Hold"]],
  [712, ["Standing Arms-Back Chest Opener", "Standing Arms-Back Chest-Opener Hold"]],
  [969, ["Chair-Pose Core Hold", "Chair-Pose Hold"]],
  [1000, ["Standing Forward Fold", "Standing Forward-Fold Hold"]],
  [136, ["Goddess Pose", "Wide Turned-Out Squat Hold"]],
  [225, ["Clenched-Fist Wrist Extensor Stretch", "Opposite-Hand Fist-Down Wrist Stretch"]],
  [241, ["Hook-Fist Tendon Glide", "Open Hand to Hook Fist"]],
  [242, ["Full-Fist Tendon Glide", "Open Hand to Full Fist"]],
  [251, ["Standing Swan-Dive Hinge", "Arm Sweep to Forward Hinge"]],
  [283, ["Straight-Fist Tendon Glide", "Open Hand to Straight Fist"]],
  [291, ["Open-to-Claw Tendon Glide", "Open Hand to Claw Fist"]],
  [293, ["Finger-Web Space Stretch", "Opposite-Hand Finger-Web Stretches"]],
  [683, ["Alternating Palm-Up T-Arm Flips", "Alternating Palm-Up Shoulder Rotations"]],
]);

export const ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES = new Map([
  [21, new Set(["Alternating Standing-Scale Balance"])],
  [145, new Set(["Alternating Standing Knee Extension"])],
  [231, new Set(["Alternating Karate Reverse Punch"])],
  [394, new Set(["Standing Open-and-Close Breathing"])],
  [395, new Set(["Standing Overhead Rib-Expansion Breathing"])],
  [397, new Set([
    "Breath-Integrated Weight Shift",
    "Alternating Breath-Integrated Weight Shift",
  ])],
  [398, new Set(["Standing Arm-Expansion Breathing"])],
  [399, new Set(["Shibashi Opening-the-Chest Breathing"])],
  [400, new Set(["Shibashi Separating-the-Clouds Breathing"])],
  [401, new Set(["Shibashi Alternating Swinging-Arms Breathing"])],
  [617, new Set(["Alternating Standing Side-Leg Circles"])],
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

export function createWorkoutSchedule(
  minutes,
  fullSideSelectionGroupIds = null,
  extraSetSelectionGroupIds = null,
) {
  if (!SUPPORTED_MINUTES.includes(minutes)) {
    throw new RangeError("Unsupported workout duration.");
  }

  const resolution = getResolution(minutes > 30 ? 30 : minutes);
  if (minutes <= 30) {
    return resolution.groups;
  }

  const extraMinutes = minutes - resolution.groups.length;
  const fullSideGroups = fullSideSelectionGroupIds instanceof Set
    ? fullSideSelectionGroupIds
    : new Set();
  const repeatedMinutes = extraMinutes - fullSideGroups.size;
  const completeExtraSets = Math.floor(repeatedMinutes / resolution.groups.length);
  const extraSets = repeatedMinutes % resolution.groups.length;
  const selectedExtraSets = extraSetSelectionGroupIds instanceof Set
    ? extraSetSelectionGroupIds
    : new Set(extraSets === 0
      ? []
      : resolution.groups.slice(-extraSets).map((group) => group.id));
  const rounds = [];
  for (let groupIndex = 0; groupIndex < resolution.groups.length; groupIndex++) {
    const selectionGroup = resolution.groups[groupIndex];
    const setCount = 1 + completeExtraSets +
      (selectedExtraSets.has(selectionGroup.id) ? 1 : 0);
    for (let setNumber = 1; setNumber <= setCount; setNumber++) {
      rounds.push(Object.freeze({
        ...selectionGroup,
        id: `${selectionGroup.id}.set${setNumber}`,
        order: rounds.length + 1,
        selectionGroupId: selectionGroup.id,
        usesFullSideTiming: setNumber === 1 && fullSideGroups.has(selectionGroup.id),
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
  return getCanonicalCoverage(exercise, group) >= getRequiredCanonicalCoverage(group);
}

export function isPrimaryForGroup(exercise, group) {
  return group.canonicalGroups.includes(exercise.primaryCanonicalGroup);
}

export function usesTimedPair(exercise) {
  return exercise.sideSequence !== "Continuous" || exercise.directionSequence !== "None";
}

export function getMovementDurationMs(group) {
  return group?.usesFullSideTiming ? FULL_SIDE_MOVEMENT_DURATION_MS : MOVEMENT_DURATION_MS;
}

export function isModifierMetadataComplete(exercises) {
  return exercises.every((exercise) =>
    MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)));
}

export function isCompatibleWithWorkoutModifiers(exercise, modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  return MODIFIER_RULES.every((rule) =>
    (normalized & rule.flag) === 0 || rule.isCompatible(exercise));
}

export function isSelectableForWorkoutProfile(exercise, group, modifiers) {
  return isSelectable(exercise, group) &&
    isCompatibleWithWorkoutModifiers(exercise, modifiers);
}

export function findWorkoutProfileCoverageDeficiencies(exercises) {
  return [...RESOLUTIONS.entries()].flatMap(([minutes, resolution]) =>
    resolution.groups.flatMap((group) =>
      SUPPORTED_WORKOUT_MODIFIER_PROFILES.map((profile) => ({
        minutes,
        groupId: group.id,
        groupName: group.displayName,
        profile,
        selectableExerciseCount: exercises.filter((exercise) =>
          isSelectableForWorkoutProfile(exercise, group, profile)).length,
      })).filter((result) => result.selectableExerciseCount < 10)));
}

export function getMaximumDistinctLineupSize(exercises, groups, modifiers) {
  const candidateExerciseIdsByGroup = groups
    .map((group) => [...new Set(exercises
      .filter((exercise) => isSelectableForWorkoutProfile(exercise, group, modifiers))
      .map((exercise) => exercise.id))])
    .sort((left, right) => left.length - right.length);
  const assignedGroupByExerciseId = new Map();

  function tryAssignDistinctExercise(groupIndex, visitedExerciseIds) {
    for (const exerciseId of candidateExerciseIdsByGroup[groupIndex]) {
      if (visitedExerciseIds.has(exerciseId)) {
        continue;
      }
      visitedExerciseIds.add(exerciseId);

      const assignedGroupIndex = assignedGroupByExerciseId.get(exerciseId);
      if (assignedGroupIndex === undefined ||
          tryAssignDistinctExercise(assignedGroupIndex, visitedExerciseIds)) {
        assignedGroupByExerciseId.set(exerciseId, groupIndex);
        return true;
      }
    }

    return false;
  }

  let matchedGroupCount = 0;
  for (let groupIndex = 0;
    groupIndex < candidateExerciseIdsByGroup.length;
    groupIndex += 1) {
    if (tryAssignDistinctExercise(groupIndex, new Set())) {
      matchedGroupCount += 1;
    }
  }

  return matchedGroupCount;
}

export function findWorkoutProfileLineupDeficiencies(exercises) {
  return SUPPORTED_MINUTES.flatMap((minutes) => {
    const groups = getResolution(minutes > 30 ? 30 : minutes).groups;
    return SUPPORTED_WORKOUT_MODIFIER_PROFILES
      .map((profile) => ({
        minutes,
        profile,
        maximumDistinctExerciseCount: getMaximumDistinctLineupSize(
          exercises,
          groups,
          profile,
        ),
        requiredDistinctExerciseCount: groups.length,
      }))
      .filter((result) =>
        result.maximumDistinctExerciseCount < result.requiredDistinctExerciseCount);
  });
}

export function findWorkoutModifierExclusionDeficiencies(exercises) {
  return [...RESOLUTIONS.entries()].flatMap(([minutes, resolution]) =>
    resolution.groups.flatMap((group) =>
      MODIFIER_RULES.filter((rule) => rule.requiresExclusionFloor)
        .flatMap((rule) =>
        [...new Set(SUPPORTED_WORKOUT_MODIFIER_PROFILES.map((profile) =>
          profile & ~rule.flag))]
          .map((contextProfile) => ({
            minutes,
            groupId: group.id,
            groupName: group.displayName,
            modifier: rule.flag,
            contextProfile,
            excludedExerciseCount: new Set(exercises
              .filter((exercise) =>
                isSelectable(exercise, group) &&
                isCompatibleWithWorkoutModifiers(exercise, contextProfile) &&
                rule.isReviewed(exercise) &&
                !rule.isCompatible(exercise))
              .map((exercise) => exercise.id)).size,
            requiredExcludedExerciseCount: MINIMUM_EXCLUDED_EXERCISES_PER_GROUP,
          }))
          .filter((result) =>
            result.excludedExerciseCount < result.requiredExcludedExerciseCount))));
}

function solveMaximumWeightAssignment(utilities, allowed, maximumUtility) {
  const groupCount = utilities.length;
  const candidateCount = utilities[0]?.length ?? 0;
  if (candidateCount < groupCount) {
    return Array(groupCount).fill(-1);
  }

  const invalidCost = (maximumUtility + 1) * (groupCount + 1);
  const costs = utilities.map((row, groupIndex) =>
    row.map((utility, candidateIndex) =>
      allowed[groupIndex][candidateIndex]
        ? maximumUtility - utility
        : invalidCost));
  const rowPotential = Array(groupCount + 1).fill(0);
  const columnPotential = Array(candidateCount + 1).fill(0);
  const matchedRowByColumn = Array(candidateCount + 1).fill(0);
  const previousColumn = Array(candidateCount + 1).fill(0);

  for (let row = 1; row <= groupCount; row += 1) {
    matchedRowByColumn[0] = row;
    let column = 0;
    const minimumReducedCost = Array(candidateCount + 1).fill(Infinity);
    const visitedColumns = Array(candidateCount + 1).fill(false);
    do {
      visitedColumns[column] = true;
      const currentRow = matchedRowByColumn[column];
      let delta = Infinity;
      let nextColumn = 0;
      for (let candidateColumn = 1;
        candidateColumn <= candidateCount;
        candidateColumn += 1) {
        if (visitedColumns[candidateColumn]) {
          continue;
        }
        const reducedCost = costs[currentRow - 1][candidateColumn - 1] -
          rowPotential[currentRow] -
          columnPotential[candidateColumn];
        if (reducedCost < minimumReducedCost[candidateColumn]) {
          minimumReducedCost[candidateColumn] = reducedCost;
          previousColumn[candidateColumn] = column;
        }
        if (minimumReducedCost[candidateColumn] < delta) {
          delta = minimumReducedCost[candidateColumn];
          nextColumn = candidateColumn;
        }
      }
      if (!Number.isFinite(delta)) {
        return Array(groupCount).fill(-1);
      }
      for (let candidateColumn = 0;
        candidateColumn <= candidateCount;
        candidateColumn += 1) {
        if (visitedColumns[candidateColumn]) {
          rowPotential[matchedRowByColumn[candidateColumn]] += delta;
          columnPotential[candidateColumn] -= delta;
        } else {
          minimumReducedCost[candidateColumn] -= delta;
        }
      }
      column = nextColumn;
    } while (matchedRowByColumn[column] !== 0);

    do {
      const priorColumn = previousColumn[column];
      matchedRowByColumn[column] = matchedRowByColumn[priorColumn];
      column = priorColumn;
    } while (column !== 0);
  }

  const assignment = Array(groupCount).fill(-1);
  for (let column = 1; column <= candidateCount; column += 1) {
    const row = matchedRowByColumn[column];
    if (row !== 0) {
      assignment[row - 1] = column - 1;
    }
  }
  return assignment;
}

export function normalizeWorkoutModifiers(modifiers) {
  return Number.isInteger(modifiers)
    ? modifiers & SUPPORTED_WORKOUT_MODIFIER_MASK
    : WORKOUT_MODIFIERS.None;
}

export function getMovementCountdownDurationMs(group) {
  return getMovementDurationMs(group) + PREPARATION_DURATION_MS;
}

export function getMovementPhaseState(
  remainingMilliseconds,
  timedPair,
  fullSideTiming = false,
) {
  if (remainingMilliseconds <= 0) {
    return { phase: "Complete", secondsRemaining: 0, segmentDurationSeconds: 0, isExercise: false };
  }

  const movementDuration = fullSideTiming
    ? FULL_SIDE_MOVEMENT_DURATION_MS
    : MOVEMENT_DURATION_MS;
  const totalDuration = movementDuration + PREPARATION_DURATION_MS;
  const bounded = Math.min(remainingMilliseconds, totalDuration);
  if (bounded > movementDuration) {
    return {
      phase: "Preparation",
      secondsRemaining: Math.ceil((bounded - movementDuration) / 1000),
      segmentDurationSeconds: PREPARATION_DURATION_MS / 1000,
      isExercise: false,
    };
  }

  const boundedMovement = Math.min(bounded, movementDuration);
  if (!timedPair) {
    return {
      phase: "Continuous",
      secondsRemaining: Math.ceil(boundedMovement / 1000),
      segmentDurationSeconds: 45,
      isExercise: true,
    };
  }

  const sideDuration = fullSideTiming ? 45_000 : 20_000;
  const changeDuration = fullSideTiming ? 15_000 : 5_000;
  const firstSideEnd = sideDuration + changeDuration;

  if (boundedMovement > firstSideEnd) {
    return {
      phase: "FirstSide",
      secondsRemaining: Math.ceil((boundedMovement - firstSideEnd) / 1000),
      segmentDurationSeconds: sideDuration / 1000,
      isExercise: true,
    };
  }

  if (boundedMovement > sideDuration) {
    return {
      phase: "ChangeSides",
      secondsRemaining: Math.ceil((boundedMovement - sideDuration) / 1000),
      segmentDurationSeconds: changeDuration / 1000,
      isExercise: false,
    };
  }

  return {
    phase: "SecondSide",
    secondsRemaining: Math.ceil(boundedMovement / 1000),
    segmentDurationSeconds: sideDuration / 1000,
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
    version: CURRENT_WORKOUT_STATE_VERSION,
    catalogRevision: CURRENT_CATALOG_REVISION,
    catalogIdentities: {},
    selectedExerciseIds: {},
    scores: {},
    outcomes: {},
    lastKeptExerciseIds: [],
    activeExtraSetSelectionGroupIds: [],
    activeFullSideSelectionGroupIds: [],
    pendingRestGroupId: null,
    pendingRestEndsAtUnixMilliseconds: 0,
    pendingRestKept: false,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: DEFAULT_WORKOUT_MODIFIERS,
    activeWorkoutMinutes: 0,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.None,
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

  // A stored object without a version predates the modifier-profile schema.
  // Treat it as legacy instead of mistaking it for a brand-new state.
  state.version = Number.isInteger(raw.version) ? raw.version : 0;
  state.catalogRevision = Number.isInteger(raw.catalogRevision)
    ? raw.catalogRevision
    : 0;
  state.lastWorkoutMinutes = normalizeMinutes(raw.lastWorkoutMinutes);
  state.lastWorkoutModifiers = raw.lastWorkoutModifiers === undefined
    ? state.lastWorkoutModifiers
    : normalizeWorkoutModifiers(raw.lastWorkoutModifiers);
  state.activeWorkoutMinutes = Number.isInteger(raw.activeWorkoutMinutes)
    ? raw.activeWorkoutMinutes
    : 0;
  state.activeWorkoutModifiers = raw.activeWorkoutModifiers === undefined
    ? state.activeWorkoutModifiers
    : normalizeWorkoutModifiers(raw.activeWorkoutModifiers);
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
  state.activeFullSideSelectionGroupIds = Array.isArray(raw.activeFullSideSelectionGroupIds)
    ? [...new Set(raw.activeFullSideSelectionGroupIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  if (state.version < CURRENT_WORKOUT_STATE_VERSION) {
    migrateImplicitSilenceModifier(state);
  }
  return state;
}

function migrateImplicitSilenceModifier(state) {
  for (const [selectionStorageKey, exerciseId] of
    Object.entries(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    const modifierValue = match ? Number(match[1]) : WORKOUT_MODIFIERS.None;
    const selectionGroupId = match ? match[2] : selectionStorageKey;
    if (!selectionGroupId || normalizeWorkoutModifiers(modifierValue) !== modifierValue) {
      continue;
    }
    const quietProfile = normalizeWorkoutModifiers(
      modifierValue | WORKOUT_MODIFIERS.Silence,
    );
    const quietKey = quietProfile === WORKOUT_MODIFIERS.None
      ? selectionGroupId
      : `${SELECTION_PROFILE_PREFIX}${quietProfile}` +
        `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
    if (state.selectedExerciseIds[quietKey] === undefined) {
      state.selectedExerciseIds[quietKey] = exerciseId;
    }
  }
  state.lastWorkoutModifiers = normalizeWorkoutModifiers(
    state.lastWorkoutModifiers | WORKOUT_MODIFIERS.Silence,
  );
  if (state.activeWorkoutMinutes > 0) {
    state.activeWorkoutModifiers = normalizeWorkoutModifiers(
      state.activeWorkoutModifiers | WORKOUT_MODIFIERS.Silence,
    );
  }
  state.version = CURRENT_WORKOUT_STATE_VERSION;
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

    this.normalizeActiveLongWorkoutAllocation();
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

  startWorkout(minutes, modifiers = DEFAULT_WORKOUT_MODIFIERS) {
    if (!SUPPORTED_MINUTES.includes(minutes)) {
      throw new RangeError("Unsupported workout duration.");
    }
    if (this.state.activeWorkoutMinutes !== 0) {
      throw new Error("A workout is already active.");
    }

    modifiers = normalizeWorkoutModifiers(modifiers);
    const previousWorkoutMinutes = normalizeMinutes(this.state.lastWorkoutMinutes);
    const previousWorkoutModifiers = normalizeWorkoutModifiers(
      this.state.lastWorkoutModifiers,
    );
    this.state.lastWorkoutMinutes = minutes;
    this.state.lastWorkoutModifiers = modifiers;
    this.state.activeWorkoutMinutes = minutes;
    this.state.activeWorkoutModifiers = modifiers;
    this.state.outcomes = {};
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingRest();
    this.carryKeptExercisesForward(previousWorkoutMinutes, previousWorkoutModifiers);
    this.repairActiveLineup();
    this.setActiveLongWorkoutAllocation();
  }

  getActiveGroups() {
    return SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)
      ? createWorkoutSchedule(
          this.state.activeWorkoutMinutes,
          this.getEffectiveFullSideSelectionGroups(),
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
      this.state.selectedExerciseIds[this.getSelectionStorageKey(
        getSelectionKey(group),
        this.state.activeWorkoutModifiers,
      )],
    );
    if (!exercise || !this.isSavedSelectionValid(
      exercise,
      group,
      this.state.activeWorkoutModifiers,
    )) {
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
        .map((group) => this.state.selectedExerciseIds[this.getSelectionStorageKey(
          group.id,
          this.state.activeWorkoutModifiers,
        )])
        .filter(Boolean),
    );
    const rejectedExerciseIds = new Set(
      [...rejectedSelectionKeys]
        .map((selectionKey) => this.state.selectedExerciseIds[
          this.getSelectionStorageKey(
            selectionKey,
            this.state.activeWorkoutModifiers,
          )
        ])
        .filter(Boolean),
    );
    this.state.lastKeptExerciseIds = [...new Set([
      ...this.state.lastKeptExerciseIds.filter(
        (exerciseId) => !rejectedExerciseIds.has(exerciseId),
      ),
      ...newlyKeptExerciseIds,
    ])];
    const currentExerciseIds = new Map(
      selectionGroups
        .filter((group) => !rejectedSelectionKeys.has(group.id))
        .map((group) => [
          group.id,
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            group.id,
            this.state.activeWorkoutModifiers,
          )],
        ])
        .filter(([, exerciseId]) => exerciseId),
    );
    const excludedExerciseIdsByGroup = new Map();

    for (const group of selectionGroups.filter((candidate) =>
      rejectedSelectionKeys.has(candidate.id))) {
      const selectionStorageKey = this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      );
      const rejectedExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
      excludedExerciseIdsByGroup.set(group.id, new Set([rejectedExerciseId]));
      for (const [savedGroupId, savedExerciseId] of Object.entries(this.state.selectedExerciseIds)) {
        if (savedGroupId !== selectionStorageKey && savedExerciseId === rejectedExerciseId) {
          delete this.state.selectedExerciseIds[savedGroupId];
        }
      }
    }

    const nextLineup = this.chooseBestDistinctLineup(
      selectionGroups,
      this.state.activeWorkoutModifiers,
      {
        preferredExerciseIds: new Set(this.state.lastKeptExerciseIds),
        currentExerciseIds,
        excludedExerciseIdsByGroup,
      },
    );
    this.applyDistinctLineup(selectionGroups, nextLineup, false);

    this.resetTransientState();
  }

  repairActiveLineup() {
    const selectionGroups = this.getSelectionGroups();
    const activeGroups = this.getActiveGroups();
    const currentExerciseIds = new Map(
      selectionGroups
        .map((group) => [
          group.id,
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            group.id,
            this.state.activeWorkoutModifiers,
          )],
        ])
        .filter(([, exerciseId]) => exerciseId),
    );
    const repairedLineup = this.chooseBestDistinctLineup(
      selectionGroups,
      this.state.activeWorkoutModifiers,
      {
        currentExerciseIds,
        allowSavedSelectionException: true,
      },
    );
    this.applyDistinctLineup(selectionGroups, repairedLineup, true, activeGroups);
  }

  carryKeptExercisesForward(previousWorkoutMinutes, previousWorkoutModifiers) {
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    if (keptExerciseIds.size === 0) {
      return;
    }

    const previousGroups = getResolution(
      previousWorkoutMinutes > 30 ? 30 : previousWorkoutMinutes,
    ).groups;
    const orderedKeptExerciseIds = [...new Set([
      ...previousGroups.map((group) => this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, previousWorkoutModifiers)
      ]),
      ...[...keptExerciseIds].sort((left, right) => left - right),
    ].filter((exerciseId) => keptExerciseIds.has(exerciseId)))];
    const targetGroups = this.getSelectionGroups();
    const currentExerciseIds = new Map(
      targetGroups
        .map((group) => [
          group.id,
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            group.id,
            this.state.activeWorkoutModifiers,
          )],
        ])
        .filter(([, exerciseId]) => exerciseId),
    );
    const carriedLineup = this.chooseBestDistinctLineup(
      targetGroups,
      this.state.activeWorkoutModifiers,
      {
        preferredExerciseIds: keptExerciseIds,
        currentExerciseIds,
        preferredTieOrder: orderedKeptExerciseIds,
      },
    );
    this.applyDistinctLineup(targetGroups, carriedLineup, false);
  }

  chooseBestDistinctLineup(
    groups,
    modifiers = this.state.activeWorkoutModifiers,
    {
      preferredExerciseIds = new Set(),
      currentExerciseIds = new Map(),
      excludedExerciseIdsByGroup = new Map(),
      preferredTieOrder = [],
      allowSavedSelectionException = false,
    } = {},
  ) {
    if (groups.length === 0) {
      return new Map();
    }

    const isAllowed = (exercise, group) => {
      if (excludedExerciseIdsByGroup.get(group.id)?.has(exercise.id)) {
        return false;
      }
      if (this.isSelectable(exercise, group, modifiers)) {
        return true;
      }
      return allowSavedSelectionException &&
        currentExerciseIds.get(group.id) === exercise.id &&
        this.isSavedSelectionValid(exercise, group, modifiers);
    };
    let candidates = this.exercises.filter((exercise) =>
      groups.some((group) => isAllowed(exercise, group)));
    this.shuffle(candidates);
    const tieOrder = new Map();
    for (const exerciseId of preferredTieOrder) {
      if (!tieOrder.has(exerciseId)) {
        tieOrder.set(exerciseId, tieOrder.size);
      }
    }
    candidates = candidates
      .map((exercise, shuffledIndex) => ({ exercise, shuffledIndex }))
      .sort((left, right) =>
        (tieOrder.get(left.exercise.id) ?? Number.MAX_SAFE_INTEGER) -
          (tieOrder.get(right.exercise.id) ?? Number.MAX_SAFE_INTEGER) ||
        left.shuffledIndex - right.shuffledIndex)
      .map(({ exercise }) => exercise);
    if (candidates.length < groups.length) {
      throw this.createDistinctLineupError(groups, candidates.length);
    }

    const orderedScores = [...new Set(candidates.map((exercise) =>
      this.getScore(exercise)))].sort((left, right) => left - right);
    const scoreRanks = new Map(orderedScores.map((score, rank) => [score, rank]));
    const maximumCoverage = Math.max(...groups.map((group) => group.canonicalGroups.length));
    const totalCoverageRange = groups.length * maximumCoverage;
    const primaryWeight = totalCoverageRange + 1;
    const totalPrimaryAndCoverageRange = groups.length *
      (primaryWeight + maximumCoverage);
    const scoreWeight = totalPrimaryAndCoverageRange + 1;
    const totalScoreRange = groups.length *
      ((orderedScores.length - 1) * scoreWeight + primaryWeight + maximumCoverage);
    const currentSelectionWeight = totalScoreRange + 1;
    const totalCurrentSelectionRange = groups.length * currentSelectionWeight +
      totalScoreRange;
    const preferredExerciseWeight = totalCurrentSelectionRange + 1;

    const allowed = groups.map(() => candidates.map(() => false));
    const utilities = groups.map(() => candidates.map(() => 0));
    let maximumUtility = 0;
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      const group = groups[groupIndex];
      for (let exerciseIndex = 0; exerciseIndex < candidates.length; exerciseIndex += 1) {
        const exercise = candidates[exerciseIndex];
        if (!isAllowed(exercise, group)) {
          continue;
        }
        allowed[groupIndex][exerciseIndex] = true;
        const utility =
          (preferredExerciseIds.has(exercise.id) ? preferredExerciseWeight : 0) +
          (currentExerciseIds.get(group.id) === exercise.id ? currentSelectionWeight : 0) +
          scoreRanks.get(this.getScore(exercise)) * scoreWeight +
          (isPrimaryForGroup(exercise, group) ? primaryWeight : 0) +
          getCanonicalCoverage(exercise, group);
        utilities[groupIndex][exerciseIndex] = utility;
        maximumUtility = Math.max(maximumUtility, utility);
      }
    }

    const assignment = solveMaximumWeightAssignment(
      utilities,
      allowed,
      maximumUtility,
    );
    const lineup = new Map();
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      const candidateIndex = assignment[groupIndex];
      if (candidateIndex < 0 || !allowed[groupIndex][candidateIndex]) {
        throw this.createDistinctLineupError(groups, candidates.length);
      }
      lineup.set(groups[groupIndex].id, candidates[candidateIndex].id);
    }
    return lineup;
  }

  chooseBestCandidate(
    group,
    excludedExerciseIds = new Set(),
    modifiers = this.state.activeWorkoutModifiers,
  ) {
    const candidates = this.exercises.filter((exercise) =>
      this.isSelectable(exercise, group, modifiers) &&
      !excludedExerciseIds.has(exercise.id));
    if (candidates.length === 0) {
      throw new Error(`No eligible exercise exists for ${group.displayName}.`);
    }

    const highestScore = Math.max(...candidates.map((exercise) => this.getScore(exercise)));
    const highestScored = candidates.filter((exercise) => this.getScore(exercise) === highestScore);
    const primaryOwned = highestScored.filter((exercise) =>
      isPrimaryForGroup(exercise, group));
    const ownershipPreferred = primaryOwned.length > 0 ? primaryOwned : highestScored;
    const widestCoverage = Math.max(
      ...ownershipPreferred.map((exercise) => getCanonicalCoverage(exercise, group)),
    );
    const finalists = ownershipPreferred.filter((exercise) =>
      getCanonicalCoverage(exercise, group) === widestCoverage);
    const index = Math.min(finalists.length - 1, Math.floor(this.random() * finalists.length));
    return finalists[Math.max(0, index)];
  }

  applyDistinctLineup(groups, lineup, clearChangedProgress, activeGroups = this.getActiveGroups()) {
    for (const group of groups) {
      const selectionStorageKey = this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      );
      const previousExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
      const nextExerciseId = lineup.get(group.id);
      this.state.selectedExerciseIds[selectionStorageKey] = nextExerciseId;
      if (!clearChangedProgress || previousExerciseId === nextExerciseId) {
        continue;
      }
      for (const round of activeGroups.filter((candidate) =>
        getSelectionKey(candidate) === group.id)) {
        delete this.state.outcomes[round.id];
      }
      if (this.pendingRestMatchesSelectionGroup(group.id)) {
        this.clearPendingRest();
      }
    }
  }

  createDistinctLineupError(groups, candidateCount) {
    return new Error(
      `No distinct exercise lineup exists for the active workout profile across ` +
      `${groups.length} groups and ${candidateCount} eligible exercises with at least ` +
      `${MINIMUM_CANONICAL_COVERAGE_PERCENT}% coverage.`,
    );
  }

  shuffle(items) {
    for (let index = items.length - 1; index > 0; index -= 1) {
      const randomIndex = Math.min(
        index,
        Math.max(0, Math.floor(this.random() * (index + 1))),
      );
      [items[index], items[randomIndex]] = [items[randomIndex], items[index]];
    }
  }

  isCompatibleWithModifiers(exercise, modifiers) {
    return isCompatibleWithWorkoutModifiers(exercise, modifiers);
  }

  isSelectable(exercise, group, modifiers) {
    return isSelectableForWorkoutProfile(exercise, group, modifiers);
  }

  getSelectionStorageKey(selectionGroupId, modifiers) {
    const normalized = normalizeWorkoutModifiers(modifiers);
    return normalized === WORKOUT_MODIFIERS.None
      ? selectionGroupId
      : `${SELECTION_PROFILE_PREFIX}${normalized}` +
          `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
  }

  parseSelectionStorageKey(selectionStorageKey) {
    const separatorIndex = selectionStorageKey.indexOf(SELECTION_PROFILE_SEPARATOR);
    if (selectionStorageKey.startsWith(SELECTION_PROFILE_PREFIX) &&
        separatorIndex > SELECTION_PROFILE_PREFIX.length) {
      const modifierValue = Number(selectionStorageKey.slice(
        SELECTION_PROFILE_PREFIX.length,
        separatorIndex,
      ));
      const modifiers = normalizeWorkoutModifiers(modifierValue);
      if (Number.isInteger(modifierValue) && modifierValue > 0 &&
          modifiers === modifierValue) {
        return {
          selectionGroupId: selectionStorageKey.slice(separatorIndex + 1),
          modifiers,
        };
      }
    }
    return {
      selectionGroupId: selectionStorageKey.includes(SELECTION_PROFILE_SEPARATOR)
        ? ""
        : selectionStorageKey,
      modifiers: WORKOUT_MODIFIERS.None,
    };
  }

  getScore(exercise) {
    const saved = this.state.scores[String(exercise.id)];
    return Number.isInteger(saved) ? saved : Number.isInteger(exercise.score) ? exercise.score : 0;
  }

  setScore(exercise, score) {
    this.state.scores[String(exercise.id)] = Math.trunc(score);
  }

  normalizeSavedLineups() {
    for (const [selectionStorageKey, exerciseId] of
      Object.entries(this.state.selectedExerciseIds)) {
      const { selectionGroupId, modifiers } = this.parseSelectionStorageKey(
        selectionStorageKey,
      );
      const group = ALL_GROUPS.get(selectionGroupId);
      const exercise = this.exercisesById.get(exerciseId);
      if (!group || !exercise || !this.isSavedSelectionValid(
        exercise,
        group,
        modifiers,
      )) {
        delete this.state.selectedExerciseIds[selectionStorageKey];
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

  normalizeActiveLongWorkoutAllocation() {
    if (!this.isLongWorkoutAllocationValid()) {
      this.setActiveLongWorkoutAllocation();
    }
  }

  isLongWorkoutAllocationValid() {
    const selectionGroups = this.getSelectionGroups();
    const selectionGroupIds = new Set(this.getSelectionGroups().map((group) => group.id));
    const sidedSelectionGroupIds = new Set(selectionGroups
      .filter((group) => {
        const exercise = this.exercisesById.get(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
        ]);
        return exercise && exercise.sideSequence !== "Continuous";
      })
      .map((group) => group.id));
    const extraMinutes = this.getExtraMinuteCount();
    const expectedFullSides = Math.min(extraMinutes, sidedSelectionGroupIds.size);
    const repeatedMinutes = extraMinutes - expectedFullSides;
    const expectedPartialExtraSets = selectionGroups.length === 0
      ? 0
      : repeatedMinutes % selectionGroups.length;
    return this.state.activeFullSideSelectionGroupIds.length === expectedFullSides &&
      this.state.activeFullSideSelectionGroupIds.every((groupId) =>
        sidedSelectionGroupIds.has(groupId)) &&
      this.state.activeExtraSetSelectionGroupIds.length === expectedPartialExtraSets &&
      this.state.activeExtraSetSelectionGroupIds.every((groupId) =>
        selectionGroupIds.has(groupId));
  }

  getExtraMinuteCount() {
    if (this.state.activeWorkoutMinutes <= 30) {
      return 0;
    }
    return this.state.activeWorkoutMinutes - this.getSelectionGroups().length;
  }

  chooseLongWorkoutAllocation() {
    const extraMinutes = this.getExtraMinuteCount();
    if (extraMinutes === 0) {
      return { fullSideSelectionGroupIds: [], extraSetSelectionGroupIds: [] };
    }
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    const rankedGroups = [...this.getSelectionGroups()]
      .sort((left, right) => {
        const leftKept = keptExerciseIds.has(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(left.id, this.state.activeWorkoutModifiers)
        ]) ? 1 : 0;
        const rightKept = keptExerciseIds.has(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(right.id, this.state.activeWorkoutModifiers)
        ]) ? 1 : 0;
        return rightKept - leftKept || right.order - left.order;
      });
    const fullSideSelectionGroupIds = rankedGroups
      .filter((group) => {
        const exercise = this.exercisesById.get(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
        ]);
        return exercise && exercise.sideSequence !== "Continuous";
      })
      .slice(0, extraMinutes)
      .map((group) => group.id);
    const repeatedMinutes = extraMinutes - fullSideSelectionGroupIds.length;
    const partialExtraSets = repeatedMinutes % rankedGroups.length;
    return {
      fullSideSelectionGroupIds,
      extraSetSelectionGroupIds: rankedGroups
        .slice(0, partialExtraSets)
        .map((group) => group.id),
    };
  }

  setActiveLongWorkoutAllocation() {
    this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation());
  }

  applyLongWorkoutAllocation(allocation) {
    this.state.activeFullSideSelectionGroupIds = [...allocation.fullSideSelectionGroupIds];
    this.state.activeExtraSetSelectionGroupIds = [...allocation.extraSetSelectionGroupIds];
  }

  getEffectiveExtraSetSelectionGroups() {
    const groupIds = this.isLongWorkoutAllocationValid()
      ? this.state.activeExtraSetSelectionGroupIds
      : this.chooseLongWorkoutAllocation().extraSetSelectionGroupIds;
    return new Set(groupIds);
  }

  getEffectiveFullSideSelectionGroups() {
    const groupIds = this.isLongWorkoutAllocationValid()
      ? this.state.activeFullSideSelectionGroupIds
      : this.chooseLongWorkoutAllocation().fullSideSelectionGroupIds;
    return new Set(groupIds);
  }

  normalizePendingRest() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    const pendingExercise = pendingGroup
      ? this.exercisesById.get(
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            getSelectionKey(pendingGroup),
            this.state.activeWorkoutModifiers,
          )],
        )
      : null;
    if (
      !pendingGroup ||
      !pendingExercise ||
      this.state.pendingRestEndsAtUnixMilliseconds <= 0 ||
      this.state.outcomes[pendingGroup.id] !== undefined ||
      !isCompatibleWithWorkoutModifiers(
        pendingExercise,
        this.state.activeWorkoutModifiers,
      ) ||
      !this.isAssignedToGroup(pendingExercise, pendingGroup)
    ) {
      this.clearPendingRest();
    }
  }

  isSavedSelectionValid(exercise, group, modifiers) {
    return (
      this.isSelectable(exercise, group, modifiers) ||
      (this.pendingRestMatchesSelectionGroup(getSelectionKey(group)) &&
        this.isCompatibleWithModifiers(exercise, modifiers) &&
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
      SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
    );
    const scoreResetExerciseIds = catalogInvalidationIdsSince(
      this.state.catalogRevision,
      this.exercises,
      SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
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
        scoreResetExerciseIds.add(Number(exerciseId));
      }
    }

    if (changedExerciseIds.size > 0) {
      const affectedSelectionStorageKeys = Object.entries(this.state.selectedExerciseIds)
        .filter(([, exerciseId]) => changedExerciseIds.has(exerciseId))
        .map(([selectionStorageKey]) => selectionStorageKey);
      for (const selectionStorageKey of affectedSelectionStorageKeys) {
        delete this.state.selectedExerciseIds[selectionStorageKey];
        for (const round of this.getActiveGroups().filter((candidate) =>
          this.getSelectionStorageKey(
            getSelectionKey(candidate),
            this.state.activeWorkoutModifiers,
          ) === selectionStorageKey)) {
          delete this.state.outcomes[round.id];
        }
      }
      if (
        this.state.pendingRestGroupId &&
        affectedSelectionStorageKeys.some((selectionStorageKey) =>
          this.getActiveGroups().some((round) =>
            round.id === this.state.pendingRestGroupId &&
            this.getSelectionStorageKey(
              getSelectionKey(round),
              this.state.activeWorkoutModifiers,
            ) === selectionStorageKey))
      ) {
        this.clearPendingRest();
      }
    }

    for (const exerciseId of scoreResetExerciseIds) {
      delete this.state.scores[String(exerciseId)];
    }

    this.normalizeKeptExerciseIds();

    this.state.catalogIdentities = currentIdentities;
    this.state.catalogRevision = Math.max(
      this.state.catalogRevision,
      CURRENT_CATALOG_REVISION,
    );
    this.state.version = CURRENT_WORKOUT_STATE_VERSION;
  }

  resetTransientState() {
    this.state.activeWorkoutMinutes = 0;
    this.state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
    this.state.outcomes = {};
    this.state.activeExtraSetSelectionGroupIds = [];
    this.state.activeFullSideSelectionGroupIds = [];
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingRest();
  }
}

function catalogIdentity(exercise) {
  return `${exercise.name}\u001f${exercise.video}`;
}

function catalogInvalidationIdsSince(priorRevision, exercises, scopedInvalidations) {
  const invalidatedExerciseIds = new Set();
  if (priorRevision < LAST_CUMULATIVE_CATALOG_REVISION) {
    for (const exercise of exercises) {
      if (typeof exercise.retiredName === "string" && exercise.retiredName) {
        invalidatedExerciseIds.add(exercise.id);
      }
    }
  }

  for (const [revision, exerciseIds] of scopedInvalidations) {
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
