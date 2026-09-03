# Hard-floor compatibility audit

Hard Floor is the second workout-context modifier and is enabled by default. It is one
combined surface contract: the floor is both rigid and slippery. Slipperiness
is not a separate modifier or optional interpretation. Its two UI states are:

- **Hard floor ON:** only exercises reviewed for both hard-floor ergonomics and
  low-traction execution are selectable.
- **Hard floor OFF:** the user has a stable soft floor, so both `Compatible` and
  `Incompatible` exercises are selectable.

"Soft" means an ordinary stable padded exercise surface or carpet. It does not
mean a mattress, unstable foam, or deep pile that compromises footing.

## Review standard

Every catalog ID must appear exactly once in
`tools/ExerciseHardFloorCompatibility.psd1`. A new or reactivated exercise is
`Unreviewed` until that explicit decision is added; generation, Android tests,
and web tests reject an incomplete partition.

An exercise is `Incompatible` when its demonstrated execution makes a rigid,
slippery floor meaningfully less ergonomic or requires dependable traction
through one of these audited mechanisms:

- concentrated heel or forefoot loading, including sustained or repeated
  calf-raise and tiptoe work;
- repeated jumping or landing;
- running or rapid foot impact;
- deliberate stomping;
- lateral travel or weight-bearing direction changes;
- a traction-loaded wide, split, lunge, or staggered stance;
- pivoting strikes, stance-driven boxing movements, or forceful kicks; or
- wall or balance loading that depends on reliable floor traction.

Ordinary planted standing, controlled straight-line stepping, vertical
squatting, static single-leg balance, mobility, and upper-body work remains
`Compatible` unless the actual demonstration meets one of those mechanisms.
The slippery-floor review does not assume a wet, oily, or otherwise acutely
unsafe surface on which nobody should exercise. Classification follows the
final packaged movement, not its name or a coverage target. Every mandatory
sequence must use one consistent floor classification across all of its blocks.

## Current result

- `Compatible`: 309 exercises
- `Incompatible`: 199 exercises
- `Unreviewed`: 0 exercises

Catalog revision 53 rebuilds cached placements for the 97 reclassified
exercise IDs only when the saved profile has Hard Floor enabled. Soft Floor
placements remain valid. Scores, keeps, phase feedback, history, and recovery
state are preserved on both platforms. Android SQLite schema 75 applies the
same catalog refresh without deleting stored scores.

Catalog revision 59 additionally corrects exercise 565 from a supposed
heel-down mini squat to the demonstrated mini-squat calf raises. Its cached
placement is rebuilt while its user score remains intact; Android SQLite
schema 80 refreshes the corrected identity, anatomy, demand, and floor verdict.

These counts are audit results, not quotas. Pairwise availability and
materiality are validated separately against the real Hard Floor, Insect,
Silence, and Mirror UI states. Of the 30 wall-required movements, ten are
incompatible: the repeated wall calf raise and wall tibialis raise under the
same concentrated forefoot/heel rule as equivalent unsupported work; the wall
soleus and calf stretches because their split stance requires dependable
traction; fingertip wall push-ups because a low-traction floor can let the feet
slide under pressing force; and all five sole-contact movements because their
support, balance, or traction depends on reliable foot placement. The remaining
20 are compatible. Because Wall off
excludes them and Wall is not a pairwise quota dimension, they do not hide or
alter floor-coverage accounting. The validation remains quadratic in the number of
quota-bearing modifiers; it does not require every state in the full modifier
power set.

In addition to ordinary pairwise viability, the three broad 3-minute regions
must retain at least five session movements from each exact floor
category—`Compatible` and `Incompatible`—with Insect off/on, Silence off/on,
and Mirror off. Every finer 5- through 30-minute bucket must retain at least one
from each category. Turning Hard Floor off still admits both categories at
runtime; the category-specific audit exists so the larger combined pool cannot
conceal a missing soft-floor-only or hard-floor-suitable side of a pair. The
current Android and web validators report zero deficits and fail CI on any
regression. The diagnostic
[`current deficit ledger`](catalog-audit/modifier_coverage_deficits_current.json)
cannot authorize a nonzero result. The
[`2026-08-29 deficit report`](catalog-audit/modifier_coverage_deficits_2026-08-29.json)
is intentionally retained as the pre-slipperiness baseline.
