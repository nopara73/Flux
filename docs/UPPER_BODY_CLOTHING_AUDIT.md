# Upper-body clothing audit

Upper-body clothing is the first workout-context toggle and defaults to ON.
It describes the user's physical setup; it does not score, prefer, or restyle
an exercise.

- **ON:** `ClothingRequired` and `Agnostic` exercises are selectable.
- **OFF:** `BareUpperBodyRequired` and `Agnostic` exercises are selectable.
- `Unreviewed` is an internal fail-closed state and is forbidden in the
  generated production catalog.

The exact animation and tooltip text is `upper-body clothing ON` and
`upper-body clothing OFF` on Android and web.

## Review standard

Every retained catalog ID appears exactly once in
`tools/ExerciseUpperBodyClothingRequirements.psd1`. Classification follows the
packaged demonstration and the movement's real execution, not its name or a
coverage target.

`ClothingRequired` is reserved for a movement where the demonstrated upper
torso or upper back bears against a wall or floor and ordinary upper-body
clothing is needed for practical skin comfort. Contact through only hands,
forearms, elbows, feet, or lower limbs does not qualify.

`BareUpperBodyRequired` is reserved for a practice whose purpose depends on
directly seeing the upper body's musculature. Ordinary optional form checking,
mobility, facial-expression work, and mirror use do not qualify.

Everything else is `Agnostic`. Categories are semantic audit results and may
not be inflated to satisfy modifier coverage.

## Current audited partition

- `ClothingRequired` (6): Wall Sit (134), Wall Squat (137), Wall Sit March
  (175), Wall Shoulder Slides (579), Wall Angel (580), and Wall Roll-Down
  (801).
- `BareUpperBodyRequired` (5): the five mirror bodybuilding pose holds,
  IDs 524-528.
- `Agnostic` (488): every other retained exercise.
- `Unreviewed` (0).

The generator rejects missing IDs, duplicate IDs, unknown IDs, incomplete
partitions, and sequence roots whose members disagree on the requirement.
Android catalog tests and the web parity contract pin the same IDs and counts.

## Coverage and persistence

Upper-body clothing participates in the existing quadratic pairwise catalog
audit at every workout resolution and muscle bucket. Wall remains outside that
pairwise system under its separate singleton rules. Clothing is not included
in the restrictive-modifier materiality calculation because neither state is a
simple relaxation of the other; the pairwise audit still exposes every genuine
availability deficit.

Stored modifier-specific lineups are copied to the new default-ON profile
during migration, so existing keeps and phase-specific rejection history remain
usable. The saved next-workout preference becomes ON. An already active legacy
workout keeps its original modifier snapshot and timing checkpoints rather than
having its physical setup silently changed during an app update.
