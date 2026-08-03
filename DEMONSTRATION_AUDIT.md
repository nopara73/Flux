# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **321** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 13 | 93 |
| Posterior thigh and knee flexors | 10 | 31 |
| Major hip adductors | 12 | 30 |
| Lateral knee extensors | 14 | 97 |
| Gluteal extensors | 12 | 107 |
| Spinal extensors | 10 | 16 |
| Calf, deep posterior leg and plantar foot | 10 | 47 |
| Soleus | 10 | 24 |
| Scapular girdle | 10 | 62 |
| Shoulder adductors and extensors | 10 | 30 |
| Abdominal wall | 11 | 72 |
| Hip abductors | 12 | 51 |
| Chest | 10 | 29 |
| Elbow extensors | 10 | 45 |
| Hip flexors | 13 | 53 |
| Anterior/lateral lower leg and dorsal foot | 10 | 35 |
| Deep hip rotators | 13 | 26 |
| Shoulder abductors | 10 | 79 |
| Forearm flexors and pronators | 10 | 59 |
| Deep and intersegmental back | 10 | 49 |
| Elbow flexors | 10 | 24 |
| Breathing muscles | 10 | 11 |
| Forearm extensors and supinators | 10 | 41 |
| Rotator cuff | 10 | 37 |
| Accessory hip adductors | 10 | 30 |
| Posterior neck and suboccipital muscles | 11 | 20 |
| Cranial muscles | 10 | 10 |
| Anterior/lateral neck and hyoid muscles | 10 | 21 |
| Intrinsic hand | 10 | 37 |
| Pelvic floor and perineum | 10 | 10 |

## Retained source quality

- Direct human-footage demonstrations: **320**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **1**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
