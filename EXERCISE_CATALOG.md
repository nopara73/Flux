# Flux exercise catalog

Flux contains 429 human-demonstrated standing movements. Exercises are chosen
for movement quality first and assigned to canonical muscle groups afterward.
Each exercise has one primary scheduling group and zero or more meaningful
secondary groups. Full-body movements remain eligible wherever they place real
demand. Primary ownership is preferred when score and keep history tie, while
meaningful secondary associations remain real scheduling eligibility.

The 30 canonical leaves roll up explicitly into the app's 3, 5, 7, 10, 15, 20,
and 30-group resolutions. Every resolution covers every leaf exactly once and
schedules its declared mass hierarchy from smallest to largest. A selectable
exercise must train at least half of that bucket's canonical leaves. For every
modifier pair, every bucket has at least five choices under each real UI state.
A binary/binary pair has four states; a pair involving Mirror has six because
Mirror has off, compact, and tall equipment states. Restrictive modifiers relax
when off. With a compact mirror, upper-body `MirrorOnly` movements are admitted
but full-body `MirrorOnly` movements remain excluded; a tall mirror admits both.
In either mirror-equipped state, only compatible `MirrorOnly` and
`BenefitsGreatly` exercises count toward the five-exercise pairwise relevance
floor. `Agnostic` exercises remain selectable but cannot make Mirror coverage
appear complete. In a Mirror-off state, every normally selectable exercise
except `MirrorOnly` counts. Separate
quadratic materiality checks ensure restrictive modifiers remove, and Mirror
categories 1-2 supply, a meaningful, anatomically broad candidate set alone and
with each paired modifier. Primary ownership is a preference after
score/keep priority, never a reason to discard a truthful secondary association
or admit a weak quota-filler.

At session construction, kept exercises remain fixed. The remaining choices use
a soft per-muscle workload budget of 5: each scheduled primary association counts
1 and each distinct secondary counts 0.5. Every excess 0.5 becomes a 0.5
temporary downvote for candidates training that muscle. The adjustment is deterministic,
is never persisted, and replaces the current candidate only on a strictly higher
adjusted score. Timed unilateral phases count once; linked direction exercises
and actual repeated rounds count as separate scheduled work.

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
- require no wall, chair, floor exercise, prop, partner, or equipment except a
  physical mirror when explicitly categorized and gated by the Mirror modifier;
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
45-second phase. A unilateral movement
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

`tools/ExerciseMirrorRelationships.psd1` is the exhaustive mirror audit. Each
exercise is exactly one of `MirrorOnly`, `BenefitsGreatly`, or `Agnostic`.
`MirrorOnly` requires `equipment: "Mirror"`; the other two require
`equipment: "None"`. Every category 1–2 exercise also declares the minimum
useful coverage: `UpperBody` or `FullBody`; `Agnostic` requires `None`. The
catalog independently requires at least five exercises in each of the five
relationship/coverage cells. A compact mirror provides preference to
upper-body `BenefitsGreatly` movements, while full-body `BenefitsGreatly`
movements stay selectable without preference until a tall mirror is selected.
This equipment classification never controls
media mirroring; only the established timed-side presentation protocol may flip
the second playback phase.

The current 58 `BenefitsGreatly` assignments are an audited result, not a quota,
target, or ceiling. Each entry must belong to one of the manifest's narrow
audited criteria, and ordinary optional form checking does not qualify. Changes
to the count must follow the exercise semantics, never a coverage requirement.

For pairwise catalog validation, a Mirror-on muscle bucket must have five
qualifying category 1–2 exercises after applying the paired modifier state.
`Agnostic` exercises remain runtime candidates but do not satisfy this Mirror
coverage floor. Mirror-off states count every normally selectable exercise
except `MirrorOnly`. This pairwise floor is separate from the global five-cell
mirror-category floor.

## Media quality

Every entry has an offline 256 × 256 H.264 MP4 with its audio stripped. The
`silent` catalog field separately records whether performing the movement is
naturally quiet, allowing the default-on Silence modifier to exclude impact
movements. All 429 included
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
