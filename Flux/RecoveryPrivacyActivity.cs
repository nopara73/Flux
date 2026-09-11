using Android.Content;

namespace Flux;

[Activity(Name = "com.local.flux.RecoveryPrivacyActivity", Exported = true,
    Permission = "android.permission.START_VIEW_PERMISSION_USAGE")]
[IntentFilter([Intent.ActionViewPermissionUsage], Categories = ["android.intent.category.HEALTH_PERMISSIONS"])]
public sealed class RecoveryPrivacyActivity : Activity
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int padding = (int)(24 * Resources!.DisplayMetrics!.Density);
        content.SetPadding(padding, padding, padding, padding);
        var text = new TextView(this)
        {
            TextSize = 18,
            Text = "Oura recovery privacy\n\nFlux reads only Oura sleep, heart rate and HRV from Health Connect, with your permission. It does not write health records or use Oura composite scores.\n\nReadings are processed on this device. Only nightly summaries and a bounded recovery-decision log are retained in private, non-backed-up app storage. Nothing is uploaded. Workout history still records whether Light was required, without storing health readings.\n\nYou can disconnect and erase recovery data from the link beside Light, or revoke access in Health Connect.\n\nMissing, stale or unclear data leaves Flux’s existing countdown in charge. This is an experimental training heuristic, not a medical assessment.",
        };
        content.AddView(text);
        var close = new Button(this) { Text = "Close" };
        close.Click += (_, _) => Finish();
        content.AddView(close);
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        SetContentView(scroll);
    }
}
