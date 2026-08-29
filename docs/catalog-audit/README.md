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

The subsequent Wall-equipment implementation added 24 separately reviewed
wall-required movements, producing the current 499-record catalog. Their full
inventory and independent singleton-floor contract are documented in
[`../WALL_EQUIPMENT_AUDIT.md`](../WALL_EQUIPMENT_AUDIT.md); the counts above
remain the frozen outcome of the 479-record baseline review.

[`demonstration_metadata_integrity_2026-08-29.csv`](demonstration_metadata_integrity_2026-08-29.csv)
has exactly one row per pre-audit exercise. Each changed row records the prior
metadata, what the final loop actually shows, every exact changed field, and the
reason. It also records the loop, crop, seam, speed, mirroring, travel, hold
frame, and equipment review dimensions.

[`modifier_coverage_deficits_2026-08-29.json`](modifier_coverage_deficits_2026-08-29.json)
is the current output of the unchanged pairwise modifier validators. Wall is
not a pairwise quota dimension, so the pre-existing deficit totals remain
directly comparable after the 24-record extension. The ledger deliberately preserves real catalog debt
rather than making the checks green through filler or false associations.
The methodology, summary, evidence, migrations, and correction classes are in
[`../CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`](../CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md).

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
