# Muscular-demand audit

The 418 selectable exercises were individually reviewed against one frozen,
three-level rubric. The rating estimates the demonstrated movement's inherent
local muscular demand when an average healthy adult repeats the shown range and
cadence continuously for 45 seconds. It is not a personalized RPE prediction.

| Rating | Contract | Current count |
| --- | --- | ---: |
| `0` | Muscular loading is incidental; mobility, motor control, balance skill, breathing, or relaxation is the principal demand. | 111 |
| `1` | Muscular loading is meaningful, but local force or fatigue is not expected to be the principal limiter. | 184 |
| `2` | Hard muscular work; local force or fatigue is expected to be the principal limiter. | 123 |

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
`score`; it never creates hardness points or rewrites votes. A fresh rating-`2`
keep, or a fresh rating-`2` exercise already in the highest available
saved-score bucket, is preferred when its primary canonical muscle belongs to
the current workout group. Completing it starts a rolling 36-hour soft recovery
preference for that primary muscle:
same-score rating-`0` or `1` work is preferred while every rating-`2` exercise
with that primary muscle remains selectable and saved for later. Among
otherwise equivalent fresh hard choices, the longest-rested primary muscle is
preferred. Mirror relevance is a lower-order tie-break, and a rejected
lower-score hard exercise is never pulled upward by this rotation.
