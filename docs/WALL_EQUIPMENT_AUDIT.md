# Wall equipment audit

Wall is a three-state physical-equipment modifier. It is disabled by default
and is shown after Silence and before Mirror. Its states cycle in this order:

1. no wall — `equipment OFF: wall`;
2. wall available, foot contact allowed — `equipment ON: wall`;
3. wall available, feet kept off it — `equipment ON: wall · no feet on wall`.

Each catalog record carries explicit `wallRequired` and
`soleWallContactRequired` Boolean verdicts. Sole contact always implies Wall:

- Wall off excludes every `wallRequired` exercise.
- The soles-stay-off state admits base wall exercises but excludes exercises
  whose demonstrated movement requires sole-to-wall contact.
- The soles-may-touch state admits both kinds of wall exercise.
- Either available-wall state softly prefers admitted wall work only when every
  stronger selection priority ties.
- Ordinary exercises remain selectable in every state.
- Wall never changes, mirrors, or substitutes demonstration media.
- There is no clean/dirty wall or socks property.

Wall is intentionally not another pairwise quota dimension. It does not enter
per-muscle, duration, materiality, or modifier-pair coverage checks. Instead,
Android and web enforce two separate direct global floors: at least 20 distinct
base wall session movements and at least five distinct sole-contact wall
session movements. A sole-contact movement cannot count toward the base 20.
`sessionMovementId` is resolved before counting, so sides, directions, blocks,
repeated sets, aliases, and renamed duplicates cannot pad either result.

## Reviewed inventory

The current catalog contains 24 distinct base wall session movements:

- `134` — `Wall Sit`
- `137` — `Wall Squat`
- `149` — `Isometric Hip Abduction Against Wall`
- `153` — `Wall-Supported Standing Hip Extension`
- `162` — `Wall-Leaning Calf Raise`
- `163` — `Wall Soleus Stretch`
- `165` — `Wall Tibialis Raise`
- `166` — `Wall Drill March`
- `172` — `Wall Knee Drive`
- `175` — `Wall Sit March`
- `579` — `Wall Shoulder Slides`
- `580` — `Wall Angel`
- `584` — `Wall Scapular Push-Up`
- `585` — `Isometric Shoulder Abduction Against Wall`
- `586` — `Isometric Shoulder Flexion Against Wall`
- `587` — `Isometric Shoulder External Rotation Against Wall`
- `603` — `Wall-Supported Glute Kickback`
- `633` — `Wall Calf Stretch`
- `701` — `Wall Push-Up`
- `702` — `Wall Triceps Push-Up`
- `703` — `Wall Push-Up with Shoulder Tap`
- `704` — `Wall Pectoral Stretch`
- `801` — `Wall Roll-Down`
- `835` — `Wall Lat Stretch`

It also contains five distinct sole-contact wall session movements:

- `563` — `Hip Airplane with Back Foot on Wall`
- `564` — `Standing Foot-to-Wall Press Hold`
- `567` — `Rear-Foot-on-Wall Split Squat`
- `568` — `Toes-on-Wall Calf Stretch`
- `574` — `Wall Toe Taps`

`Wall Shoulder Slides` uses an equipment-free wall-slide demonstration. The
previous reviewed source was rejected after a resistance band was found in the
packaged loop; the replacement shows one complete natural wall-slide cycle
against a bare wall with no prop.

These are established movements whose wall contact is integral to the exercise,
not ordinary exercises relabeled because a nearby wall could offer optional
balance assistance. The wall calf raise and wall tibialis raise remain
hard-floor-incompatible because they concentrate repeated load through the
forefoot or heel. Wall soleus stretch and wall calf stretch are also
incompatible because Hard Floor means a rigid, slippery surface and their split
stance requires dependable traction. The five sole-contact movements are also
incompatible with Hard Floor because Hard Floor means slippery and their
demonstrated support,
balance, or traction depends on reliable foot placement. Wall availability does
not override the independent floor verdict. All 29 final packaged MP4s, crops,
loops, and applicable hold frames were reviewed against their final names and
metadata. The catalog
generator rejects missing verdicts, linked-sequence disagreement, duplicate
identities, sole-without-wall implication violations, and an undersized
distinct-movement inventory.

The authoritative classifications live in
[`tools/ExerciseWallRequirements.psd1`](../tools/ExerciseWallRequirements.psd1)
and
[`tools/ExerciseSoleWallContactRequirements.psd1`](../tools/ExerciseSoleWallContactRequirements.psd1).
The Android catalog stores both verdicts in SQLite schema 74; web consumes the
same generated catalog and enforces the same selection and startup invariants.
