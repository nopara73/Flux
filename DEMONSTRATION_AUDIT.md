# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **214** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Region | Retained exercises |
| --- | ---: |
| FEET | 3 |
| LEGS | 89 |
| HANDS | 39 |
| ARMS | 3 |
| HEAD | 19 |
| SHOULDERS | 10 |
| HIPS | 18 |
| CHEST | 3 |
| BACK | 9 |
| CORE | 21 |

## Retained source quality

- Direct human-footage demonstrations: **213**
- Exact copies of human footage: **0**
- Exact deterministic transforms of human footage: **1**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
