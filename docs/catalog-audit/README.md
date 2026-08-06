# Exercise catalog usability audit

This audit models a first-time user standing in a 3 m × 3 m space with no wall,
equipment, or spoken instruction. For every catalog entry, the reviewer recorded
what the user would infer from the plain exercise name, what the silent human
demonstration visibly shows, and whether those two signals make the action
immediately copyable.

- Catalog entries reviewed: 328
- Entries retained unchanged: 234
- Entries retired: 94
- Reviewed replacements added: 94
- Retained exercises changed from motion to a still endpoint: 30
- Replacement still endpoints: 24
- Final presentation mix: 273 motion repetitions, 54 still holds, 1 motion-then-freeze hold
- Final canonical-leaf coverage: at least 10 primary-owned exercises in every leaf
- Final workout coverage: at least 10 selectable exercises in every supported roll-up

[`exercise_usability_audit.csv`](exercise_usability_audit.csv) is the complete
one-row-per-original-exercise review. [`catalog_replacements.csv`](catalog_replacements.csv)
is the authoritative one-for-one replacement set and includes the literal action,
classification, timing, presentation, human-media metadata, and review rationale.

Existing scores are preserved for every retained exercise. A retired numeric slot
is deleted and reinserted as its replacement so the replacement starts at score 0
instead of inheriting the retired exercise's history.
