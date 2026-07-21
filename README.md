# Flux

A minimal native Android app written in C# with .NET for Android. It targets
Android 7.0 (API 24) and newer.

## Exercise database

Flux ships with a local SQLite database seeded from a bundled catalog of 1,000
exercises: exactly 100 for each `DominantRegion` enum value. It works entirely
offline and saves each exercise's score in SQLite.

The entries are distinct established standing movements rather than generated
tempo/range/side permutations. See [EXERCISE_CATALOG.md](EXERCISE_CATALOG.md)
for the catalog rules and reference families. Every movement is symmetric or
completes both sides inside its single loop; the timer never needs a midpoint
side switch.

Every exercise contains:

- a stable numeric ID
- a unique name
- its own bundled H.264 MP4 demonstration
- exactly one dominant region
- its practice family and movement-specific animation profile
- an explicit repetition-or-hold mode and, for holds, a reviewed target frame
- an integer score initialized to `0`
- explicit constraint metadata: only feet touch the ground, shoe agnostic,
  no more than 3 m × 3 m of space, no equipment, and silent

Both the generator and the app validate the catalog invariants. SQLite also
enforces the movement constraints with `CHECK` constraints. The source catalog
and MP4s can be regenerated from the reviewed source media with:

```powershell
.\tools\Generate-ExerciseCatalog.ps1 -OutputRoot .\Flux\Assets -Force
```

Verify all 1,000 runtime videos—including codec, dimensions, silence, duration,
and every hold's final frame—with:

```powershell
.\tools\Test-ExerciseVideos.ps1
```

Animation accuracy is tracked separately from catalog validity. The current
high-bar review verifies 300 demonstrations, including 83 human-footage loops,
and lists all 700 remaining
placeholders in [DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md). Regenerate
that report after catalog changes with:

```powershell
.\tools\Write-DemonstrationAudit.ps1
```

Database schema version 2 replaces the earlier synthetic modifier catalog.
Because those obsolete names have no real-record equivalent, upgrading resets
their scores and repairs the saved workout lineup from the new catalog.
Schema version 3 applies the bilateral catalog names and reviewed media while
preserving every score by stable exercise ID. Any saved lineup that references
a replaced name is repaired automatically on launch.
Schema version 4 adds explicit hold metadata while preserving scores and the
current lineup. The current catalog contains 67 symmetric holds and 933
repetition exercises.
Schema version 5 migrates the runtime media field from GIF to MP4 while
preserving scores by stable exercise ID. The reproducible GIF intermediates and
hold PNGs remain generator inputs but are excluded from the APK.

## Workout flow

Each workout contains one randomly selected exercise from every dominant region,
in enum order from `FEET` through `CORE`. Press **Start** to begin a 60-second
timer, or **Skip** to finish the timer immediately while testing. After each
timer, record the result with **X**, **−**, or **✓**.

Repetitions keep their MP4 looping throughout the timer. Holds are labeled
**HOLD**: their MP4 loops only as a preview, then plays once into the reviewed
target pose and remains frozen there for the rest of the countdown.

The current lineup and outcomes are saved locally with Android shared
preferences. Scores are saved in SQLite. An **X** reduces that exercise's score
by one and replaces it next session. If there were no X results, one randomly
chosen neutral exercise is replaced. An all-tick workout keeps the complete
lineup. Every replacement is selected randomly from the highest-score bucket in
the same dominant region.

## Build

From this directory:

```powershell
dotnet build .\Flux.slnx
```

## Run on your phone

1. On the phone, enable **Developer options** and **USB debugging**.
2. Connect it by USB and accept the debugging prompt on the phone.
3. Check that the phone is visible:

   ```powershell
   adb devices
   ```

4. Build, install, and launch the debug app:

   ```powershell
   dotnet build .\Flux\Flux.csproj -t:Run -f net10.0-android
   ```

The debug build uses the automatically generated development signing key. That
is suitable for installing the app on your own phone during development.

## Create an APK

```powershell
dotnet publish .\Flux\Flux.csproj -c Release -f net10.0-android
```

Release APKs are written under
`Flux\bin\Release\net10.0-android\publish\`. The generated
`com.local.flux-Signed.apk` is ready to install on the connected phone with:

```powershell
adb install -r .\Flux\bin\Release\net10.0-android\publish\com.local.flux-Signed.apk
```
