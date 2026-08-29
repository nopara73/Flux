# Hard-floor compatibility audit

Hard Floor is the first workout modifier and is enabled by default. Its two UI
states are:

- **Hard floor ON:** only `Compatible` exercises are selectable.
- **Hard floor OFF:** the user has a stable soft floor, so both `Compatible` and
  `Incompatible` exercises are selectable.

"Soft" means an ordinary stable padded exercise surface or carpet. It does not
mean a mattress, unstable foam, or deep pile that compromises footing.

## Review standard

Every catalog ID must appear exactly once in
`tools/ExerciseHardFloorCompatibility.psd1`. A new or reactivated exercise is
`Unreviewed` until that explicit decision is added; generation, Android tests,
and web tests reject an incomplete partition.

An exercise is `Incompatible` when its demonstrated execution makes an
ordinary rigid floor meaningfully less ergonomic through one of these audited
mechanisms:

- concentrated heel or forefoot loading, including sustained or repeated
  calf-raise and tiptoe work;
- repeated jumping or landing;
- running or rapid foot impact; or
- deliberate stomping.

Ordinary planted standing, stepping, lunging, squatting, balance, mobility,
boxing, dance, and mirror work remains `Compatible` unless the actual
demonstration meets one of those mechanisms. Classification follows the final
packaged movement, not its name or a coverage target. Every mandatory sequence
must use one consistent floor classification across all of its blocks.

## Current result

- `Compatible`: 384 exercises
- `Incompatible`: 91 exercises
- `Unreviewed`: 0 exercises

These counts are audit results, not quotas. Pairwise availability and
materiality are validated separately against the real Hard Floor, Insect,
Silence, and Mirror UI states. The validation remains quadratic in the number
of modifiers; it does not require every state in the full modifier power set.

In addition to ordinary pairwise viability, every workout group must retain at
least five exercises from each exact floor category—`Compatible` and
`Incompatible`—with Insect off/on, Silence off/on, and Mirror off. Turning Hard
Floor off still admits both categories at runtime; the category-specific audit
exists so the larger combined pool cannot conceal a missing soft-floor-only or
hard-floor-suitable side of a pair. The complete demonstration-integrity audit
exposed **112** genuine category deficiencies across **44** group IDs. They
remain explicitly reported in
[`catalog-audit/modifier_coverage_deficits_2026-08-29.json`](catalog-audit/modifier_coverage_deficits_2026-08-29.json)
rather than being hidden with false floor or muscle classifications.
