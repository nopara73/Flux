# Flux

**A whole-body workout for exactly the time you have.**

Flux is a zero-admin workout app. Choose a duration, receive one standing
exercise at a time, then keep or discard each movement. Exercises require no
equipment by default; a compact or tall mirror can be declared available as an
optional modifier. Flux
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
30-minute workout addresses all 30 leaves individually. Groups are scheduled
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
2. give a fresh hard keep, or a suitable highest-score fresh hard exercise, a
   hard-work opportunity;
3. retain contextual keeps, except demand-`1` and demand-`2` keeps whose
   primary muscle is still inside the applicable recovery window;
4. prefer exercises with better saved keep/discard scores;
5. prefer work outside its recovery window, then fresh hard work and its
   longest-rested primary muscle;
6. preserve a valid existing selection when the higher priorities tie;
7. apply the soft Mirror preference;
8. prefer primary ownership and then wider coverage of the target group;
9. randomize only otherwise equivalent choices.

The priority weights are constructed so that all lower priorities combined
cannot outweigh a higher one. Global assignment also prevents an early greedy
choice from consuming the only exercise capable of filling a later group.

### Personalization stays inside anatomical guardrails

Tap the large **heart** during the final rest for an exercise sequence to retain
the whole sequence. Let that decision rest expire, or press **Next** during any
block, to reject it. Rejection decreases each distinct exercise identity in the
sequence once and removes saved copies of the sequence; keeping creates one
durable sequence preference. Intermediate block rests do not score or offer the
heart.

When duration or modifiers change, Flux remaps the whole lineup and maximizes
the useful saved preferences that can occupy legitimate slots. A kept exercise
is preserved across Android and web deployments as long as that exercise still
exists in the catalog. Explicit rejection or semantic removal from the catalog
is what releases it, but a keep is a contextual preference rather than a lock.
It never changes the exercise's saved user score.

At the start of every session, the global assignment carries keeps
contextually. A fresh hard keep, or a fresh suitable hard exercise already in
the highest available score bucket for that slot, gets an opportunity ahead of
a non-hard keep. During recovery, an affected demand-`1` or demand-`2` keep
loses only its current lineup preference and remains saved; ordinary score
ordering continues to prevent a rejected lower-score exercise from returning.
A separate soft per-muscle workload budget may rebalance unkept selections.
Demand-`0` exercises consume no muscular budget. Demand-`1` exercises add 0.5
unit to their primary muscle and nothing to secondary associations. Demand-`2`
exercises add 1 unit to their primary muscle and 0.5 to each distinct secondary
association. These weights determine accumulated load; the resulting excess is
then applied temporarily while comparing candidates associated with that
muscle.
Every 0.5 unit above 5 produces a 0.5 temporary candidate downvote for the
affected muscle; these adjustments exist only while completing that lineup and
never alter saved scores. Each distinct exercise identity counts once per
sequence set: two side or direction blocks belonging to the same exercise do
not double-count bilateral muscle work, while a genuinely different linked
exercise does. Repeating the complete sequence in another set counts the work
again. A muscle is never counted twice merely because it appears more than once
in one exercise's metadata.

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

Flux currently provides four composable modifiers:

- **Hard Floor**, enabled by default and shown first, admits only movements
  reviewed as ergonomic on an ordinary rigid floor. Turning it off selects a
  stable soft floor and relaxes that restriction;
- **Silence**, enabled by default, admits only naturally quiet movements;
- **Insect** favors demonstrations that keep most of the body visibly and
  continuously moving at a useful pace;
- **Mirror**, disabled by default, cycles through no mirror, compact mirror, and
  tall mirror. A compact mirror shows roughly the upper body; a tall mirror can
  show the full body. Mirror relevance breaks only otherwise remaining ties,
  after real scores, hard-work rotation, contextual keeps, and the muscle
  budget.

Mirror availability affects exercise eligibility and selection only. It never
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

For every modifier pair, duration, and workout group, the catalog must provide
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

A separate materiality test prevents placebo modifiers. Hard Floor, Insect,
and Silence must remove at least five exercises or 5% of the previous candidate
pool, whichever is larger. Mirror must actually prefer at least that many
compatible exercises for compact and tall equipment independently. Each
modifier must affect at least 10% of the canonical buckets, both alone and with
its paired modifier enabled. The current 71 `BenefitsGreatly` assignments are
an audited result, not a target or ceiling. Ordinary form checking never
qualifies, and relationship labels cannot be promoted to satisfy coverage or
materiality checks. Hard-floor classifications and their review criteria are
recorded in [`docs/HARD_FLOOR_COMPATIBILITY_AUDIT.md`](docs/HARD_FLOOR_COMPATIBILITY_AUDIT.md).

These guarantees grow quadratically with the number of modifiers. They prove
single and pairwise behavior, not arbitrary intersections of three or more
future modifiers.

### Atomic exercise sequences

Flux schedules one-block and multi-block exercise sequences as its only workout
unit. A block is always 45 seconds of the exact named exercise followed by 15
seconds of rest. Blocks in one sequence are adjacent, cannot be divided between
sessions, and share one Keep/reject decision after the final block.

Long-workout extra sets are allocated in set-count rounds. Within the same
round, one-block sequences are preferred before multi-block sequences; hard
work, Keep status, and workout order then break ties. This gives standalone
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

Every retained catalog record has an explicit scheduling verdict in
`tools/ExerciseSequences.psd1`: it is either a member of exactly one mandatory
sequence or is deliberately listed as standalone. Generation rejects implicit
standalone defaults, orphans, overlaps, and hidden members used as roots. The
current semantic audit yields 395 schedulable roots from 450 exercise records:
245 one-block, 107 two-block, 26 three-block, 16 four-block, and one five-block
root. Forty-seven roots couple multiple named exercise records, including 17
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
exercise sequence: each equal segment is one real 45-second work block, its
blue/red/chartreuse accent comes from that block's actual side or direction
cue, and an external playhead marks the current block without changing the
segment. Repeated sets repeat their real segment pattern; the 15-second rests
remain in the separate rest timer and never appear as timeline segments or
gaps. The main header counter counts each selected sequence once, so it remains
unchanged across that sequence's sides, directions, linked exercises, and
repeated sets. The workout controls follow a media-player model: shuffle
rejects an unstarted sequence with the same -1 vote as Next and replaces it in
place, play starts, pause/resume controls the active movement or rest timer,
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

Flux ships with 450 reviewed movements spanning compound strength and
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
- require no wall, chair, floor work, prop, partner, or equipment other than a
  physical mirror explicitly gated by the Mirror modifier; travel is permitted
  only when intrinsic to the named exercise;
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
[DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md).

## Persistence and upgrades

Duration, modifier profile, lineups, keeps, scores, active progress, active
movement checkpoints, and pending rest are stored locally. Modifier combinations
retain separate stable lineups while sharing durable keeps.

Every workout started after audit logging was introduced also has one durable,
append-only local session record. It snapshots the start/end time, duration,
modifiers, starting Keep set and lineup, every pre-start Shuffle, every actually
completed 45-second block, and every final Keep/reject decision. Block snapshots
include the exercise name, demand, primary and secondary canonical muscles,
side/direction/media cues, and sequence/set position; decisions include the
starting score and completed versus planned block count. Completed and
interrupted records are finalized idempotently and never mutate scores. Because
history stores snapshots as well as IDs, later catalog edits cannot rewrite what
happened. This makes exact hard-block and prior-session Keep-repeat comparisons
possible; sessions completed before this version cannot be reconstructed.
Movement start and resume checkpoints use Android's non-blocking preference
apply so the Play control responds without waiting for a disk flush; pausing,
finishing, and score-changing actions still commit synchronously.

The Android catalog uses SQLite schema version 68. Catalog migrations distinguish
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
