# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **475** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 31 | 160 |
| Posterior thigh and knee flexors | 20 | 68 |
| Major hip adductors | 17 | 55 |
| Lateral knee extensors | 25 | 162 |
| Gluteal extensors | 14 | 175 |
| Spinal extensors | 13 | 43 |
| Calf, deep posterior leg and plantar foot | 34 | 148 |
| Soleus | 9 | 81 |
| Scapular girdle | 10 | 144 |
| Shoulder adductors and extensors | 15 | 64 |
| Abdominal wall | 12 | 169 |
| Hip abductors | 27 | 99 |
| Chest | 13 | 59 |
| Elbow extensors | 14 | 39 |
| Hip flexors | 35 | 97 |
| Anterior/lateral lower leg and dorsal foot | 11 | 44 |
| Deep hip rotators | 18 | 45 |
| Shoulder abductors | 19 | 136 |
| Forearm flexors and pronators | 11 | 64 |
| Deep and intersegmental back | 21 | 77 |
| Elbow flexors | 15 | 40 |
| Breathing muscles | 2 | 39 |
| Forearm extensors and supinators | 11 | 46 |
| Rotator cuff | 13 | 54 |
| Accessory hip adductors | 10 | 38 |
| Posterior neck and suboccipital muscles | 11 | 32 |
| Cranial muscles | 22 | 35 |
| Anterior/lateral neck and hyoid muscles | 11 | 31 |
| Intrinsic hand | 11 | 43 |
| Pelvic floor and perineum | 0 | 45 |

## Retained source quality

- Direct human-footage demonstrations: **471**
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
