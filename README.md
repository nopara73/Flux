# Flux

A minimal native Android app written in C# with .NET for Android. It targets
Android 7.0 (API 24) and newer.

## Exercise database

Flux ships with a local SQLite database seeded from a human-demonstrated catalog
of 183 exercises. Every `DominantRegion` has at least three choices; the current
region counts range from 5 to 64. It works entirely offline and saves each
exercise's score in SQLite.

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

Verify all 183 runtime videos—including codec, dimensions, silence, duration,
and every hold's final frame—with:

```powershell
.\tools\Test-ExerciseVideos.ps1
```

The bundled catalog contains no placeholder, synthetic, schematic, anatomical,
or 3D media. All 183 demonstrations show an actual person: 155 direct footage
clips plus 28 exact human-footage derivatives. The source-quality and region
counts are recorded in [DEMONSTRATION_AUDIT.md](DEMONSTRATION_AUDIT.md).
Regenerate that report after catalog changes with:

```powershell
.\tools\Write-DemonstrationAudit.ps1
```

Database schema version 2 replaces the earlier synthetic modifier catalog.
Because those obsolete names have no real-record equivalent, upgrading resets
their scores and repairs the saved workout lineup from the new catalog.
Schema version 3 applies the bilateral catalog names and reviewed media while
preserving every score by stable exercise ID. Any saved lineup that references
a replaced name is repaired automatically on launch.
Schema version 6 refreshes the latest reviewed names, media, and hold metadata
while continuing to preserve scores by stable exercise ID.
Schema version 4 adds explicit hold metadata while preserving scores and the
current lineup.
Schema version 5 migrates the runtime media field from GIF to MP4 while
preserving scores by stable exercise ID. The reproducible GIF intermediates and
hold PNGs remain generator inputs but are excluded from the APK.
Schema version 8 removes the unverified placeholder and weaker schematic rows,
preserving scores for all retained IDs and repairing any retired saved lineup.
Schema version 9 corrects reviewed hold targets while preserving scores.
Schema version 10 removes every non-human demonstration, preserving scores for
all 183 retained IDs and repairing any retired saved lineup. The current catalog
contains 18 symmetric holds and 165 repetition exercises.

## Workout flow

Each workout contains one randomly selected exercise from every dominant region,
in enum order from `FEET` through `CORE`. Press **Start** to begin a 60-second
timer, or **Skip** to finish the timer immediately while testing. Each exercise
is followed by a 10-second rest. Tap the large **Tap to keep** button during the
rest to retain that exercise; otherwise the rest expires automatically.

Repetitions keep their MP4 looping throughout the timer. Holds are labeled
**HOLD**: their MP4 loops only as a preview, then plays once into the reviewed
target pose. Flux displays the reviewed target frame explicitly for the rest of
the countdown, avoiding device-specific `VideoView` end-frame behavior.

The current lineup, rest deadline, and keep choice are saved locally with
Android shared preferences. Scores are saved in SQLite. If the rest expires
without a tap, that exercise's score is reduced by one and the exercise is
replaced next session. Tapping keeps both its score and lineup slot unchanged.
Every replacement is selected randomly from the highest-score bucket in the
same dominant region.

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
