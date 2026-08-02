# Flux

Flux is a private, offline Android workout app written in C# with .NET for
Android. It targets Android 7.0 (API 24) and newer.

## Exercise database

The app ships with a local SQLite database seeded from 236 reviewed exercises.
Movements are selected for their value first, then assigned on a best-effort
basis to one or more of these ordered muscle groups:

1. Glutes
2. Core
3. Quadriceps
4. Hamstrings
5. Upper back
6. Shoulders
7. Chest
8. Lower back
9. Calves
10. Hip flexors
11. Adductors
12. Abductors
13. Mid back
14. Trapezius
15. Forearms
16. Triceps
17. Biceps
18. Rotator cuff
19. Neck
20. Shins

Full-body exercises remain eligible wherever their hardest work fits. Every
muscle group has at least 10 choices. An exercise contains a stable ID, unique
name and MP4, one or more muscle groups, practice and movement metadata,
repetition-or-hold mode, score, and explicit movement-constraint metadata.

Every retained movement:

- is demonstrated by an actual person;
- is bilateral/symmetric or completes both sides in one uninterrupted loop;
- keeps all ground contact at the feet;
- works in ordinary shoes or barefoot;
- fits inside 3 m × 3 m;
- needs no wall, chair, floor work, prop, partner, or equipment;
- avoids jumping, stomping, clapping, and vocalization.

All 236 MP4 demonstrations are bundled for offline use. Holds loop as previews,
then play once and remain on a reviewed final-pose image during the exercise
timer. Reproducible GIF intermediates are excluded from the APK.

Database schema version 13 stores muscle assignments in a normalized
many-to-many table. Upgrading preserves the score of each stable exercise whose
ID and name are unchanged. See [EXERCISE_CATALOG.md](EXERCISE_CATALOG.md) and
[DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md) for the catalog rules and
verified counts.

## Workout flow

The opening screen selects a 3–20 minute workout. It defaults to the last choice,
or 10 minutes on first use. Each selected minute is one ordered muscle-group
round: 45 seconds of exercise and 15 seconds of rest. Selection always uses the
prefix above, so a five-minute workout ends with Upper back and a 20-minute
workout ends with Shins.

Press **Start** to begin a round or **Skip** to finish its exercise timer
immediately while testing. During rest, tap **Tap to keep** to retain the current
exercise. If rest expires without a tap, its integer score drops by one and it
is replaced for the next workout by a random exercise from the highest-score
bucket in the same muscle group. New lineups avoid duplicate exercises whenever
the group pools allow it, without replacing a kept slot merely to enforce
uniqueness. Progress, rest state, last-used duration, and scores persist locally.

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
