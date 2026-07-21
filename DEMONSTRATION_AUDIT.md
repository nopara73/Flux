# Flux demonstration quality audit

Flux now ships a quality-first exercise catalog with no placeholder media.
All **327** bundled exercises have a reviewed, directly matching demonstration.
The 673 unverified placeholders and weaker custom schematic animations were
removed from both the catalog and the application package.

| Region | Retained exercises |
| --- | ---: |
| FEET | 15 |
| LEGS | 94 |
| HANDS | 15 |
| ARMS | 32 |
| HEAD | 24 |
| SHOULDERS | 48 |
| HIPS | 34 |
| CHEST | 17 |
| BACK | 23 |
| CORE | 25 |

## Retained source quality

- Reviewed human footage: **155**
- Other reviewed external demonstrations: **23**
- Reviewed Posecode 3D renders: **77**
- Exact semantically identical copies: **62**
- Exact deterministic transforms: **10**

The discarded custom SVG tier contained clear but comparatively weak
schematic stick-figure artwork. Keeping only footage, reviewed external
animation, reviewed 3D motion, and exact derivatives gives the app a more
consistent and legible demonstration set.

The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,
copy, and transform mappings live in the corresponding media manifests.
