# Flux exercise catalog

Flux contains 328 human-demonstrated standing movements. Exercises are chosen
for movement quality first and assigned to canonical muscle groups afterward.
Each exercise has one primary scheduling group and zero or more meaningful
secondary groups. Full-body movements remain eligible wherever they place real
demand; assignment is a practical workout index, not an anatomical claim.

The 30 canonical leaves roll up explicitly into the app's 3, 5, 7, 10, 15, 20,
and 30-group resolutions. Every resolution covers every leaf exactly once and
keeps its declared descending mass order. A selectable exercise must own its
bucket through its primary assignment and train at least half of that bucket's
canonical leaves. Every bucket at every supported resolution has at least 10
such choices; the 30-group resolution therefore also maintains at least 10
primary representatives in every canonical leaf.

The catalog mixes low-impact compound strength and conditioning with standing
stretching, dynamic balance, active range of motion, rehabilitation, Pilates,
yoga, tai chi, qigong, boxing, dance, and martial arts.

## Editorial rules

Every entry must:

- be a complete named movement or posture, never a tempo/range/side suffix used
  to inflate the count;
- keep all ground contact at the feet;
- remain practical in ordinary shoes or barefoot;
- fit inside a 3 m × 3 m space;
- require no wall, chair, floor exercise, prop, partner, or equipment;
- avoid jumping, stomping, clapping, vocalization, and other
  neighbor-disturbing actions;
- be bilateral/symmetric, or visibly complete left and right repetitions inside
  one uninterrupted loop;
- have one or more muscle-group assignments and its own bundled H.264 MP4.

Each entry is explicitly a repetition or hold. A hold is one bilateral or
symmetric position sustained for the full timer. Alternating sides, breath
cycles, raise-and-lower actions, flows, and repeated contractions remain
repetitions even when their traditional name contains “hold.” Hold MP4s loop
only during preview; during the timer they play once and freeze on the curated
target from `tools/HoldExerciseFrames.psd1`.

`tools/ExerciseCanonicalGroups.psd1` is the stable primary/secondary assignment
source, and `tools/CanonicalMuscleGroups.psd1` defines the canonical identities.
The runtime catalog emits those assignments directly. The historical ten source
families remain generator-only so stable exercise IDs and reviewed media paths
do not change. The generator rejects missing, duplicate, or unknown assignments;
canonical leaves with fewer than 10 primaries; duplicate names; synthetic
modifier suffixes; missing motion profiles; non-human media; and constraint
violations.

## Media quality

Every entry has an offline 256 × 256 silent H.264 MP4. All 328 included
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

`tools/Test-ExerciseVideos.ps1` verifies the complete media inventory and
compares every hold’s decoded final frame with its reviewed target. Counts are
reported in [DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md). External clips are
used for this private personal build and are not a commercial clearance record.

Practice provenance and the deliberately constrained gaps in the supplementary
movement-practices DAG are reported in
[docs/PRACTICE_COVERAGE_AUDIT.md](docs/PRACTICE_COVERAGE_AUDIT.md).
