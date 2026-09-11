using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Flux.Services;

namespace Flux;

public partial class MainActivity
{
    private Dialog? _workoutScopeDialog;

    private async Task ReviewUpdatedWorkoutAsync()
    {
        WorkoutAvailability availability = _sessionService.GetWorkoutAvailability(_state, _selectedWorkoutMinutes, _selectedWorkoutModifiers);
        await ReviewWorkoutScopeAsync(availability with { Groups = [] }, preservedWork: true);
        if (_activityDestroyed) return;
        _state.WorkoutSetupReviewRequired = false;
        _stateStore.Save(_state);
    }

    private Task<bool> ReviewWorkoutScopeAsync(WorkoutAvailability availability, bool preservedWork = false)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var content = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
        content.SetPadding(DpInt(26), DpInt(26), DpInt(26), DpInt(24));
        Color foreground = new(GetColor(Resource.Color.primary_text));
        Color secondary = new(GetColor(Resource.Color.secondary_text));
        var surface = new GradientDrawable();
        surface.SetColor(new Color(GetColor(Resource.Color.surface)));
        surface.SetCornerRadius(Dp(28));
        content.Background = surface;
        TextView Text(string text, int size, bool muted = false)
        {
            var view = new TextView(this) { Text = text, TextSize = size };
            view.SetTextColor(muted ? secondary : foreground);
            return view;
        }
        TextView title = Text(preservedWork ? "Review your workout" : availability.CanStart ? "Limited coverage" : "Adjust your setup", 25);
        title.SetTypeface(null, TypefaceStyle.Bold);
        content.AddView(title);
        TextView summary = Text(preservedWork ? "Your completed work is saved. Review your setup to continue." : availability.CanStart
            ? $"{availability.Minutes} min · fewer targets, complete movements"
            : "No complete workout fits these settings and duration.", 14, true);
        summary.SetPadding(0, DpInt(10), 0, DpInt(26));
        content.AddView(summary);
        var regions = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
        string[] names = ["Upper body", "Torso", "Lower body"];
        for (int index = 0; index < availability.Regions.Count; index++)
        {
            WorkoutRegionAvailability region = availability.Regions[index];
            var cell = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
            cell.SetPadding(0, 0, DpInt(index == 2 ? 0 : 12), 0);
            cell.AddView(Text(names[index], 13, !region.Included));
            var bar = new ProgressBar(this, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
            {
                Max = region.TotalTargets,
                Progress = region.Included ? region.AvailableTargets : 0,
                ProgressTintList = ColorStateList.ValueOf(foreground),
                ProgressBackgroundTintList = ColorStateList.ValueOf(new Color(GetColor(Resource.Color.surface_outline_soft))),
            };
            cell.AddView(bar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, DpInt(24)));
            cell.AddView(Text(region.Included ? $"{region.AvailableTargets}/{region.TotalTargets} available" : "left out", 11, true));
            regions.AddView(cell, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        }
        regions.SetPadding(0, 0, 0, DpInt(24));
        content.AddView(regions);
        if (availability.MissingGroups.Count > 0)
        {
            TextView expand = Text("Unavailable targets  ›", 13, true);
            expand.SetPadding(0, 0, 0, DpInt(18));
            content.AddView(expand);
            var details = new ScrollView(this) { Visibility = ViewStates.Gone };
            details.AddView(Text(string.Join(" · ", availability.MissingGroups), 13, true));
            content.AddView(details, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, DpInt(130)));
            expand.Click += (_, _) => details.Visibility = details.Visibility == ViewStates.Gone ? ViewStates.Visible : ViewStates.Gone;
        }
        var actions = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
        Dialog dialog = new Dialog(this);
        _workoutScopeDialog = dialog;
        Button Action(string label, bool accent)
        {
            var button = new Button(this) { Text = label, TextSize = 14 };
            button.SetAllCaps(false);
            button.SetTextColor(foreground);
            var background = new GradientDrawable();
            background.SetColor(new Color(GetColor(accent ? Resource.Color.accent : Resource.Color.media_background)));
            background.SetCornerRadius(Dp(16));
            button.Background = background;
            var parameters = new LinearLayout.LayoutParams(0, DpInt(52), 1);
            if (accent) parameters.LeftMargin = DpInt(10);
            actions.AddView(button, parameters);
            return button;
        }
        Action("Adjust setup", false).Click += (_, _) => dialog.Dismiss();
        if (availability.CanStart)
        {
            Action("Start limited", true).Click += (_, _) =>
            {
                completion.TrySetResult(true);
                dialog.Dismiss();
            };
        }
        content.AddView(actions);
        dialog.SetContentView(content);
        dialog.DismissEvent += (_, _) =>
        {
            completion.TrySetResult(false);
            if (ReferenceEquals(_workoutScopeDialog, dialog)) _workoutScopeDialog = null;
        };
        dialog.Show();
        dialog.Window?.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialog.Window?.SetLayout(Math.Min(DpInt(440), Resources!.DisplayMetrics!.WidthPixels - DpInt(32)), ViewGroup.LayoutParams.WrapContent);
        return completion.Task;
    }
}
