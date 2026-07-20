using Android.Graphics;
using Android.Views;
using Android.Webkit;
using System.Diagnostics.CodeAnalysis;
using Flux.Data;
using Flux.Models;
using Flux.Services;

namespace Flux;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
public class MainActivity : Activity
{
    private const int CountdownSeconds = 60;

    private SqliteExerciseDatabase _exerciseDatabase = null!;
    private ExerciseSessionService _sessionService = null!;
    private IWorkoutStateStore _stateStore = null!;
    private WorkoutState _state = null!;
    private DominantRegion _currentRegion;

    private View _workoutScreen = null!;
    private View _congratulationsScreen = null!;
    private TextView _regionName = null!;
    private TextView _exerciseName = null!;
    private WebView _exerciseGif = null!;
    private Button _startButton = null!;
    private View _countdownPanel = null!;
    private TextView _countdownText = null!;
    private Button _speedUpButton = null!;
    private View _ratingPanel = null!;
    private Button _failedButton = null!;
    private Button _neutralButton = null!;
    private Button _completedButton = null!;
    private TextView _congratulationsSummary = null!;
    private Button _doneButton = null!;

    private WorkoutCountDownTimer? _countdownTimer;
    private Android.Media.ToneGenerator? _toneGenerator;
    private bool _countdownActive;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        SetContentView(Resource.Layout.activity_main);

        BindViews();
        BindEvents();
        ConfigureGifView();

        _exerciseDatabase = new SqliteExerciseDatabase(this);
        _sessionService = new ExerciseSessionService(_exerciseDatabase.Exercises);
        _stateStore = new SharedPreferencesWorkoutStateStore(this);
        _state = _stateStore.Load();
        _sessionService.Initialize(_state);
        _stateStore.Save(_state);

        if (_state.WorkoutCompleted && !_state.CompletionAcknowledged)
        {
            ShowCongratulations();
        }
        else
        {
            ShowNextExercise();
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _exerciseGif?.OnResume();
    }

    protected override void OnPause()
    {
        CancelCountdown(resetToStart: true);
        _exerciseGif?.OnPause();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        CancelCountdown(resetToStart: false);
        _toneGenerator?.Release();
        _toneGenerator?.Dispose();
        _toneGenerator = null;
        _exerciseGif?.Destroy();
        _exerciseDatabase?.Dispose();
        base.OnDestroy();
    }

    private void BindViews()
    {
        _workoutScreen = FindRequiredView<View>(Resource.Id.workout_screen);
        _congratulationsScreen = FindRequiredView<View>(Resource.Id.congratulations_screen);
        _regionName = FindRequiredView<TextView>(Resource.Id.region_name);
        _exerciseName = FindRequiredView<TextView>(Resource.Id.exercise_name);
        _exerciseGif = FindRequiredView<WebView>(Resource.Id.exercise_gif);
        _startButton = FindRequiredView<Button>(Resource.Id.start_button);
        _countdownPanel = FindRequiredView<View>(Resource.Id.countdown_panel);
        _countdownText = FindRequiredView<TextView>(Resource.Id.countdown_text);
        _speedUpButton = FindRequiredView<Button>(Resource.Id.speed_up_button);
        _ratingPanel = FindRequiredView<View>(Resource.Id.rating_panel);
        _failedButton = FindRequiredView<Button>(Resource.Id.failed_button);
        _neutralButton = FindRequiredView<Button>(Resource.Id.neutral_button);
        _completedButton = FindRequiredView<Button>(Resource.Id.completed_button);
        _congratulationsSummary = FindRequiredView<TextView>(
            Resource.Id.congratulations_summary);
        _doneButton = FindRequiredView<Button>(Resource.Id.done_button);
    }

    private void BindEvents()
    {
        _startButton.Click += (_, _) => StartCountdown();
        _speedUpButton.Click += (_, _) => SkipCountdown();
        _failedButton.Click += (_, _) => RecordOutcome(ExerciseOutcome.X);
        _neutralButton.Click += (_, _) => RecordOutcome(ExerciseOutcome.Neutral);
        _completedButton.Click += (_, _) => RecordOutcome(ExerciseOutcome.Tick);
        _doneButton.Click += (_, _) => CloseCompletedWorkout();
    }

    private void ConfigureGifView()
    {
        _exerciseGif.SetBackgroundColor(Color.Transparent);
        _exerciseGif.VerticalScrollBarEnabled = false;
        _exerciseGif.HorizontalScrollBarEnabled = false;
        _exerciseGif.Settings.JavaScriptEnabled = false;
        _exerciseGif.Settings.AllowContentAccess = false;
        _exerciseGif.Settings.AllowFileAccess = false;
        _exerciseGif.Settings.BuiltInZoomControls = false;
        _exerciseGif.Settings.DisplayZoomControls = false;
    }

    private void LoadAnimatedGif(Exercise exercise)
    {
        using Stream gifStream = Assets!.Open(exercise.Gif);
        using var buffer = new MemoryStream();
        gifStream.CopyTo(buffer);
        string base64Gif = Convert.ToBase64String(buffer.ToArray());

        string html = $$"""
            <!doctype html>
            <html>
              <head>
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <style>
                  html, body {
                    width: 100%;
                    height: 100%;
                    margin: 0;
                    overflow: hidden;
                    background: transparent;
                  }
                  body {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                  }
                  img {
                    display: block;
                    width: 100%;
                    height: 100%;
                    object-fit: contain;
                  }
                </style>
              </head>
              <body>
                <img src="data:image/gif;base64,{{base64Gif}}" alt="Exercise animation" />
              </body>
            </html>
            """;

        _exerciseGif.LoadDataWithBaseURL(
            "https://local.flux/",
            html,
            "text/html",
            "UTF-8",
            null);
    }

    private void ShowNextExercise()
    {
        DominantRegion? nextRegion = _sessionService.GetNextRegion(_state);

        if (nextRegion is null)
        {
            ShowCongratulations();
            return;
        }

        _currentRegion = nextRegion.Value;
        Exercise exercise = _sessionService.GetSelectedExercise(_state, _currentRegion);
        int position = ExerciseSessionService.RegionOrder
            .TakeWhile(region => region != _currentRegion)
            .Count() + 1;

        _regionName.Text = $"{position} / {ExerciseSessionService.RegionOrder.Count}  ·  {_currentRegion}";
        _exerciseName.Text = $"{position}. {exercise.Name}";
        LoadAnimatedGif(exercise);
        _workoutScreen.Visibility = ViewStates.Visible;
        _congratulationsScreen.Visibility = ViewStates.Gone;
        ShowStartButton();
    }

    private void ShowStartButton()
    {
        _startButton.Enabled = true;
        _startButton.Visibility = ViewStates.Visible;
        _countdownPanel.Visibility = ViewStates.Gone;
        _ratingPanel.Visibility = ViewStates.Gone;
        SetRatingButtonsEnabled(enabled: true);
    }

    private void StartCountdown()
    {
        if (_countdownActive)
        {
            return;
        }

        PlayBeep(Android.Media.Tone.PropBeep);
        _countdownActive = true;
        _startButton.Visibility = ViewStates.Gone;
        _ratingPanel.Visibility = ViewStates.Gone;
        _countdownPanel.Visibility = ViewStates.Visible;
        _countdownText.Text = CountdownSeconds.ToString();

        _countdownTimer = new WorkoutCountDownTimer(
            CountdownSeconds * 1000L,
            1000L,
            secondsRemaining => _countdownText.Text = secondsRemaining.ToString(),
            CompleteCountdown);
        _countdownTimer.Start();
    }

    private void SkipCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        CompleteCountdown();
    }

    private void CompleteCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        _countdownActive = false;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        _countdownText.Text = "0";
        PlayBeep(Android.Media.Tone.PropBeep2);

        _startButton.Visibility = ViewStates.Gone;
        _countdownPanel.Visibility = ViewStates.Gone;
        _ratingPanel.Visibility = ViewStates.Visible;
        SetRatingButtonsEnabled(enabled: true);
    }

    private void CancelCountdown(bool resetToStart)
    {
        if (!_countdownActive)
        {
            return;
        }

        _countdownActive = false;
        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;

        if (resetToStart && !_state.WorkoutCompleted)
        {
            ShowStartButton();
        }
    }

    private void RecordOutcome(ExerciseOutcome outcome)
    {
        if (_ratingPanel.Visibility != ViewStates.Visible)
        {
            return;
        }

        SetRatingButtonsEnabled(enabled: false);
        Exercise exercise = _sessionService.RecordOutcome(_state, _currentRegion, outcome);

        if (outcome == ExerciseOutcome.X)
        {
            _exerciseDatabase.UpdateScore(exercise);
        }

        _stateStore.Save(_state);

        if (_state.WorkoutCompleted)
        {
            ShowCongratulations();
        }
        else
        {
            ShowNextExercise();
        }
    }

    private void ShowCongratulations()
    {
        CancelCountdown(resetToStart: false);
        (int failed, int neutral, int completed) = _sessionService.GetOutcomeCounts(_state);

        _congratulationsSummary.Text =
            $"✓  {completed} complete    −  {neutral} neutral    ×  {failed} replace";
        _workoutScreen.Visibility = ViewStates.Gone;
        _congratulationsScreen.Visibility = ViewStates.Visible;
    }

    private void CloseCompletedWorkout()
    {
        _sessionService.AcknowledgeCompletion(_state);
        _stateStore.Save(_state);
        FinishAndRemoveTask();
    }

    private void PlayBeep(Android.Media.Tone tone)
    {
        try
        {
            _toneGenerator ??= new Android.Media.ToneGenerator(
                Android.Media.Stream.Music,
                90);
            _toneGenerator.StartTone(tone, 220);
        }
        catch (Exception)
        {
            // A missing or unavailable audio output should not stop a workout.
        }
    }

    private void SetRatingButtonsEnabled(bool enabled)
    {
        _failedButton.Enabled = enabled;
        _neutralButton.Enabled = enabled;
        _completedButton.Enabled = enabled;
    }

    private T FindRequiredView<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(int resourceId)
        where T : View
    {
        return FindViewById<T>(resourceId)
            ?? throw new InvalidOperationException($"Missing view resource {resourceId}.");
    }

    private sealed class WorkoutCountDownTimer : Android.OS.CountDownTimer
    {
        private readonly Action<int> _onTick;
        private readonly Action _onFinish;

        public WorkoutCountDownTimer(
            long millisInFuture,
            long countDownInterval,
            Action<int> onTick,
            Action onFinish)
            : base(millisInFuture, countDownInterval)
        {
            _onTick = onTick;
            _onFinish = onFinish;
        }

        public override void OnTick(long millisUntilFinished)
        {
            int secondsRemaining = (int)Math.Ceiling(millisUntilFinished / 1000d);
            _onTick(secondsRemaining);
        }

        public override void OnFinish()
        {
            _onFinish();
        }
    }
}
