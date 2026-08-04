# Flux

Flux is a private, offline Android workout app written in C# with .NET for
Android. It targets Android 7.0 (API 24) and newer.

## Exercise database

The app ships with a local SQLite database seeded from 328 reviewed exercises.
Movements are selected for their value first, then assigned on a best-effort
basis to a 30-leaf canonical muscle taxonomy. Each exercise has one primary
scheduling group plus every secondary group it meaningfully trains. Full-body
exercises remain eligible wherever their hardest work fits. Every workout
bucket has at least 10 primary-owned choices that meaningfully cover at least
half of its canonical leaves.

The canonical leaves roll up explicitly into seven mass-ordered workout
resolutions: 3, 5, 7, 10, 15, 20, or 30 groups. Every workout schedules those
groups from the smallest to the largest estimated bilateral skeletal-muscle
mass, using the fixed hierarchy in
`Flux/Services/MassGroupingTaxonomy.cs`; the buckets are practical nominal
targets, not claims that every indivisible muscle has an exact percentage.

An exercise contains a stable numeric ID, unique name and MP4, primary and
secondary canonical groups, practice and movement metadata,
repetition-or-hold mode, score, and explicit movement-constraint metadata.

Every retained movement:

- is demonstrated by an actual person;
- is bilateral/symmetric or completes both sides in one uninterrupted loop;
- keeps all ground contact at the feet;
- works in ordinary shoes or barefoot;
- fits inside 3 m × 3 m;
- needs no wall, chair, floor work, prop, partner, or equipment;
- avoids jumping, stomping, clapping, and vocalization.

All 328 MP4 demonstrations are bundled for offline use. Holds loop as previews,
then play once and remain on a reviewed final-pose image during the exercise
timer. Reproducible GIF intermediates are excluded from the APK.

Database schema version 17 stores canonical assignments, assignment roles, and
every resolution roll-up in normalized tables. Its additive v14/v15/v16
migrations keep all existing exercise IDs, names, demonstrations, and scores
while adding new catalog records and applying the current schedule order. See
[EXERCISE_CATALOG.md](EXERCISE_CATALOG.md) and
[DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md) for the catalog rules and
verified counts.

## Workout flow

The opening screen selects 3, 5, 7, 10, 15, 20, or 30 minutes. It defaults to
the last choice, or 10 minutes on first use; unsupported legacy values migrate
to the nearest choice. Each minute is one smallest-to-largest mass-ordered
rolled-up group: 45 seconds of exercise and a 15-second rest/decision window.

Press **Start** to begin a round or **Skip** to finish its exercise timer
immediately while testing. There is no separate rest skip. During rest, tap
**Tap to keep** to retain the current exercise and advance immediately. If rest
expires without a tap, its integer score drops by one and it is replaced for the
next workout by an exercise from the same active rolled-up group. A candidate
must own the bucket through its primary assignment and train at least half of
that bucket's canonical leaves. Flux then chooses the highest score, prefers the
widest in-bucket coverage, and randomizes exact ties. Every workout uses one
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
