# Muscular-demand audit

The 501 selectable exercises were individually reviewed against one frozen,
three-level rubric. The rating estimates the demonstrated movement's inherent
local muscular demand when an average healthy adult repeats the shown range and
cadence continuously for 45 seconds. It is not a personalized RPE prediction.

| Rating | Contract | Current count |
| --- | --- | ---: |
| `0` | Muscular loading is incidental; mobility, motor control, balance skill, breathing, or relaxation is the principal demand. | 121 |
| `1` | Muscular loading is meaningful, but local force or fatigue is not expected to be the principal limiter. | 232 |
| `2` | Hard muscular work; local force or fatigue is expected to be the principal limiter. | 148 |

Ratings were assigned exercise by exercise. There is no desired distribution,
quota, or balancing target. Stretching and mobility are not promoted merely
because a muscle is named; unloaded striking and rhythmic cardio do not become
`2` merely because they are fast; demanding squat/lunge patterns, substantial
bodyweight isometrics, self-resistance, and repeated plyometric work qualify
when local force or fatigue is the limiting demand.

The complete 2026-08-29 demonstration-integrity review corrected three demand
ratings strictly from the then-current final loops. Later semantic replacements
are rated from their replacement demonstrations rather than inheriting those
historical values. The current cleanup rates fingertip wall push-ups and the
isometric palm press at `2`; the standing upper-body drills and martial strikes
at `1`; and the reverse-prayer stretch at `0`. Exercise 565 is also rated `2`
because its loop repeatedly raises both heels while sustaining a mini squat.
No distribution was targeted. See
[`CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`](CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md).

The authoritative reviewed ID lists and rubric live in
[`tools/ExerciseMuscularDemand.psd1`](../tools/ExerciseMuscularDemand.psd1).
The generated rating for each named exercise is shipped as `muscularDemand` in
[`Flux/Assets/exercises.json`](../Flux/Assets/exercises.json). Catalog generation
fails if a retained exercise is missing, duplicated across ratings, or assigned
outside `0..2`; linked opposite-direction exercises must agree.

`muscularDemand` is intentionally independent of the mutable user-preference
`score`; it never creates hardness points or rewrites votes. Completing a
rating-`1` exercise starts a rolling 18-hour soft recovery preference for its
primary canonical muscle, during which same-score rating-`0` work is preferred.
Completing a rating-`2` exercise starts both that 18-hour window and the
separate 36-hour hard-work window, during which same-score non-hard work is
preferred to rating-`2` work for the same primary muscle.

A fresh rating-`2` keep, or a fresh rating-`2` exercise already in the highest
available saved-score bucket, is preferred when its primary canonical muscle
belongs to the current workout group. Recovering exercises remain selectable,
and their keeps remain saved for later; a higher user score still wins. Among
otherwise equivalent fresh hard choices, the longest-rested primary muscle is
preferred. Available-equipment relevance for Wall and Mirror is a lower-order
tie-break, and a rejected
lower-score exercise is never pulled upward by recovery rotation.

Every fourth consecutive local-calendar training day defaults the session's
explicit Light control to ON. The feather control can also be enabled on an
ordinary day or disabled on an automatic light day, before or during the
workout. Flux first maximizes sequences whose every distinct member is demand
`0`. Saved score, Keep, recovery, and equipment preferences then arbitrate
among those light choices. A harder sequence fills a slot only when the
compatible catalog cannot cover it with demand-`0` work; displaced Keeps and
user scores remain persisted unchanged.
Completed session history supplies the default cadence; interrupted sessions
and duplicate completions on the same date do not advance it. Light itself is
session-scoped and is not copied into the next session's remembered physical
setup.

The separate within-session muscle rebalancer uses the same reviewed rating
without changing it:

| `muscularDemand` | Primary canonical muscle | Each distinct secondary |
|---:|---:|---:|
| `0` | 0.25 | 0.125 |
| `1` | 0.5 | 0.25 |
| `2` | 1 | 0.5 |

Actual repeated sets multiply those contributions. Multiple side or direction
blocks of the same exercise identity within one sequence set count once;
different linked identities count separately. The resulting canonical loads
are summed into every existing 3-, 5-, 7-, 10-, 15-, 20-, and 30-minute muscle
resolution independently.

At each resolution, the weakest bucket's share of the strongest is evaluated.
The soft goal is at least 25% at all seven resolutions. Flux chooses one legal
replacement that lexicographically improves the sorted resolution shares,
weakest first, recalculates, and repeats until all goals are met, no improvement
exists, a lineup repeats, or 30 passes complete. Exact-slot Keeps remain frozen,
and no candidate may have a lower saved score than a displaced selection in any
covered slot. The process preserves modifier eligibility, recovery rotation,
atomic sequences, unique session movements, exact duration, and repeated-set
allocation. It changes neither `muscularDemand` nor persisted user scores, and
it may deliberately stop below the goal when the catalog has no valid better
lineup.
