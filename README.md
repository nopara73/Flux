# Hello Android

A minimal native Android app written in C# with .NET for Android. It targets
Android 7.0 (API 24) and newer and displays **Hello, Android!**.

## Build

From this directory:

```powershell
dotnet build .\HelloAndroid.slnx
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
   dotnet build .\HelloAndroid\HelloAndroid.csproj -t:Run -f net10.0-android
   ```

The debug build uses the automatically generated development signing key. That
is suitable for installing the app on your own phone during development.

## Create an APK

```powershell
dotnet publish .\HelloAndroid\HelloAndroid.csproj -c Release -f net10.0-android
```

Release APKs are written under
`HelloAndroid\bin\Release\net10.0-android\publish\`. Android requires release
packages to be signed before normal installation; for personal development,
the debug install command above is the simplest workflow.
