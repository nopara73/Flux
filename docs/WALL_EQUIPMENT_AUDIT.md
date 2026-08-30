# Wall equipment audit

Wall is a binary physical-equipment modifier. It is disabled by default and is
shown after Silence and before Mirror. Its exact toggle feedback and tooltip
copy is `equipment ON: wall` and `equipment OFF: wall`.

Each catalog record carries an explicit Boolean `wallRequired` verdict:

- Wall off excludes `wallRequired` exercises.
- Wall on admits them and softly prefers them only when every stronger
  selection priority ties.
- Ordinary exercises remain selectable in either state.
- Wall never changes, mirrors, or substitutes demonstration media.

Wall is intentionally not another pairwise quota dimension. It does not enter
per-muscle, duration, materiality, or modifier-pair coverage checks. Instead,
Android and web enforce one direct global floor of at least 20 distinct
wall-required session movements. `sessionMovementId` is resolved before
counting, so sides, directions, blocks, repeated sets, aliases, and renamed
duplicates cannot pad the result.

## Reviewed inventory

The current catalog contains 24 distinct wall-required session movements:

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
stance requires dependable traction. Wall availability does not override the
independent floor verdict. All 24 final packaged MP4s, crops, loops, and applicable
hold frames were reviewed against their final names and metadata. The catalog
generator rejects missing verdicts, linked-sequence disagreement, duplicate
identities, and an undersized distinct-movement inventory.

The authoritative classifications live in
[`tools/ExerciseWallRequirements.psd1`](../tools/ExerciseWallRequirements.psd1).
The Android catalog stores the verdict in SQLite schema 73; web consumes the
same generated catalog and enforces the same selection and startup invariants.
