# Mirror relationship audit

The 418 retained exercises were reviewed against the physical-mirror equipment
contract, not against whether their demonstration video may be horizontally
mirrored for a timed second side.

- `MirrorOnly`: the reflection is essential to the defined exercise. Count: 0.
- `BenefitsGreatly`: continuous visual feedback materially improves alignment,
  symmetry, balance, movement plane, or technique. Count: 300.
- `Agnostic`: a mirror offers no more than ordinary optional form checking.
  Count: 118.

No existing exercise is marked `MirrorOnly` because every retained exact
demonstration currently defines a movement that can be executed without a
reflection. Adding a fabricated requirement would make the metadata false.
The runtime, database, selector, and tests nevertheless support `MirrorOnly`
with `equipment: "Mirror"` for a future established exercise whose exact human
demonstration genuinely depends on mirror feedback.

The Mirror modifier never transforms demonstration media. Horizontal playback
mirroring remains confined to the existing timed second-side protocol.

The authoritative exhaustive ID lists live in
`tools/ExerciseMirrorRelationships.psd1`. Generation fails if an exercise is
missing, duplicated between categories, paired with contradictory equipment,
or disagrees with its linked opposite-direction partner.
