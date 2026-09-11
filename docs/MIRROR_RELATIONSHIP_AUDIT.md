# Mirror relationship audit

The 540 retained exercises are reviewed against physical mirror equipment.
Timed side/direction demonstrations have a separate media contract.

Mirror has three actual equipment states: `None`, `Compact` (upper-body view),
and `Tall` (full-body view). Each exercise declares a relationship and its
minimum useful mirror coverage.

| Relationship | Coverage | Runtime behavior | Count |
| --- | --- | --- | ---: |
| `MirrorOnly` | `UpperBody` | Requires compact or tall | 0 |
| `MirrorOnly` | `FullBody` | Requires tall | 0 |
| `BenefitsGreatly` | `UpperBody` | Preferred with compact or tall | 40 |
| `BenefitsGreatly` | `FullBody` | Preferred with tall; selectable without preference with compact | 59 |
| `Agnostic` | `None` | Unaffected | 441 |

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

The 99 assignments and their coverage split are explicitly reviewed results,
never targets or a means of filling a coverage shortage. The criterion keys
remain mandatory even when their lists are empty.

## Availability and materiality

The 2026-09-11 completion policy requires a real selectable movement in each
applicable broad or fine group, plus complete atomic workout lineups. Agnostic
movements count while Mirror is available. Compact/tall equipment restrictions,
the established trained-muscle breadth rule and all physical predicates apply.

Percentage-based materiality remains a diagnostic inventory, not a release
gate. No extra mirror-relevant exercise is required simply to reach a count or
percentage. Compact still prefers genuinely useful upper-body mirror work;
tall includes eligible upper- and full-body work. Membership and preference
behavior remain truthful. The audit remains quadratic in logical modifiers,
and its results are visible in the
[current ledger](catalog-audit/modifier_coverage_deficits_current.json).

Mirror equipment never transforms media. Timed side mirroring and explicitly
reviewed direction assets follow the exercise's actual sequence independently.
