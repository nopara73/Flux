# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **431** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Canonical muscle group | Primary exercises | All meaningful assignments |
| --- | ---: | ---: |
| Medial and deep knee extensors | 22 | 147 |
| Posterior thigh and knee flexors | 14 | 67 |
| Major hip adductors | 13 | 54 |
| Lateral knee extensors | 18 | 149 |
| Gluteal extensors | 13 | 163 |
| Spinal extensors | 13 | 41 |
| Calf, deep posterior leg and plantar foot | 20 | 129 |
| Soleus | 9 | 91 |
| Scapular girdle | 10 | 129 |
| Shoulder adductors and extensors | 14 | 48 |
| Abdominal wall | 12 | 174 |
| Hip abductors | 24 | 92 |
| Chest | 12 | 56 |
| Elbow extensors | 14 | 35 |
| Hip flexors | 27 | 86 |
| Anterior/lateral lower leg and dorsal foot | 11 | 64 |
| Deep hip rotators | 14 | 44 |
| Shoulder abductors | 16 | 115 |
| Forearm flexors and pronators | 10 | 63 |
| Deep and intersegmental back | 16 | 75 |
| Elbow flexors | 12 | 32 |
| Breathing muscles | 15 | 67 |
| Forearm extensors and supinators | 11 | 45 |
| Rotator cuff | 12 | 65 |
| Accessory hip adductors | 10 | 47 |
| Posterior neck and suboccipital muscles | 11 | 40 |
| Cranial muscles | 28 | 39 |
| Anterior/lateral neck and hyoid muscles | 11 | 40 |
| Intrinsic hand | 11 | 45 |
| Pelvic floor and perineum | 8 | 14 |

## Retained source quality

- Direct human-footage demonstrations: **427**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **4**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
