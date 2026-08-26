# Flux exercise catalog

Flux contains 448 human-demonstrated standing movements. Exercises are chosen
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

At session construction, keeps remain saved but are preferred contextually. The
remaining choices use a soft per-muscle workload budget of 5. Demand-`0` work
does not consume it. Demand-`1` work counts 0.5 for its primary muscle and zero
for secondaries. Demand-`2` work counts 1 for its primary and 0.5 for each
distinct secondary. Every excess 0.5 becomes a 0.5 temporary downvote while
comparing candidates associated with that muscle. The adjustment is
deterministic, is never persisted, and replaces the current candidate only on a
strictly higher adjusted score. Each distinct exercise identity counts once per
sequence set, so the two sides or directions of one identity do not double-count
bilateral muscle work. Different linked exercise identities count separately,
and an actual repeated set counts again.

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
- be a genuinely simultaneous/symmetric movement, naturally alternate in one
  uninterrupted loop, or belong to one complete reviewed atomic sequence that
  includes every required side and direction as full 45-second blocks;
- have one or more muscle-group assignments and its own bundled H.264 MP4.

Each entry is explicitly a repetition or hold and belongs to exactly one atomic
sequence root. A genuinely simultaneous bilateral movement or a natural
alternating loop remains one 45-second block. Fixed-side or fixed-stance work
uses two complete consecutive side blocks. Cyclic work that is incomplete in
only one direction uses consecutive direction blocks. Combining those
requirements can produce four blocks; a useful alternating integration block
is included only when it adds a real coordination effect rather than awkward
filler. Distinct established movements may also form one reviewed sequence when
they are valuable only as a complete set. Hold MP4s loop only during preview;
during active work they play once and freeze on the curated target from
`tools/HoldExerciseFrames.psd1`.

Every block is 45 seconds with a 15-second rest. Sequence blocks are always
adjacent and share one Keep/Reject decision after the final block and final set.
There is no explicit short-workout exclusion. Exact block capacity determines
what fits: same-primary extra blocks consume capacity without claiming another
muscle slot, while blocks with genuinely different primary workout groups may
collectively fill those slots. This can place a multi-block sequence in a
sub-30-minute workout and may shift surrounding muscle-group order, but never
the sequence's internal order.

`tools/ExerciseSequences.psd1` exhaustively partitions the retained inventory:
every record is either in exactly one mandatory sequence or is explicitly
standalone. Generation rejects implicit defaults, overlaps, orphans, and hidden
members scheduled as roots. The current reviewed inventory has 393 schedulable
roots: 243 one-block, 107 two-block, 26 three-block, 16 four-block, and one
five-block root. Forty-seven roots couple multiple named records, including
17 exact alternating integrations added after the side/integration audit.
These numbers are audit outputs rather than coverage targets.

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

The current 71 `BenefitsGreatly` assignments are an audited result, not a quota,
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
movements. All 448 included
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
- `tools/ExerciseAlternatingSequences.psd1`
- `tools/ExerciseDirectionSequences.psd1`
- `tools/ExerciseSequences.psd1`
- `tools/ReviewedContinuousExercises.psd1`

`tools/Test-ExerciseVideos.ps1` verifies the complete media inventory and
compares every hold’s decoded final frame with its reviewed target. Counts are
reported in [DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md). External clips are
used for this private personal build and are not a commercial clearance record.

Practice provenance and the deliberately constrained gaps in the supplementary
movement-practices DAG are reported in
[docs/PRACTICE_COVERAGE_AUDIT.md](docs/PRACTICE_COVERAGE_AUDIT.md).
