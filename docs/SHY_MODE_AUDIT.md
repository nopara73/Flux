# Shy mode audit

Shy mode is for exercising near people who are not participating in the
workout. It is disabled by default. When enabled, Flux admits only exercises
with an explicit `Compatible` review; `Incompatible` exercises are removed and
unreviewed metadata fails closed.

This is not a low-motion or "doesn't look like exercise" filter. Ordinary,
recognizable strength, cardio, mobility, stretching, balance, jumping, running
in place, arm work, familiar boxing, and cardio-boxing remain available. The
review is independent of upper-body clothing, Silence, Light, and equipment.

The current 512-exercise catalog has 428 compatible and 84 incompatible
records. The incompatible records use one primary visible reason:

| Review reason | Records |
| --- | ---: |
| Physique or facial performance | 13 |
| Jaw, eye-tracking, or vestibular drill | 23 |
| Vocal breathing or body tapping | 9 |
| Dance or conspicuous choreography | 10 |
| Intimate or rear-body focus | 6 |
| Conspicuous combat or fight choreography | 23 |

The exact exhaustive partition is
[`tools/ExerciseShyCompatibility.psd1`](../tools/ExerciseShyCompatibility.psd1).
Catalog generation rejects missing IDs, duplicates, overlap, unknown IDs, and
different Shy classifications within one atomic exercise sequence. This makes
new exercises unreviewed by default instead of silently treating them as
public-friendly.

Familiar boxing and cardio-boxing drills remain compatible, including punches
combined with squats, jumps, and running. Martial-arts kicks, capoeira, elbow
strikes, knife-hand strikes, backfists, and theatrical fight
choreography remain excluded. Dance-like routines, posing, pelvic or
rear-focused movements, face and eye drills, vocalized breathing, and body
tapping are excluded. For the deliberately close boundary,
`Pogo Bounces with Fixed-Gaze Head Turns` remains recognizable cardio and is
compatible, while `March in Place with Sideward Eye Shifts Between Thumbs` is
excluded because the conspicuous eye-tracking action is the point of the drill.

Shy participates in the same quadratic single/pair validation as the other
restrictive workout conditions. On the current catalog, all 28 validation
profiles pass with zero hierarchical availability, muscular-demand,
materiality, or capacity-exact lineup deficits. No exercise relationship,
demand rating, or anatomy claim was changed to satisfy those checks.
