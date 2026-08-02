# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **236** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Muscle group | Assigned exercises |
| --- | ---: |
| Glutes | 73 |
| Core | 47 |
| Quadriceps | 75 |
| Hamstrings | 19 |
| Upper back | 15 |
| Shoulders | 60 |
| Chest | 19 |
| Lower back | 15 |
| Calves | 28 |
| Hip flexors | 36 |
| Adductors | 34 |
| Abductors | 34 |
| Mid back | 20 |
| Trapezius | 29 |
| Forearms | 40 |
| Triceps | 24 |
| Biceps | 11 |
| Rotator cuff | 28 |
| Neck | 20 |
| Shins | 27 |

## Retained source quality

- Direct human-footage demonstrations: **235**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **1**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
