using Android.Views;
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
    private const int CountdownSeconds = 45;
    private const int RestSeconds = 15;

    private SqliteExerciseDatabase _exerciseDatabase = null!;
    private ExerciseSessionService _sessionService = null!;
    private IWorkoutStateStore _stateStore = null!;
    private WorkoutState _state = null!;
    private MuscleGroup _currentMuscleGroup;
    private Exercise? _currentExercise;
    private int _selectedWorkoutMinutes = ExerciseSessionService.DefaultWorkoutMinutes;

    private View _durationScreen = null!;
    private TextView _durationMinutesValue = null!;
    private TextView _durationSummary = null!;
    private Button _durationDecreaseButton = null!;
    private SeekBar _durationSeekBar = null!;
    private Button _durationIncreaseButton = null!;
    private LinearLayout _durationRouteSegments = null!;
    private TextView _durationEndpointText = null!;
    private Button _beginWorkoutButton = null!;
    private View _workoutScreen = null!;
    private View _congratulationsScreen = null!;
    private TextView _muscleGroupName = null!;
    private TextView _exerciseName = null!;
    private VideoView _exerciseVideo = null!;
    private ImageView _holdFrameImage = null!;
    private Button _startButton = null!;
    private View _countdownPanel = null!;
    private TextView _countdownText = null!;
    private Button _speedUpButton = null!;
    private View _restPanel = null!;
    private TextView _restCountdownText = null!;
    private Button _keepButton = null!;
    private TextView _congratulationsSummary = null!;
    private Button _doneButton = null!;

    private WorkoutCountDownTimer? _countdownTimer;
    private WorkoutCountDownTimer? _restTimer;
    private Android.Media.ToneGenerator? _toneGenerator;
    private Android.Media.MediaPlayer? _activeMediaPlayer;
    private VideoPreparedListener? _videoPreparedListener;
    private Android.Graphics.Bitmap? _holdFrameBitmap;
    private bool _countdownActive;
    private bool _restActive;
    private bool _loopExerciseVideo = true;
    private bool _freezeHoldAtEnd;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        SetContentView(Resource.Layout.activity_main);

        BindViews();
        BindEvents();
        ConfigureVideoView();

        _exerciseDatabase = new SqliteExerciseDatabase(this);
        _sessionService = new ExerciseSessionService(_exerciseDatabase.Exercises);
        _stateStore = new SharedPreferencesWorkoutStateStore(this);
        _state = _stateStore.Load();
        _sessionService.Initialize(_state);
        _stateStore.Save(_state);

        _selectedWorkoutMinutes = _state.LastWorkoutMinutes;

        if (_state.WorkoutCompleted && !_state.CompletionAcknowledged)
        {
            ShowCongratulations();
        }
        else if (_state.ActiveWorkoutMinutes >=
                 ExerciseSessionService.MinimumWorkoutMinutes)
        {
            ShowNextExercise();
            RestorePendingRest();
        }
        else
        {
            ShowDurationSelection();
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_restActive)
        {
            ResumeRestCountdown();
        }
        else if (_currentExercise is not null && !_freezeHoldAtEnd)
        {
            _exerciseVideo?.Start();
        }
    }

    protected override void OnPause()
    {
        CancelCountdown(resetToStart: true);
        PauseRestCountdown();
        _exerciseVideo?.Pause();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _toneGenerator?.Release();
        _toneGenerator?.Dispose();
        _toneGenerator = null;
        _exerciseVideo?.StopPlayback();
        ClearHoldFrame();
        _activeMediaPlayer = null;
        _videoPreparedListener = null;
        _exerciseDatabase?.Dispose();
        base.OnDestroy();
    }

    private void BindViews()
    {
        _durationScreen = FindRequiredView<View>(Resource.Id.duration_screen);
        _durationMinutesValue = FindRequiredView<TextView>(
            Resource.Id.duration_minutes_value);
        _durationSummary = FindRequiredView<TextView>(Resource.Id.duration_summary);
        _durationDecreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_decrease_button);
        _durationSeekBar = FindRequiredView<SeekBar>(Resource.Id.duration_seek_bar);
        _durationIncreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_increase_button);
        _durationRouteSegments = FindRequiredView<LinearLayout>(
            Resource.Id.duration_route_segments);
        _durationEndpointText = FindRequiredView<TextView>(
            Resource.Id.duration_endpoint_text);
        _beginWorkoutButton = FindRequiredView<Button>(Resource.Id.begin_workout_button);
        _workoutScreen = FindRequiredView<View>(Resource.Id.workout_screen);
        _congratulationsScreen = FindRequiredView<View>(Resource.Id.congratulations_screen);
        _muscleGroupName = FindRequiredView<TextView>(Resource.Id.muscle_group_name);
        _exerciseName = FindRequiredView<TextView>(Resource.Id.exercise_name);
        _exerciseVideo = FindRequiredView<VideoView>(Resource.Id.exercise_video);
        _holdFrameImage = FindRequiredView<ImageView>(Resource.Id.hold_frame_image);
        _startButton = FindRequiredView<Button>(Resource.Id.start_button);
        _countdownPanel = FindRequiredView<View>(Resource.Id.countdown_panel);
        _countdownText = FindRequiredView<TextView>(Resource.Id.countdown_text);
        _speedUpButton = FindRequiredView<Button>(Resource.Id.speed_up_button);
        _restPanel = FindRequiredView<View>(Resource.Id.rest_panel);
        _restCountdownText = FindRequiredView<TextView>(Resource.Id.rest_countdown_text);
        _keepButton = FindRequiredView<Button>(Resource.Id.keep_button);
        _congratulationsSummary = FindRequiredView<TextView>(
            Resource.Id.congratulations_summary);
        _doneButton = FindRequiredView<Button>(Resource.Id.done_button);
    }

    private void BindEvents()
    {
        _durationDecreaseButton.Click += (_, _) =>
            SetSelectedWorkoutMinutes(_selectedWorkoutMinutes - 1);
        _durationIncreaseButton.Click += (_, _) =>
            SetSelectedWorkoutMinutes(_selectedWorkoutMinutes + 1);
        _durationSeekBar.ProgressChanged += (_, eventArgs) =>
        {
            if (eventArgs.FromUser)
            {
                SetSelectedWorkoutMinutes(
                    eventArgs.Progress + ExerciseSessionService.MinimumWorkoutMinutes);
            }
        };
        _beginWorkoutButton.Click += (_, _) => StartSelectedWorkout();
        _startButton.Click += (_, _) => StartCountdown();
        _speedUpButton.Click += (_, _) => SkipCountdown();
        _keepButton.Click += (_, _) => KeepCurrentExercise();
        _doneButton.Click += (_, _) => CloseCompletedWorkout();
    }

    private void ShowDurationSelection()
    {
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _restActive = false;
        _exerciseVideo.StopPlayback();
        ClearHoldFrame();
        _currentExercise = null;

        _durationScreen.Visibility = ViewStates.Visible;
        _workoutScreen.Visibility = ViewStates.Gone;
        _congratulationsScreen.Visibility = ViewStates.Gone;
        SetSelectedWorkoutMinutes(_state.LastWorkoutMinutes);
    }

    private void SetSelectedWorkoutMinutes(int minutes)
    {
        int clampedMinutes = Math.Clamp(
            minutes,
            ExerciseSessionService.MinimumWorkoutMinutes,
            ExerciseSessionService.MaximumWorkoutMinutes);
        _selectedWorkoutMinutes = clampedMinutes;

        int expectedProgress =
            clampedMinutes - ExerciseSessionService.MinimumWorkoutMinutes;
        if (_durationSeekBar.Progress != expectedProgress)
        {
            _durationSeekBar.Progress = expectedProgress;
        }

        MuscleGroup endpoint = ExerciseSessionService.MuscleGroupOrder[clampedMinutes - 1];
        string endpointName = GetMuscleGroupDisplayName(endpoint);
        const string minuteLabel = "minutes";

        _durationMinutesValue.Text = clampedMinutes.ToString();
        _durationMinutesValue.ContentDescription =
            $"{clampedMinutes} minutes selected";
        _durationSummary.Text =
            $"{clampedMinutes} rounds · 45 sec move · 15 sec rest";
        _durationEndpointText.Text =
            $"Finishes with {clampedMinutes} · {endpointName}";
        _beginWorkoutButton.Text =
            $"Start {clampedMinutes}-minute workout";
        _beginWorkoutButton.ContentDescription =
            $"Start {clampedMinutes} {minuteLabel} workout";
        _durationSeekBar.ContentDescription =
            $"Workout duration, {clampedMinutes} {minuteLabel}. Range 3 to 20 minutes";
        _durationRouteSegments.ContentDescription =
            $"{clampedMinutes} of 20 muscle groups selected, from Glutes through {endpointName}";

        _durationDecreaseButton.Enabled =
            clampedMinutes > ExerciseSessionService.MinimumWorkoutMinutes;
        _durationIncreaseButton.Enabled =
            clampedMinutes < ExerciseSessionService.MaximumWorkoutMinutes;

        for (int index = 0; index < _durationRouteSegments.ChildCount; index++)
        {
            View segment = _durationRouteSegments.GetChildAt(index)
                ?? throw new InvalidOperationException("A duration route segment is missing.");
            segment.SetBackgroundResource(index < clampedMinutes
                ? Resource.Drawable.duration_segment_active
                : Resource.Drawable.duration_segment_inactive);
        }
    }

    private void StartSelectedWorkout()
    {
        _sessionService.StartWorkout(_state, _selectedWorkoutMinutes);
        _stateStore.Save(_state);
        ShowNextExercise();
    }

    private void ConfigureVideoView()
    {
        _videoPreparedListener = new VideoPreparedListener(mediaPlayer =>
        {
            _activeMediaPlayer = mediaPlayer;
            _activeMediaPlayer.Looping = _loopExerciseVideo;
            _activeMediaPlayer.SetVolume(0f, 0f);
            _exerciseVideo.Start();
        });
        _exerciseVideo.SetOnPreparedListener(_videoPreparedListener);
        _exerciseVideo.Completion += (_, _) =>
        {
            if (_freezeHoldAtEnd)
            {
                FreezeHoldOnFinalFrame();
            }
        };
    }

    private void LoadExerciseMedia(Exercise exercise)
    {
        _loopExerciseVideo = true;
        _freezeHoldAtEnd = false;
        _activeMediaPlayer = null;
        ClearHoldFrame();
        _exerciseVideo.StopPlayback();
        _exerciseVideo.SetVideoPath(CacheVideoAsset(exercise.Video));
    }

    private string CacheVideoAsset(string assetPath)
    {
        string cacheRoot = System.IO.Path.Combine(CacheDir!.AbsolutePath, "exercise-videos-v4");
        Directory.CreateDirectory(cacheRoot);
        string cachedPath = System.IO.Path.Combine(cacheRoot, System.IO.Path.GetFileName(assetPath));

        using Stream source = Assets!.Open(assetPath);
        using FileStream destination = File.Create(cachedPath);
        source.CopyTo(destination);

        return cachedPath;
    }

    private void PlayHoldOnce()
    {
        ClearHoldFrame();
        _loopExerciseVideo = false;
        _freezeHoldAtEnd = true;
        if (_activeMediaPlayer is not null)
        {
            _activeMediaPlayer.Looping = false;
        }

        _exerciseVideo.SeekTo(0);
        _exerciseVideo.Start();
    }

    private void FreezeHoldOnFinalFrame()
    {
        Exercise? exercise = _currentExercise;
        if (exercise?.Mode != ExerciseMode.Hold)
        {
            return;
        }

        _exerciseVideo.Pause();
        ShowHoldFrame(exercise.Id);
    }

    private void ShowHoldFrame(int exerciseId)
    {
        if (_holdFrameImage.Visibility == ViewStates.Visible)
        {
            return;
        }

        string assetPath = $"exercise_hold_frames/exercise_{exerciseId:D4}.png";
        using Stream stream = Assets!.Open(assetPath);
        Android.Graphics.Bitmap bitmap = Android.Graphics.BitmapFactory.DecodeStream(stream)
            ?? throw new InvalidOperationException(
                $"Unable to decode the reviewed hold frame for exercise {exerciseId}.");

        _holdFrameBitmap = bitmap;
        _holdFrameImage.SetImageBitmap(bitmap);
        _holdFrameImage.Visibility = ViewStates.Visible;
    }

    private void ClearHoldFrame()
    {
        if (_holdFrameImage is null)
        {
            return;
        }

        _holdFrameImage.Visibility = ViewStates.Gone;
        _holdFrameImage.SetImageDrawable(null);
        _holdFrameBitmap?.Dispose();
        _holdFrameBitmap = null;
    }

    private void ShowNextExercise()
    {
        MuscleGroup? nextMuscleGroup = _sessionService.GetNextMuscleGroup(_state);

        if (nextMuscleGroup is null)
        {
            ShowCongratulations();
            return;
        }

        _currentMuscleGroup = nextMuscleGroup.Value;
        Exercise exercise = _sessionService.GetSelectedExercise(
            _state,
            _currentMuscleGroup);
        _currentExercise = exercise;
        int position = ExerciseSessionService.MuscleGroupOrder
            .TakeWhile(muscleGroup => muscleGroup != _currentMuscleGroup)
            .Count() + 1;

        _muscleGroupName.Text =
            $"{position} / {_state.ActiveWorkoutMinutes}  ·  " +
            GetMuscleGroupDisplayName(_currentMuscleGroup).ToUpperInvariant();
        _exerciseName.Text = exercise.Mode == ExerciseMode.Hold
            ? $"{exercise.Name}  ·  HOLD"
            : exercise.Name;
        LoadExerciseMedia(exercise);
        _durationScreen.Visibility = ViewStates.Gone;
        _workoutScreen.Visibility = ViewStates.Visible;
        _congratulationsScreen.Visibility = ViewStates.Gone;
        ShowStartButton();
    }

    private void ShowStartButton()
    {
        _startButton.Enabled = true;
        _startButton.Visibility = ViewStates.Visible;
        _countdownPanel.Visibility = ViewStates.Gone;
        _restPanel.Visibility = ViewStates.Gone;
    }

    private void StartCountdown()
    {
        if (_countdownActive)
        {
            return;
        }

        PlayBeep(Android.Media.Tone.PropBeep);
        _countdownActive = true;
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            PlayHoldOnce();
        }
        _startButton.Visibility = ViewStates.Gone;
        _restPanel.Visibility = ViewStates.Gone;
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
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        PlayBeep(Android.Media.Tone.PropBeep2);

        _startButton.Visibility = ViewStates.Gone;
        _countdownPanel.Visibility = ViewStates.Gone;
        BeginRest();
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
            if (_currentExercise?.Mode == ExerciseMode.Hold)
            {
                LoadExerciseMedia(_currentExercise);
            }
            ShowStartButton();
        }
    }

    private void BeginRest()
    {
        _state.PendingRestMuscleGroup = _currentMuscleGroup;
        _state.PendingRestEndsAtUnixMilliseconds =
            DateTimeOffset.UtcNow.AddSeconds(RestSeconds).ToUnixTimeMilliseconds();
        _state.PendingRestKept = false;
        _stateStore.Save(_state);

        _restActive = true;
        ShowRestPanel();
        AnnouncePhaseForAccessibility(
            _restPanel,
            "Rest, 15 seconds. Tap to keep this exercise.");
        ResumeRestCountdown();
    }

    private void RestorePendingRest()
    {
        if (_state.PendingRestMuscleGroup != _currentMuscleGroup)
        {
            return;
        }

        _restActive = true;
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            _loopExerciseVideo = false;
            _freezeHoldAtEnd = true;
            ShowHoldFrame(_currentExercise.Id);
        }

        ShowRestPanel();
    }

    private void ShowRestPanel()
    {
        _startButton.Visibility = ViewStates.Gone;
        _countdownPanel.Visibility = ViewStates.Gone;
        _restPanel.Visibility = ViewStates.Visible;
        _keepButton.Enabled = !_state.PendingRestKept;
        _keepButton.Text = _state.PendingRestKept
            ? GetString(Resource.String.kept)
            : GetString(Resource.String.tap_to_keep);

        UpdateRestCountdownText();
    }

    private void ResumeRestCountdown()
    {
        if (!_restActive)
        {
            return;
        }

        _restTimer?.Cancel();
        _restTimer?.Dispose();
        _restTimer = null;

        long millisecondsRemaining =
            _state.PendingRestEndsAtUnixMilliseconds -
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (millisecondsRemaining <= 0)
        {
            CompleteRest();
            return;
        }

        UpdateRestCountdownText();
        _restTimer = new WorkoutCountDownTimer(
            millisecondsRemaining,
            250L,
            _ => UpdateRestCountdownText(),
            CompleteRest);
        _restTimer.Start();
    }

    private void UpdateRestCountdownText()
    {
        long millisecondsRemaining = Math.Max(
            0,
            _state.PendingRestEndsAtUnixMilliseconds -
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        int secondsRemaining = (int)Math.Ceiling(millisecondsRemaining / 1000d);
        _restCountdownText.Text = $"Rest · {secondsRemaining}";
    }

    private void PauseRestCountdown()
    {
        _restTimer?.Cancel();
        _restTimer?.Dispose();
        _restTimer = null;
    }

    private void KeepCurrentExercise()
    {
        if (!_restActive || _state.PendingRestKept)
        {
            return;
        }

        _state.PendingRestKept = true;
        _stateStore.Save(_state);
        _keepButton.Enabled = false;
        _keepButton.Text = GetString(Resource.String.kept);
    }

    private void CompleteRest()
    {
        if (!_restActive || _state.PendingRestMuscleGroup != _currentMuscleGroup)
        {
            return;
        }

        _restActive = false;
        PauseRestCountdown();
        bool keep = _state.PendingRestKept;
        Exercise exercise = _sessionService.RecordOutcome(
            _state,
            _currentMuscleGroup,
            keep);
        _sessionService.ClearPendingRest(_state);

        if (!keep)
        {
            _exerciseDatabase.UpdateScore(exercise);
        }

        _stateStore.Save(_state);
        PlayBeep(Android.Media.Tone.PropBeep);

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
        PauseRestCountdown();
        _restActive = false;
        _exerciseVideo.StopPlayback();
        _currentExercise = null;
        (int replaced, int kept) = _sessionService.GetOutcomeCounts(_state);

        _congratulationsSummary.Text =
            $"{_state.ActiveWorkoutMinutes} minutes complete\n" +
            $"✓  {kept} kept    ×  {replaced} replaced";
        _durationScreen.Visibility = ViewStates.Gone;
        _workoutScreen.Visibility = ViewStates.Gone;
        _congratulationsScreen.Visibility = ViewStates.Visible;
        AnnouncePhaseForAccessibility(
            _congratulationsScreen,
            $"Workout complete. {_state.ActiveWorkoutMinutes} minutes finished.");
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

    private static string GetMuscleGroupDisplayName(MuscleGroup muscleGroup)
    {
        return muscleGroup switch
        {
            MuscleGroup.UpperBack => "Upper back",
            MuscleGroup.LowerBack => "Lower back",
            MuscleGroup.HipFlexors => "Hip flexors",
            MuscleGroup.MidBack => "Mid back",
            MuscleGroup.RotatorCuff => "Rotator cuff",
            _ => muscleGroup.ToString(),
        };
    }

    [SuppressMessage(
        "Interoperability",
        "CA1422:Validate platform compatibility",
        Justification = "This private API 24+ app uses the established one-shot " +
            "announcement behavior; Android 36 deprecation does not remove it.")]
    private static void AnnouncePhaseForAccessibility(View view, string announcement)
    {
        view.AnnounceForAccessibility(announcement);
    }

    private T FindRequiredView<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(int resourceId)
        where T : View
    {
        View? view = FindViewById(resourceId);
        if (view is T typedView)
        {
            return typedView;
        }

        string resourceName;
        try
        {
            resourceName = Resources?.GetResourceEntryName(resourceId) ?? resourceId.ToString();
        }
        catch (Android.Content.Res.Resources.NotFoundException)
        {
            resourceName = resourceId.ToString();
        }

        string actualType = view?.GetType().FullName ?? "missing";
        throw new InvalidOperationException(
            $"View resource {resourceName} is {actualType}; expected {typeof(T).FullName}.");
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

    private sealed class VideoPreparedListener(Action<Android.Media.MediaPlayer> onPrepared)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnPreparedListener
    {
        public void OnPrepared(Android.Media.MediaPlayer? mediaPlayer)
        {
            if (mediaPlayer is not null)
            {
                onPrepared(mediaPlayer);
            }
        }
    }
}
