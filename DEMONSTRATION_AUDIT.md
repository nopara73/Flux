# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **429** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 23 | 143 |
| Posterior thigh and knee flexors | 14 | 65 |
| Major hip adductors | 13 | 56 |
| Lateral knee extensors | 18 | 145 |
| Gluteal extensors | 13 | 165 |
| Spinal extensors | 13 | 43 |
| Calf, deep posterior leg and plantar foot | 20 | 124 |
| Soleus | 9 | 88 |
| Scapular girdle | 12 | 127 |
| Shoulder adductors and extensors | 13 | 45 |
| Abdominal wall | 11 | 173 |
| Hip abductors | 23 | 94 |
| Chest | 11 | 53 |
| Elbow extensors | 13 | 32 |
| Hip flexors | 27 | 85 |
| Anterior/lateral lower leg and dorsal foot | 11 | 64 |
| Deep hip rotators | 15 | 46 |
| Shoulder abductors | 16 | 112 |
| Forearm flexors and pronators | 10 | 58 |
| Deep and intersegmental back | 16 | 78 |
| Elbow flexors | 11 | 27 |
| Breathing muscles | 15 | 67 |
| Forearm extensors and supinators | 11 | 42 |
| Rotator cuff | 13 | 65 |
| Accessory hip adductors | 10 | 49 |
| Posterior neck and suboccipital muscles | 12 | 41 |
| Cranial muscles | 25 | 36 |
| Anterior/lateral neck and hyoid muscles | 11 | 40 |
| Intrinsic hand | 10 | 42 |
| Pelvic floor and perineum | 10 | 16 |

## Retained source quality

- Direct human-footage demonstrations: **418**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **11**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
