# Mirror relationship audit

The 534 retained exercises are reviewed against physical mirror equipment.
Timed side/direction demonstrations have a separate media contract.

Mirror has three actual equipment states: `None`, `Compact` (upper-body view),
and `Tall` (full-body view). Each exercise declares a relationship and its
minimum useful mirror coverage.

| Relationship | Coverage | Runtime behavior | Count |
| --- | --- | --- | ---: |
| `MirrorOnly` | `UpperBody` | Requires compact or tall | 0 |
| `MirrorOnly` | `FullBody` | Requires tall | 0 |
| `BenefitsGreatly` | `UpperBody` | Preferred with compact or tall | 39 |
| `BenefitsGreatly` | `FullBody` | Preferred with tall; selectable without preference with compact | 58 |
| `Agnostic` | `None` | Unaffected | 437 |

These are audit results. No relationship/coverage cell has a population minimum,
and any permitted BenefitsGreatly criterion may be empty. The exhaustive lists
in `tools/ExerciseMirrorRelationships.psd1` must still partition the catalog
exactly, without missing or duplicate IDs, invalid coverage, contradictory
equipment, or disagreement between linked directions.

## Semantic criteria

A reflection must be essential to the actual exercise for `MirrorOnly`.
Ordinary posing, imitation, and optional form checking do not establish that
requirement. No current demonstration supports a MirrorOnly classification.

Continuous self-view must substantially change execution under one of the six
narrow audited criteria for `BenefitsGreatly`:

1. Technique-sensitive martial arts.
2. Dance or alignment-sensitive poses.
3. Complex single-leg alignment.
4. Live plane, path, or symmetry correction.
5. Gaze-stability feedback.
6. Subtle pelvic-position feedback.

The 97 assignments and their coverage split are explicitly reviewed results,
never targets or a means of filling a coverage shortage. The criterion keys
remain mandatory even when their lists are empty.

## Availability and materiality

Under the user-authorized 2026-09-10 policy, pairwise coverage counts every
actually selectable session movement, including Agnostic movements while
Mirror is available. Each broad 3-minute region requires five distinct
movements; each finer 5- through 30-minute bucket requires one. Compact/tall
equipment restrictions and all other profile predicates still apply.

Materiality is independent and unchanged. Compact must actually prefer a
meaningful, anatomically broad upper-body set; full-body BenefitsGreatly
exercises receive no preference credit there. Tall includes the compatible
upper- and full-body set. These checks also run when the other modifier in a
pair is already enabled. The current catalog has zero materiality deficits.

The validation remains quadratic in the quota-bearing logical modifiers.
Wall has its separate global inventory floor and is outside pairwise quotas.
All enforceable coverage and distinct-lineup deficits must be zero before
release; the current unresolved counts are in the
[current deficit ledger](catalog-audit/modifier_coverage_deficits_current.json).

Mirror equipment never transforms media. Timed side mirroring and explicitly
reviewed direction assets follow the exercise's actual sequence independently.
