# Exercise catalog usability audit

This audit models a first-time user standing in a 3 m × 3 m space with no wall,
equipment, or spoken instruction. For every catalog entry, the reviewer recorded
what the user would infer from the plain exercise name, what the silent human
demonstration visibly shows, and whether those two signals make the action
immediately copyable.

- Catalog entries reviewed: 329
- Entries retained: 181
- Entries retired: 148
- Reviewed replacements added: 148
- Retained still endpoints: 24
- Replacement still endpoints: 33
- Final presentation mix: 268 motion demonstrations, 61 still endpoints
- Final canonical-leaf coverage: at least 10 primary-owned exercises in every leaf
- Final workout coverage: at least 10 selectable exercises in every supported roll-up

[`exercise_usability_audit.csv`](exercise_usability_audit.csv) is the complete
one-row-per-original-exercise review. [`catalog_replacements.csv`](catalog_replacements.csv)
is the authoritative one-for-one replacement set and includes the literal action,
classification, timing, presentation, human-media metadata, and review rationale.

Existing scores are preserved for every retained exercise. A retired numeric slot
is deleted and reinserted as its replacement so the replacement starts at score 0
instead of inheriting the retired exercise's history.
