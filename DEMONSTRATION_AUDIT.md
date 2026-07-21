# Flux demonstration quality audit

Flux now ships a strictly human-demonstrated exercise catalog.
All **183** bundled exercises show an actual person performing the movement.
Synthetic, schematic, anatomical, and 3D demonstrations are excluded from
both the runtime catalog and the application package.

| Region | Retained exercises |
| --- | ---: |
| FEET | 8 |
| LEGS | 64 |
| HANDS | 14 |
| ARMS | 8 |
| HEAD | 24 |
| SHOULDERS | 25 |
| HIPS | 18 |
| CHEST | 5 |
| BACK | 7 |
| CORE | 10 |

## Retained source quality

- Direct human-footage demonstrations: **155**
- Exact copies of human footage: **26**
- Exact deterministic transforms of human footage: **2**

Copy and transform targets are retained only when their reviewed source is
human footage and the target movement has identical mechanics. This rule is
validated by the catalog generator and this audit script.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
