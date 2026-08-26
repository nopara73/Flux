# Muscular-demand audit

The 448 selectable exercises were individually reviewed against one frozen,
three-level rubric. The rating estimates the demonstrated movement's inherent
local muscular demand when an average healthy adult repeats the shown range and
cadence continuously for 45 seconds. It is not a personalized RPE prediction.

| Rating | Contract | Current count |
| --- | --- | ---: |
| `0` | Muscular loading is incidental; mobility, motor control, balance skill, breathing, or relaxation is the principal demand. | 117 |
| `1` | Muscular loading is meaningful, but local force or fatigue is not expected to be the principal limiter. | 197 |
| `2` | Hard muscular work; local force or fatigue is expected to be the principal limiter. | 134 |

Ratings were assigned exercise by exercise. There is no desired distribution,
quota, or balancing target. Stretching and mobility are not promoted merely
because a muscle is named; unloaded striking and rhythmic cardio do not become
`2` merely because they are fast; demanding squat/lunge patterns, substantial
bodyweight isometrics, self-resistance, and repeated plyometric work qualify
when local force or fatigue is the limiting demand.

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
preferred. Mirror relevance is a lower-order tie-break, and a rejected
lower-score exercise is never pulled upward by recovery rotation.

The separate within-session muscle-workload budget uses the same reviewed
rating without changing it. Rating `0` contributes no load. Rating `1`
contributes 0.5 unit only to the primary canonical muscle. Rating `2`
contributes 1 unit to the primary and 0.5 to each distinct secondary canonical
muscle. Consequently, incidental work and moderate secondary associations do
not increase accumulated load. Candidate comparison still applies any existing
excess temporarily across the candidate's truthful muscle associations.
