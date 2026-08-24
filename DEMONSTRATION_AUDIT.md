# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **448** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 22 | 154 |
| Posterior thigh and knee flexors | 17 | 71 |
| Major hip adductors | 13 | 54 |
| Lateral knee extensors | 20 | 156 |
| Gluteal extensors | 14 | 172 |
| Spinal extensors | 14 | 44 |
| Calf, deep posterior leg and plantar foot | 20 | 133 |
| Soleus | 9 | 91 |
| Scapular girdle | 10 | 131 |
| Shoulder adductors and extensors | 14 | 50 |
| Abdominal wall | 12 | 179 |
| Hip abductors | 25 | 98 |
| Chest | 12 | 57 |
| Elbow extensors | 14 | 35 |
| Hip flexors | 28 | 92 |
| Anterior/lateral lower leg and dorsal foot | 11 | 64 |
| Deep hip rotators | 16 | 48 |
| Shoulder abductors | 16 | 117 |
| Forearm flexors and pronators | 10 | 66 |
| Deep and intersegmental back | 16 | 78 |
| Elbow flexors | 16 | 36 |
| Breathing muscles | 15 | 67 |
| Forearm extensors and supinators | 11 | 46 |
| Rotator cuff | 13 | 68 |
| Accessory hip adductors | 10 | 47 |
| Posterior neck and suboccipital muscles | 11 | 40 |
| Cranial muscles | 28 | 39 |
| Anterior/lateral neck and hyoid muscles | 11 | 39 |
| Intrinsic hand | 11 | 48 |
| Pelvic floor and perineum | 9 | 15 |

## Retained source quality

- Direct human-footage demonstrations: **444**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **4**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
