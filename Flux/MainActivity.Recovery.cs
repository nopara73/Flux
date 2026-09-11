using Android.Content;
using Android.Content.PM;
using Android.Views;
using Flux.Data;
using Flux.Models;
using Flux.Services;

namespace Flux;

public partial class MainActivity
{
    private const int OuraPermissionRequest = 6321;
    private OuraRecoveryStore? _ouraStore;
    private OuraConnection _ouraConnection = new();
    private CancellationTokenSource? _ouraReadCancellation;
    private bool _ouraRefreshing;
    private bool _manualLightModeRequested;
    private ImageButton? _ouraButton;
    private string _ouraReadStatus = string.Empty;

    private bool HasOuraPermission => OperatingSystem.IsAndroidVersionAtLeast(34) &&
        OuraHealthConnectReader.HasAccess(this);

    private void InitializeOuraRecovery()
    {
        _ouraStore = new OuraRecoveryStore(this);
        _ouraConnection = _ouraStore.Load();
        _state.OuraRecovery = GetAvailableOuraSnapshot();
        _ouraButton = FindViewById<ImageButton>(Resource.Id.oura_recovery_button);
        if (_ouraButton is not null)
        {
            _ouraButton.Click += (_, _) => ShowOuraRecoveryDialog();
            _ouraButton.ContentDescription = "Recovery data: connect Oura";
        }
        UpdateOuraConnectionIcon();
    }

    private OuraRecoverySnapshot? GetAvailableOuraSnapshot() =>
        _ouraConnection.Enabled && HasOuraPermission ? _ouraConnection.Snapshot : null;

    private async Task RefreshOuraRecoveryAsync(bool force = false)
    {
        if (_ouraStore is null || _ouraRefreshing || !_ouraConnection.Enabled) return;
        if (!HasOuraPermission)
        {
            DisconnectOuraRecovery();
            _ouraReadStatus = "Access was removed. Flux is using its workout countdown.";
            return;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!force && _ouraConnection.Snapshot is { } cached &&
            now >= cached.FetchedAtUnixMilliseconds && now - cached.FetchedAtUnixMilliseconds < 300_000)
        {
            UpdateOuraConnectionIcon();
            return;
        }
        _ouraRefreshing = true;
        _ouraReadStatus = "Reading Oura sleep, heart rate and HRV…";
        _ouraReadCancellation = new CancellationTokenSource();
        CancellationToken cancellation = _ouraReadCancellation.Token;
        UpdateOuraConnectionIcon();
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(34)) return;
            OuraRecoverySnapshot snapshot = await new OuraHealthConnectReader(this).ReadAsync(cancellation);
            if (_activityDestroyed || cancellation.IsCancellationRequested || !_ouraConnection.Enabled) return;
            _ouraConnection.Snapshot = snapshot;
            _state.OuraRecovery = GetAvailableOuraSnapshot();
            _ouraReadStatus = string.Empty;
            LogOuraDecision();
            RefreshRecoverySetup();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (_activityDestroyed || cancellation.IsCancellationRequested) return;
            // This optional sensor boundary must not take down an active workout.
            // Log only the error type, never exception messages or health records.
            Android.Util.Log.Warn("FluxRecovery", $"Health read failed: {e.GetType().Name}");
            // Failed/partial reads must never keep an old positive clearance.
            _ouraConnection.Snapshot = null;
            _state.OuraRecovery = null;
            _ouraReadStatus = "Oura could not be refreshed. Flux is using its workout countdown.";
            SaveOuraConnection();
            RefreshRecoverySetup();
        }
        finally
        {
            _ouraRefreshing = false;
            if (!_activityDestroyed) UpdateOuraConnectionIcon();
        }
    }

    private void RefreshRecoverySetup()
    {
        if (_appScreen != AppScreen.Duration || !_applicationStartupCompleted) return;
        UpdateLightModifierPresentation((_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);
        // New evidence affects the next workout, never a running one. Invalidating
        // a ready plan here preserves instant Start without using stale evidence.
        if (!_editingActiveWorkoutSetup)
        {
            _workoutPreparationCancellation?.Cancel();
            _preparedWorkout = null;
            _workoutPreparationTask = null;
            QueueWorkoutPreparation();
        }
        UpdateOuraConnectionIcon();
    }

    private void UpdateOuraConnectionIcon()
    {
        if (_ouraButton is null) return;
        bool connected = _ouraConnection.Enabled && HasOuraPermission;
        _ouraButton.Alpha = connected ? 1f : .55f;
        _ouraButton.ContentDescription = _ouraRefreshing ? "Recovery data: syncing Oura" :
            connected ? "Recovery data: Oura connected" : "Recovery data: connect Oura";
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            _ouraButton.TooltipText = _ouraButton.ContentDescription;
    }

    private void ShowOuraRecoveryDialog()
    {
        OuraRecoveryAssessment result = OuraRecoveryPolicy.Evaluate(_state,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        bool connected = _ouraConnection.Enabled && HasOuraPermission;
        string status = !string.IsNullOrEmpty(_ouraReadStatus) ? _ouraReadStatus :
            !connected ? "Use Oura’s recorded sleep, sleeping heart rate and HRV to guide Light mode. No Readiness or Sleep scores." :
            result.Verdict switch
            {
                OuraRecoveryVerdict.Light => "Oura recovery signals call for Light.",
                OuraRecoveryVerdict.Regular => "Oura recovery signals permit regular training. Muscle recovery still applies.",
                _ => "Not enough clear recovery evidence. Flux is using its workout countdown.",
            };
        if (connected)
            status += $"\n\n{result.RecentNights} recent nights · {result.BaselineNights} baseline nights\n" +
                ExplainOuraReason(result.Reason);
        status += "\n\nOptional. Read-only and on this device. No upload. This is a training heuristic, not medical advice.";
        if (_state.ActiveWorkoutSession is not null)
            status += "\nChanges apply to your next workout.";
        var dialog = new AlertDialog.Builder(this)!
            .SetTitle("Oura recovery")!.SetMessage(status)!
            .SetNegativeButton("Close", (_, _) => { });
        if (connected)
        {
            dialog!.SetPositiveButton("Refresh", async (_, _) =>
            {
                await RefreshOuraRecoveryAsync(force: true);
                if (!_activityDestroyed) ShowOuraRecoveryDialog();
            });
            dialog.SetNeutralButton("Disconnect", (_, _) =>
            {
                new AlertDialog.Builder(this)!.SetTitle("Disconnect Oura?")!
                    .SetMessage("Erase Flux’s private recovery summaries and decision log? Your workout history and preferences stay unchanged.")!
                    .SetPositiveButton("Disconnect & erase", (_, _) => DisconnectOuraRecovery())!
                    .SetNegativeButton("Cancel", (_, _) => { })!.Show();
            });
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(34))
            dialog!.SetPositiveButton("Connect Oura", (_, _) =>
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(34))
                    RequestPermissions(OuraHealthConnectReader.Permissions, OuraPermissionRequest);
            });
        else dialog!.SetNeutralButton("Unavailable", (_, _) =>
            Toast.MakeText(this, "Oura connection requires Android 14 or newer. Your countdown still works.", ToastLength.Long)?.Show());
        dialog!.Show();
    }

    private static string ExplainOuraReason(string reason) => reason switch
    {
        "not-connected" => "Oura is not connected.",
        "no-sleep" => "Sync your ring in Oura, then refresh here.",
        "stale-sleep" => "The latest main sleep is more than 18 hours old.",
        "incomplete-sleep" => "The sleep record is incomplete or may be a nap.",
        "insufficient-coverage" => "Too much asleep heart-rate or HRV data is missing.",
        "insufficient-recent-nights" => "Need 3 usable nights in the last 4 days.",
        "insufficient-baseline" => "Need 14 earlier usable nights in the previous 28 days.",
        "very-short-sleep" => "Recorded sleep totals less than 5 hours.",
        "multiple-warnings" => "At least two recovery signals are outside the agreed limits.",
        "mixed-signals" => "The recovery signals do not agree clearly.",
        "unusually-high-hrv" => "Unusually high HRV is not treated as proof of recovery.",
        "work-since-sleep" => "You trained after this sleep. Overnight readings cannot clear that new work.",
        "within-baseline" => "Recent readings are within your personal baseline.",
        _ => "Recovery data could not be verified.",
    };

    private void DisconnectOuraRecovery()
    {
        _ouraReadCancellation?.Cancel();
        _ouraConnection = new();
        _state.OuraRecovery = null;
        try { _ouraStore?.Disconnect(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        RefreshRecoverySetup();
        UpdateOuraConnectionIcon();
    }

    private void SaveOuraConnection()
    {
        try { _ouraStore?.Save(_ouraConnection); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ouraReadStatus = "Recovery data could not be saved. Reconnect next time.";
        }
    }

    private void LogOuraDecision()
    {
        if (!_ouraConnection.Enabled) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool cadence = WorkoutLightDayPolicy.IsLightDayDue(_state.WorkoutHistory, now,
            TimeZoneInfo.Local, _state.LegacyCompletedTrainingDayUnixMilliseconds);
        var result = OuraRecoveryPolicy.Evaluate(_state, now);
        _ouraConnection.Decisions.Add(new(now, cadence,
            OuraRecoveryPolicy.RequiresLight(cadence, result), result));
        SaveOuraConnection();
    }

    public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != OuraPermissionRequest) return;
        if (HasOuraPermission)
        {
            _ouraConnection.Enabled = true;
            SaveOuraConnection();
            await RefreshOuraRecoveryAsync(force: true);
            if (!_activityDestroyed && _activityResumed && _appScreen == AppScreen.Duration)
                ShowOuraRecoveryDialog();
        }
        else
        {
            DisconnectOuraRecovery();
            Toast.MakeText(this, "Oura was not connected. Your workout countdown is unchanged.", ToastLength.Long)?.Show();
        }
    }

}
