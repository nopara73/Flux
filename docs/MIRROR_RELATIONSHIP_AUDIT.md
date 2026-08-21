# Mirror relationship audit

The 418 retained exercises were re-reviewed against the physical-mirror
equipment contract, not against whether their demonstration video may be
horizontally mirrored for a timed second side.

- `MirrorOnly`: the reflection is part of the defined exercise. Count: 1.
- `BenefitsGreatly`: continuous live self-view substantially changes technique,
  alignment, or symmetry. Count: 52.
- `Agnostic`: the mirror offers no more than ordinary optional form checking.
  Count: 365.

Merely being able to see oneself does not qualify as `BenefitsGreatly`. Basic
squats, lunges, stretches, reaches, marches, circles, and simple mobility are
agnostic unless the exact movement has an unusually strong visual-feedback
dependency. The retained greatly-benefited set is concentrated in technical
martial-arts movements, dance and alignment-sensitive poses, difficult
single-leg balance work, and a small number of movements where live feedback
reveals otherwise hidden plane or symmetry errors.

Exercise 500 is `Mirror-Guided Straight Jaw Opening`. Its packaged front-view
demonstration includes a midline guide, and the exercise requires watching that
the jaw opens vertically without lateral deviation. It is unavailable when
Mirror equipment is off. The other jaw and head glides remain agnostic or, for
the lateral head glide, greatly benefited; they were not promoted merely to
increase the mirror-only count.

This requirement follows the established straight-opening protocol published
by [Newcastle Hospitals NHS Foundation Trust](https://www.newcastle-hospitals.nhs.uk/resources/temporomandibular-disorders-tmd/),
which prescribes live mirror feedback to detect sideways jaw movement.

The five-exercise pairwise floor is a workout-viability guarantee. Every
selectable exercise counts toward it, including `Agnostic` exercises when Mirror
is on. It is intentionally not a five-per-muscle mirror-relevance quota. Mirror
materiality is checked separately: `MirrorOnly` and `BenefitsGreatly` must
jointly meet the global materiality and anatomical-breadth thresholds alone and
with either paired modifier enabled.

## Classification guardrails

The current 52 `BenefitsGreatly` assignments are the result of this audit, not a
quota, target, or ceiling. The count may move in either direction as the catalog
changes, but only because individual exercises genuinely meet or cease to meet
the semantic standard—not to satisfy modifier coverage or materiality checks.

The authoritative manifest groups every `BenefitsGreatly` exercise under
exactly one narrow reason: technique-sensitive martial arts, dance or
alignment-sensitive poses, complex single-leg alignment, or live plane/path/
symmetry correction. Merely seeing the movement, comparing oneself with the
demonstration, or receiving ordinary form feedback never qualifies. Catalog
generation rejects unknown or missing criteria and duplicate assignments.
Android and web catalog tests independently pin the current reviewed count, so
changing the generated catalog also requires an explicit test review rather
than silently drifting through regeneration. Deployment CI also compares every
reviewed ID and relationship with the packaged shared catalog whenever the
mirror audit changes.

The Mirror modifier never transforms demonstration media. Horizontal playback
mirroring remains confined to the existing timed second-side protocol.

The authoritative exhaustive ID lists live in
`tools/ExerciseMirrorRelationships.psd1`. Generation fails if an exercise is
missing, duplicated between categories, paired with contradictory equipment,
or disagrees with its linked opposite-direction partner.
