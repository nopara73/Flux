# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **501** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 33 | 169 |
| Posterior thigh and knee flexors | 20 | 74 |
| Major hip adductors | 17 | 55 |
| Lateral knee extensors | 26 | 170 |
| Gluteal extensors | 17 | 187 |
| Spinal extensors | 13 | 45 |
| Calf, deep posterior leg and plantar foot | 33 | 158 |
| Soleus | 11 | 87 |
| Scapular girdle | 14 | 161 |
| Shoulder adductors and extensors | 16 | 68 |
| Abdominal wall | 13 | 191 |
| Hip abductors | 29 | 104 |
| Chest | 18 | 69 |
| Elbow extensors | 17 | 51 |
| Hip flexors | 38 | 103 |
| Anterior/lateral lower leg and dorsal foot | 12 | 48 |
| Deep hip rotators | 19 | 49 |
| Shoulder abductors | 23 | 144 |
| Forearm flexors and pronators | 8 | 58 |
| Deep and intersegmental back | 21 | 79 |
| Elbow flexors | 15 | 41 |
| Breathing muscles | 2 | 37 |
| Forearm extensors and supinators | 12 | 40 |
| Rotator cuff | 14 | 69 |
| Accessory hip adductors | 11 | 40 |
| Posterior neck and suboccipital muscles | 11 | 31 |
| Cranial muscles | 22 | 34 |
| Anterior/lateral neck and hyoid muscles | 11 | 30 |
| Intrinsic hand | 5 | 39 |
| Pelvic floor and perineum | 0 | 45 |

## Retained source quality

- Direct human-footage demonstrations: **497**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **4**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.

The complete 2026-08-29 demonstration-to-metadata review, including one
ledger row for every pre-audit exercise and every exact correction, is in
`docs/CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`.
