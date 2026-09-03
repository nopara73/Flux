# Exercise catalog audit artifacts

Flux models a first-time user standing in a 2 m × 2 m space with no external
support, equipment, or spoken instruction except where a stable wall or
physical mirror is explicitly declared. The final packaged silent human demonstration is authoritative for
the action, name, muscular demand, anatomy, structure, and presentation.

## 2026-08-29 full-loop integrity baseline

The 2026-08-29 review inspected all **479** pre-audit records and produced a
final catalog of **475** entries:

- **349** passed without correction;
- **126** were corrected while retaining their stable IDs and scores;
- **4** were retired;
- final presentation mix: **417** motion loops and **58** still endpoints;
- hard-floor compatibility: **384** compatible and **91** incompatible;
- mirror relationships: **10** `MirrorOnly`, **77** `BenefitsGreatly`, and
  **388** `Agnostic`.

The subsequent Wall-equipment implementation added 24 separately reviewed base
wall movements. A later exact fingertip wall-push-up replacement brings the
current base inventory to 25. Five existing slots contain separately reviewed
sole-contact wall movements. Their full 30-movement inventory and two independent direct-floor contracts are
documented in
[`../WALL_EQUIPMENT_AUDIT.md`](../WALL_EQUIPMENT_AUDIT.md); the counts above
remain the frozen outcome of the 479-record baseline review.

The upper-body-clothing audit initially classified those 499 records. Its
proactive practice review then admitted two established, separately demonstrated
bare-torso mirror practices, producing a 501-record catalog. The later reduced
muscular-demand coverage pass admitted seven separately reviewed exercise
records forming five session movements, producing the current 508-record
catalog: six require clothing for torso/back
contact, seven require a visible bare upper body or abdominal contour, and 495
are agnostic.
Its exhaustive source and review rules are documented in
[`../UPPER_BODY_CLOTHING_AUDIT.md`](../UPPER_BODY_CLOTHING_AUDIT.md).

[`demonstration_metadata_integrity_2026-08-29.csv`](demonstration_metadata_integrity_2026-08-29.csv)
has exactly one row per pre-audit exercise. Each changed row records the prior
metadata, what the final loop actually shows, every exact changed field, and the
reason. It also records the loop, crop, seam, speed, mirroring, travel, hold
frame, and equipment review dimensions.

[`modifier_coverage_deficits_2026-08-29.json`](modifier_coverage_deficits_2026-08-29.json)
is the frozen output of the unchanged pairwise modifier validators at that
audit baseline. Wall is not a pairwise quota dimension, so the totals remained
directly comparable after the 24-record extension. The later combined
hard-and-slippery floor contract changes candidate membership, so this dated
artifact is not rewritten. The live hierarchical contract requires five
movements in broad regions and one in fine buckets, with broad-only demand
coverage; Android and web CI require every live deficit array to be empty.
[`modifier_coverage_deficits_current.json`](modifier_coverage_deficits_current.json)
is regenerated with `npm run refresh:catalog-deficit-ledger` from `web/`. It is
a reproducible diagnostic snapshot, not a waiver: the production build fails
when pairwise, hard-floor-category, or muscular-demand deficits are nonzero,
even if the snapshot is refreshed. The frozen 2026-08-29 artifact predates the
hierarchical validator and remains unchanged.
The methodology, summary, evidence, migrations, and correction classes are in
[`../CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`](../CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md).

Catalog revision 62 closes the live hierarchical gaps without adding an
exercise, changing a demand rating, or promoting a mirror/floor relationship.
It corrects under-recorded secondary anatomy already required by the packaged
demonstrations: scapular/rotator-cuff control in side-tap palm pushes, overhead
punches, and outside blocks (IDs 248, 281, 286, and 545); abdominal bracing in
single-leg deadlifts (367 and 529); and calf/deep-hip stabilization in
deadlift-to-runner-march variants (393 and 537). Cached placements for those
IDs are rebuilt while all scores, Keeps, phase feedback, history, and recovery
state remain intact.

## Historical usability audit

The earlier review started from 410 original entries: 130 were retained and
280 retired. Subsequent reviewed replacements and reactivations produced the
pre-integrity inventory audited above.

[`exercise_usability_audit.csv`](exercise_usability_audit.csv) preserves that
one-row-per-original-exercise review. [`catalog_replacements.csv`](catalog_replacements.csv)
preserves the original replacement pass, including action, classification,
timing, presentation, human-media metadata, and rationale. The authoritative
current replacement and safe-reactivation manifest is
[`tools/CatalogExerciseReplacements.psd1`](../../tools/CatalogExerciseReplacements.psd1).

Existing scores are preserved for retained identities. A genuinely replaced
numeric slot is deleted and reinserted so it cannot inherit the retired
exercise's score. Permanent retirements cannot silently return.
