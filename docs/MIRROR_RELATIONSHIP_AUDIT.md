# Mirror relationship audit

The 429 retained exercises are reviewed against physical mirror equipment, not
against whether a timed second-side demonstration is horizontally flipped.

Mirror has three equipment states:

- `None`: no physical mirror is available;
- `Compact`: roughly the upper body is visible;
- `Tall`: the full body is visible.

Each exercise has both a relationship and a minimum useful coverage:

| Relationship | Coverage | Runtime behavior | Count |
| --- | --- | --- | ---: |
| `MirrorOnly` | `UpperBody` | Requires compact or tall | 5 |
| `MirrorOnly` | `FullBody` | Requires tall | 5 |
| `BenefitsGreatly` | `UpperBody` | Preferred with compact or tall | 22 |
| `BenefitsGreatly` | `FullBody` | Preferred with tall; selectable without preference with compact | 36 |
| `Agnostic` | `None` | Unaffected | 361 |

The first four cells declare `UpperBody` or `FullBody`; `Agnostic` must declare
`None`. `MirrorOnly` requires `equipment: "Mirror"`; the other relationships
require `equipment: "None"`.

## Mirror-only inventory

The upper-body set is:

- `Mirror-Guided Eyebrow Raise`
- `Mirror-Guided Firm Eye Closure`
- `Mirror-Guided Straight Jaw Opening`
- `Mirror-Guided Lip Pucker`
- `Mirror-Guided Symmetric Smile`

The full-body set is:

- `Mirror-Guided Bodyweight Squat`
- `Mirror-Guided Lateral Weight Shift`
- `Mirror-Guided Single-Leg Pelvic Control`
- `Mirror-Guided Bent-Knee Front-to-Back Leg Swing`
- `Mirror-Guided Bent-Knee Leg Swing with Pause`

These are established visual-feedback exercises with exact human
demonstrations. The facial movements come from a facial-neuromuscular
telerehabilitation protocol with mirror feedback. Straight jaw opening follows
the established live-midline protocol used to detect lateral deviation. The
full-body set uses published or clinical visual-feedback squat, weight-shift,
pelvic-control, and dynamic-leg-control demonstrations. A generic movement was
not renamed merely because a mirror could provide ordinary form checking.

## Benefits-greatly guardrails

Continuous live self-view must substantially change execution under exactly one
of four audited reasons:

1. technique-sensitive martial arts;
2. dance or alignment-sensitive poses;
3. complex single-leg alignment;
4. live plane, path, or symmetry correction.

Merely seeing oneself, comparing oneself with the demonstration, or receiving
ordinary optional form feedback does not qualify. The current 58 assignments
are an audit result, never a quota, target, cap, or reason to promote or demote
an exercise. Their coverage split is independently reviewed and must exactly
partition the same 58 IDs.

## Catalog guarantees

Every one of the five relationship/coverage cells must contain at least five
reviewed exercises. This is a direct category floor, separate from pairwise
workout viability.

For every modifier pair, workout duration, and muscle bucket, at least five
qualifying exercises must exist in every real state. A binary/binary pair has
four states. A pair involving Mirror has six: the other modifier is off/on and
Mirror is off/compact/tall. In a mirror-equipped state, only `MirrorOnly` and
`BenefitsGreatly` count toward the five-exercise relevance floor, after actual
equipment compatibility is applied. `Agnostic` exercises remain runtime
candidates but cannot make Mirror coverage appear complete.

Materiality is checked independently for compact and tall mirrors. Compact must
actually prefer a meaningful, anatomically broad upper-body set; full-body
`BenefitsGreatly` exercises do not receive credit there. Tall must actually
prefer the compatible upper- and full-body set. These checks also run when
either other modifier is already enabled.

The guarantees remain quadratic in the number of logical modifiers. Mirror's
extra equipment state enlarges the states of a pair but does not create an
all-modifier power set.

The authoritative exhaustive lists live in
`tools/ExerciseMirrorRelationships.psd1`. Generation and Android/web tests fail
on missing or duplicate IDs, criterion/coverage drift, contradictory equipment,
an undersized cell, linked-direction disagreement, pairwise deficiencies,
materiality deficiencies, or infeasible distinct lineups.

Mirror equipment never transforms demonstration media. Horizontal playback
mirroring remains confined to the timed second-side protocol.
