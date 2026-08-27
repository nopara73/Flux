# Exercise catalog usability audit

This audit models a first-time user standing in a 2 m × 2 m space with no wall,
equipment, or spoken instruction. For every catalog entry, the reviewer recorded
what the user would infer from the plain exercise name, what the silent human
demonstration visibly shows, and whether those two signals make the action
immediately copyable.

- Original entries reviewed: 410
- Original entries retained: 130
- Original entries retired: 280
- Reviewed replacements/reactivations in the current manifest: 320 (the
  original 288-row audit pass plus 32 later reviewed catalog changes)
- Final catalog entries: 450
- Final presentation mix: 392 motion demonstrations, 58 still endpoints
- Pairwise modifier coverage: for every modifier pair and every supported
  roll-up, each real UI state (on/on, on/off, off/on, and off/off) contains at
  least 5 selectable exercises. Off relaxes the corresponding filter. A separate
  quadratic materiality check requires each modifier to exclude at least 5
  exercises or 5% of the prior candidate pool, whichever is larger, across at
  least 10% of canonical buckets, both alone and when its paired modifier is on.
  The rule grows with modifier pairs, not the full power set.
- Hard-floor compatibility: all 450 entries have an explicit review; 378 are
  suitable on an ordinary rigid floor and 72 require a stable soft floor.
- Distinct-lineup feasibility: every duration and enabled profile must admit one
  different exercise per scheduled group.

[`exercise_usability_audit.csv`](exercise_usability_audit.csv) is the complete
one-row-per-original-exercise review. [`catalog_replacements.csv`](catalog_replacements.csv)
preserves the original 288-row replacement pass, including its literal action,
classification, timing, presentation, human-media metadata, and rationale.
[`tools/CatalogExerciseReplacements.psd1`](../../tools/CatalogExerciseReplacements.psd1)
is the authoritative current 320-entry replacement and safe-reactivation
manifest.

Existing scores are preserved for every retained exercise. A replaced numeric slot
is deleted and reinserted so the new exercise starts at score 0 instead of inheriting
the retired exercise's history. Two invalid slots are permanently retired without a
replacement; ten older retired identities are safely reactivated for the linked
opposite-direction exercises and also start at score 0.
