# Flux exercise catalog

Flux contains 418 human-demonstrated standing movements. Exercises are chosen
for movement quality first and assigned to canonical muscle groups afterward.
Each exercise has one primary scheduling group and zero or more meaningful
secondary groups. Full-body movements remain eligible wherever they place real
demand. Primary ownership is preferred when score and keep history tie, while
meaningful secondary associations remain real scheduling eligibility.

The 30 canonical leaves roll up explicitly into the app's 3, 5, 7, 10, 15, 20,
and 30-group resolutions. Every resolution covers every leaf exactly once and
schedules its declared mass hierarchy from smallest to largest. A selectable
exercise must train at least half of that bucket's canonical leaves. For every
modifier pair, every bucket has at least five choices under each real UI state:
on/on, on/off, off/on, and off/off. OFF relaxes a predicate rather than requiring
an incompatible exercise. Separate quadratic materiality checks ensure each
modifier removes a meaningful, anatomically broad candidate set both alone and
with its paired modifier enabled. Primary ownership is a preference after
score/keep priority, never a reason to discard a truthful secondary association
or admit a weak quota-filler.

The catalog mixes low-impact compound strength and conditioning with standing
stretching, dynamic balance, active range of motion, rehabilitation, Pilates,
yoga, tai chi, qigong, boxing, dance, and martial arts.

## Editorial rules

Every entry must:

- be a complete named movement or posture, never a tempo/range/side suffix used
  to inflate the count;
- keep all ground contact at the feet;
- remain practical in ordinary shoes or barefoot;
- fit inside a 2 m × 2 m space;
- require no wall, chair, floor exercise, prop, partner, or equipment;
- be naturally quiet whenever the default-on Silence modifier is enabled;
  established impact movements may be eligible only after Silence is explicitly
  disabled;
- be bilateral/symmetric, naturally alternate in one uninterrupted loop, or use
  an explicit reviewed screen-side order for the 20 / 5 / 20 timed-side flow;
- have one or more muscle-group assignments and its own bundled H.264 MP4.

Each entry is explicitly a repetition or hold and a continuous, alternating, or
timed-unilateral sequence. Naturally alternating sides, breath cycles,
raise-and-lower actions, flows, and repeated contractions remain repetitions.
An alternating sequence switches its working side inside one uninterrupted
45-second phase and is labeled `ALTERNATING` before Start. A unilateral movement
runs for 20 seconds on its demonstrated side, pauses for a 5-second side
change, then replays the same human demonstration mirrored for 20 seconds; it is
labeled `UNILATERAL` before Start.
Hold MP4s loop only during preview; during an active side they play once and
freeze on the curated target from `tools/HoldExerciseFrames.psd1`.

Opposite directions are separate, plainly named catalog exercises with separate
demonstrations and scores. Reciprocal `directionPartnerExerciseId` links let a
long workout add the missing direction before lengthening a two-sided timer.
Direction is never represented as another 20 / 5 / 20 phase inside one exercise,
so an exercise can be two-sided without becoming a four-phase drill.

`tools/ExerciseCanonicalGroups.psd1` is the stable primary/secondary assignment
source, and `tools/CanonicalMuscleGroups.psd1` defines the canonical identities.
Replacement definitions repeat those fields for reviewability, and generation
fails if the two manifests drift. The runtime catalog emits the canonical map
directly. The historical ten source
families remain generator-only so stable exercise IDs and reviewed media paths
do not change. The generator rejects missing, duplicate, or unknown assignments;
modifier-profile coverage or distinct-lineup deficits; duplicate names;
synthetic modifier suffixes; missing motion profiles; non-human media; and
constraint violations. It also requires an explicit reviewed timed-side or continuous
decision for every retained movement, so new entries cannot silently default
to the wrong playback behavior.

## Media quality

Every entry has an offline 256 × 256 H.264 MP4 with its audio stripped. The
`silent` catalog field separately records whether performing the movement is
naturally quiet, allowing the default-on Silence modifier to exclude impact
movements. All 418 included
demonstrations show an actual person. Retained media consists of visually
reviewed human footage, semantically identical copies of reviewed footage, or
an exact deterministic transform when the transformed footage demonstrates the
named movement accurately. Placeholder, synthetic, schematic, anatomical, and
3D media is excluded.

The retained inventory and media mappings live in:

- `tools/VerifiedExerciseDemos.psd1`
- `tools/ExternalExerciseMedia.psd1`
- `tools/ExactExerciseMediaCopies.psd1`
- `tools/ExactExerciseMediaTransforms.psd1`
- `tools/HoldExerciseFrames.psd1`
- `tools/ExerciseSideSequences.psd1`
- `tools/ExerciseDirectionPartners.psd1`
- `tools/ReviewedContinuousExercises.psd1`

`tools/Test-ExerciseVideos.ps1` verifies the complete media inventory and
compares every hold’s decoded final frame with its reviewed target. Counts are
reported in [DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md). External clips are
used for this private personal build and are not a commercial clearance record.

Practice provenance and the deliberately constrained gaps in the supplementary
movement-practices DAG are reported in
[docs/PRACTICE_COVERAGE_AUDIT.md](docs/PRACTICE_COVERAGE_AUDIT.md).
