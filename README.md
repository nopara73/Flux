# Flux

A minimal native Android app written in C# with .NET for Android. It targets
Android 7.0 (API 24) and newer.

## Fake exercise data

The app currently uses an in-memory `FakeExerciseDatabase`, so there is no
database package, schema migration, or persistence yet. It creates 100 fake
exercises at startup: exactly 10 for each `DominantRegion` enum value.

Every exercise contains:

- a unique name
- the shared, looping animated `exercise_placeholder.gif` asset
- exactly one dominant region
- an integer score initialized to `0`

The fake database validates these invariants when it is created. The
`IExerciseDatabase` interface is the replacement seam for a persistent database
later.

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
`Flux\bin\Release\net10.0-android\publish\`. Android requires release
packages to be signed before normal installation; for personal development,
the debug install command above is the simplest workflow.
