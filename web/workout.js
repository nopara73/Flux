export const SUPPORTED_MINUTES = Object.freeze([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
export const WORKOUT_MODIFIERS = Object.freeze({
  None: 0,
  Insect: 1,
  Silence: 2,
  Mirror: 4,
  TallMirror: 8,
});
export const MIRROR_EQUIPMENT = Object.freeze({
  None: "None",
  Compact: "Compact",
  Tall: "Tall",
});
export const EXERCISE_INSECT_COMPATIBILITY = Object.freeze({
  Unreviewed: "Unreviewed",
  Compatible: "Compatible",
  Incompatible: "Incompatible",
});
export const EXERCISE_MIRROR_RELATIONSHIP = Object.freeze({
  Unreviewed: "Unreviewed",
  MirrorOnly: "MirrorOnly",
  BenefitsGreatly: "BenefitsGreatly",
  Agnostic: "Agnostic",
});
export const EXERCISE_MIRROR_COVERAGE = Object.freeze({
  None: "None",
  UpperBody: "UpperBody",
  FullBody: "FullBody",
});

export function getSessionMovementId(exercise) {
  return Number.isInteger(exercise?.sessionMovementId) &&
    exercise.sessionMovementId > 0
    ? exercise.sessionMovementId
    : exercise.id;
}

const MODIFIER_RULES = Object.freeze([
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Insect,
    isReviewed: (exercise) =>
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.Insect) === 0 ||
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Silence,
    isReviewed: (exercise) => typeof exercise.silent === "boolean",
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.Silence) === 0 || exercise.silent === true,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Mirror,
    isReviewed: isMirrorMetadataReviewed,
    isCompatibleForProfile: isMirrorCompatible,
  }),
]);

function isMirrorMetadataReviewed(exercise) {
  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return exercise.equipment === "Mirror" &&
      (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
       exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody);
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly) {
    return exercise.equipment === "None" &&
      (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
       exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody);
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic) {
    return exercise.equipment === "None" &&
      exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.None;
  }

  return false;
}

function isMirrorCompatible(exercise, profile) {
  if (exercise.mirrorRelationship !== EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return true;
  }

  const equipment = getMirrorEquipment(profile);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return false;
  }
  return equipment === MIRROR_EQUIPMENT.Tall ||
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody;
}

function getModifierRuleStateProfiles(rule) {
  return rule.flag === WORKOUT_MODIFIERS.Mirror
    ? [
        WORKOUT_MODIFIERS.None,
        WORKOUT_MODIFIERS.Mirror,
        WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror,
      ]
    : [WORKOUT_MODIFIERS.None, rule.flag];
}

function createWorkoutModifierValidationProfiles() {
  const profiles = [WORKOUT_MODIFIERS.None];
  for (const rule of MODIFIER_RULES) {
    profiles.push(...getModifierRuleStateProfiles(rule));
  }
  for (let firstIndex = 0; firstIndex < MODIFIER_RULES.length - 1; firstIndex += 1) {
    for (let secondIndex = firstIndex + 1;
      secondIndex < MODIFIER_RULES.length;
      secondIndex += 1) {
      for (const firstState of getModifierRuleStateProfiles(MODIFIER_RULES[firstIndex])) {
        for (const secondState of getModifierRuleStateProfiles(MODIFIER_RULES[secondIndex])) {
          profiles.push(normalizeWorkoutModifiers(firstState | secondState));
        }
      }
    }
  }
  return profiles.filter((profile, index) => profiles.indexOf(profile) === index);
}
export const SUPPORTED_WORKOUT_MODIFIER_MASK = MODIFIER_RULES.reduce(
  (mask, rule) => mask | rule.flag,
  WORKOUT_MODIFIERS.TallMirror,
);
export const WORKOUT_MODIFIER_VALIDATION_PROFILES = Object.freeze(
  createWorkoutModifierValidationProfiles(),
);
const SELECTION_PROFILE_PREFIX = "p";
const SELECTION_PROFILE_SEPARATOR = "|";
const MINIMUM_CANONICAL_COVERAGE_PERCENT = 50;
export const MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP = 5;
export const MINIMUM_EXERCISES_PER_MIRROR_CATEGORY = 5;
export const MINIMUM_MODIFIER_MATERIALITY_EXERCISES = 5;
export const MINIMUM_MODIFIER_MATERIALITY_PERCENT = 5;
export const MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT = 10;
export const MUSCLE_SESSION_BUDGET_HALF_UNITS = 10;
export const MINIMUM_MUSCULAR_DEMAND = 0;
export const MODERATE_MUSCULAR_DEMAND = 1;
export const MAXIMUM_MUSCULAR_DEMAND = 2;
export const HARD_MUSCULAR_DEMAND = MAXIMUM_MUSCULAR_DEMAND;
export const MODERATE_RECOVERY_WINDOW_MS = 18 * 60 * 60 * 1000;
export const HARD_RECOVERY_WINDOW_MS = 36 * 60 * 60 * 1000;
export const HARD_ROTATION_STATUS = Object.freeze({
  RecoveringHard: "RecoveringHard",
  Neutral: "Neutral",
  FreshHard: "FreshHard",
});
export const PRIMARY_MUSCLE_LOAD_HALF_UNITS = 2;
export const SECONDARY_MUSCLE_LOAD_HALF_UNITS = 1;
export const SCORE_HALF_UNITS_PER_VOTE = 2;
export const MUSCLE_BUDGET_MAX_REBALANCE_PASSES = 12;
export const DEFAULT_WORKOUT_MODIFIERS = WORKOUT_MODIFIERS.Silence;
export const CURRENT_WORKOUT_STATE_VERSION = 14;
const EXPLICIT_MIRROR_EQUIPMENT_STATE_VERSION = 9;
const IMPLICIT_SILENCE_STATE_VERSION = 5;
const SOURCE_STATE_VERSION = Symbol("sourceStateVersion");
export const MOVEMENT_DURATION_MS = 45_000;
export const PREPARATION_DURATION_MS = 5_000;
export const REST_DURATION_MS = 15_000;
export const CURRENT_CATALOG_REVISION = 48;
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
  [26, new Set([31, 282, 391, 507, 508, 577])],
  [27, new Set([231, 685, 687])],
  [28, new Set([251])],
  [29, new Set([
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ])],
  [30, new Set([
    229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
    501, 504, 513, 516,
  ])],
  [31, new Set([414, 415, 416, 418, 419])],
  [32, new Set([31, 219, 395, 507, 577, 618, 654, 834])],
  [33, new Set([
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ])],
  [34, new Set([98, 390, 508, 576, 816])],
  [35, new Set([219])],
  [36, new Set([684])],
  [37, new Set([31, 176, 195, 391, 413, 884, 885])],
  [41, new Set([500])],
  [42, new Set([105, 107, 108, 245, 280, 591, 884, 885, 905])],
  [43, new Set([90, 94, 95, 99, 100, 497, 498, 511, 514])],
  [44, new Set([90, 94, 95, 99, 100, 497, 498, 500, 511, 514])],
  [45, new Set([
    264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
    757, 759, 760, 761, 762, 763, 764,
  ])],
  [46, new Set([
    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
  ])],
  [47, new Set([
    198, 398, 421, 427, 468, 512, 515,
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
  [27, new Set([687])],
  [28, new Set([251])],
  [29, new Set([
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ])],
  [30, new Set([229, 497, 501, 504, 513])],
  [31, new Set([414, 415, 416, 418, 419])],
  [32, new Set([31, 219, 395, 507, 577, 618, 654, 834])],
  [33, new Set([
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ])],
  [36, new Set([684])],
  [43, new Set([90, 94, 95, 99, 100, 497, 498, 511, 514])],
  [44, new Set([90])],
  [45, new Set([
    264, 275, 406, 409, 460, 588, 608, 611, 743,
    757, 759, 760, 761, 762, 763, 764,
  ])],
]);
const ALTERNATING_PREFIX = "Alternating ";
const CONTINUOUS_ALTERNATION_NORMALIZATION_IDS = new Set();
export const APPROVED_EXERCISE_CORRECTIONS = new Map([
  [31, ["High-Knee Overhead-Reach March", "Alternating Knee Raises with Two-Arm Pull-Down"]],
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
  [282, ["High-Knee Horizontal Punches", "Side-Step Knee Drive with Alternating Side Punches"]],
  [219, ["Single-Side High-Knee Cross-Body Pull", "Alternating High-Knee Cross-Body Pull"]],
  [684, ["Karate Step-Through Cross-Elbow Strike", "Knee Strike to Horizontal Elbow Strike"]],
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
  [507, ["Hamstring Curl with Elbow Pull", "Knee Raise with Elbow Pull"]],
  [508, ["Wide-Step Elbow Pull", "Side-Step with Two-Arm Overhead Reach"]],
  [588, ["Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls"]],
  [577, ["High-Knee Goalpost Pull", "Standing Side-Leg Raise with Side Reach"]],
  [915, ["Split-Stance Knee Drive with Overhead Reach", "Single-Side Split-Stance Knee Drive with Overhead Reach"]],
  [617, ["Standing Side-Leg Circles", "Standing Forward Side-Leg Circles"]],
  [626, ["Sumo Stance", "Sumo Squat Hold"]],
  [712, ["Standing Arms-Back Chest Opener", "Standing Arms-Back Chest-Opener Hold"]],
  [969, ["Chair-Pose Core Hold", "Chair-Pose Hold"]],
  [1000, ["Standing Forward Fold", "Standing Forward-Fold Hold"]],
  [136, ["Goddess Pose", "Wide Turned-Out Squat Hold"]],
  [225, ["Clenched-Fist Wrist Extensor Stretch", "Opposite-Hand Fist-Down Wrist Stretch"]],
  [241, ["Hook-Fist Tendon Glide", "Open Hand to Hook Fist"]],
  [242, ["Full-Fist Tendon Glide", "Open Hand to Full Fist"]],
  [248, ["Side-Tap Palm Pushes", "Alternating Side-Tap Palm Pushes"]],
  [283, ["Straight-Fist Tendon Glide", "Open Hand to Straight Fist"]],
  [291, ["Open-to-Claw Tendon Glide", "Open Hand to Claw Fist"]],
  [293, ["Finger-Web Space Stretch", "Opposite-Hand Finger-Web Stretches"]],
  [683, ["Alternating Palm-Up T-Arm Flips", "Alternating Palm-Up Shoulder Rotations"]],
  [214, ["Forward Wrist Circles", "Inward Wrist Circles"]],
  [223, ["Forward Controlled Wrist Circles", "Inward Controlled Wrist Circles"]],
  [755, ["Reverse Wrist Circles", "Outward Wrist Circles"]],
  [756, ["Reverse Controlled Wrist Circles", "Outward Controlled Wrist Circles"]],
  [758, ["Reverse Knee-and-Ankle Circles", "Backward Knee-and-Ankle Circles"]],
  [94, ["Mirror-Guided Lateral Weight Shift", "Lateral Weight Shift"]],
  [95, ["Mirror-Guided Single-Leg Pelvic Control", "Single-Leg Pelvic Control"]],
  [99, ["Mirror-Guided Bent-Knee Front-to-Back Leg Swing", "Bent-Knee Front-to-Back Leg Swing"]],
  [100, ["Mirror-Guided Bent-Knee Leg Swing with Pause", "Bent-Knee Leg Swing with Pause"]],
  [497, ["Mirror-Guided Eyebrow Raise", "Eyebrow Raise"]],
  [498, ["Mirror-Guided Firm Eye Closure", "Firm Eye Closure"]],
  [500, ["Mirror-Guided Straight Jaw Opening", "Straight Jaw Opening"]],
  [511, ["Mirror-Guided Lip Pucker", "Lip Pucker"]],
  [514, ["Mirror-Guided Symmetric Smile", "Symmetric Smile"]],
]);

export const ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES = new Map([
  [31, new Set([
    "Knee Raise with Overhead Reach",
    "Single-Side Knee Raise with Two-Arm Pull-Down",
  ])],
  [21, new Set(["Alternating Standing-Scale Balance"])],
  [145, new Set(["Alternating Standing Knee Extension"])],
  [231, new Set(["Alternating Karate Reverse Punch"])],
  [394, new Set(["Standing Open-and-Close Breathing"])],
  [395, new Set([
    "Standing Overhead Rib-Expansion Breathing",
  ])],
  [397, new Set([
    "Breath-Integrated Weight Shift",
    "Alternating Breath-Integrated Weight Shift",
  ])],
  [500, new Set(["Controlled Jaw Open and Close"])],
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

export function hasReviewedMuscularDemand(exercise) {
  return Number.isInteger(exercise?.muscularDemand) &&
    exercise.muscularDemand >= MINIMUM_MUSCULAR_DEMAND &&
    exercise.muscularDemand <= MAXIMUM_MUSCULAR_DEMAND;
}

export function getLastHardWorkUnixMilliseconds(
  lastHardWorkByPrimaryMuscle,
  primaryMuscle,
) {
  return getLastWorkUnixMilliseconds(lastHardWorkByPrimaryMuscle, primaryMuscle);
}

export function getLastMeaningfulWorkUnixMilliseconds(
  lastMeaningfulWorkByPrimaryMuscle,
  primaryMuscle,
) {
  return getLastWorkUnixMilliseconds(lastMeaningfulWorkByPrimaryMuscle, primaryMuscle);
}

function getLastWorkUnixMilliseconds(lastWorkByPrimaryMuscle, primaryMuscle) {
  const value = lastWorkByPrimaryMuscle?.[primaryMuscle];
  return Number.isSafeInteger(value) && value > 0 ? value : 0;
}

export function isPrimaryMuscleRecovering(
  lastHardWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return isPrimaryMuscleWithinRecoveryWindow(
    lastHardWorkByPrimaryMuscle,
    primaryMuscle,
    nowUnixMilliseconds,
    HARD_RECOVERY_WINDOW_MS,
  );
}

export function isPrimaryMuscleInModerateRecovery(
  lastMeaningfulWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return isPrimaryMuscleWithinRecoveryWindow(
    lastMeaningfulWorkByPrimaryMuscle,
    primaryMuscle,
    nowUnixMilliseconds,
    MODERATE_RECOVERY_WINDOW_MS,
  );
}

export function isModerateExerciseRecovering(
  exercise,
  lastMeaningfulWorkByPrimaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return exercise?.muscularDemand === MODERATE_MUSCULAR_DEMAND &&
    isPrimaryMuscleInModerateRecovery(
      lastMeaningfulWorkByPrimaryMuscle,
      exercise.primaryCanonicalGroup,
      nowUnixMilliseconds,
    );
}

function isPrimaryMuscleWithinRecoveryWindow(
  lastWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds,
  recoveryWindowMilliseconds,
) {
  const lastWork = getLastWorkUnixMilliseconds(lastWorkByPrimaryMuscle, primaryMuscle);
  return lastWork > 0 &&
    nowUnixMilliseconds - lastWork < recoveryWindowMilliseconds;
}

export function getHardRotationStatus(
  exercise,
  group,
  lastHardWorkByPrimaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  if (exercise?.muscularDemand !== HARD_MUSCULAR_DEMAND) {
    return HARD_ROTATION_STATUS.Neutral;
  }
  if (isPrimaryMuscleRecovering(
    lastHardWorkByPrimaryMuscle,
    exercise.primaryCanonicalGroup,
    nowUnixMilliseconds,
  )) {
    return HARD_ROTATION_STATUS.RecoveringHard;
  }
  return group?.canonicalGroups?.includes(exercise.primaryCanonicalGroup)
    ? HARD_ROTATION_STATUS.FreshHard
    : HARD_ROTATION_STATUS.Neutral;
}

export function createWorkoutSchedule(
  minutes,
  sequenceRootsBySelectionGroupId = null,
  setCountsBySelectionGroupId = null,
) {
  if (!SUPPORTED_MINUTES.includes(minutes)) {
    throw new RangeError("Unsupported workout duration.");
  }

  const resolution = getResolution(minutes > 30 ? 30 : minutes);
  if (minutes <= 30) {
    return resolution.groups;
  }

  const sequenceRoots = sequenceRootsBySelectionGroupId instanceof Map
    ? sequenceRootsBySelectionGroupId
    : new Map();
  const setCounts = setCountsBySelectionGroupId instanceof Map
    ? setCountsBySelectionGroupId
    : createDefaultLongWorkoutSetCounts(
      minutes,
      resolution.groups,
      sequenceRoots,
    );
  const rounds = [];
  for (let groupIndex = 0; groupIndex < resolution.groups.length; groupIndex++) {
    const selectionGroup = resolution.groups[groupIndex];
    const setCount = Math.max(1, setCounts.get(selectionGroup.id) ?? 1);
    const root = sequenceRoots.get(selectionGroup.id);
    const blocks = root?.sequenceBlocks;
    if (!Array.isArray(blocks) || blocks.length === 0) {
      throw new Error(`${selectionGroup.displayName} has no selected sequence.`);
    }
    for (let setNumber = 1; setNumber <= setCount; setNumber += 1) {
      for (let blockIndex = 0; blockIndex < blocks.length; blockIndex += 1) {
        const block = blocks[blockIndex];
        rounds.push(Object.freeze({
          ...selectionGroup,
          id: `${selectionGroup.id}.set${setNumber}.block${blockIndex + 1}`,
          order: rounds.length + 1,
          selectionGroupId: selectionGroup.id,
          exerciseOverrideId: block.exerciseId,
          sequenceBlockIndex: blockIndex,
          sequenceBlockCount: blocks.length,
          setNumber,
          setCount,
          sequenceSideCue: block.sideCue ?? "None",
          sequenceDirectionCue: block.directionCue ?? "None",
          mirrorSequenceMedia: block.mirrorMedia === true,
          sequenceMediaSegment: block.mediaSegment ?? "Full",
        }));
      }
    }
  }
  if (rounds.length !== minutes) {
    throw new Error(
      `The ${minutes}-minute workout scheduled ${rounds.length} exercise blocks.`,
    );
  }
  return Object.freeze(rounds);
}

function createDefaultLongWorkoutSetCounts(
  minutes,
  groups,
  sequenceRoots,
) {
  const setCounts = new Map(groups.map((group) => [group.id, 1]));
  const blockCosts = new Map(groups.map((group) => {
    const blocks = sequenceRoots.get(group.id)?.sequenceBlocks;
    if (!Array.isArray(blocks) || blocks.length === 0) {
      throw new Error(`${group.displayName} has no selected sequence.`);
    }
    return [group.id, blocks.length];
  }));
  let remainingMinutes = minutes - [...blockCosts.values()]
    .reduce((total, cost) => total + cost, 0);
  if (remainingMinutes < 0) {
    throw new Error("The selected sequences do not fit this workout duration.");
  }
  const rankedGroups = [...groups].reverse();
  const canFill = (remaining) => {
    const reachable = new Array(remaining + 1).fill(false);
    reachable[0] = true;
    for (let value = 1; value <= remaining; value += 1) {
      reachable[value] = [...blockCosts.values()].some((cost) =>
        cost <= value && reachable[value - cost]);
    }
    return reachable[remaining];
  };
  while (remainingMinutes > 0) {
    const group = rankedGroups.find((candidate) => {
      const cost = blockCosts.get(candidate.id);
      return cost <= remainingMinutes && canFill(remainingMinutes - cost);
    });
    if (!group) {
      throw new Error("The selected sequence lengths cannot fill this workout duration.");
    }
    setCounts.set(group.id, setCounts.get(group.id) + 1);
    remainingMinutes -= blockCosts.get(group.id);
  }
  return setCounts;
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

export function calculateMuscleLoadHalfUnits(scheduledExercises) {
  const result = new Map();
  for (const exercise of scheduledExercises) {
    result.set(
      exercise.primaryCanonicalGroup,
      (result.get(exercise.primaryCanonicalGroup) ?? 0) +
        PRIMARY_MUSCLE_LOAD_HALF_UNITS,
    );
    for (const secondary of new Set(exercise.secondaryCanonicalGroups ?? [])) {
      result.set(
        secondary,
        (result.get(secondary) ?? 0) + SECONDARY_MUSCLE_LOAD_HALF_UNITS,
      );
    }
  }
  return result;
}

export function getMuscleBudgetTemporaryDownvoteHalfUnits(
  loadHalfUnits,
  candidateMuscleGroups,
) {
  return [...new Set(candidateMuscleGroups)].reduce(
    (total, group) => total + Math.max(
      0,
      (loadHalfUnits.get(group) ?? 0) - MUSCLE_SESSION_BUDGET_HALF_UNITS,
    ),
    0,
  );
}

export function getTemporaryDownvoteHalfUnitsAfterAddingExercise(
  existingLoadHalfUnits,
  exercise,
) {
  const addedLoad = new Map([
    [exercise.primaryCanonicalGroup, PRIMARY_MUSCLE_LOAD_HALF_UNITS],
  ]);
  for (const secondary of new Set(exercise.secondaryCanonicalGroups ?? [])) {
    addedLoad.set(
      secondary,
      (addedLoad.get(secondary) ?? 0) + SECONDARY_MUSCLE_LOAD_HALF_UNITS,
    );
  }
  return [...addedLoad].reduce(
    (total, [group, addedHalfUnits]) => total + Math.max(
      0,
      (existingLoadHalfUnits.get(group) ?? 0) + addedHalfUnits -
        MUSCLE_SESSION_BUDGET_HALF_UNITS,
    ),
    0,
  );
}

export function getAdjustedScoreHalfUnits(realScore, temporaryDownvoteHalfUnits) {
  return realScore * SCORE_HALF_UNITS_PER_VOTE - temporaryDownvoteHalfUnits;
}

export function isSequenceContinuationRound(group) {
  return (group?.setNumber ?? 1) > 1 ||
    (group?.sequenceBlockIndex ?? 0) > 0;
}

export function isFinalSequenceRound(group) {
  return (group?.sequenceBlockIndex ?? 0) ===
      (group?.sequenceBlockCount ?? 1) - 1 &&
    (group?.setNumber ?? 1) === (group?.setCount ?? 1);
}

export function getMovementDurationMs() {
  return MOVEMENT_DURATION_MS;
}

export function isModifierMetadataComplete(exercises) {
  return exercises.every((exercise) =>
    MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)));
}

export function isSessionMovementMetadataValid(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  if (exercisesById.size !== exercises.length) {
    return false;
  }

  const validSideCues = new Set([
    "None", "ScreenLeft", "ScreenRight", "ShownLeadStance", "OppositeLeadStance",
  ]);
  const validDirectionCues = new Set([
    "None", "Forward", "Backward", "Clockwise", "Counterclockwise", "Inward", "Outward",
  ]);
  const validMediaSegments = new Set([
    "Full", "FirstDirection", "SecondDirection",
  ]);
  const sequenceOwnerByExerciseId = new Map();
  for (const root of exercises) {
    if (!Array.isArray(root.sequenceBlocks)) {
      return false;
    }
    if (root.sequenceBlocks.length === 0) {
      continue;
    }
    const rootMuscles = new Set([
      root.primaryCanonicalGroup,
      ...(root.secondaryCanonicalGroups ?? []),
    ]);
    const uniqueMemberIds = new Set();
    for (const block of root.sequenceBlocks) {
      const member = exercisesById.get(block?.exerciseId);
      if (!member ||
          !validSideCues.has(block.sideCue ?? "None") ||
          !validDirectionCues.has(block.directionCue ?? "None") ||
          typeof block.mirrorMedia !== "boolean" ||
          !validMediaSegments.has(block.mediaSegment ?? "Full") ||
          ![
            member.primaryCanonicalGroup,
            ...(member.secondaryCanonicalGroups ?? []),
          ].some((muscle) => rootMuscles.has(muscle))) {
        return false;
      }
      uniqueMemberIds.add(member.id);
    }
    if (!uniqueMemberIds.has(root.id)) {
      return false;
    }
    for (const memberId of uniqueMemberIds) {
      if (sequenceOwnerByExerciseId.has(memberId)) {
        return false;
      }
      sequenceOwnerByExerciseId.set(memberId, root.id);
    }
  }
  if (sequenceOwnerByExerciseId.size !== exercises.length) {
    return false;
  }

  const membersByMovementId = new Map();
  for (const exercise of exercises) {
    if (exercise.sessionMovementId === undefined ||
        exercise.sessionMovementId === 0) {
      continue;
    }
    if (!Number.isInteger(exercise.sessionMovementId) ||
        exercise.sessionMovementId < 0) {
      return false;
    }
    const members = membersByMovementId.get(exercise.sessionMovementId) ?? [];
    members.push(exercise);
    membersByMovementId.set(exercise.sessionMovementId, members);
  }

  for (const [movementId, members] of membersByMovementId) {
    const root = exercisesById.get(movementId);
    const rootCanonicalGroups = new Set([
      root?.primaryCanonicalGroup,
      ...(root?.secondaryCanonicalGroups ?? []),
    ]);
    if (members.length < 2 ||
        !root ||
        root.sessionMovementId !== root.id ||
        members.some((exercise) =>
          ![
            exercise.primaryCanonicalGroup,
            ...exercise.secondaryCanonicalGroups,
          ].some((group) => rootCanonicalGroups.has(group)))) {
      return false;
    }
  }
  return true;
}

export function isCompatibleWithWorkoutModifiers(exercise, modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  return MODIFIER_RULES.every((rule) =>
    rule.isCompatibleForProfile(exercise, normalized));
}

export function isMirrorRelevant(exercise) {
  return exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly ||
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly;
}

export function isMirrorPreferred(exercise, modifiers) {
  const equipment = getMirrorEquipment(modifiers);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return false;
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return isMirrorCompatible(exercise, normalizeWorkoutModifiers(modifiers));
  }
  return exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
      equipment === MIRROR_EQUIPMENT.Tall);
}

export function isSelectableForWorkoutProfile(exercise, group, modifiers) {
  return isSelectable(exercise, group) &&
    isCompatibleWithWorkoutModifiers(exercise, modifiers);
}

function isSequenceUnitEligible(
  exercise,
  exercisesById,
  group,
  modifiers,
  workoutMinutes = null,
) {
  if (!Array.isArray(exercise?.sequenceBlocks) ||
      exercise.sequenceBlocks.length === 0 ||
      (workoutMinutes !== null && workoutMinutes <= 30 &&
        exercise.sequenceBlocks.length > 1) ||
      !isSelectableForWorkoutProfile(exercise, group, modifiers)) {
    return false;
  }

  const memberIds = [...new Set(exercise.sequenceBlocks.map((block) =>
    block.exerciseId))];
  return memberIds.length > 0 && memberIds.every((exerciseId) => {
    const member = exercisesById.get(exerciseId);
    return member &&
      MODIFIER_RULES.every((rule) => rule.isReviewed(member)) &&
      isSelectableForWorkoutProfile(member, group, modifiers);
  });
}

export function findWorkoutModifierPairCoverageDeficiencies(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const rulePairs = MODIFIER_RULES.flatMap((firstRule, firstIndex) =>
    MODIFIER_RULES.slice(firstIndex + 1).map((secondRule) =>
      ({ firstRule, secondRule })));
  return [...RESOLUTIONS.entries()].flatMap(([minutes, resolution]) =>
    resolution.groups.flatMap((group) =>
      rulePairs.flatMap(({ firstRule, secondRule }) =>
        getModifierRuleStateProfiles(firstRule).flatMap((firstState) =>
          getModifierRuleStateProfiles(secondRule).map((secondState) => {
            const profile = normalizeWorkoutModifiers(firstState | secondState);
            const mirrorEquipment = getMirrorEquipment(profile);
            const requiresMirrorRelevance =
              mirrorEquipment !== MIRROR_EQUIPMENT.None;
            return {
              minutes,
              groupId: group.id,
              groupName: group.displayName,
              firstModifier: firstRule.flag,
              firstModifierEnabled: firstState !== WORKOUT_MODIFIERS.None,
              secondModifier: secondRule.flag,
              secondModifierEnabled: secondState !== WORKOUT_MODIFIERS.None,
              mirrorEquipment,
              matchingExerciseCount: new Set(exercises
                .filter((exercise) =>
                  MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)) &&
                  isSequenceUnitEligible(
                    exercise,
                    exercisesById,
                    group,
                    profile,
                  ) &&
                  (!requiresMirrorRelevance || isMirrorRelevant(exercise)))
                .map(getSessionMovementId)).size,
              requiredExerciseCount:
                MINIMUM_EXERCISES_PER_MODIFIER_PAIR_STATE_PER_GROUP,
            };
          })))
        .filter((result) =>
          result.matchingExerciseCount < result.requiredExerciseCount)));
}

export function findMirrorCategoryDeficiencies(exercises) {
  const categories = [
    [EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
      EXERCISE_MIRROR_COVERAGE.UpperBody],
    [EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
      EXERCISE_MIRROR_COVERAGE.FullBody],
    [EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
      EXERCISE_MIRROR_COVERAGE.UpperBody],
    [EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
      EXERCISE_MIRROR_COVERAGE.FullBody],
    [EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
      EXERCISE_MIRROR_COVERAGE.None],
  ];
  return categories.map(([mirrorRelationship, minimumMirrorCoverage]) => ({
    mirrorRelationship,
    minimumMirrorCoverage,
    matchingExerciseCount: new Set(exercises
      .filter((exercise) => isMirrorMetadataReviewed(exercise) &&
        exercise.mirrorRelationship === mirrorRelationship &&
        exercise.minimumMirrorCoverage === minimumMirrorCoverage)
      .map(getSessionMovementId)).size,
    requiredExerciseCount: MINIMUM_EXERCISES_PER_MIRROR_CATEGORY,
  })).filter((result) =>
    result.matchingExerciseCount < result.requiredExerciseCount);
}

export function findWorkoutModifierMaterialityDeficiencies(exercises) {
  const canonicalGroups = RESOLUTIONS.get(30).groups;
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const reviewedExercises = exercises.filter((exercise) =>
    MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)));
  const rulePairs = MODIFIER_RULES.flatMap((firstRule, firstIndex) =>
    MODIFIER_RULES.slice(firstIndex + 1).map((secondRule) =>
      ({ firstRule, secondRule })));
  const enabledStates = (rule) =>
    getModifierRuleStateProfiles(rule).filter((state) =>
      state !== WORKOUT_MODIFIERS.None);
  const edges = MODIFIER_RULES.flatMap((rule) =>
    enabledStates(rule).map((enabledStateProfile) => ({
      rule,
      baseProfile: WORKOUT_MODIFIERS.None,
      enabledStateProfile,
    })));
  for (const { firstRule, secondRule } of rulePairs) {
    for (const firstState of enabledStates(firstRule)) {
      for (const secondState of enabledStates(secondRule)) {
        edges.push({
          rule: firstRule,
          baseProfile: secondState,
          enabledStateProfile: firstState,
        });
        edges.push({
          rule: secondRule,
          baseProfile: firstState,
          enabledStateProfile: secondState,
        });
      }
    }
  }

  return edges.map(({ rule, baseProfile, enabledStateProfile }) => {
    const enabledModifier = rule.flag;
    const enabledProfile = normalizeWorkoutModifiers(
      baseProfile | enabledStateProfile,
    );
    const beforeExerciseIds = new Set(reviewedExercises
      .filter((exercise) => canonicalGroups.some((group) =>
        isSequenceUnitEligible(
          exercise,
          exercisesById,
          group,
          baseProfile,
        )))
      .map(getSessionMovementId));
    const afterExerciseIds = new Set(reviewedExercises
      .filter((exercise) => canonicalGroups.some((group) =>
        isSequenceUnitEligible(
          exercise,
          exercisesById,
          group,
          enabledProfile,
        )))
      .map(getSessionMovementId));
    const isMirror = enabledModifier === WORKOUT_MODIFIERS.Mirror;
    const materialExerciseIds = isMirror
      ? new Set(reviewedExercises
          .filter((exercise) =>
            isMirrorPreferred(exercise, enabledProfile) &&
              canonicalGroups.some((group) =>
              isSequenceUnitEligible(
                exercise,
                exercisesById,
                group,
                enabledProfile,
              )))
          .map(getSessionMovementId))
      : new Set([...beforeExerciseIds].filter((exerciseId) =>
          !afterExerciseIds.has(exerciseId)));
    const requiredMaterialExerciseCount = Math.max(
      MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
      Math.ceil(
        (isMirror ? afterExerciseIds.size : beforeExerciseIds.size) *
          MINIMUM_MODIFIER_MATERIALITY_PERCENT / 100,
      ),
    );
    const affectedGroupCount = canonicalGroups.filter((group) => {
      if (isMirror) {
        return reviewedExercises.some((exercise) =>
          isMirrorPreferred(exercise, enabledProfile) &&
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            enabledProfile,
          ));
      }
      const baselineMovementIds = new Set(reviewedExercises
        .filter((exercise) =>
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            baseProfile,
          ))
        .map(getSessionMovementId));
      const modifiedMovementIds = new Set(reviewedExercises
        .filter((exercise) =>
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            enabledProfile,
          ))
        .map(getSessionMovementId));
      return [...baselineMovementIds].some((movementId) =>
        !modifiedMovementIds.has(movementId));
    }).length;
    const requiredAffectedGroupCount = Math.ceil(
      canonicalGroups.length * MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT / 100,
    );

    return {
      baseProfile,
      enabledModifier,
      modifiedProfile: enabledProfile,
      baselineExerciseCount: beforeExerciseIds.size,
      modifiedExerciseCount: afterExerciseIds.size,
      materialExerciseCount: materialExerciseIds.size,
      requiredMaterialExerciseCount,
      affectedGroupCount,
      requiredAffectedGroupCount,
    };
  }).filter((result) =>
    result.materialExerciseCount < result.requiredMaterialExerciseCount ||
    result.affectedGroupCount < result.requiredAffectedGroupCount);
}

export function getMaximumDistinctLineupSize(
  exercises,
  groups,
  modifiers,
  workoutMinutes = 30,
) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const candidateMovementIdsByGroup = groups
    .map((group) => [...new Set(exercises
      .filter((exercise) => isSequenceUnitEligible(
        exercise,
        exercisesById,
        group,
        modifiers,
        workoutMinutes,
      ))
      .map(getSessionMovementId))])
    .sort((left, right) => left.length - right.length);
  const assignedGroupByMovementId = new Map();

  function tryAssignDistinctMovement(groupIndex, visitedMovementIds) {
    for (const movementId of candidateMovementIdsByGroup[groupIndex]) {
      if (visitedMovementIds.has(movementId)) {
        continue;
      }
      visitedMovementIds.add(movementId);

      const assignedGroupIndex = assignedGroupByMovementId.get(movementId);
      if (assignedGroupIndex === undefined ||
          tryAssignDistinctMovement(assignedGroupIndex, visitedMovementIds)) {
        assignedGroupByMovementId.set(movementId, groupIndex);
        return true;
      }
    }

    return false;
  }

  let matchedGroupCount = 0;
  for (let groupIndex = 0;
    groupIndex < candidateMovementIdsByGroup.length;
    groupIndex += 1) {
    if (tryAssignDistinctMovement(groupIndex, new Set())) {
      matchedGroupCount += 1;
    }
  }

  return matchedGroupCount;
}

export function findWorkoutProfileLineupDeficiencies(exercises) {
  return SUPPORTED_MINUTES.flatMap((minutes) => {
    const groups = getResolution(minutes > 30 ? 30 : minutes).groups;
    return WORKOUT_MODIFIER_VALIDATION_PROFILES
      .map((profile) => ({
        minutes,
        profile,
        maximumDistinctExerciseCount: getMaximumDistinctLineupSize(
          exercises,
          groups,
          profile,
          minutes,
        ),
        requiredDistinctExerciseCount: groups.length,
      }))
      .filter((result) =>
        result.maximumDistinctExerciseCount < result.requiredDistinctExerciseCount);
  });
}

function solveMaximumWeightAssignment(utilities, allowed, maximumUtility) {
  const groupCount = utilities.length;
  const candidateCount = utilities[0]?.length ?? 0;
  if (candidateCount < groupCount) {
    return Array(groupCount).fill(-1);
  }

  const invalidCost = (maximumUtility + 1n) * BigInt(groupCount + 1);
  const costs = utilities.map((row, groupIndex) =>
    row.map((utility, candidateIndex) =>
      allowed[groupIndex][candidateIndex]
        ? maximumUtility - utility
        : invalidCost));
  const rowPotential = Array(groupCount + 1).fill(0n);
  const columnPotential = Array(candidateCount + 1).fill(0n);
  const matchedRowByColumn = Array(candidateCount + 1).fill(0);
  const previousColumn = Array(candidateCount + 1).fill(0);

  for (let row = 1; row <= groupCount; row += 1) {
    matchedRowByColumn[0] = row;
    let column = 0;
    const minimumReducedCost = Array(candidateCount + 1).fill(null);
    const visitedColumns = Array(candidateCount + 1).fill(false);
    do {
      visitedColumns[column] = true;
      const currentRow = matchedRowByColumn[column];
      let delta = null;
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
        if (minimumReducedCost[candidateColumn] === null ||
            reducedCost < minimumReducedCost[candidateColumn]) {
          minimumReducedCost[candidateColumn] = reducedCost;
          previousColumn[candidateColumn] = column;
        }
        if (delta === null || minimumReducedCost[candidateColumn] < delta) {
          delta = minimumReducedCost[candidateColumn];
          nextColumn = candidateColumn;
        }
      }
      if (delta === null) {
        return Array(groupCount).fill(-1);
      }
      for (let candidateColumn = 0;
        candidateColumn <= candidateCount;
        candidateColumn += 1) {
        if (visitedColumns[candidateColumn]) {
          rowPotential[matchedRowByColumn[candidateColumn]] += delta;
          columnPotential[candidateColumn] -= delta;
        } else if (minimumReducedCost[candidateColumn] !== null) {
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
  if (!Number.isInteger(modifiers)) {
    return WORKOUT_MODIFIERS.None;
  }
  let normalized = modifiers & SUPPORTED_WORKOUT_MODIFIER_MASK;
  if ((normalized & WORKOUT_MODIFIERS.Mirror) === 0) {
    normalized &= ~WORKOUT_MODIFIERS.TallMirror;
  }
  return normalized;
}

export function getMirrorEquipment(modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  if ((normalized & WORKOUT_MODIFIERS.Mirror) === 0) {
    return MIRROR_EQUIPMENT.None;
  }
  return (normalized & WORKOUT_MODIFIERS.TallMirror) !== 0
    ? MIRROR_EQUIPMENT.Tall
    : MIRROR_EQUIPMENT.Compact;
}

export function withMirrorEquipment(modifiers, equipment) {
  const withoutMirror = normalizeWorkoutModifiers(modifiers) &
    ~(WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return withoutMirror;
  }
  if (equipment === MIRROR_EQUIPMENT.Compact) {
    return withoutMirror | WORKOUT_MODIFIERS.Mirror;
  }
  if (equipment === MIRROR_EQUIPMENT.Tall) {
    return withoutMirror | WORKOUT_MODIFIERS.Mirror |
      WORKOUT_MODIFIERS.TallMirror;
  }
  throw new RangeError(`Unknown mirror equipment: ${equipment}`);
}

export function getMovementCountdownDurationMs(group) {
  return MOVEMENT_DURATION_MS +
    (isSequenceContinuationRound(group) ? 0 : PREPARATION_DURATION_MS);
}

export function getMovementPhaseState(
  remainingMilliseconds,
  includePreparation = true,
) {
  if (remainingMilliseconds <= 0) {
    return { phase: "Complete", secondsRemaining: 0, segmentDurationSeconds: 0, isExercise: false };
  }

  const totalDuration = MOVEMENT_DURATION_MS +
    (includePreparation ? PREPARATION_DURATION_MS : 0);
  const bounded = Math.min(remainingMilliseconds, totalDuration);
  if (includePreparation && bounded > MOVEMENT_DURATION_MS) {
    return {
      phase: "Preparation",
      secondsRemaining: Math.ceil((bounded - MOVEMENT_DURATION_MS) / 1000),
      segmentDurationSeconds: PREPARATION_DURATION_MS / 1000,
      isExercise: false,
    };
  }

  return {
    phase: "Continuous",
    secondsRemaining: Math.ceil(Math.min(bounded, MOVEMENT_DURATION_MS) / 1000),
    segmentDurationSeconds: MOVEMENT_DURATION_MS / 1000,
    isExercise: true,
  };
}

export function getMovementPresentation(group, phase) {
  if (phase === "Complete") {
    return {
      sideCue: "None",
      directionCue: "None",
      mirrorMedia: false,
      activeScreenSide: null,
    };
  }
  const sideCue = group?.sequenceSideCue ?? "None";
  return {
    sideCue,
    directionCue: group?.sequenceDirectionCue ?? "None",
    mirrorMedia: group?.mirrorSequenceMedia === true,
    activeScreenSide: sideCue === "ScreenLeft"
      ? "Left"
      : sideCue === "ScreenRight" ? "Right" : null,
  };
}

export function getExerciseVideoPath(exercise, mediaSegment = "Full") {
  return mediaSegment === "Full"
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
    lastHardWorkUnixMillisecondsByPrimaryMuscle: {},
    lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle: {},
    nextWorkoutExcludedExerciseIds: [],
    activeExtraSetSelectionGroupIds: [],
    activeSetCountsBySelectionGroupId: {},
    activeDirectionPartnerExerciseIds: {},
    activeFullSideRoundIds: [],
    pendingMovementGroupId: null,
    pendingMovementMillisecondsRemaining: 0,
    pendingMovementEndsAtUnixMilliseconds: 0,
    pendingMovementPausedByUser: false,
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
  const sourceVersion = Number.isInteger(raw[SOURCE_STATE_VERSION])
    ? raw[SOURCE_STATE_VERSION]
    : Number.isInteger(raw.version) ? raw.version : 0;
  state.version = sourceVersion;
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
  state.pendingMovementGroupId = typeof raw.pendingMovementGroupId === "string"
    ? raw.pendingMovementGroupId
    : null;
  state.pendingMovementMillisecondsRemaining = Number.isFinite(
    raw.pendingMovementMillisecondsRemaining,
  )
    ? Math.trunc(raw.pendingMovementMillisecondsRemaining)
    : 0;
  state.pendingMovementEndsAtUnixMilliseconds = Number.isFinite(
    raw.pendingMovementEndsAtUnixMilliseconds,
  )
    ? Math.trunc(raw.pendingMovementEndsAtUnixMilliseconds)
    : 0;
  state.pendingMovementPausedByUser = raw.pendingMovementPausedByUser === true;

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
    if (outcome === "x" || outcome === "neutral" || outcome === "tick") {
      state.outcomes[groupId] = outcome;
    }
  }
  state.lastKeptExerciseIds = uniquePositiveIntegers(raw.lastKeptExerciseIds);
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
    raw.lastHardWorkUnixMillisecondsByPrimaryMuscle,
  );
  state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
    raw.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
  );
  state.nextWorkoutExcludedExerciseIds = uniquePositiveIntegers(
    raw.nextWorkoutExcludedExerciseIds,
  );
  state.activeExtraSetSelectionGroupIds = Array.isArray(raw.activeExtraSetSelectionGroupIds)
    ? [...new Set(raw.activeExtraSetSelectionGroupIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  for (const [groupId, setCount] of Object.entries(
    objectOrEmpty(raw.activeSetCountsBySelectionGroupId),
  )) {
    if (typeof groupId === "string" && Number.isInteger(setCount) && setCount > 0) {
      state.activeSetCountsBySelectionGroupId[groupId] = setCount;
    }
  }
  for (const [groupId, exerciseId] of Object.entries(
    objectOrEmpty(raw.activeDirectionPartnerExerciseIds),
  )) {
    if (typeof groupId === "string" && Number.isInteger(exerciseId) && exerciseId > 0) {
      state.activeDirectionPartnerExerciseIds[groupId] = exerciseId;
    }
  }
  state.activeFullSideRoundIds = Array.isArray(raw.activeFullSideRoundIds)
    ? [...new Set(raw.activeFullSideRoundIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  if (state.version < IMPLICIT_SILENCE_STATE_VERSION) {
    migrateImplicitSilenceModifier(state);
  }
  if (state.version < EXPLICIT_MIRROR_EQUIPMENT_STATE_VERSION) {
    migrateExplicitMirrorEquipment(state);
  }
  state.version = CURRENT_WORKOUT_STATE_VERSION;
  Object.defineProperty(state, SOURCE_STATE_VERSION, {
    value: sourceVersion,
    enumerable: false,
  });
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
}

function migrateExplicitMirrorEquipment(state) {
  for (const selectionStorageKey of Object.keys(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    if (match &&
        (normalizeWorkoutModifiers(Number(match[1])) &
          WORKOUT_MODIFIERS.Mirror) !== 0) {
      delete state.selectedExerciseIds[selectionStorageKey];
    }
  }
  // The old binary value did not say whether the available mirror was compact
  // or tall, so do not silently claim either piece of equipment after upgrade.
  state.lastWorkoutModifiers = withMirrorEquipment(
    state.lastWorkoutModifiers,
    MIRROR_EQUIPMENT.None,
  );
  state.activeWorkoutModifiers = withMirrorEquipment(
    state.activeWorkoutModifiers,
    MIRROR_EQUIPMENT.None,
  );
}

function uniquePositiveIntegers(value) {
  return Array.isArray(value)
    ? [...new Set(value.filter((item) => Number.isInteger(item) && item > 0))]
    : [];
}

function objectOrEmpty(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function normalizeWorkHistory(value) {
  const canonicalMuscleGroups = new Set(CANONICAL_GROUPS.slice(1));
  return Object.fromEntries(Object.entries(objectOrEmpty(value))
    .filter(([primaryMuscle, completedAtUnixMilliseconds]) =>
      canonicalMuscleGroups.has(primaryMuscle) &&
      Number.isSafeInteger(completedAtUnixMilliseconds) &&
      completedAtUnixMilliseconds > 0));
}

function sameStringSet(left, right) {
  const leftSet = new Set(left);
  const rightSet = new Set(right);
  return leftSet.size === rightSet.size &&
    [...leftSet].every((value) => rightSet.has(value));
}

export class WorkoutSession {
  constructor(
    exercises,
    storedState = createDefaultState(),
    random = Math.random,
    nowProvider = () => Date.now(),
  ) {
    if (!Array.isArray(exercises)) {
      throw new TypeError("Exercise catalog must be an array.");
    }
    this.exercises = exercises;
    this.exercisesById = new Map(exercises.map((exercise) => [exercise.id, exercise]));
    if (this.exercisesById.size !== exercises.length) {
      throw new Error("Exercise catalog contains duplicate IDs.");
    }
    this.sequenceRootByExerciseId = new Map();
    for (const root of exercises.filter((exercise) =>
      Array.isArray(exercise.sequenceBlocks) && exercise.sequenceBlocks.length > 0)) {
      for (const memberId of new Set(root.sequenceBlocks.map((block) =>
        block.exerciseId))) {
        if (!this.exercisesById.has(memberId) ||
            this.sequenceRootByExerciseId.has(memberId)) {
          throw new Error(`Exercise ${memberId} has an invalid sequence owner.`);
        }
        this.sequenceRootByExerciseId.set(memberId, root);
      }
    }
    if (this.sequenceRootByExerciseId.size !== exercises.length) {
      throw new Error("Every exercise must belong to exactly one sequence.");
    }
    this.state = normalizeStateShape(storedState);
    this.loadedStateVersion = this.state[SOURCE_STATE_VERSION] ?? this.state.version;
    this.random = random;
    this.nowProvider = nowProvider;
  }

  getCurrentUnixTimeMilliseconds() {
    const value = this.nowProvider();
    if (!Number.isSafeInteger(value) || value <= 0) {
      throw new TypeError("Time provider must return positive Unix milliseconds.");
    }
    return value;
  }

  initialize() {
    const atomicSequenceMigration =
      this.loadedStateVersion === 13 && this.state.activeWorkoutMinutes > 0
        ? this.captureLegacyActiveProgress()
        : null;
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

    this.normalizePendingRest();
    this.repairActiveLineup();
    this.normalizeActiveLongWorkoutAllocation();
    if (atomicSequenceMigration) {
      this.migrateLegacyActiveProgress(atomicSequenceMigration);
    }
    this.normalizeOutcomes();
    this.normalizeCompletionState();
    this.normalizePendingRest();
    this.normalizePendingMovement();
    this.normalizeCompletionState();
    if (this.state.workoutCompleted && this.state.completionAcknowledged) {
      this.prepareNextSession();
    }
  }

  startWorkout(minutes, modifiers = DEFAULT_WORKOUT_MODIFIERS) {
    if (!SUPPORTED_MINUTES.includes(minutes)) {
      throw new RangeError("Unsupported workout duration.");
    }
    if (this.state.activeWorkoutMinutes !== 0) {
      throw new Error("A workout is already active.");
    }

    this.normalizeKeptExerciseIds();
    this.state.version = CURRENT_WORKOUT_STATE_VERSION;
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
    this.clearPendingMovement();
    this.clearPendingRest();
    this.carryKeptExercisesForward(previousWorkoutMinutes, previousWorkoutModifiers);
    this.repairActiveLineup();
    this.rebalanceNewExercisesByMuscleBudget();
    this.setActiveLongWorkoutAllocation();
    this.state.nextWorkoutExcludedExerciseIds = [];
  }

  getActiveGroups() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      return [];
    }
    const roots = new Map(this.getSelectionGroups().map((group) => [
      group.id,
      this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ]),
    ]));
    return createWorkoutSchedule(
      this.state.activeWorkoutMinutes,
      roots,
      this.getEffectiveSetCounts(),
    );
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

  isIntermediateSequenceBlock(group) {
    const activeGroups = this.getActiveGroups();
    const groupIndex = activeGroups.findIndex((activeGroup) =>
      activeGroup.id === group?.id);
    return groupIndex >= 0 &&
      groupIndex + 1 < activeGroups.length &&
      getSelectionKey(activeGroups[groupIndex + 1]) === getSelectionKey(group);
  }

  isSequenceContinuationBlock(group) {
    if (!group) {
      return false;
    }
    const activeGroups = this.getActiveGroups();
    const groupIndex = activeGroups.findIndex((activeGroup) =>
      activeGroup.id === group.id);
    return groupIndex > 0 &&
      getSelectionKey(activeGroups[groupIndex - 1]) === getSelectionKey(group);
  }

  canShuffleNextExercise(group) {
    return this.getNextGroup()?.id === group.id &&
      !this.isSequenceContinuationBlock(group) &&
      this.getCompatibleShuffleCandidates(group).length > 0;
  }

  shuffleNextExercise(group) {
    if (this.getNextGroup()?.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (this.isSequenceContinuationBlock(group)) {
      return null;
    }

    const candidates = this.getCompatibleShuffleCandidates(group);
    if (candidates.length === 0) {
      return null;
    }

    const rejectedExercise = this.getSelectedExercise(group);
    const scoreUpdates = this.getSequenceExercises(rejectedExercise);

    this.shuffle(candidates);
    const selectionGroup = this.getSelectionGroups().find((candidate) =>
      candidate.id === getSelectionKey(group));
    if (!selectionGroup) {
      return null;
    }
    const selected = candidates[0];

    this.state.selectedExerciseIds[this.getSelectionStorageKey(
      selectionGroup.id,
      this.state.activeWorkoutModifiers,
    )] = selected.exercise.id;
    this.applyShuffleRejection(scoreUpdates);
    this.applyLongWorkoutAllocation(selected.allocation);
    return {
      rejectedExercise,
      replacementExercise: selected.exercise,
      scoreUpdates,
    };
  }

  applyShuffleRejection(exercises) {
    const rejectedExerciseIds = new Set(exercises.map((exercise) => exercise.id));
    for (const exercise of exercises) {
      this.setScore(exercise, this.getScore(exercise) - 1);
    }
    this.state.nextWorkoutExcludedExerciseIds = [...new Set([
      ...this.state.nextWorkoutExcludedExerciseIds,
      ...rejectedExerciseIds,
    ])];
    this.state.lastKeptExerciseIds = this.state.lastKeptExerciseIds.filter(
      (exerciseId) => !rejectedExerciseIds.has(exerciseId),
    );
    for (const [savedGroupId, exerciseId] of Object.entries(
      this.state.selectedExerciseIds,
    )) {
      if (rejectedExerciseIds.has(exerciseId)) {
        delete this.state.selectedExerciseIds[savedGroupId];
      }
    }
  }

  getSelectedExercise(group) {
    if (Number.isInteger(group.exerciseOverrideId) && group.exerciseOverrideId > 0) {
      const overrideExercise = this.exercisesById.get(group.exerciseOverrideId);
      if (!overrideExercise || !this.isSequenceOverrideValid(
        overrideExercise,
        group,
        this.state.activeWorkoutModifiers,
      )) {
        throw new Error(`The linked sequence block for ${group.displayName} is unavailable.`);
      }
      return overrideExercise;
    }
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

  tryGetSelectedExercise(group) {
    try {
      return this.getSelectedExercise(group);
    } catch {
      return null;
    }
  }

  getCompatibleShuffleCandidates(currentRound) {
    const activeRounds = this.getActiveGroups();
    const selectionGroupId = getSelectionKey(currentRound);
    if (
      activeRounds.some((round) =>
        getSelectionKey(round) === selectionGroupId &&
        this.state.outcomes[round.id] !== undefined) ||
      !this.isLongWorkoutAllocationValid()
    ) {
      return [];
    }

    const selectionGroup = this.getSelectionGroups().find((group) =>
      group.id === selectionGroupId);
    if (!selectionGroup) {
      return [];
    }
    const selectionStorageKey = this.getSelectionStorageKey(
      selectionGroup.id,
      this.state.activeWorkoutModifiers,
    );
    const currentExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
    const currentExercise = this.exercisesById.get(currentExerciseId);
    if (!Number.isInteger(currentExerciseId) ||
        currentExerciseId <= 0 ||
        !currentExercise) {
      return [];
    }

    const rejectedExerciseIds = new Set(this.getSequenceExercises(currentExercise)
      .map((exercise) => exercise.id));
    const startedSelectionGroupIds = new Set(activeRounds
      .filter((round) => this.state.outcomes[round.id] !== undefined)
      .map(getSelectionKey));

    const unavailableExerciseIds = new Set(this.getSelectionGroups()
      .filter((group) => group.id !== selectionGroup.id)
      .map((group) => this.state.selectedExerciseIds[this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      )])
      .filter((exerciseId) => Number.isInteger(exerciseId) && exerciseId > 0));
    const unavailableMovementIds = new Set([
      ...[...unavailableExerciseIds]
        .map((exerciseId) => this.exercisesById.get(exerciseId))
        .filter(Boolean)
        .map(getSessionMovementId),
      getSessionMovementId(currentExercise),
    ]);
    const candidates = [];
    for (const exercise of this.exercises) {
      if (
        exercise.id === currentExerciseId ||
        this.state.nextWorkoutExcludedExerciseIds.includes(exercise.id) ||
        unavailableExerciseIds.has(exercise.id) ||
        unavailableMovementIds.has(getSessionMovementId(exercise)) ||
        !this.isWorkoutSelectionCandidate(
          exercise,
          selectionGroup,
          this.state.activeWorkoutModifiers,
        )
      ) {
        continue;
      }
      const allocation = this.tryGetCompatibleShuffleAllocation(
        selectionStorageKey,
        exercise,
        startedSelectionGroupIds,
        rejectedExerciseIds,
      );
      if (allocation) {
        candidates.push({ exercise, allocation });
      }
    }
    return candidates;
  }

  tryGetCompatibleShuffleAllocation(
    selectionStorageKey,
    candidate,
    startedSelectionGroupIds,
    rejectedExerciseIds,
  ) {
    const previousExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
    const previousLastKeptExerciseIds = this.state.lastKeptExerciseIds;
    this.state.selectedExerciseIds[selectionStorageKey] = candidate.id;
    this.state.lastKeptExerciseIds = previousLastKeptExerciseIds
      .filter((exerciseId) => !rejectedExerciseIds.has(exerciseId));
    try {
      return this.chooseLongWorkoutAllocation(startedSelectionGroupIds);
    } catch {
      return null;
    } finally {
      this.state.selectedExerciseIds[selectionStorageKey] = previousExerciseId;
      this.state.lastKeptExerciseIds = previousLastKeptExerciseIds;
    }
  }

  beginMovement(group, millisecondsRemaining, endsAtUnixMilliseconds) {
    const normalizedRemaining = Math.trunc(millisecondsRemaining);
    const normalizedDeadline = Math.trunc(endsAtUnixMilliseconds);
    this.validatePendingMovement(
      group,
      normalizedRemaining,
      normalizedDeadline,
      false,
    );
    this.clearPendingRest();
    this.state.pendingMovementGroupId = group.id;
    this.state.pendingMovementMillisecondsRemaining = normalizedRemaining;
    this.state.pendingMovementEndsAtUnixMilliseconds = normalizedDeadline;
    this.state.pendingMovementPausedByUser = false;
  }

  pauseMovement(group, millisecondsRemaining, pausedByUser) {
    const normalizedRemaining = Math.trunc(millisecondsRemaining);
    this.validatePendingMovement(group, normalizedRemaining, 0, true);
    this.clearPendingRest();
    this.state.pendingMovementGroupId = group.id;
    this.state.pendingMovementMillisecondsRemaining = normalizedRemaining;
    this.state.pendingMovementEndsAtUnixMilliseconds = 0;
    this.state.pendingMovementPausedByUser = pausedByUser === true;
  }

  getPendingMovementGroup() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingMovementGroupId,
    );
    if (!pendingGroup ||
        this.getNextGroup()?.id !== pendingGroup.id ||
        this.state.outcomes[pendingGroup.id] !== undefined ||
        !Number.isSafeInteger(this.state.pendingMovementMillisecondsRemaining) ||
        this.state.pendingMovementMillisecondsRemaining <= 0 ||
        this.state.pendingMovementMillisecondsRemaining >
          getMovementCountdownDurationMs(pendingGroup) ||
        !Number.isSafeInteger(this.state.pendingMovementEndsAtUnixMilliseconds) ||
        this.state.pendingMovementEndsAtUnixMilliseconds < 0) {
      return null;
    }

    try {
      this.getSelectedExercise(pendingGroup);
    } catch {
      return null;
    }
    return pendingGroup;
  }

  getPendingRestGroup() {
    const pendingGroup = this.getValidPendingRestGroup();
    return pendingGroup && this.getNextGroup()?.id === pendingGroup.id
      ? pendingGroup
      : null;
  }

  getValidPendingRestGroup() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    if (!pendingGroup ||
        this.state.pendingRestEndsAtUnixMilliseconds <= 0 ||
        this.state.outcomes[pendingGroup.id] !== undefined) {
      return null;
    }

    try {
      const pendingExercise = this.getSelectedExercise(pendingGroup);
      return isCompatibleWithWorkoutModifiers(
          pendingExercise,
          this.state.activeWorkoutModifiers,
        ) && this.isAssignedToGroup(pendingExercise, pendingGroup)
        ? pendingGroup
        : null;
    } catch {
      return null;
    }
  }

  getPendingMovementMillisecondsRemaining(nowUnixMilliseconds) {
    if (!Number.isSafeInteger(nowUnixMilliseconds) || nowUnixMilliseconds <= 0) {
      throw new RangeError("Current time must be positive Unix milliseconds.");
    }
    const pendingGroup = this.getPendingMovementGroup();
    if (!pendingGroup) {
      return 0;
    }
    const storedRemaining = this.state.pendingMovementMillisecondsRemaining;
    const remaining = this.state.pendingMovementEndsAtUnixMilliseconds >
        nowUnixMilliseconds
      ? Math.min(
          storedRemaining,
          this.state.pendingMovementEndsAtUnixMilliseconds - nowUnixMilliseconds,
        )
      : storedRemaining;
    return Math.max(
      1,
      Math.min(remaining, getMovementCountdownDurationMs(pendingGroup)),
    );
  }

  clearPendingMovement() {
    this.state.pendingMovementGroupId = null;
    this.state.pendingMovementMillisecondsRemaining = 0;
    this.state.pendingMovementEndsAtUnixMilliseconds = 0;
    this.state.pendingMovementPausedByUser = false;
  }

  validatePendingMovement(
    group,
    millisecondsRemaining,
    endsAtUnixMilliseconds,
    allowPausedDeadline,
  ) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!Number.isSafeInteger(millisecondsRemaining) ||
        millisecondsRemaining <= 0 ||
        millisecondsRemaining > getMovementCountdownDurationMs(group)) {
      throw new RangeError("Movement time remaining is invalid.");
    }
    if ((!allowPausedDeadline &&
          (!Number.isSafeInteger(endsAtUnixMilliseconds) ||
            endsAtUnixMilliseconds <= 0)) ||
        (allowPausedDeadline && endsAtUnixMilliseconds !== 0)) {
      throw new RangeError("Movement deadline is invalid.");
    }
    this.getSelectedExercise(group);
  }

  beginRest(group, endsAtUnixMilliseconds) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!Number.isSafeInteger(endsAtUnixMilliseconds) || endsAtUnixMilliseconds <= 0) {
      throw new RangeError("Rest deadline must be positive Unix milliseconds.");
    }
    this.clearPendingMovement();
    this.state.pendingRestGroupId = group.id;
    this.state.pendingRestEndsAtUnixMilliseconds = Math.trunc(endsAtUnixMilliseconds);
    this.state.pendingRestKept = false;
    const exercise = this.getSelectedExercise(group);
    if (exercise.muscularDemand === MODERATE_MUSCULAR_DEMAND ||
        exercise.muscularDemand === HARD_MUSCULAR_DEMAND) {
      const primaryMuscle = exercise.primaryCanonicalGroup;
      const completedAtUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[primaryMuscle] =
        Math.max(
          completedAtUnixMilliseconds,
          getLastMeaningfulWorkUnixMilliseconds(
            this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
            primaryMuscle,
          ),
        );
      if (exercise.muscularDemand === HARD_MUSCULAR_DEMAND) {
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle[primaryMuscle] =
          Math.max(
            completedAtUnixMilliseconds,
            getLastHardWorkUnixMilliseconds(
              this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
              primaryMuscle,
            ),
          );
      }
    }
  }

  keepPendingRest() {
    const pendingGroup = this.getPendingRestGroup();
    if (!pendingGroup ||
        this.isIntermediateSequenceBlock(pendingGroup)) {
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
    if (!isFinalSequenceRound(group)) {
      throw new Error("A sequence can only be rated after its final block.");
    }

    this.clearPendingMovement();
    return this.applySequenceOutcome(group, keep);
  }

  rejectCurrentSequence(group) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }

    const sequenceRounds = this.getActiveGroups()
      .filter((round) => getSelectionKey(round) === getSelectionKey(group))
      .sort((left, right) => left.order - right.order);
    const decisionRound = sequenceRounds.at(-1);
    for (const round of sequenceRounds) {
      if (round.id !== decisionRound.id) {
        this.state.outcomes[round.id] ??= "neutral";
      }
    }
    this.clearPendingMovement();
    this.clearPendingRest();
    return this.applySequenceOutcome(decisionRound, false);
  }

  advanceSequence(group) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!this.isIntermediateSequenceBlock(group)) {
      throw new Error(`${group.displayName} is not an intermediate sequence block.`);
    }
    this.state.outcomes[group.id] = "neutral";
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
  }

  applySequenceOutcome(group, keep) {
    const exercise = this.getSelectedExercise(group);
    const sequenceExercises = this.getSequenceExercises(exercise);
    if (!keep) {
      for (const member of sequenceExercises) {
        this.setScore(member, this.getScore(member) - 1);
      }
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
        if (this.isIntermediateSequenceBlock(group)) {
          this.advanceSequence(group);
        } else {
          this.applySequenceOutcome(group, this.state.pendingRestKept);
        }
      }
      this.clearPendingRest();
    }
    this.prepareNextSession();
  }

  prepareNextSession() {
    const activeGroups = this.getActiveGroups();
    const selectionGroups = this.getSelectionGroups();
    const rejectedSelectionKeys = new Set();
    const newlyKeptExerciseIds = new Set();
    const rejectedExerciseIds = new Set();
    for (const selectionGroup of selectionGroups) {
      const decisionRound = activeGroups
        .filter((round) => getSelectionKey(round) === selectionGroup.id)
        .sort((left, right) => left.order - right.order)
        .at(-1);
      const outcome = decisionRound
        ? this.state.outcomes[decisionRound.id]
        : undefined;
      if (outcome !== "tick" && outcome !== "x") {
        continue;
      }
      const root = this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(
          selectionGroup.id,
          this.state.activeWorkoutModifiers,
        )
      ]);
      if (!root) {
        continue;
      }
      const memberIds = this.getSequenceExercises(root).map((exercise) =>
        exercise.id);
      if (outcome === "tick") {
        memberIds.forEach((exerciseId) => newlyKeptExerciseIds.add(exerciseId));
      } else {
        rejectedSelectionKeys.add(selectionGroup.id);
        memberIds.forEach((exerciseId) => rejectedExerciseIds.add(exerciseId));
      }
    }
    this.state.nextWorkoutExcludedExerciseIds = [...new Set([
      ...this.state.nextWorkoutExcludedExerciseIds,
      ...rejectedExerciseIds,
    ])];
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
    let activeGroups = [];
    try {
      activeGroups = this.getActiveGroups();
    } catch {
      activeGroups = [];
    }
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
      const sequenceExercises = this.getSequenceExercises(exercise);
      if (sequenceExercises.some((member) =>
        this.state.nextWorkoutExcludedExerciseIds.includes(member.id))) {
        return false;
      }
      if (sequenceExercises.some((member) =>
        excludedExerciseIdsByGroup.get(group.id)?.has(member.id))) {
        return false;
      }
      if (this.isWorkoutSelectionCandidate(exercise, group, modifiers)) {
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
    const orderedScores = [...new Set(candidates.map((exercise) =>
      this.getSelectionScore(exercise)))].sort((left, right) => left - right);
    const scoreRanks = new Map(orderedScores.map((score, rank) => [score, rank]));
    const highestScoreByGroup = new Map(groups.map((group) => {
      const allowedScores = candidates
        .filter((exercise) => isAllowed(exercise, group))
        .map((exercise) => this.getSelectionScore(exercise));
      return [
        group.id,
        allowedScores.length > 0
          ? Math.max(...allowedScores)
          : Number.MIN_SAFE_INTEGER,
      ];
    }));
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    const freshHardMuscleTimestamps = [...new Set(candidates
      .filter((exercise) =>
        exercise.muscularDemand === HARD_MUSCULAR_DEMAND &&
        groups.some((group) => group.canonicalGroups.includes(
          exercise.primaryCanonicalGroup,
        )) &&
        !isPrimaryMuscleRecovering(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
          selectionTimeUnixMilliseconds,
        ))
      .map((exercise) => getLastHardWorkUnixMilliseconds(
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        exercise.primaryCanonicalGroup,
      )))].sort((left, right) => right - left);
    const freshHardMuscleRanks = new Map(
      freshHardMuscleTimestamps.map((timestamp, rank) => [timestamp, rank]),
    );
    const maximumCoverage = Math.max(...groups.map((group) => group.canonicalGroups.length));
    // These are exact lexicographic assignment dimensions, not hardness
    // points. BigInt keeps arbitrary saved-score histories lossless without
    // persisting any derived value.
    let totalLowerPriorityRange = BigInt(groups.length * maximumCoverage);
    const addPriorityDimension = (maximumValue) => {
      const weight = totalLowerPriorityRange + 1n;
      totalLowerPriorityRange +=
        BigInt(groups.length) * BigInt(maximumValue) * weight;
      return weight;
    };
    const primaryWeight = addPriorityDimension(1);
    const mirrorPreferenceWeight = addPriorityDimension(1);
    const currentSelectionWeight = addPriorityDimension(1);
    const hardMuscleAgeWeight = addPriorityDimension(
      Math.max(0, freshHardMuscleRanks.size - 1),
    );
    const moderateRecoveryAvoidanceWeight = addPriorityDimension(1);
    const hardRecoveryAvoidanceWeight = addPriorityDimension(1);
    const freshHardWeight = addPriorityDimension(1);
    const scoreWeight = addPriorityDimension(Math.max(0, orderedScores.length - 1));
    const keptExerciseWeight = addPriorityDimension(1);
    const hardOpportunityWeight = addPriorityDimension(1);
    const preservedActiveSelectionWeight = allowSavedSelectionException
      ? totalLowerPriorityRange + 1n
      : 0n;

    const allowed = groups.map(() => candidates.map(() => false));
    const utilities = groups.map(() => candidates.map(() => 0n));
    let maximumUtility = 0n;
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      const group = groups[groupIndex];
      for (let exerciseIndex = 0; exerciseIndex < candidates.length; exerciseIndex += 1) {
        const exercise = candidates[exerciseIndex];
        if (!isAllowed(exercise, group)) {
          continue;
        }
        allowed[groupIndex][exerciseIndex] = true;
        const hardRotationStatus = getHardRotationStatus(
          exercise,
          group,
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          selectionTimeUnixMilliseconds,
        );
        const isRecoveringModerate = isModerateExerciseRecovering(
          exercise,
          this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
          selectionTimeUnixMilliseconds,
        );
        const hardMuscleAgeRank = hardRotationStatus === HARD_ROTATION_STATUS.FreshHard
          ? freshHardMuscleRanks.get(getLastHardWorkUnixMilliseconds(
              this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
              exercise.primaryCanonicalGroup,
            )) ?? 0
          : 0;
        const isKept = preferredExerciseIds.has(exercise.id);
        // A non-kept hard exercise can displace a keep only while it is fresh,
        // primary for this slot, and already in that slot's top saved-score
        // bucket. A fresh hard keep is the explicit second path.
        const hasHardOpportunity =
          hardRotationStatus === HARD_ROTATION_STATUS.FreshHard &&
          (isKept ||
            this.getSelectionScore(exercise) === highestScoreByGroup.get(group.id));
        const hasContextualKeepPreference = isKept &&
          hardRotationStatus !== HARD_ROTATION_STATUS.RecoveringHard &&
          !isRecoveringModerate;
        const utility =
          (allowSavedSelectionException &&
            currentExerciseIds.get(group.id) === exercise.id
            ? preservedActiveSelectionWeight
            : 0n) +
          (hasHardOpportunity ? hardOpportunityWeight : 0n) +
          (hasContextualKeepPreference ? keptExerciseWeight : 0n) +
          BigInt(scoreRanks.get(this.getSelectionScore(exercise))) * scoreWeight +
          (hardRotationStatus !== HARD_ROTATION_STATUS.RecoveringHard
            ? hardRecoveryAvoidanceWeight
            : 0n) +
          (!isRecoveringModerate ? moderateRecoveryAvoidanceWeight : 0n) +
          (hardRotationStatus === HARD_ROTATION_STATUS.FreshHard
            ? freshHardWeight
            : 0n) +
          BigInt(hardMuscleAgeRank) * hardMuscleAgeWeight +
          (currentExerciseIds.get(group.id) === exercise.id ? currentSelectionWeight : 0n) +
          (isMirrorPreferred(exercise, modifiers) ? mirrorPreferenceWeight : 0n) +
          (isPrimaryForGroup(exercise, group) ? primaryWeight : 0n) +
          BigInt(getCanonicalCoverage(exercise, group));
        utilities[groupIndex][exerciseIndex] = utility;
        maximumUtility = maximumUtility > utility ? maximumUtility : utility;
      }
    }

    const movementIds = [...new Set(candidates.map(getSessionMovementId))];
    if (movementIds.length < groups.length) {
      throw this.createDistinctLineupError(groups, movementIds.length);
    }
    const movementIndexes = new Map(movementIds.map((movementId, index) =>
      [movementId, index]));
    const movementAllowed = groups.map(() => movementIds.map(() => false));
    const movementUtilities = groups.map(() => movementIds.map(() => 0n));
    const candidateIndexesByGroupAndMovement = groups.map(() =>
      movementIds.map(() => -1));
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      for (let candidateIndex = 0;
        candidateIndex < candidates.length;
        candidateIndex += 1) {
        if (!allowed[groupIndex][candidateIndex]) {
          continue;
        }
        const movementIndex = movementIndexes.get(getSessionMovementId(
          candidates[candidateIndex],
        ));
        if (!movementAllowed[groupIndex][movementIndex] ||
            utilities[groupIndex][candidateIndex] >
              movementUtilities[groupIndex][movementIndex]) {
          movementAllowed[groupIndex][movementIndex] = true;
          movementUtilities[groupIndex][movementIndex] =
            utilities[groupIndex][candidateIndex];
          candidateIndexesByGroupAndMovement[groupIndex][movementIndex] =
            candidateIndex;
        }
      }
    }

    const assignment = solveMaximumWeightAssignment(
      movementUtilities,
      movementAllowed,
      maximumUtility,
    );
    const lineup = new Map();
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      const movementIndex = assignment[groupIndex];
      if (movementIndex < 0 || !movementAllowed[groupIndex][movementIndex]) {
        throw this.createDistinctLineupError(groups, movementIds.length);
      }
      const candidateIndex =
        candidateIndexesByGroupAndMovement[groupIndex][movementIndex];
      lineup.set(groups[groupIndex].id, candidates[candidateIndex].id);
    }

    const scheduledBlockCount = () => [...lineup.values()]
      .reduce((total, exerciseId) => total +
        this.exercisesById.get(exerciseId).sequenceBlocks.length, 0);
    const hasSingleBlockSequence = () => [...lineup.values()].some((exerciseId) =>
      this.exercisesById.get(exerciseId).sequenceBlocks.length === 1);
    while (scheduledBlockCount() > this.state.activeWorkoutMinutes ||
           (this.state.activeWorkoutMinutes > 30 && !hasSingleBlockSequence())) {
      const selectedMovementIds = new Set([...lineup.values()].map((exerciseId) =>
        getSessionMovementId(this.exercisesById.get(exerciseId))));
      let best = null;
      for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
        const currentExerciseId = lineup.get(groups[groupIndex].id);
        const currentCandidateIndex = candidates.findIndex((candidate) =>
          candidate.id === currentExerciseId);
        const currentExercise = this.exercisesById.get(currentExerciseId);
        const currentCost = currentExercise.sequenceBlocks.length;
        const currentMovementId = getSessionMovementId(currentExercise);
        for (let candidateIndex = 0;
          candidateIndex < candidates.length;
          candidateIndex += 1) {
          if (!allowed[groupIndex][candidateIndex]) {
            continue;
          }
          const alternative = candidates[candidateIndex];
          const savedBlocks = currentCost - alternative.sequenceBlocks.length;
          if (savedBlocks <= 0) {
            continue;
          }
          const alternativeMovementId = getSessionMovementId(alternative);
          if (alternativeMovementId !== currentMovementId &&
              selectedMovementIds.has(alternativeMovementId)) {
            continue;
          }
          const utilityLoss = utilities[groupIndex][currentCandidateIndex] -
            utilities[groupIndex][candidateIndex];
          if (!best || utilityLoss < best.utilityLoss ||
              (utilityLoss === best.utilityLoss &&
                savedBlocks < best.savedBlocks)) {
            best = { groupIndex, candidateIndex, utilityLoss, savedBlocks };
          }
        }
      }
      if (!best) {
        throw new Error("No distinct sequence lineup fits the selected workout duration.");
      }
      lineup.set(groups[best.groupIndex].id, candidates[best.candidateIndex].id);
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

    const highestScore = Math.max(...candidates.map((exercise) =>
      this.getSelectionScore(exercise)));
    const highestScored = candidates.filter((exercise) =>
      this.getSelectionScore(exercise) === highestScore);
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    const rotationStatusByExercise = new Map(highestScored.map((exercise) => [
      exercise.id,
      getHardRotationStatus(
        exercise,
        group,
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      ),
    ]));
    const isRecoveringModerateByExercise = new Map(highestScored.map((exercise) => [
      exercise.id,
      isModerateExerciseRecovering(
        exercise,
        this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      ),
    ]));
    const rotationTiers = [
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.FreshHard),
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) !== HARD_ROTATION_STATUS.RecoveringHard &&
        !isRecoveringModerateByExercise.get(exercise.id)),
      highestScored.filter((exercise) => isRecoveringModerateByExercise.get(exercise.id)),
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.RecoveringHard),
    ];
    const rotationPreferred = rotationTiers.find((tier) => tier.length > 0);
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    const kept = rotationPreferred.filter((exercise) => keptExerciseIds.has(exercise.id));
    let keepPreferred = kept.length > 0 ? kept : rotationPreferred;
    if (rotationPreferred.every((exercise) =>
      rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.FreshHard)) {
      const oldestHardWork = Math.min(...keepPreferred.map((exercise) =>
        getLastHardWorkUnixMilliseconds(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
        )));
      keepPreferred = keepPreferred.filter((exercise) =>
        getLastHardWorkUnixMilliseconds(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
        ) === oldestHardWork);
    }
    const mirrorRelevant = keepPreferred.filter((exercise) =>
      isMirrorPreferred(exercise, modifiers));
    const mirrorPreferred = mirrorRelevant.length > 0 ? mirrorRelevant : keepPreferred;
    const primaryOwned = mirrorPreferred.filter((exercise) =>
      isPrimaryForGroup(exercise, group));
    const ownershipPreferred = primaryOwned.length > 0 ? primaryOwned : mirrorPreferred;
    const widestCoverage = Math.max(
      ...ownershipPreferred.map((exercise) => getCanonicalCoverage(exercise, group)),
    );
    const finalists = ownershipPreferred.filter((exercise) =>
      getCanonicalCoverage(exercise, group) === widestCoverage);
    const index = Math.min(finalists.length - 1, Math.floor(this.random() * finalists.length));
    return finalists[Math.max(0, index)];
  }

  applyDistinctLineup(groups, lineup, clearChangedProgress, activeGroups = null) {
    activeGroups ??= clearChangedProgress ? this.getActiveGroups() : [];
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

  createDistinctLineupError(groups, movementCount) {
    return new Error(
      `No distinct exercise lineup exists for the active workout profile across ` +
      `${groups.length} groups and ${movementCount} eligible session movements with at least ` +
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

  getSequenceRoot(exercise) {
    const root = exercise
      ? this.sequenceRootByExerciseId.get(exercise.id)
      : null;
    if (!root) {
      throw new Error(`Exercise ${exercise?.id ?? "unknown"} has no sequence root.`);
    }
    return root;
  }

  getSequenceExercises(exercise) {
    const root = this.getSequenceRoot(exercise);
    return [...new Map(root.sequenceBlocks.map((block) => {
      const member = this.exercisesById.get(block.exerciseId);
      return [member.id, member];
    })).values()];
  }

  isWorkoutSelectionCandidate(exercise, group, modifiers) {
    if (!Array.isArray(exercise?.sequenceBlocks) ||
        exercise.sequenceBlocks.length === 0 ||
        this.getSequenceRoot(exercise).id !== exercise.id ||
        (this.state.activeWorkoutMinutes <= 30 &&
          exercise.sequenceBlocks.length > 1) ||
        (this.state.activeWorkoutMinutes > 30 &&
          exercise.sequenceBlocks.length > this.state.activeWorkoutMinutes - 29)) {
      return false;
    }
    return this.getSequenceExercises(exercise).every((member) =>
      this.isSelectable(member, group, modifiers));
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
      if (!group || !exercise || !this.isStoredLineupSelectionValid(
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

  getSelectionScore(exercise) {
    return Math.min(...this.getSequenceExercises(exercise).map((member) =>
      this.getScore(member)));
  }

  normalizeKeptExerciseIds() {
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds.filter((exerciseId) =>
      this.exercisesById.has(exerciseId)));
    this.expandSequenceIds(keptExerciseIds);
    this.state.lastKeptExerciseIds = [...keptExerciseIds];
    this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
    );
    this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
    );
    const nextExcludedExerciseIds = new Set(
      this.state.nextWorkoutExcludedExerciseIds.filter((exerciseId) =>
        this.exercisesById.has(exerciseId)),
    );
    this.expandSequenceIds(nextExcludedExerciseIds);
    this.state.nextWorkoutExcludedExerciseIds = [...nextExcludedExerciseIds];
  }

  expandSequenceIds(exerciseIds) {
    for (const exerciseId of [...exerciseIds]) {
      const exercise = this.exercisesById.get(exerciseId);
      if (exercise) {
        for (const member of this.getSequenceExercises(exercise)) {
          exerciseIds.add(member.id);
        }
      }
    }
  }

  isStoredLineupSelectionValid(exercise, group, modifiers) {
    if (this.isSavedSelectionValid(exercise, group, modifiers)) {
      return true;
    }
    const isLongWorkoutSelectionGroup = RESOLUTIONS.get(30).groups.some(
      (candidate) => candidate.id === group.id,
    );
    return this.state.activeWorkoutMinutes === 0 &&
      isLongWorkoutSelectionGroup &&
      Array.isArray(exercise.sequenceBlocks) &&
      exercise.sequenceBlocks.length > 0 &&
      this.getSequenceRoot(exercise).id === exercise.id &&
      this.getSequenceExercises(exercise).every((member) =>
        this.isCompatibleWithModifiers(member, modifiers) &&
        this.isAssignedToGroup(member, group));
  }

  normalizeActiveLongWorkoutAllocation() {
    if (!this.isLongWorkoutAllocationValid()) {
      this.setActiveLongWorkoutAllocation();
    }
  }

  isLongWorkoutAllocationValid() {
    if (this.state.activeWorkoutMinutes <= 30) {
      return Object.keys(this.state.activeDirectionPartnerExerciseIds).length === 0 &&
        this.state.activeFullSideRoundIds.length === 0 &&
        this.state.activeExtraSetSelectionGroupIds.length === 0 &&
        Object.keys(this.state.activeSetCountsBySelectionGroupId).length === 0;
    }

    let expected;
    try {
      expected = this.chooseLongWorkoutAllocation();
    } catch {
      return false;
    }
    if (Object.keys(this.state.activeDirectionPartnerExerciseIds).length !== 0 ||
        this.state.activeFullSideRoundIds.length !== 0) {
      return false;
    }

    const selectionGroups = this.getSelectionGroups();
    const actualSetCounts = new Map(Object.entries(
      this.state.activeSetCountsBySelectionGroupId,
    ));
    if (actualSetCounts.size !== selectionGroups.length ||
        selectionGroups.some((group) =>
          !Number.isInteger(actualSetCounts.get(group.id)) ||
          actualSetCounts.get(group.id) < 1)) {
      return false;
    }

    const expectedExtraSetGroups = [...actualSetCounts]
      .filter(([, setCount]) => setCount > 1)
      .map(([groupId]) => groupId);
    if (!sameStringSet(
      this.state.activeExtraSetSelectionGroupIds,
      expectedExtraSetGroups,
    )) {
      return false;
    }

    try {
      const roots = new Map(selectionGroups.map((group) => [
        group.id,
        this.exercisesById.get(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
        ]),
      ]));
      const rounds = createWorkoutSchedule(
        this.state.activeWorkoutMinutes,
        roots,
        actualSetCounts,
      );
      return rounds.length === this.state.activeWorkoutMinutes &&
        actualSetCounts.size === expected.setCountsBySelectionGroupId.size &&
        [...actualSetCounts].every(([groupId, setCount]) =>
          expected.setCountsBySelectionGroupId.get(groupId) === setCount);
    } catch {
      return false;
    }
  }

  chooseLongWorkoutAllocation(lockedSelectionGroupIds = new Set()) {
    if (this.state.activeWorkoutMinutes <= 30) {
      return {
        extraSetSelectionGroupIds: [],
        setCountsBySelectionGroupId: new Map(),
      };
    }

    const selected = (group) => {
      const exercise = this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ]);
      if (!exercise) {
        throw new Error(`${group.displayName} has no selected sequence.`);
      }
      return exercise;
    };
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    const rankedGroups = [...this.getSelectionGroups()].sort((left, right) => {
      const leftExercise = selected(left);
      const rightExercise = selected(right);
      return Number(rightExercise.muscularDemand === HARD_MUSCULAR_DEMAND) -
          Number(leftExercise.muscularDemand === HARD_MUSCULAR_DEMAND) ||
        Number(keptExerciseIds.has(rightExercise.id)) -
          Number(keptExerciseIds.has(leftExercise.id)) ||
        right.order - left.order;
    });
    const blockCostByGroup = new Map(rankedGroups.map((group) => [
      group.id,
      selected(group).sequenceBlocks.length,
    ]));
    const setCountsBySelectionGroupId = new Map(rankedGroups.map((group) => [
      group.id,
      1,
    ]));
    let remainingMinutes = this.state.activeWorkoutMinutes -
      [...blockCostByGroup.values()].reduce((total, cost) => total + cost, 0);

    for (const group of rankedGroups.filter(({ id }) =>
      lockedSelectionGroupIds.has(id))) {
      const lockedSetCount =
        this.state.activeSetCountsBySelectionGroupId[group.id] ?? 1;
      if (!Number.isInteger(lockedSetCount) || lockedSetCount < 1) {
        throw new Error(
          `The completed set allocation for ${group.displayName} is invalid.`,
        );
      }
      setCountsBySelectionGroupId.set(group.id, lockedSetCount);
      remainingMinutes -=
        (lockedSetCount - 1) * blockCostByGroup.get(group.id);
    }
    if (remainingMinutes < 0) {
      throw new Error("The selected mandatory sequences exceed the workout duration.");
    }

    const repeatableGroups = rankedGroups.filter(({ id }) =>
      !lockedSelectionGroupIds.has(id));
    const repeatableCosts = [...new Set(repeatableGroups.map((group) =>
      blockCostByGroup.get(group.id)))];
    const canFill = (minutes) => {
      const fillable = new Array(minutes + 1).fill(false);
      fillable[0] = true;
      for (let value = 1; value <= minutes; value += 1) {
        fillable[value] = repeatableCosts.some((cost) =>
          cost <= value && fillable[value - cost]);
      }
      return fillable[minutes];
    };

    while (remainingMinutes > 0) {
      const selectedGroup = [...repeatableGroups]
        .sort((left, right) =>
          setCountsBySelectionGroupId.get(left.id) -
            setCountsBySelectionGroupId.get(right.id))
        .find((group) => {
          const cost = blockCostByGroup.get(group.id);
          return cost <= remainingMinutes &&
            canFill(remainingMinutes - cost);
        });
      if (!selectedGroup) {
        throw new Error(
          "The selected sequence lengths cannot fill the workout duration.",
        );
      }
      setCountsBySelectionGroupId.set(
        selectedGroup.id,
        setCountsBySelectionGroupId.get(selectedGroup.id) + 1,
      );
      remainingMinutes -= blockCostByGroup.get(selectedGroup.id);
    }

    return {
      extraSetSelectionGroupIds: [...setCountsBySelectionGroupId]
        .filter(([, setCount]) => setCount > 1)
        .map(([groupId]) => groupId),
      setCountsBySelectionGroupId,
    };
  }
  rebalanceNewExercisesByMuscleBudget() {
    const groups = this.getSelectionGroups();
    const keptExerciseIds = new Set(this.state.lastKeptExerciseIds);
    if (groups.length === 0) {
      return;
    }
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();

    const seenLineups = new Set();
    for (let pass = 0; pass < MUSCLE_BUDGET_MAX_REBALANCE_PASSES; pass += 1) {
      const signature = groups.map((group) => this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ] ?? 0).join(",");
      if (seenLineups.has(signature)) {
        break;
      }
      seenLineups.add(signature);

      let changed = false;
      for (const group of groups) {
        const selectionStorageKey = this.getSelectionStorageKey(
          group.id,
          this.state.activeWorkoutModifiers,
        );
        const currentExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
        const currentExercise = this.exercisesById.get(currentExerciseId);
        if (!currentExercise || keptExerciseIds.has(currentExerciseId)) {
          continue;
        }

        const unavailableExerciseIds = new Set([
          ...groups
            .filter((candidateGroup) => candidateGroup.id !== group.id)
            .map((candidateGroup) => this.state.selectedExerciseIds[
              this.getSelectionStorageKey(
                candidateGroup.id,
                this.state.activeWorkoutModifiers,
              )
            ])
            .filter(Boolean),
          ...keptExerciseIds,
        ]);
        const unavailableMovementIds = new Set([
          ...[...unavailableExerciseIds]
            .map((exerciseId) => this.exercisesById.get(exerciseId))
            .filter(Boolean)
            .map(getSessionMovementId),
          getSessionMovementId(currentExercise),
        ]);
        const loadWithoutCurrent = this.state.activeWorkoutMinutes <= 30
          ? calculateMuscleLoadHalfUnits(groups
              .filter((candidateGroup) => candidateGroup.id !== group.id)
              .map((candidateGroup) => this.getSelectedExercise(candidateGroup)))
          : null;
        const current = loadWithoutCurrent
          ? this.evaluateSingleRoundMuscleBudgetCandidate(
              group,
              currentExercise,
              loadWithoutCurrent,
              selectionTimeUnixMilliseconds,
            )
          : this.evaluateMuscleBudgetCandidate(
              group,
              currentExercise,
              selectionTimeUnixMilliseconds,
            );
        const alternatives = this.exercises
          .filter((exercise) =>
            exercise.id !== currentExerciseId &&
            getAdjustedScoreHalfUnits(this.getSelectionScore(exercise), 0) >
              current.adjustedScoreHalfUnits &&
            !unavailableExerciseIds.has(exercise.id) &&
            !unavailableMovementIds.has(getSessionMovementId(exercise)) &&
            !this.state.nextWorkoutExcludedExerciseIds.includes(exercise.id) &&
            this.isWorkoutSelectionCandidate(
              exercise,
              group,
              this.state.activeWorkoutModifiers,
            ))
          .map((exercise) => loadWithoutCurrent
            ? this.evaluateSingleRoundMuscleBudgetCandidate(
                group,
                exercise,
                loadWithoutCurrent,
                selectionTimeUnixMilliseconds,
              )
            : this.evaluateMuscleBudgetCandidate(
                group,
                exercise,
                selectionTimeUnixMilliseconds,
              ))
          .sort((left, right) =>
            right.adjustedScoreHalfUnits - left.adjustedScoreHalfUnits ||
            right.realScore - left.realScore ||
            Number(right.isFreshHard) - Number(left.isFreshHard) ||
            Number(left.isRecoveringHard) - Number(right.isRecoveringHard) ||
            Number(left.isRecoveringModerate) - Number(right.isRecoveringModerate) ||
            Number(right.isKept) - Number(left.isKept) ||
            left.lastHardWorkUnixMilliseconds - right.lastHardWorkUnixMilliseconds ||
            Number(right.isMirrorPreferred) - Number(left.isMirrorPreferred) ||
            Number(right.isPrimary) - Number(left.isPrimary) ||
            right.canonicalCoverage - left.canonicalCoverage ||
            left.exerciseId - right.exerciseId);
        const bestAlternative = alternatives[0];
        if (!bestAlternative ||
            bestAlternative.adjustedScoreHalfUnits <= current.adjustedScoreHalfUnits) {
          continue;
        }

        this.state.selectedExerciseIds[selectionStorageKey] = bestAlternative.exerciseId;
        changed = true;
      }

      if (!changed) {
        break;
      }
    }
  }

  evaluateMuscleBudgetCandidate(group, candidate, selectionTimeUnixMilliseconds) {
    const selectionStorageKey = this.getSelectionStorageKey(
      group.id,
      this.state.activeWorkoutModifiers,
    );
    const previousExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
    this.state.selectedExerciseIds[selectionStorageKey] = candidate.id;
    try {
      const allocation = this.chooseLongWorkoutAllocation();
      const roots = new Map(this.getSelectionGroups().map((selectionGroup) => [
        selectionGroup.id,
        this.exercisesById.get(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(
            selectionGroup.id,
            this.state.activeWorkoutModifiers,
          )
        ]),
      ]));
      const rounds = createWorkoutSchedule(
        this.state.activeWorkoutMinutes,
        roots,
        allocation.setCountsBySelectionGroupId,
      );
      const scheduledExercises = rounds.map((round) => this.getSelectedExercise(round));
      const loadHalfUnits = calculateMuscleLoadHalfUnits(scheduledExercises);
      const candidateMuscleGroups = [...new Set(rounds
        .filter((round) => getSelectionKey(round) === group.id)
        .flatMap((round) => {
          const exercise = this.getSelectedExercise(round);
          return [
            exercise.primaryCanonicalGroup,
            ...(exercise.secondaryCanonicalGroups ?? []),
          ];
        }))];
      const temporaryDownvoteHalfUnits = getMuscleBudgetTemporaryDownvoteHalfUnits(
        loadHalfUnits,
        candidateMuscleGroups,
      );
      const realScore = this.getSelectionScore(candidate);
      const rotationStatus = getHardRotationStatus(
        candidate,
        group,
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      );
      const isRecoveringModerate = isModerateExerciseRecovering(
        candidate,
        this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      );
      return {
        exerciseId: candidate.id,
        realScore,
        adjustedScoreHalfUnits: getAdjustedScoreHalfUnits(
          realScore,
          temporaryDownvoteHalfUnits,
        ),
        isFreshHard: rotationStatus === HARD_ROTATION_STATUS.FreshHard,
        isRecoveringHard: rotationStatus === HARD_ROTATION_STATUS.RecoveringHard,
        isRecoveringModerate,
        isKept: this.state.lastKeptExerciseIds.includes(candidate.id),
        lastHardWorkUnixMilliseconds:
          rotationStatus === HARD_ROTATION_STATUS.FreshHard
            ? getLastHardWorkUnixMilliseconds(
                this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
                candidate.primaryCanonicalGroup,
              )
            : 0,
        isMirrorPreferred: isMirrorPreferred(
          candidate,
          this.state.activeWorkoutModifiers,
        ),
        isPrimary: isPrimaryForGroup(candidate, group),
        canonicalCoverage: getCanonicalCoverage(candidate, group),
      };
    } finally {
      this.state.selectedExerciseIds[selectionStorageKey] = previousExerciseId;
    }
  }

  evaluateSingleRoundMuscleBudgetCandidate(
    group,
    candidate,
    loadWithoutCandidate,
    selectionTimeUnixMilliseconds,
  ) {
    const realScore = this.getSelectionScore(candidate);
    const temporaryDownvoteHalfUnits =
      getTemporaryDownvoteHalfUnitsAfterAddingExercise(
        loadWithoutCandidate,
        candidate,
      );
    const rotationStatus = getHardRotationStatus(
      candidate,
      group,
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
      selectionTimeUnixMilliseconds,
    );
    const isRecoveringModerate = isModerateExerciseRecovering(
      candidate,
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
      selectionTimeUnixMilliseconds,
    );
    return {
      exerciseId: candidate.id,
      realScore,
      adjustedScoreHalfUnits: getAdjustedScoreHalfUnits(
        realScore,
        temporaryDownvoteHalfUnits,
      ),
      isFreshHard: rotationStatus === HARD_ROTATION_STATUS.FreshHard,
      isRecoveringHard: rotationStatus === HARD_ROTATION_STATUS.RecoveringHard,
      isRecoveringModerate,
      isKept: this.state.lastKeptExerciseIds.includes(candidate.id),
      lastHardWorkUnixMilliseconds:
        rotationStatus === HARD_ROTATION_STATUS.FreshHard
          ? getLastHardWorkUnixMilliseconds(
              this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
              candidate.primaryCanonicalGroup,
            )
          : 0,
      isMirrorPreferred: isMirrorPreferred(
        candidate,
        this.state.activeWorkoutModifiers,
      ),
      isPrimary: isPrimaryForGroup(candidate, group),
      canonicalCoverage: getCanonicalCoverage(candidate, group),
    };
  }

  setActiveLongWorkoutAllocation() {
    this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation());
  }

  applyLongWorkoutAllocation(allocation) {
    this.state.activeDirectionPartnerExerciseIds = {};
    this.state.activeFullSideRoundIds = [];
    this.state.activeExtraSetSelectionGroupIds = [...allocation.extraSetSelectionGroupIds];
    this.state.activeSetCountsBySelectionGroupId = Object.fromEntries(
      allocation.setCountsBySelectionGroupId,
    );
  }

  getEffectiveSetCounts() {
    return this.isLongWorkoutAllocationValid()
      ? new Map(Object.entries(this.state.activeSetCountsBySelectionGroupId))
      : this.chooseLongWorkoutAllocation().setCountsBySelectionGroupId;
  }

  normalizeOutcomes() {
    const activeGroups = new Map(this.getActiveGroups().map((group) =>
      [group.id, group]));
    for (const groupId of Object.keys(this.state.outcomes)) {
      if (!activeGroups.has(groupId)) {
        delete this.state.outcomes[groupId];
      }
    }
    for (const [groupId, outcome] of Object.entries(this.state.outcomes)) {
      if (outcome === "neutral" &&
          !this.isIntermediateSequenceBlock(activeGroups.get(groupId))) {
        this.state.outcomes[groupId] = "tick";
      }
    }
  }

  normalizeCompletionState() {
    const activeGroups = this.getActiveGroups();
    this.state.workoutCompleted = activeGroups.length > 0 &&
      activeGroups.every((group) => this.state.outcomes[group.id] !== undefined);
    if (!this.state.workoutCompleted) {
      this.state.completionAcknowledged = false;
    }
  }

  captureLegacyActiveProgress() {
    return {
      outcomes: { ...this.state.outcomes },
      selectedExerciseIds: { ...this.state.selectedExerciseIds },
      directionPartnerExerciseIds: {
        ...this.state.activeDirectionPartnerExerciseIds,
      },
      fullSideRoundIds: new Set(this.state.activeFullSideRoundIds),
      pendingMovementGroupId: this.state.pendingMovementGroupId,
      pendingMovementMillisecondsRemaining:
        this.state.pendingMovementMillisecondsRemaining,
      pendingMovementEndsAtUnixMilliseconds:
        this.state.pendingMovementEndsAtUnixMilliseconds,
      pendingMovementPausedByUser: this.state.pendingMovementPausedByUser,
      pendingRestGroupId: this.state.pendingRestGroupId,
      pendingRestEndsAtUnixMilliseconds: this.state.pendingRestEndsAtUnixMilliseconds,
      pendingRestKept: this.state.pendingRestKept,
    };
  }

  migrateLegacyActiveProgress(snapshot) {
    const rounds = [...this.getActiveGroups()].sort((left, right) =>
      left.order - right.order);
    this.state.outcomes = {};
    this.clearPendingMovement();
    this.clearPendingRest();

    for (const selectionGroup of this.getSelectionGroups()) {
      const sequenceRounds = rounds.filter((round) =>
        getSelectionKey(round) === selectionGroup.id);
      const oldOutcomes = Object.entries(snapshot.outcomes)
        .filter(([roundId]) =>
          this.resolveLegacySelectionKey(roundId) === selectionGroup.id);
      const decision = oldOutcomes
        .filter(([, outcome]) => outcome === "tick" || outcome === "x")
        .at(-1)?.[1];
      if (decision) {
        for (const round of sequenceRounds.slice(0, -1)) {
          this.state.outcomes[round.id] = "neutral";
        }
        this.state.outcomes[sequenceRounds.at(-1).id] = decision;
        continue;
      }
      for (const [legacyRoundId, outcome] of oldOutcomes
        .filter(([, value]) => value === "neutral")
        .sort((left, right) =>
          this.getLegacyRoundOrdinal(left[0]) - this.getLegacyRoundOrdinal(right[0]))) {
        if (outcome !== "neutral") {
          continue;
        }
        for (const round of this.resolveLegacyRepresentedRounds(
          snapshot,
          sequenceRounds,
          legacyRoundId,
        )) {
          if (!isFinalSequenceRound(round)) {
            this.state.outcomes[round.id] = "neutral";
          }
        }
      }
    }

    const legacyPendingRoundId =
      snapshot.pendingRestGroupId ?? snapshot.pendingMovementGroupId;
    const pendingSelectionKey = this.resolveLegacySelectionKey(
      legacyPendingRoundId,
    );
    if (!legacyPendingRoundId || !pendingSelectionKey) {
      return;
    }
    const pendingSequenceRounds = rounds.filter((round) =>
      getSelectionKey(round) === pendingSelectionKey);
    const representedRounds = this.resolveLegacyRepresentedRounds(
      snapshot,
      pendingSequenceRounds,
      legacyPendingRoundId,
    );
    if (representedRounds.length === 0) {
      return;
    }

    if (snapshot.pendingRestGroupId &&
        snapshot.pendingRestEndsAtUnixMilliseconds > 0) {
      const pendingRound = representedRounds.at(-1);
      this.markSequenceRoundsBeforePending(pendingSequenceRounds, pendingRound);
      this.state.pendingRestGroupId = pendingRound.id;
      this.state.pendingRestEndsAtUnixMilliseconds =
        snapshot.pendingRestEndsAtUnixMilliseconds;
      this.state.pendingRestKept = snapshot.pendingRestKept &&
        isFinalSequenceRound(pendingRound);
      return;
    }
    if (!snapshot.pendingMovementGroupId ||
        snapshot.pendingMovementMillisecondsRemaining <= 0) {
      return;
    }

    const mapped = this.mapLegacyMovementProgress(
      snapshot,
      legacyPendingRoundId,
      representedRounds.length,
    );
    const pendingRound = representedRounds[Math.max(
      0,
      Math.min(representedRounds.length - 1, mapped.representedRoundIndex),
    )];
    this.markSequenceRoundsBeforePending(pendingSequenceRounds, pendingRound);
    const maximum = getMovementCountdownDurationMs(pendingRound);
    this.state.pendingMovementGroupId = pendingRound.id;
    this.state.pendingMovementMillisecondsRemaining = Math.max(
      1,
      Math.min(maximum, mapped.remainingMilliseconds),
    );
    this.state.pendingMovementEndsAtUnixMilliseconds =
      representedRounds.length === 1 &&
        mapped.remainingMilliseconds ===
          snapshot.pendingMovementMillisecondsRemaining
        ? snapshot.pendingMovementEndsAtUnixMilliseconds
        : 0;
    this.state.pendingMovementPausedByUser = snapshot.pendingMovementPausedByUser;
  }

  resolveLegacyRepresentedRounds(snapshot, sequenceRounds, legacyRoundId) {
    const selectionKey = this.resolveLegacySelectionKey(legacyRoundId);
    if (!selectionKey) {
      return [];
    }
    const setNumber = this.getLegacySetNumber(legacyRoundId);
    const setRounds = sequenceRounds
      .filter((round) => (round.setNumber ?? 1) === setNumber)
      .sort((left, right) =>
        (left.sequenceBlockIndex ?? 0) - (right.sequenceBlockIndex ?? 0));
    if (setRounds.length === 0) {
      return [];
    }
    const memberExerciseId = this.resolveLegacyMemberExerciseId(
      snapshot,
      selectionKey,
      legacyRoundId,
    );
    const represented = memberExerciseId > 0
      ? setRounds.filter((round) => this.getSelectedExercise(round).id ===
        memberExerciseId)
      : [];
    return represented.length > 0 ? represented : setRounds;
  }

  resolveLegacyMemberExerciseId(snapshot, selectionKey, legacyRoundId) {
    if (legacyRoundId.startsWith(`${selectionKey}.direction`) &&
        Number.isInteger(snapshot.directionPartnerExerciseIds[selectionKey])) {
      return snapshot.directionPartnerExerciseIds[selectionKey];
    }
    const activeStorageKey = this.getSelectionStorageKey(
      selectionKey,
      this.state.activeWorkoutModifiers,
    );
    if (Number.isInteger(snapshot.selectedExerciseIds[activeStorageKey])) {
      return snapshot.selectedExerciseIds[activeStorageKey];
    }
    return Object.entries(snapshot.selectedExerciseIds)
      .find(([storageKey]) => {
        const parsed = this.parseSelectionStorageKey(storageKey);
        return parsed.selectionGroupId === selectionKey &&
          normalizeWorkoutModifiers(parsed.modifiers) ===
            this.state.activeWorkoutModifiers;
      })?.[1] ?? 0;
  }

  mapLegacyMovementProgress(snapshot, legacyRoundId, representedRoundCount) {
    const remaining = snapshot.pendingMovementMillisecondsRemaining;
    if (representedRoundCount < 2) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    const selectionKey = this.resolveLegacySelectionKey(legacyRoundId);
    const memberExerciseId = selectionKey
      ? this.resolveLegacyMemberExerciseId(snapshot, selectionKey, legacyRoundId)
      : 0;
    const member = this.exercisesById.get(memberExerciseId);
    const usedTimedPair = member &&
      ([
        "ScreenLeftThenRight",
        "ScreenRightThenLeft",
        "ScreenLeftLeadThenRightLead",
        "ScreenRightLeadThenLeftLead",
      ].includes(member.sideSequence) || member.directionSequence !== "None");
    if (!usedTimedPair) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    if (snapshot.fullSideRoundIds.has(legacyRoundId)) {
      if (remaining > 60_000) {
        return {
          representedRoundIndex: 0,
          remainingMilliseconds: remaining - 60_000,
        };
      }
      return {
        representedRoundIndex: 1,
        remainingMilliseconds: remaining > 45_000 ? 45_000 : remaining,
      };
    }
    if (remaining > 25_000) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    return {
      representedRoundIndex: 1,
      remainingMilliseconds: remaining > 20_000 ? 45_000 : remaining + 25_000,
    };
  }

  markSequenceRoundsBeforePending(sequenceRounds, pendingRound) {
    for (const round of sequenceRounds) {
      if (round.id === pendingRound.id) {
        break;
      }
      this.state.outcomes[round.id] ??= "neutral";
    }
  }

  getLegacySetNumber(roundId) {
    for (const marker of [".set", ".direction"]) {
      const index = roundId.lastIndexOf(marker);
      const value = index >= 0 ? Number(roundId.slice(index + marker.length)) : 0;
      if (Number.isInteger(value) && value > 0) {
        return value;
      }
    }
    return 1;
  }

  getLegacyRoundOrdinal(roundId) {
    return (this.getLegacySetNumber(roundId) - 1) * 2 +
      (roundId.includes(".direction") ? 1 : 0);
  }

  resolveLegacySelectionKey(roundId) {
    if (typeof roundId !== "string" || !roundId) {
      return null;
    }
    return [...ALL_GROUPS.keys()]
      .filter((groupId) => roundId === groupId ||
        roundId.startsWith(`${groupId}.`))
      .sort((left, right) => right.length - left.length)[0] ?? null;
  }

  normalizePendingRest() {
    try {
      if (this.getValidPendingRestGroup()) {
        return;
      }
    } catch {
      // An obsolete schedule cannot identify a valid current checkpoint.
    }
    this.clearPendingRest();
  }

  normalizePendingMovement() {
    if (!this.getPendingMovementGroup()) {
      this.clearPendingMovement();
      return;
    }

    // A valid rest means this movement already completed. Persisted phases
    // are mutually exclusive, and rest therefore wins over movement.
    const pendingRest = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    if (pendingRest &&
        this.state.pendingRestEndsAtUnixMilliseconds > 0 &&
        this.state.outcomes[pendingRest.id] === undefined) {
      this.clearPendingMovement();
    }
  }

  isSavedSelectionValid(exercise, group, modifiers) {
    if (this.isWorkoutSelectionCandidate(exercise, group, modifiers)) {
      return true;
    }
    if (
      !this.pendingRestMatchesSelectionGroup(getSelectionKey(group)) ||
      this.getSequenceRoot(exercise).id !== exercise.id
    ) {
      return false;
    }
    return (this.state.activeWorkoutMinutes > 30 ||
        exercise.sequenceBlocks.length === 1) &&
      this.getSequenceExercises(exercise).every((member) =>
        this.isCompatibleWithModifiers(member, modifiers) &&
        this.isAssignedToGroup(member, group));
  }

  isSequenceOverrideValid(exercise, group, modifiers) {
    const blockIndex = group.sequenceBlockIndex ?? -1;
    const blockCount = group.sequenceBlockCount ?? 0;
    if (blockIndex < 0 || blockIndex >= blockCount) {
      return false;
    }
    const root = this.exercisesById.get(
      this.state.selectedExerciseIds[this.getSelectionStorageKey(
        getSelectionKey(group),
        modifiers,
      )],
    );
    const block = root?.sequenceBlocks?.[blockIndex];
    return Boolean(root &&
      root.sequenceBlocks.length === blockCount &&
      block?.exerciseId === exercise.id &&
      (block.sideCue ?? "None") === (group.sequenceSideCue ?? "None") &&
      (block.directionCue ?? "None") ===
        (group.sequenceDirectionCue ?? "None") &&
      (block.mirrorMedia === true) === (group.mirrorSequenceMedia === true) &&
      (block.mediaSegment ?? "Full") ===
        (group.sequenceMediaSegment ?? "Full") &&
      this.isWorkoutSelectionCandidate(root, group, modifiers));
  }

  pendingRestMatchesSelectionGroup(selectionGroupId) {
    if (!this.state.pendingRestGroupId) {
      return false;
    }
    const roundMatch = /^(.*)\.set[1-9]\d*\.block[1-9]\d*$/.exec(
      this.state.pendingRestGroupId,
    );
    return (roundMatch?.[1] ?? this.state.pendingRestGroupId) === selectionGroupId;
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
    this.state.activeSetCountsBySelectionGroupId = {};
    this.state.activeDirectionPartnerExerciseIds = {};
    this.state.activeFullSideRoundIds = [];
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingMovement();
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

  const usesLegacyTimedSides = [
    "ScreenLeftThenRight",
    "ScreenRightThenLeft",
    "ScreenLeftLeadThenRightLead",
    "ScreenRightLeadThenLeftLead",
  ].includes(currentExercise.sideSequence);
  const timedSideNormalization =
    usesLegacyTimedSides &&
    previousName.startsWith(ALTERNATING_PREFIX) &&
    previousName.slice(ALTERNATING_PREFIX.length) === currentExercise.name;
  const continuousAlternationNormalization =
    CONTINUOUS_ALTERNATION_NORMALIZATION_IDS.has(exerciseId) &&
    !usesLegacyTimedSides &&
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
