# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **357** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 16 | 117 |
| Posterior thigh and knee flexors | 14 | 41 |
| Major hip adductors | 12 | 44 |
| Lateral knee extensors | 18 | 119 |
| Gluteal extensors | 12 | 138 |
| Spinal extensors | 10 | 32 |
| Calf, deep posterior leg and plantar foot | 14 | 82 |
| Soleus | 10 | 47 |
| Scapular girdle | 10 | 99 |
| Shoulder adductors and extensors | 10 | 36 |
| Abdominal wall | 10 | 143 |
| Hip abductors | 18 | 73 |
| Chest | 10 | 39 |
| Elbow extensors | 10 | 28 |
| Hip flexors | 17 | 65 |
| Anterior/lateral lower leg and dorsal foot | 10 | 43 |
| Deep hip rotators | 11 | 36 |
| Shoulder abductors | 10 | 86 |
| Forearm flexors and pronators | 10 | 48 |
| Deep and intersegmental back | 14 | 66 |
| Elbow flexors | 10 | 21 |
| Breathing muscles | 11 | 27 |
| Forearm extensors and supinators | 10 | 36 |
| Rotator cuff | 10 | 54 |
| Accessory hip adductors | 10 | 39 |
| Posterior neck and suboccipital muscles | 10 | 31 |
| Cranial muscles | 20 | 22 |
| Anterior/lateral neck and hyoid muscles | 10 | 30 |
| Intrinsic hand | 10 | 36 |
| Pelvic floor and perineum | 10 | 16 |

## Retained source quality

- Direct human-footage demonstrations: **356**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **1**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
