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
a maximum-weight one-to-one assignment between workout groups and eligible
exercises. The result must contain one distinct exercise per base group.

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

Tap the large **heart** during rest to retain an exercise. Let rest expire, or
press **Next** during movement, to reject it. Rejection decreases that exercise's
local score once and removes saved copies of it; keeping creates a durable
preference.

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
A separate soft per-muscle workload budget may rebalance unkept selections:
each scheduled primary association counts as 1 unit and each secondary
association as 0.5.
Every 0.5 unit above 5 produces a 0.5 temporary candidate downvote for the
affected muscle; these adjustments exist only while completing that lineup and
never alter saved scores. A timed side, stance, or direction pair counts once;
linked opposite-direction exercises count separately, and genuinely repeated
rounds count again.

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

Flux currently provides three composable modifiers:

- **Silence**, enabled by default, admits only naturally quiet movements;
- **Insect** favors demonstrations that keep most of the body visibly and
  continuously moving at a useful pace;
- **Mirror**, disabled by default, cycles through no mirror, compact mirror, and
  tall mirror. A compact mirror shows roughly the upper body; a tall mirror can
  show the full body. Mirror relevance breaks only otherwise remaining ties,
  after real scores, hard-work rotation, contextual keeps, and the muscle
  budget.

Mirror availability affects exercise eligibility and selection only. It never
horizontally flips demonstration media; timed second-side playback remains the
separate side-sequence behavior.

Turning Insect or Silence off relaxes its requirement. Mirror behavior is
coverage-aware:

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
duration and profile must also admit a completely distinct lineup.

A separate materiality test prevents placebo modifiers. Insect and Silence must
remove at least five exercises or 5% of the previous candidate pool, whichever
is larger. Mirror must actually prefer at least that many compatible exercises
for compact and tall equipment independently. Each modifier must affect at
least 10% of the canonical buckets, both alone and with its paired modifier
enabled. The current 58 `BenefitsGreatly` assignments are an audited result,
not a target or ceiling. Ordinary form checking never qualifies, and
relationship labels cannot be promoted to satisfy coverage or materiality
checks.

These guarantees grow quadratically with the number of modifiers. They prove
single and pairwise behavior, not arbitrary intersections of three or more
future modifiers.

### Extra time buys completeness before repetition

For workouts longer than 30 minutes, Flux allocates each additional minute in
this order:

1. add an eligible linked opposite-direction exercise;
2. expand eligible unilateral rounds from 20 / 5 / 20 to 45 / 15 / 45;
3. add repeated sets only after direction and side opportunities are exhausted.

Within each optional side-expansion or repeated-set pass, selected demand-2
exercises receive priority over non-hard exercises. Keep status breaks ties,
followed by later groups in the mass-ordered schedule. This is an intra-session
dose decision; the 18- and 36-hour recovery preferences apply only when choosing
the next session's lineup. The allocation is persisted when the workout starts,
so reopening the app cannot silently change the remaining session.

Opposite directions are separate, plainly named, demonstrated, and scored
exercise identities connected by reciprocal links. Direction is never hidden
as another timer phase inside an exercise, avoiding ambiguous four-part drills.

## What a workout looks like

Every round begins with five seconds of quiet preparation.

- Bilateral and continuous movements run for 45 seconds.
- Naturally alternating movements switch sides inside the same 45-second phase.
- Unilateral movements run for 20 seconds on the demonstrated side, pause for a
  five-second change, then run for 20 seconds on the mirrored side.
- Fixed-lead stance movements use the same timing while cueing the demonstrated
  lead stance, a stance change, and the opposite lead stance.
- Bidirectional movements run for 20 seconds in the first direction, pause for a
  five-second direction change, then run for 20 seconds in the opposite direction.
- Expanded timed-pair rounds use 45 seconds, a 15-second change, and 45 seconds.
- Each unlinked round ends with a 15-second rest and keep/discard decision.
  Linked opposite-direction exercises are available only in workouts longer
  than 30 minutes. They always run consecutively: the first rest advances to
  the other direction without a Keep action, and the second rest presents one
  shared heart decision. Rejecting either direction rejects and
  scores both records together.

Red means movement. Blue means change or rest. A large asymmetric human icon
marks timed side or lead-stance pairs, while `BIDIRECTIONAL` identifies timed
opposite-direction pairs. The workout controls follow a media-player model:
shuffle rejects an unstarted exercise with the same -1 vote as Next and replaces
it in place, play starts, pause/resume controls the active timer, repeat restarts
the current exercise without scoring it, and next rejects it and advances.
Shuffle preserves the session profile and any linked direction pair, and is
unavailable after an earlier round from the same paired or repeated unit has
begun. Repeated shuffles draw without repetition from every remaining exercise
valid for that anatomical slot and modifier profile; they do not re-rank the
pool toward the normal automatic-selection winner. In long workouts, completed
timer allocations stay locked while still-unstarted allocations are safely
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
checkpoint instead of crediting unseen exercise time. Rest uses an absolute
deadline and also restores after process death, advancing normally if its
deadline already passed. Closing during Ready still leaves the unreached
exercise neutral.

## Exercise catalog

Flux ships with 431 reviewed movements spanning compound strength and
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

The Android catalog uses SQLite schema version 60. Catalog migrations distinguish
semantic exercise replacements from approved name, timing, and media repairs.
Unchanged identities retain their scores and valid keeps; changed identities
invalidate only affected workout state. Score changes use a small recovery
journal so an interruption between workout-state and SQLite writes cannot lose
or double-apply a rejection.

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
