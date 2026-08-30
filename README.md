# Flux

**A whole-body workout for exactly the time you have.**

Flux is a zero-admin workout app. Choose a duration, receive one standing
exercise at a time, then keep or discard each movement. Exercises require no
equipment by default; an ordinary wall and a compact or tall mirror can be
declared available as optional modifiers. Flux
uses those decisions to shape later sessions without allowing preference,
randomness, or filters to destroy anatomical coverage.

Try the web app: [nopara73.github.io/Flux](https://nopara73.github.io/Flux/)

Flux is also a private, offline Android app written in C# with .NET for Android.
It supports Android 7.0 (API 24) and newer. No account is required.

## The Flux method

Flux randomizes exercises, not workout structure.

### Duration changes anatomical resolution

The catalog uses 30 canonical muscle groups. Flux explicitly rolls those leaves
up into seven complete anatomical partitions containing 3, 5, 7, 10, 15, 20,
or 30 workout groups. Every canonical leaf belongs to exactly one group at each
resolution.

Choosing a shorter session therefore coarsens the body map instead of cutting
the end off a longer routine. A 5-minute workout has five broad targets; a
30-minute workout addresses all 30 leaves individually. Selected exercise
sequences are scheduled first by muscular demand: demand `0`, then demand `2`,
then demand `1`. Within each demand tier, groups retain their existing order
from smaller to larger estimated bilateral skeletal-muscle mass.

The supported workout durations are 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90
minutes. Durations above 30 begin with the 30-group resolution and spend the
remaining time according to the expansion rules below.

### An exercise must meaningfully cover its target

Each exercise has one primary canonical group and every secondary group it
meaningfully trains. An exercise may represent a rolled-up workout group only
when it trains at least half of that group's canonical leaves. Primary ownership
is preferred, but a truthful secondary association remains valid.

This prevents a broad target such as a body region from being satisfied by a
movement with only a token association to one small part of it.

### The lineup is solved as one constrained assignment

Flux does not select each round independently. It solves the complete lineup as
a maximum-weight atomic assignment between workout groups and eligible exercise
sequences. Most sequences occupy one base group. A sequence whose consecutive
blocks genuinely have primary muscles in different workout groups may occupy all
of those groups together, provided its complete sequence meaningfully covers
each claimed group and fits the exact workout duration.
Catalog records that are merely repetition, hold, or naming variants of the
same demonstrated movement share an explicit `sessionMovementId`; only one of
those aliases may occupy the base lineup. Every block of a selected sequence
remains adjacent even when that shifts the otherwise mass-ordered muscle-group
schedule. Extra side or direction blocks with the same primary muscle consume
time but do not pretend to fill another muscle slot. The strict reviewed identity families
live in `tools/ExerciseSessionMovements.psd1`, and generation plus deployment
audits reject missing, overlapping, or anatomically inconsistent mappings.

The optimizer applies these priorities lexicographically:

1. preserve a valid in-progress lineup while restoring an active workout;
2. on every fourth consecutive local-calendar training day, prefer complete
   demand-`0` sequences from each slot's highest available score bucket; this
   outranks non-demand-`0` Keeps but never reaches into a lower score bucket;
3. give a fresh hard keep, or a suitable highest-score fresh hard exercise, a
   hard-work opportunity;
4. retain contextual keeps, except demand-`1` and demand-`2` keeps whose
   primary muscle is still inside the applicable recovery window;
5. prefer exercises with better saved keep/discard scores;
6. prefer work outside its recovery window, then fresh hard work and its
   longest-rested primary muscle;
7. preserve a valid existing selection when the higher priorities tie;
8. apply the soft available-equipment preference for Wall and Mirror;
9. prefer primary ownership and then wider coverage of the target group;
10. randomize only otherwise equivalent choices.

The priority weights are constructed so that all lower priorities combined
cannot outweigh a higher one. Global assignment also prevents an early greedy
choice from consuming the only exercise capable of filling a later group.

### Personalization stays inside anatomical guardrails

Tap the large **heart** during the final rest for an exercise sequence to retain
the whole sequence. Let that decision rest expire, or press **Next** during any
block, to reject it. Keeping stores the sequence root against its stable
anatomical workout slot. A rejection applies one `-1` adjustment to the
sequence root only in the phase where the rejection occurs: blocks 1–15
are **warmup**, blocks 16–45 are **peak performance**, and blocks 46 onward are
**fatigued**. It does not alter the sequence members' catalog scores or penalize
the movement in either of the other two phases.
Intermediate block rests do not score or offer the heart.

Modifier profiles share a logical slot, so a Keep made with Insect off remains
the Keep for that same slot with Insect on whenever the exercise is eligible.
When the duration changes anatomical resolution, Flux maps a Keep through the
kept sequence's primary canonical muscle into the corresponding slot at the new
resolution. A multi-block sequence still stores one preference at its anchor;
the other groups it covers cannot borrow that preference. Keeps survive Android
and web deployments while their exercise remains valid, but remain soft
preferences rather than locks. Downvotes do not follow anatomical slots or
duration resolutions at all: they follow the exercise sequence root and the
three execution phases.

At the start of every session, the global assignment evaluates anatomical Keeps
and the adjustment for each candidate's projected execution phase. A fresh hard
keep, or a fresh suitable hard exercise already in the highest available score
bucket for that phase, gets an opportunity ahead of a non-hard keep. During
recovery, an affected demand-`1` or demand-`2` keep loses only its current lineup
preference and remains saved; ordinary score ordering continues to prevent a
lower-scored exercise from returning in that phase. Historical global scores
remain a read-only migration baseline because older releases did not record
truthful phase provenance. Version-22 slot-scoped downvotes are migrated from
their workout logs when possible and otherwise projected into the phase where
that slot would have executed.

A completed workout on each of the three immediately preceding local calendar
days makes the fourth day a light day. The four-day cadence repeats inside a
longer uninterrupted daily streak, so days 4, 8, 12, and so on are light rather
than every day after day 3. Interrupted or merely prepared workouts do not
count, and multiple completed workouts on one date still count as one day. The
mode is a selection priority, not a filter: an all-demand-`0` sequence wins only
when it already belongs to the slot's highest saved-score bucket. If that bucket
has no such sequence, the normal Keep, hard-rotation, recovery, Mirror, and
anatomical priorities apply. A selected light-day alternative does not erase a
saved non-demand-`0` Keep.

A separate soft within-session rebalancer audits the resulting lineup with the
complete workload table: demand-`0` contributes 0.25 for its primary muscle and
0.125 for each distinct secondary; demand-`1` contributes 0.5 and 0.25; and
demand-`2` contributes 1 and 0.5. Each distinct exercise identity counts once
per sequence set, so two side or direction blocks of one identity do not
double-count bilateral muscle work. Different linked identities count
separately, and an actual repeated set counts again.

The audit independently rolls those canonical loads into every existing 3-,
5-, 7-, 10-, 15-, 20-, and 30-minute muscle resolution. At each resolution the
weakest bucket is compared with the strongest; the soft goal is for every
weakest bucket to reach at least 25% of its strongest. Flux repeatedly applies
one legal replacement that improves the lexicographically sorted bucket shares
(weakest first), recalculating all seven resolutions after every change. It
stops when every resolution reaches the goal, no replacement improves the
lineup, the lineup repeats, or 30 passes have run. An undersupplied catalog may
therefore remain imbalanced rather than receiving a fabricated exercise.

Selected Keeps are frozen to their anatomical placements within the prepared
lineup, and a saved Keep cannot be moved elsewhere by balancing. A candidate
must be no lower-scored than every displaced selection in the execution phase
of each slot it covers. Modifiers, recovery and hard-work priority, Mirror
preference, light-day priority, global assignment, atomic sequences, unique session movements,
exact duration, and long-workout set allocation remain constraints. The
balancing state is temporary and never changes persisted user scores or adds a
numerical hardness score.

Android and web prepare this complete constrained plan in the background while
the duration screen is visible. Pressing Start only activates the prepared plan
and timestamps its workout log, so selection work cannot block the Start tap.
Changing duration or a modifier discards the stale plan and prepares the newly
selected profile instead. During an active workout, the setup control pauses the
current movement or rest and reuses this screen with duration locked. Modifier
changes preserve completed work. A compatible atomic selection currently on
screen is not automatically protected: the unfinished current slot is selected
again using the complete target modifier profile. If that profile independently
chooses the same exercise, its checkpoint and allocated sets survive. If it
chooses another exercise because of either a restriction or a newly enabled
equipment preference, the replacement appears immediately in Ready state with a
full timer. A block already completed and resting is not rewritten retroactively.
Remaining unstarted selections are rebuilt, and the workout itself is never
restarted.

The catalog also carries a separate reviewed `muscularDemand` value for every
exercise. `0` means muscular loading is incidental, `1` means meaningful but
not locally limiting work, and `2` means hard muscular work whose force or
fatigue is expected to limit a continuously performed 45-second round. This
metadata never overwrites or masquerades as the user's persisted preference
`score`.

Completing a `muscularDemand` `1` exercise records meaningful muscular work for
its primary canonical muscle. For the next rolling 18 hours, same-score
demand-`0` work is preferred to every demand-`1` exercise with that primary
muscle. Completing a demand-`2` exercise starts this 18-hour window as well as
the separate 36-hour hard-work window. During the latter, same-score non-hard
work is preferred to every demand-`2` exercise with that primary muscle.

Both windows are soft preferences, never exclusions or score changes. A
higher-score recovering exercise remains selectable. Once hard work is fresh,
a demand-`2` exercise whose primary muscle belongs to the current workout group
outranks a same-score non-hard keep and the soft Mirror preference. Recovering
keeps remain saved for later. Among otherwise equivalent fresh hard choices,
the primary muscle that has gone longest without completed hard work wins,
allowing short workouts to rotate hard opportunities across their canonical
muscles. Only a completed movement reaching rest records muscular work;
shuffle, skip, and repeat do not.

### Modifiers are not allowed to break the workout

Flux currently provides five composable modifiers, in this UI order:

- **Hard Floor**, enabled by default and shown first, means one combined
  surface condition: rigid **and slippery**. It admits only movements reviewed
  for both impact comfort and low-traction execution. Turning it off selects a
  stable soft floor and relaxes both restrictions;
- **Insect** favors demonstrations that keep most of the body visibly and
  continuously moving at a useful pace;
- **Silence**, enabled by default, admits only naturally quiet movements;
- **Wall**, disabled by default, cycles through no wall, wall available with
  soles kept off, and wall available with sole contact allowed. It admits
  established exercises that genuinely require the selected wall access and
  softly prefers them when all stronger selection priorities tie;
- **Mirror**, disabled by default, cycles through no mirror, compact mirror, and
  tall mirror. A compact mirror shows roughly the upper body; a tall mirror can
  show the full body. Mirror relevance breaks only otherwise remaining ties,
  after real scores, hard-work rotation, contextual keeps, and within-session
  muscle balance.

Wall and Mirror availability affect exercise eligibility and selection only.
Wall-required exercises remain unavailable while Wall is off, and exercises
whose demonstrated movement requires sole-to-wall contact remain unavailable
in the soles-stay-off state. Ordinary wall-agnostic exercises remain selectable
in every state. Mirror never
horizontally flips demonstration media; reviewed atomic side blocks remain a
separate exercise-sequence behavior.

Turning Hard Floor, Insect, or Silence off relaxes its requirement. Mirror
behavior is coverage-aware:

- upper-body `MirrorOnly` exercises accept compact or tall mirrors;
- full-body `MirrorOnly` exercises require a tall mirror;
- upper-body `BenefitsGreatly` exercises are preferred with either mirror;
- full-body `BenefitsGreatly` exercises remain selectable with a compact
  mirror but receive mirror preference only with a tall mirror;
- `Agnostic` exercises are unaffected.

Wall is deliberately outside the pairwise, per-muscle, and duration quota
system. It instead has two direct catalog invariants: at least 20 distinct
wall-required session movements that do not require sole contact, plus at least
five distinct sole-contact wall movements. Sides, directions, sequence blocks,
repeated sets, aliases, and names cannot multiply either count. The current
audited inventory contains 24 base-wall movements and five sole-wall movements.

For every existing non-Wall modifier pair, duration, and workout group, the catalog must provide
at least five exercises under every real state. Two binary modifiers have four
states; a pair involving Mirror has six because Mirror has off, compact, and
tall states. In a mirror-equipped state, only `MirrorOnly` and
`BenefitsGreatly` count toward that five-exercise relevance floor, after actual
equipment compatibility is applied. Separately, each of the five global mirror
classification cells—`MirrorOnly` upper/full body, `BenefitsGreatly`
upper/full body, and `Agnostic`—must contain at least five reviewed exercises.
Relationship labels are never promoted to hide a genuine gap. Every supported
duration and profile must also admit a capacity-exact atomic lineup without
reusing a session movement.

Hard Floor also has an explicit category-preservation check. For every workout
group, at least five combined hard-and-slippery-compatible and five incompatible
sequences must remain available with Insect off/on, Silence off/on, and Mirror
off. This prevents the relaxed soft-floor state from passing merely because it
contains five exercises in total while one floor category is effectively
absent. The check uses the same pairwise state model; it does not enumerate
three- or four-modifier combinations.

A separate materiality test prevents placebo modifiers. Hard Floor, Insect,
and Silence must remove at least five exercises or 5% of the previous candidate
pool, whichever is larger. Mirror must actually prefer at least that many
compatible exercises for compact and tall equipment independently. Each
modifier must affect at least 10% of the canonical buckets, both alone and with
its paired modifier enabled. The current 77 `BenefitsGreatly` assignments are
an audited result, not a target or ceiling. Ordinary form checking never
qualifies, and relationship labels cannot be promoted to satisfy coverage or
materiality checks. Hard-floor classifications and their review criteria are
recorded in [`docs/HARD_FLOOR_COMPATIBILITY_AUDIT.md`](docs/HARD_FLOOR_COMPATIBILITY_AUDIT.md).
Wall's three-state contract, separate direct floors, and reviewed 29-movement
inventory are recorded in
[`docs/WALL_EQUIPMENT_AUDIT.md`](docs/WALL_EQUIPMENT_AUDIT.md).

The 2026-08-29 full-loop integrity audit exposed real gaps after false inherited
anatomy was removed: 178 pairwise deficiencies, 112 hard-floor category
deficiencies, and one Silence materiality deficiency. That frozen baseline and
every exact deficit are retained in
[`docs/catalog-audit/modifier_coverage_deficits_2026-08-29.json`](docs/catalog-audit/modifier_coverage_deficits_2026-08-29.json);
no relationship or muscle assignment is inflated to hide them. After Hard
Floor was defined as both rigid and slippery, the current validators expose
245 pairwise deficiencies across 38 group IDs and 81 floor-category
deficiencies across 24 group IDs. The stricter filter also resolves the former
Silence materiality deficiency. The reproducible current ledger is
[`docs/catalog-audit/modifier_coverage_deficits_current.json`](docs/catalog-audit/modifier_coverage_deficits_current.json).
All supported profiles still admit a distinct atomic lineup.

The pairwise guarantees grow quadratically with the number of quota-bearing
modifiers. Wall's two direct floors remain constant-size and do not create new
pairwise edges or arbitrary all-modifier intersections.

### Atomic exercise sequences

Flux schedules one-block and multi-block exercise sequences as its only workout
unit. A block is always 45 seconds of the exact named exercise followed by 15
seconds of rest. Blocks in one sequence are adjacent, cannot be divided between
sessions, and share one Keep/reject decision after the final block.

Long-workout extra sets are allocated in set-count rounds. Within the same
round, one-block sequences are preferred before multi-block sequences; hard
work, anatomical Keep status, phase-scoped downvotes, and workout order then
break ties. This gives standalone
movements their second-set opportunity first without allowing third sets to
starve a multi-block sequence that still has only one set. Exact remaining
duration remains mandatory.

Sequence structure is derived from the demonstrated movement:

- A genuinely simultaneous bilateral or naturally alternating movement remains
  one block.
- Side-specific or fixed-lead work uses consecutive side/stance blocks.
- Direction-specific work uses consecutive direction blocks.
- A side-by-direction movement may therefore require four blocks.
- A worthwhile alternating integration may follow the two isolated sides;
  awkward or redundant variants are omitted.
- Distinct established exercises that are useful only together may be linked in
  one sequence.

Scheduling never splits an atomic sequence to obtain the demand order. A
sequence's scheduling demand is the highest `muscularDemand` among all of its
blocks, and the complete sequence moves as one unit into the corresponding
`0`, `2`, or `1` tier. Repeated sets remain adjacent to that sequence. Once a
workout has started, its resolved sequence order is frozen so restoring or
continuing it cannot move completed and remaining work around.

Every retained catalog record has an explicit scheduling verdict in
`tools/ExerciseSequences.psd1`: it is either a member of exactly one mandatory
sequence or is deliberately listed as standalone. Generation rejects implicit
standalone defaults, orphans, overlaps, and hidden members used as roots. The
current semantic audit yields 443 schedulable roots from 499 exercise records:
278 one-block, 121 two-block, 28 three-block, 15 four-block, and one five-block
root. Forty-eight roots couple multiple named exercise records, including 17
exact alternating integrations. These counts are pinned audit results, not
quotas; an awkward block must not be added merely to increase a bucket.

There is no duration-based sequence exclusion. The solver counts every block
against exact workout capacity. In a 30-minute-or-shorter workout, the number of
base groups equals the number of available blocks, so a two-block same-muscle
sequence naturally yields when it would leave another required group unfilled.
A two-block sequence with two genuinely distinct primary workout groups can fill
both slots and therefore can fit even a 3-minute workout. Larger sequences can
fit by the same rule when their real primary-muscle coverage accounts for their
blocks. Their blocks remain consecutive; only the surrounding muscle-group order
may move.

When the duration has additional capacity, remaining time is filled by repeating
complete sequences, prioritizing selected demand-2 work before kept work and
balancing set counts where an exact fit permits. The allocation is persisted when
the workout starts, so reopening the app cannot silently change the remaining
session. Recovery preferences apply between sessions, not when repeating a
selected sequence within the current one.

When several members of a sequence share the primary muscle for a workout slot,
selection and recovery priority use the hardest relevant member rather than
silently inheriting the root record's demand. Completion still records recovery
from the block actually performed.

## What a workout looks like

The first block of a sequence begins with five seconds of quiet preparation.
Every continuation block starts automatically after its preceding 15-second rest
without another preparation phase or Start action. Every block then runs for the
full 45 seconds. Intermediate rests hide the heart and remain neutral; only the
final rest presents the one shared Keep/reject decision.

Red means movement. Blue means rest or the inactive side of a side-specific
block. The workout header uses one literal execution timeline for every
exercise sequence: each equal segment is one real 45-second work block. Side
and direction sequences use their actual blue/red/chartreuse cues. An uncued
three-block sequence made from three genuinely different exercises uses a
blue/chartreuse/red identity pattern, so it cannot be mistaken for repeated sets;
extra sets repeat that same pattern. An external playhead marks the current block
without changing the segment. The 15-second rests remain in the separate rest
timer and never appear as timeline segments or gaps. The main header counter
counts each selected sequence once, so it remains
unchanged across that sequence's sides, directions, linked exercises, and
repeated sets. The workout controls follow a media-player model: shuffle
rejects an unstarted sequence with the same phase-scoped `-1` vote as Next and
replaces it in place, play starts, pause/resume controls the active movement or rest timer,
repeat restarts the current block without scoring it, and next rejects the
sequence and advances.
Shuffle preserves the session profile and replaces the complete sequence. It is
unavailable after any earlier block from that sequence has begun. Repeated
shuffles draw without repetition from every remaining session
movement valid for that anatomical slot and modifier profile; an alias of the
rejected movement is not a replacement. They do not re-rank the pool toward the
normal automatic-selection winner. Completed timer allocations stay locked
while still-unstarted allocations are safely
recomputed around the replacement. Anatomical group routing remains internal
rather than appearing as a potentially misleading exercise label. A large
heart is the shared Keep action during rest.

Repetition demonstrations run at normal speed. Hold demonstrations loop during
the preview, play once when work begins, and then remain on a reviewed target
frame for the rest of the hold. Start, side-change, rest, and completion use
distinct whistle cues; the Silence modifier controls exercise selection, not
those cues.

If movement media buffers or the app is backgrounded, movement time pauses. The
active round, exact remaining movement time, and user-pause state are committed
locally; reopening after process death returns to that movement instead of
discarding the workout. A live foreground deadline preserves elapsed time; if
that deadline expired while Flux was absent, restoration uses the last safe
checkpoint instead of crediting unseen exercise time. Running Rest uses an
absolute deadline and also restores after process death, advancing normally if
its deadline already passed. Tapping its pause control instead persists the
exact remaining Rest time with no deadline; reopening keeps it paused until the
user explicitly resumes it. During the 15-second transition inside an atomic
sequence, Rest previews the upcoming block's exact exercise, side, direction,
and media segment without advancing the saved workout; final decision Rest
continues to show the sequence that was just completed. Closing during Ready
still leaves the unreached exercise neutral.

## Exercise catalog

Flux ships with 499 reviewed movements spanning compound strength and
conditioning, mobility, dynamic balance, active range of motion,
rehabilitation-style movement, Pilates, yoga, tai chi, qigong, boxing, dance,
martial arts, breathing, and isometrics.

Every retained exercise must:

- be an established, plainly named movement or posture;
- be immediately copyable from its name and silent demonstration;
- show an actual person performing the complete natural movement;
- keep all ground contact at the feet;
- work in ordinary shoes or barefoot;
- fit inside 2 m x 2 m;
- require no chair, floor work, prop, or partner; the only admitted external
  supports are an ordinary stable wall explicitly gated by Wall and a physical
  mirror explicitly gated by Mirror; travel is permitted only when intrinsic
  to the named exercise;
- declare repetition or hold behavior and its exact side protocol;
- derive its name, timing, direction, muscle associations, crop, and hold target
  from the reviewed demonstration.

All runtime demonstrations are offline 256 x 256 H.264 MP4s with audio removed.
The catalog's `silent` field describes the sound of performing the exercise, not
the media track. Placeholder, schematic, anatomical, synthetic, and 3D
demonstrations are excluded.

Catalog generation rejects missing or duplicate identities, unknown anatomical
assignments, fake variation suffixes, incomplete modifier reviews, impossible
lineups, constraint violations, non-human media, and movements without an
explicit side decision. See [EXERCISE_CATALOG.md](EXERCISE_CATALOG.md) and
[DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md). The complete current full-loop
ledger is documented in
[`docs/CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`](docs/CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md).

## Persistence and upgrades

Duration, modifier profile, lineups, keeps, scores, active progress, active
movement checkpoints, and pending rest are stored locally. Modifier combinations
retain separate stable lineups while sharing durable keeps.

Every workout started after audit logging was introduced also has one durable,
append-only local session record. It snapshots the start/end time, duration,
modifiers, frozen light-day mode, starting Keep set and lineup, every pre-start Shuffle, every actually
completed 45-second block, every mid-workout modifier transition and resulting
future plan, and every final Keep/reject decision. Block snapshots
include the exercise name, demand, primary and secondary canonical muscles,
side/direction/media cues, and sequence/set position; decisions include the
starting score and completed versus planned block count. Completed and
interrupted records are finalized idempotently and never mutate scores. Because
history stores snapshots as well as IDs, later catalog edits cannot rewrite what
happened. This makes exact hard-block and prior-session Keep-repeat comparisons
possible; sessions completed before this version cannot be reconstructed.
Light-day detection is derived from these completed-session timestamps, so an
upgrade recognizes an existing three-day streak without fabricating a new
counter. An unstarted lineup prepared by an older build is rebuilt during that
migration; a workout already in progress is never reclassified.
Movement start and resume checkpoints use Android's non-blocking preference
apply so the Play control responds without waiting for a disk flush; pausing,
finishing, and score-changing actions still commit synchronously.

The Android catalog uses SQLite schema version 73. Catalog migrations distinguish
semantic exercise replacements from approved name, timing, and media repairs.
Unchanged identities retain their scores and valid keeps; changed identities
invalidate only affected workout state. Score changes use a small recovery
journal so an interruption between workout-state and SQLite writes cannot lose
or double-apply a rejection.
An older keep for only one member of a newly coupled sequence is discarded
rather than being promoted into a keep for work the user never chose; a complete
sequence keep preserves every member.

Packaged media is content-addressed with SHA-256 fingerprints. Android copies
videos atomically into a versioned cache, and the web build fingerprints both
runtime media and its JavaScript/CSS shell so a corrected demonstration cannot
be mistaken for an older cached file.

## Android and web parity

The Android implementation in `Flux/` is the canonical product contract. The web
build copies its catalog, exercise videos, hold frames, and audio directly from
the Android runtime assets.

Cross-platform contract tests compare duration choices, selection behavior,
keeps, migrations, timing, long-workout allocation, modifiers, media behavior,
and controls. A source hash additionally fails validation whenever relevant
Android code or resources change without a reviewed web parity update.

## Build and verify

Build the Android solution:

```powershell
dotnet build .\Flux.slnx
```

Run the Android-independent test suite:

```powershell
dotnet test .\Flux.Tests\Flux.Tests.csproj
```

Run and build the web app:

```powershell
Set-Location .\web
npm test
npm run build
```

Run the Android app on an authorized USB-debugging device:

```powershell
adb devices
dotnet build .\Flux\Flux.csproj -c Debug -f net10.0-android
adb install -r .\Flux\bin\Debug\net10.0-android\com.local.flux-Signed.apk
adb shell run-as com.local.flux pwd
```

The connected development phone must always receive this debuggable build. Use
an in-place replacement to preserve its saved workout state; never uninstall or
clear the package during deployment. A successful `run-as` check verifies that
the installed build is actually debuggable.

Create a release APK:

```powershell
dotnet publish .\Flux\Flux.csproj -c Release -f net10.0-android
```

The APK is written below `Flux/bin/Release/net10.0-android/publish/`.

## Catalog maintenance

Regenerate the catalog and runtime media from reviewed sources:

```powershell
.\tools\Generate-ExerciseCatalog.ps1 -OutputRoot .\Flux\Assets -Force
```

Verify assignments, media encoding, silence, duplicate renders, and hold targets:

```powershell
.\tools\Test-ExerciseVideos.ps1
```

Regenerate the human-media and muscle-group audit:

```powershell
.\tools\Write-DemonstrationAudit.ps1
```

## Scope

Flux is designed for broad, frequent, low-friction movement—not for maximizing
one specialized adaptation. It does not replace progressive resistance training
for maximal strength or hypertrophy, sport-specific practice, measured endurance
programming, diagnosis, rehabilitation, or individualized medical guidance.

Its narrower promise is concrete: for the exact time and constraints selected,
Flux constructs a varied workout while preserving anatomical targets, meaningful
exercise-to-target assignments, bilateral execution, and the user's durable
preferences.
