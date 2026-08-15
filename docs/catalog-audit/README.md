# Exercise catalog usability audit

This audit models a first-time user standing in a 2 m × 2 m space with no wall,
equipment, or spoken instruction. For every catalog entry, the reviewer recorded
what the user would infer from the plain exercise name, what the silent human
demonstration visibly shows, and whether those two signals make the action
immediately copyable.

- Catalog entries reviewed: 333
- Entries retained: 180
- Entries retired: 153
- Reviewed replacements added: 153
- Retained still endpoints: 25
- Replacement still endpoints: 37
- Final presentation mix: 271 motion demonstrations, 62 still endpoints
- Final canonical-leaf coverage: at least 10 meaningfully associated exercises in every leaf
- Final workout coverage: at least 10 selectable exercises in every supported roll-up
- Modifier coverage: at least 10 selectable exercises for every enabled profile in
  every roll-up, plus at least 5 normal-selectable exercises explicitly excluded by
  each primitive modifier. Both floors apply automatically to future modifiers.
- Distinct-lineup feasibility: every duration and enabled profile must admit one
  different exercise per scheduled group.

[`exercise_usability_audit.csv`](exercise_usability_audit.csv) is the complete
one-row-per-original-exercise review. [`catalog_replacements.csv`](catalog_replacements.csv)
is the authoritative one-for-one replacement set and includes the literal action,
classification, timing, presentation, human-media metadata, and review rationale.

Existing scores are preserved for every retained exercise. A retired numeric slot
is deleted and reinserted as its replacement so the replacement starts at score 0
instead of inheriting the retired exercise's history.
