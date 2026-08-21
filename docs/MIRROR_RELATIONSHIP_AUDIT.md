# Mirror relationship audit

The 420 retained exercises were re-reviewed against the physical-mirror
equipment contract, not against whether their demonstration video may be
horizontally mirrored for a timed second side.

- `MirrorOnly`: the reflection is part of the defined exercise. Count: 1.
- `BenefitsGreatly`: continuous live self-view substantially changes technique,
  alignment, or symmetry. Count: 58.
- `Agnostic`: the mirror offers no more than ordinary optional form checking.
  Count: 361.

Merely being able to see oneself does not qualify as `BenefitsGreatly`. Basic
squats, lunges, stretches, reaches, marches, circles, and simple mobility are
agnostic unless the exact movement has an unusually strong visual-feedback
dependency. The retained greatly-benefited set is concentrated in technical
martial-arts movements, dance and alignment-sensitive poses, difficult
single-leg balance work, and a small number of movements where live feedback
reveals otherwise hidden plane or symmetry errors.

The current technical set includes the exact planted shadow-boxing head-defense
family: `Shadow Boxing`, `Boxing Slip`, `Boxing Roll`, `Boxing Duck`, and
`Boxing Pullback`. A live reflection provides the visual target and exposes
guard position, head path, and whether the boxer actually clears the centerline.
The alignment-sensitive set also includes the four retained wide-plié movements,
where live frontal feedback materially exposes knee tracking, stance symmetry,
and torso drift. These are movement-specific reasons, not generic claims that
all boxing or squat movements benefit greatly from seeing oneself.

Exercise 500 is `Mirror-Guided Straight Jaw Opening`. Its packaged front-view
demonstration includes a midline guide, and the exercise requires watching that
the jaw opens vertically without lateral deviation. It is unavailable when
Mirror equipment is off. The other jaw and head glides remain agnostic or, for
the lateral head glide, greatly benefited; they were not promoted merely to
increase the mirror-only count.

This requirement follows the established straight-opening protocol published
by [Newcastle Hospitals NHS Foundation Trust](https://www.newcastle-hospitals.nhs.uk/resources/temporomandibular-disorders-tmd/),
which prescribes live mirror feedback to detect sideways jaw movement.

The five-exercise pairwise floor is both a workout-viability and modifier-
coverage guarantee. In Mirror-on states, only `MirrorOnly` and
`BenefitsGreatly` exercises count toward the floor for each workout muscle
bucket and paired-modifier state. `Agnostic` exercises remain selectable but
cannot make Mirror coverage appear complete. In Mirror-off states, every
normally selectable exercise except `MirrorOnly` counts. Genuine deficiencies
must remain visible rather than being hidden by inflated relationship labels.
Mirror materiality is checked separately across the whole relevant set and its
anatomical breadth.

## Classification guardrails

The current 58 `BenefitsGreatly` assignments are the result of this audit, not a
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
