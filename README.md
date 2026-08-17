# Flux

Flux is a private, offline Android workout app written in C# with .NET for
Android. It targets Android 7.0 (API 24) and newer.

## Exercise database

The app ships with a local SQLite database seeded from 418 reviewed exercises.
Movements are selected for their value first, then assigned on a best-effort
basis to a 30-leaf canonical muscle taxonomy. Each exercise has one primary
scheduling group plus every secondary group it meaningfully trains. Full-body
exercises remain eligible wherever their hardest work fits. For every unordered
pair of modifiers and every workout bucket, all four real UI states (on/on,
on/off, off/on, and off/off) must contain at least five exercises that
meaningfully cover at least half of the bucket's canonical leaves. Off always
means that the corresponding requirement is relaxed. A separate materiality
check requires each modifier to remove at least five exercises or 5% of the
prior candidate pool, whichever is larger, across at least 10% of the canonical
buckets, both alone and when either member of a pair is already enabled. These
checks grow quadratically as modifiers are added instead of requiring a full
higher-order power-set quota. They do not claim that an untested three-or-more-
modifier intersection is feasible. Primary ownership is preferred after
score/keep priority; truthful
secondary associations remain eligible. Every supported duration/profile
combination must still admit a distinct exercise for every scheduled group.

The canonical leaves roll up explicitly into seven mass-ordered workout
resolutions: 3, 5, 7, 10, 15, 20, or 30 groups. Every workout schedules those
groups from the smallest to the largest estimated bilateral skeletal-muscle
mass, using the fixed hierarchy in
`Flux/Services/MassGroupingTaxonomy.cs`; the buckets are practical nominal
targets, not claims that every indivisible muscle has an exact percentage.

An exercise contains a stable numeric ID, unique name and MP4, primary and
secondary canonical groups, practice and movement metadata,
repetition-or-hold mode, continuous, alternating, or timed-unilateral playback,
score, and explicit movement-constraint metadata. Opposite movement directions are separately named,
demonstrated, scored exercises joined by a reciprocal catalog link; direction
never adds another timer phase inside an exercise.

Every retained movement:

- is demonstrated by an actual person;
- is bilateral/symmetric, naturally alternates, or completes both sides through
  the reviewed 20-second / 5-second change / 20-second protocol;
- keeps all ground contact at the feet;
- works in ordinary shoes or barefoot;
- fits inside 2 m × 2 m;
- needs no wall, chair, floor work, prop, partner, or equipment;
- is naturally quiet when the default-on Silence modifier is enabled; established
  impact movements are eligible only when Silence is explicitly disabled.

All 418 MP4 demonstrations are bundled for offline use and contain no audio.
The catalog's `silent` field describes the sound naturally produced by performing
the exercise, not the demonstration file's audio track. Holds loop as previews,
then play once and remain on a reviewed final-pose image during the exercise
timer. Android cache filenames and web media URLs use the packaged file's SHA-256
fingerprint, so an updated demonstration cannot be mistaken for an older cached
file. Reproducible GIF intermediates are excluded from the APK.

Database schema version 55 stores canonical assignments, modifier metadata,
assignment roles, side-sequence and direction-partner metadata, and every resolution roll-up in
normalized tables. The v42 migration relaxes the historical all-quiet schema
constraint; subsequent metadata migrations preserve scores for unchanged
exercise identities. See
[EXERCISE_CATALOG.md](EXERCISE_CATALOG.md) and
[DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md) for the catalog rules and
verified counts.

## Workout flow

The opening screen selects 3, 5, 7, 10, 15, 20, 30, 45, 60, or 90 minutes and
has Insect and Silence modifier tiles. Silence defaults on; turning it off
relaxes only the natural-noise requirement and never forces a noisy exercise.
It does not mute
Flux's start, side-change, rest, or completion whistles. Modifier choices,
duration, keeps, scores, and muscle coverage form one selection context. The
screen defaults to the last choices, or 10 minutes with Silence on at first use;
unsupported legacy values migrate safely. Each minute is one
smallest-to-largest mass-ordered rolled-up group: 45 seconds of exercise and a
15-second rest/decision window.

Press **Start** to begin a round. The quiet **Skip** action records the current
exercise as not kept and advances immediately, bypassing both its remaining
movement time and rest. Naturally alternating and bilateral movements use a
continuous 45-second timer. Unilateral movements use 20 seconds on the
demonstrated side, a wordless 5-second blue change phase, then 20 seconds on the
mirrored side. Red tint always means movement and blue tint means change/rest;
the tint fills the workout canvas while the human demonstration stays untinted.
Before Start, a strong `ALTERNATING` label identifies movements that switch sides
inside that uninterrupted phase, while `UNILATERAL` identifies movements that use
the timed side protocol. In workouts longer than 30 minutes, an available linked
opposite-direction exercise is added before either side timer is extended; only
then does Flux use full 45 / 15 / 45 side timing, followed by repeated sets.
There is no separate rest skip. During a normally reached rest, tap
**Tap to keep** to retain the current exercise and advance immediately. If rest
expires without a tap, its integer score drops by one and it is replaced for the
next workout by an exercise from the same active rolled-up group. A candidate
must train at least half of that bucket's canonical leaves. Flux then chooses
the highest score, prefers primary ownership, prefers the widest in-bucket
coverage, and randomizes exact ties. Every workout uses one
distinct exercise per group.
Progress, rest state, last-used resolution, and scores persist locally.

If Flux is closed or killed during a workout, the next cold launch applies all
completed keep/not-keep results (including a pending rest choice), performs the
required replacements, and returns to the duration selector. An exercise that
never reached its rest phase is left unchanged. Briefly backgrounding the same
live activity continues its current workout normally.

## Catalog tools

Regenerate the catalog and runtime MP4s from reviewed source media:

```powershell
.\tools\Generate-ExerciseCatalog.ps1 -OutputRoot .\Flux\Assets -Force
```

Verify catalog assignments, MP4 codec/dimensions/silence, and every hold target:

```powershell
.\tools\Test-ExerciseVideos.ps1
```

Regenerate the human-media and muscle-group report:

```powershell
.\tools\Write-DemonstrationAudit.ps1
```

## Build and run

```powershell
dotnet build .\Flux.slnx
adb devices
dotnet build .\Flux\Flux.csproj -t:Run -f net10.0-android
```

USB debugging must be enabled and authorized on the connected phone. The debug
build uses the automatically generated development signing key, which is
appropriate for this private installation.

To create a release APK:

```powershell
dotnet publish .\Flux\Flux.csproj -c Release -f net10.0-android
```

The signed APK is written below `Flux/bin/Release/net10.0-android/publish/`.
