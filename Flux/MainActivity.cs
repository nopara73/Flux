using Android.Views;
using System.Diagnostics.CodeAnalysis;
using Flux.Data;
using Flux.Models;
using Flux.Services;

namespace Flux;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    ConfigurationChanges =
        Android.Content.PM.ConfigChanges.Orientation |
        Android.Content.PM.ConfigChanges.ScreenSize |
        Android.Content.PM.ConfigChanges.ScreenLayout |
        Android.Content.PM.ConfigChanges.SmallestScreenSize,
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
public class MainActivity : Activity
{
    private const int CountdownSeconds = 45;
    private const int RestSeconds = 15;
    private const long PhaseMotionDurationMilliseconds = 140L;

    private enum AppScreen
    {
        Duration,
        Workout,
        Completion,
    }

    private enum WorkoutPhase
    {
        Ready,
        Move,
        Rest,
    }

    private SqliteExerciseDatabase _exerciseDatabase = null!;
    private ExerciseSessionService _sessionService = null!;
    private IWorkoutStateStore _stateStore = null!;
    private WorkoutState _state = null!;
    private MuscleGroup _currentMuscleGroup;
    private Exercise? _currentExercise;
    private int _selectedWorkoutMinutes = ExerciseSessionService.DefaultWorkoutMinutes;

    private View _durationScreen = null!;
    private View _durationInsetContent = null!;
    private TextView _durationMinutesValue = null!;
    private TextView _durationSummary = null!;
    private Button _durationDecreaseButton = null!;
    private SeekBar _durationSeekBar = null!;
    private Button _durationIncreaseButton = null!;
    private LinearLayout _durationRouteSegments = null!;
    private TextView _durationEndpointText = null!;
    private Button _beginWorkoutButton = null!;
    private View _workoutScreen = null!;
    private View _workoutInsetContent = null!;
    private View _workoutHeader = null!;
    private TextView _workoutProgressText = null!;
    private ProgressBar _workoutProgressBar = null!;
    private View _congratulationsScreen = null!;
    private View _completionInsetContent = null!;
    private TextView _muscleGroupName = null!;
    private TextView _exerciseName = null!;
    private TextView _exerciseModeBadge = null!;
    private View _exerciseMediaArea = null!;
    private View _exerciseMediaCard = null!;
    private VideoView _exerciseVideo = null!;
    private ImageView _holdFrameImage = null!;
    private View _mediaScrim = null!;
    private TextView _mediaLoadingText = null!;
    private View _mediaErrorPanel = null!;
    private Button _mediaRetryButton = null!;
    private View _readyPanel = null!;
    private Button _startButton = null!;
    private View _countdownPanel = null!;
    private TextView _countdownText = null!;
    private ProgressBar _countdownProgress = null!;
    private Button _speedUpButton = null!;
    private View _restPanel = null!;
    private TextView _restCountdownText = null!;
    private ProgressBar _restProgress = null!;
    private TextView _restHelperText = null!;
    private Button _keepButton = null!;
    private TextView _congratulationsDuration = null!;
    private TextView _congratulationsSummary = null!;
    private View _completionMark = null!;
    private Button _doneButton = null!;
    private SystemBarsController[] _systemBarsControllers = [];
    private DurationSeekAccessibilityDelegate? _durationSeekAccessibilityDelegate;

    private WorkoutCountDownTimer? _countdownTimer;
    private WorkoutCountDownTimer? _restTimer;
    private Android.Media.ToneGenerator? _toneGenerator;
    private Android.Media.MediaPlayer? _activeMediaPlayer;
    private VideoPreparedListener? _videoPreparedListener;
    private VideoErrorListener? _videoErrorListener;
    private VideoInfoListener? _videoInfoListener;
    private Android.Graphics.Bitmap? _holdFrameBitmap;
    private bool _countdownActive;
    private bool _countdownPaused;
    private long _countdownEndsAtElapsedMilliseconds;
    private long _countdownMillisecondsRemaining;
    private bool _restActive;
    private bool _activityResumed;
    private bool _loopExerciseVideo = true;
    private bool _freezeHoldAtEnd;
    private bool _mediaReady;
    private bool _countdownPausedForMediaError;
    private int _mediaLoadGeneration;
    private bool _hasRenderedScreen;
    private AppScreen _appScreen = AppScreen.Duration;
    private WorkoutPhase _workoutPhase = WorkoutPhase.Ready;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetContentView(Resource.Layout.activity_main);

        BindViews();
        ConfigureResponsiveText();
        ApplyResponsiveDimensions();
        ConfigureAccessibility();
        ConfigureSystemBars();
        BindEvents();
        ConfigureVideoView();

        _exerciseDatabase = new SqliteExerciseDatabase(this);
        _sessionService = new ExerciseSessionService(_exerciseDatabase.Exercises);
        _stateStore = new SharedPreferencesWorkoutStateStore(this);
        _state = _stateStore.Load();
        RecoverPendingScoreUpdate();
        _sessionService.Initialize(_state);

        if (!_state.WorkoutCompleted &&
            _state.ActiveWorkoutMinutes >= ExerciseSessionService.MinimumWorkoutMinutes)
        {
            FinishInterruptedWorkout();
        }
        else
        {
            _stateStore.Save(_state);
        }

        _selectedWorkoutMinutes = _state.LastWorkoutMinutes;

        if (_state.WorkoutCompleted && !_state.CompletionAcknowledged)
        {
            ShowCongratulations();
        }
        else
        {
            ShowDurationSelection();
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _activityResumed = true;
        ApplySystemBarAppearance();
        if (_restActive)
        {
            ResumeRestCountdown();
        }
        else if (_countdownPaused &&
                 (!_countdownPausedForMediaError || _mediaReady))
        {
            _countdownPausedForMediaError = false;
            ResumeCountdown();
        }
        else if (_currentExercise is not null &&
                 !_freezeHoldAtEnd &&
                 _workoutPhase != WorkoutPhase.Rest)
        {
            _exerciseVideo?.Start();
        }
    }

    protected override void OnPause()
    {
        _activityResumed = false;
        PauseCountdown();
        PauseRestCountdown();
        _exerciseVideo?.Pause();
        CancelUiAnimations();
        base.OnPause();
    }

    public override void OnConfigurationChanged(
        Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyResponsiveDimensions();
        GetInsetContent(_appScreen).RequestApplyInsets();
        _exerciseMediaArea.Post(ResizeMediaCard);
    }

    protected override void OnDestroy()
    {
        _mediaReady = false;
        _mediaLoadGeneration++;
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _toneGenerator?.Release();
        _toneGenerator?.Dispose();
        _toneGenerator = null;
        _exerciseVideo?.StopPlayback();
        ClearHoldFrame();
        _activeMediaPlayer = null;
        _videoPreparedListener = null;
        _videoErrorListener = null;
        _videoInfoListener = null;
        _durationSeekBar.SetAccessibilityDelegate(null);
        _durationSeekAccessibilityDelegate?.Dispose();
        _durationSeekAccessibilityDelegate = null;
        foreach (SystemBarsController controller in _systemBarsControllers)
        {
            controller.Dispose();
        }
        _systemBarsControllers = [];
        _exerciseDatabase?.Dispose();
        base.OnDestroy();
    }

    private void BindViews()
    {
        _durationScreen = FindRequiredView<View>(Resource.Id.duration_screen);
        _durationInsetContent = FindRequiredView<View>(Resource.Id.duration_inset_content);
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
        _workoutInsetContent = FindRequiredView<View>(Resource.Id.workout_inset_content);
        _workoutHeader = FindRequiredView<View>(Resource.Id.workout_header);
        _workoutProgressText = FindRequiredView<TextView>(Resource.Id.workout_progress_text);
        _workoutProgressBar = FindRequiredView<ProgressBar>(Resource.Id.workout_progress_bar);
        _congratulationsScreen = FindRequiredView<View>(Resource.Id.congratulations_screen);
        _completionInsetContent = FindRequiredView<View>(
            Resource.Id.completion_inset_content);
        _muscleGroupName = FindRequiredView<TextView>(Resource.Id.muscle_group_name);
        _exerciseName = FindRequiredView<TextView>(Resource.Id.exercise_name);
        _exerciseModeBadge = FindRequiredView<TextView>(Resource.Id.exercise_mode_badge);
        _exerciseMediaArea = FindRequiredView<View>(Resource.Id.exercise_media_area);
        _exerciseMediaCard = FindRequiredView<View>(Resource.Id.exercise_media_card);
        _exerciseVideo = FindRequiredView<VideoView>(Resource.Id.exercise_video);
        _holdFrameImage = FindRequiredView<ImageView>(Resource.Id.hold_frame_image);
        _mediaScrim = FindRequiredView<View>(Resource.Id.media_scrim);
        _mediaLoadingText = FindRequiredView<TextView>(Resource.Id.media_loading_text);
        _mediaErrorPanel = FindRequiredView<View>(Resource.Id.media_error_panel);
        _mediaRetryButton = FindRequiredView<Button>(Resource.Id.media_retry_button);
        _readyPanel = FindRequiredView<View>(Resource.Id.ready_panel);
        _startButton = FindRequiredView<Button>(Resource.Id.start_button);
        _countdownPanel = FindRequiredView<View>(Resource.Id.countdown_panel);
        _countdownText = FindRequiredView<TextView>(Resource.Id.countdown_text);
        _countdownProgress = FindRequiredView<ProgressBar>(Resource.Id.countdown_progress);
        _speedUpButton = FindRequiredView<Button>(Resource.Id.speed_up_button);
        _restPanel = FindRequiredView<View>(Resource.Id.rest_panel);
        _restCountdownText = FindRequiredView<TextView>(Resource.Id.rest_countdown_text);
        _restProgress = FindRequiredView<ProgressBar>(Resource.Id.rest_progress);
        _restHelperText = FindRequiredView<TextView>(Resource.Id.rest_helper_text);
        _keepButton = FindRequiredView<Button>(Resource.Id.keep_button);
        _congratulationsDuration = FindRequiredView<TextView>(
            Resource.Id.congratulations_duration);
        _congratulationsSummary = FindRequiredView<TextView>(
            Resource.Id.congratulations_summary);
        _completionMark = FindRequiredView<View>(Resource.Id.completion_mark);
        _doneButton = FindRequiredView<Button>(Resource.Id.done_button);

        _exerciseMediaArea.LayoutChange += (_, _) => ResizeMediaCard();
    }

    private void BindEvents()
    {
        _durationDecreaseButton.Click += (_, _) =>
            SetSelectedWorkoutMinutes(_selectedWorkoutMinutes - 1, userInitiated: true);
        _durationIncreaseButton.Click += (_, _) =>
            SetSelectedWorkoutMinutes(_selectedWorkoutMinutes + 1, userInitiated: true);
        _durationSeekBar.ProgressChanged += (_, eventArgs) =>
        {
            if (eventArgs.FromUser)
            {
                SetSelectedWorkoutMinutes(
                    eventArgs.Progress + ExerciseSessionService.MinimumWorkoutMinutes,
                    userInitiated: true);
            }
        };
        _beginWorkoutButton.Click += (_, _) => StartSelectedWorkout();
        _startButton.Click += (_, _) => StartCountdown();
        _speedUpButton.Click += (_, _) => SkipCountdown();
        _keepButton.Click += (_, _) => KeepCurrentExercise();
        _mediaRetryButton.Click += (_, _) =>
        {
            if (_currentExercise is not null)
            {
                LoadExerciseMedia(_currentExercise, forceCacheRefresh: true);
            }
        };
        _doneButton.Click += (_, _) => CloseCompletedWorkout();
    }

    private void ConfigureSystemBars()
    {
        _systemBarsControllers =
        [
            new SystemBarsController(this, _durationInsetContent),
            new SystemBarsController(this, _workoutInsetContent),
            new SystemBarsController(this, _completionInsetContent),
        ];
    }

    private void ConfigureResponsiveText()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _exerciseName.SetAutoSizeTextTypeUniformWithConfiguration(
                18,
                23,
                1,
                (int)Android.Util.ComplexUnitType.Sp);
        }
    }

    private void ApplyResponsiveDimensions()
    {
        _exerciseMediaArea.SetMinimumHeight(
            Resources!.GetDimensionPixelSize(Resource.Dimension.workout_media_min_height));
        _readyPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.ready_panel_min_height));
        _countdownPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.move_panel_min_height));
        _restPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.rest_panel_min_height));
    }

    private void ConfigureAccessibility()
    {
        _durationSeekAccessibilityDelegate = new DurationSeekAccessibilityDelegate(
            () => _selectedWorkoutMinutes,
            minutes => SetSelectedWorkoutMinutes(minutes, userInitiated: true));
        _durationSeekBar.SetAccessibilityDelegate(_durationSeekAccessibilityDelegate);
    }

    private void ShowAppScreen(AppScreen screen)
    {
        bool animate = _hasRenderedScreen && screen != _appScreen;
        _appScreen = screen;

        View target = screen switch
        {
            AppScreen.Duration => _durationScreen,
            AppScreen.Workout => _workoutScreen,
            AppScreen.Completion => _congratulationsScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(screen)),
        };

        foreach (View candidate in new[]
                 {
                     _durationScreen,
                     _workoutScreen,
                     _congratulationsScreen,
                 })
        {
            candidate.Animate()?.Cancel();
            candidate.Alpha = 1f;
            candidate.TranslationY = 0f;
            candidate.Visibility = candidate == target
                ? ViewStates.Visible
                : ViewStates.Gone;
            candidate.ImportantForAccessibility = candidate == target
                ? ImportantForAccessibility.Auto
                : ImportantForAccessibility.NoHideDescendants;
        }

        if (screen == AppScreen.Workout)
        {
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        }
        else
        {
            Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        }

        ApplySystemBarAppearance();
        GetInsetContent(screen).RequestApplyInsets();

        if (animate)
        {
            if (screen == AppScreen.Workout)
            {
                AnimateViewIn(_workoutHeader, 8f);
            }
            else
            {
                AnimateViewIn(target, 10f);
            }
        }

        _hasRenderedScreen = true;
    }

    private void ApplySystemBarAppearance()
    {
        if (_systemBarsControllers.Length == 0)
        {
            return;
        }

        _systemBarsControllers[0].SetAppearance(_appScreen != AppScreen.Completion);
    }

    private View GetInsetContent(AppScreen screen)
    {
        return screen switch
        {
            AppScreen.Duration => _durationInsetContent,
            AppScreen.Workout => _workoutInsetContent,
            AppScreen.Completion => _completionInsetContent,
            _ => throw new ArgumentOutOfRangeException(nameof(screen)),
        };
    }

    private void ShowWorkoutPhase(WorkoutPhase phase, bool animate = true)
    {
        _workoutPhase = phase;
        View target = phase switch
        {
            WorkoutPhase.Ready => _readyPanel,
            WorkoutPhase.Move => _countdownPanel,
            WorkoutPhase.Rest => _restPanel,
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        foreach (View candidate in new[] { _readyPanel, _countdownPanel, _restPanel })
        {
            candidate.Animate()?.Cancel();
            candidate.Alpha = 1f;
            candidate.TranslationY = 0f;
            candidate.Visibility = candidate == target
                ? ViewStates.Visible
                : ViewStates.Invisible;
            candidate.ImportantForAccessibility = candidate == target
                ? ImportantForAccessibility.Auto
                : ImportantForAccessibility.NoHideDescendants;
        }

        if (animate && _hasRenderedScreen)
        {
            AnimateViewIn(target, 8f);
        }
    }

    private void AnimateViewIn(View view, float translationDp)
    {
        view.Animate()?.Cancel();
        view.Alpha = 0f;
        view.TranslationY = Dp(translationDp);
        if (view.Animate() is { } animator)
        {
            animator
                .Alpha(1f)
                .TranslationY(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void CancelUiAnimations()
    {
        foreach (View view in new[]
                 {
                     _durationScreen,
                     _workoutScreen,
                     _congratulationsScreen,
                     _workoutHeader,
                     _readyPanel,
                     _countdownPanel,
                     _restPanel,
                     _completionMark,
                 })
        {
            view.Animate()?.Cancel();
            view.TranslationY = 0f;
            view.ScaleX = 1f;
            view.ScaleY = 1f;
            if (view.Visibility == ViewStates.Visible)
            {
                view.Alpha = 1f;
            }
        }

        _mediaScrim.Animate()?.Cancel();
        if (_mediaLoadingText.Visibility == ViewStates.Visible ||
            _mediaErrorPanel.Visibility == ViewStates.Visible)
        {
            _mediaScrim.Alpha = 1f;
            _mediaScrim.Visibility = ViewStates.Visible;
        }
        else
        {
            _mediaScrim.Alpha = 0f;
            _mediaScrim.Visibility = ViewStates.Gone;
        }
    }

    private float Dp(float value)
    {
        return value * Resources!.DisplayMetrics!.Density;
    }

    private void ResizeMediaCard()
    {
        int size = Math.Min(_exerciseMediaArea.Width, _exerciseMediaArea.Height);
        if (size <= 0 ||
            (_exerciseMediaCard.LayoutParameters?.Width == size &&
             _exerciseMediaCard.LayoutParameters.Height == size))
        {
            return;
        }

        _exerciseMediaCard.LayoutParameters = new FrameLayout.LayoutParams(
            size,
            size,
            GravityFlags.Center);
    }

    private void ShowDurationSelection()
    {
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _restActive = false;
        _exerciseVideo.StopPlayback();
        ClearHoldFrame();
        _currentExercise = null;

        ShowAppScreen(AppScreen.Duration);
        SetSelectedWorkoutMinutes(_state.LastWorkoutMinutes);
    }

    private void SetSelectedWorkoutMinutes(int minutes, bool userInitiated = false)
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
        _durationSummary.Text = GetString(Resource.String.duration_summary_default);
        _durationEndpointText.Text =
            $"{clampedMinutes} groups  ·  ends at {endpointName}";
        _beginWorkoutButton.Text = GetString(Resource.String.duration_start_default);
        _beginWorkoutButton.ContentDescription =
            $"Continue with a {clampedMinutes} {minuteLabel} workout";
        _durationSeekBar.ContentDescription =
            $"Workout duration, {clampedMinutes} {minuteLabel}. Range 3 to 20 minutes";
        _durationRouteSegments.ContentDescription =
            $"{clampedMinutes} of 20 muscle groups selected, from Glutes through {endpointName}";

        _durationDecreaseButton.Enabled =
            clampedMinutes > ExerciseSessionService.MinimumWorkoutMinutes;
        _durationIncreaseButton.Enabled =
            clampedMinutes < ExerciseSessionService.MaximumWorkoutMinutes;
        _durationDecreaseButton.Alpha = _durationDecreaseButton.Enabled ? 1f : 0.42f;
        _durationIncreaseButton.Alpha = _durationIncreaseButton.Enabled ? 1f : 0.42f;

        if (userInitiated)
        {
            _durationMinutesValue.PerformHapticFeedback(FeedbackConstants.ClockTick);
        }

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

    private void FinishInterruptedWorkout()
    {
        Exercise? scorePenalty = _sessionService.FinishInterruptedWorkout(_state);
        SaveStateAndScore(scorePenalty);
    }

    private void RecoverPendingScoreUpdate()
    {
        if (_state.PendingScoreExerciseId <= 0)
        {
            return;
        }

        Exercise? exercise = _exerciseDatabase.Exercises.SingleOrDefault(
            candidate => candidate.Id == _state.PendingScoreExerciseId);
        if (exercise is not null)
        {
            exercise.Score = _state.PendingScoreValue;
            _exerciseDatabase.UpdateScore(exercise);
        }

        _state.PendingScoreExerciseId = 0;
        _state.PendingScoreValue = 0;
        _stateStore.Save(_state);
    }

    private void SaveStateAndScore(Exercise? scorePenalty)
    {
        if (scorePenalty is not null)
        {
            _state.PendingScoreExerciseId = scorePenalty.Id;
            _state.PendingScoreValue = scorePenalty.Score;
        }

        _stateStore.Save(_state);

        if (scorePenalty is null)
        {
            return;
        }

        _exerciseDatabase.UpdateScore(scorePenalty);
        _state.PendingScoreExerciseId = 0;
        _state.PendingScoreValue = 0;
        _stateStore.Save(_state);
    }

    private void ConfigureVideoView()
    {
        _videoPreparedListener = new VideoPreparedListener(mediaPlayer =>
        {
            _mediaReady = true;
            _activeMediaPlayer = mediaPlayer;
            _activeMediaPlayer.Looping = _loopExerciseVideo;
            _activeMediaPlayer.SetVolume(0f, 0f);
            _speedUpButton.Enabled = true;
            _speedUpButton.Alpha = 1f;
            if (_countdownPausedForMediaError && _activityResumed)
            {
                _countdownPausedForMediaError = false;
                ResumeCountdown();
            }
            else if (_activityResumed && _workoutPhase != WorkoutPhase.Rest)
            {
                _exerciseVideo.Start();
            }

            SetStartAvailability(available: true);
            int preparedGeneration = _mediaLoadGeneration;
            _exerciseVideo.PostDelayed(
                () =>
                {
                    if (_mediaReady &&
                        preparedGeneration == _mediaLoadGeneration &&
                        _mediaErrorPanel.Visibility != ViewStates.Visible)
                    {
                        RevealExerciseMedia();
                    }
                },
                250L);
        });
        _exerciseVideo.SetOnPreparedListener(_videoPreparedListener);
        _videoErrorListener = new VideoErrorListener(() =>
        {
            ShowMediaError();
            return true;
        });
        _exerciseVideo.SetOnErrorListener(_videoErrorListener);
        _videoInfoListener = new VideoInfoListener(mediaInfo =>
        {
            if (mediaInfo == Android.Media.MediaInfo.VideoRenderingStart)
            {
                RevealExerciseMedia();
            }

            return false;
        });
        _exerciseVideo.SetOnInfoListener(_videoInfoListener);
        _exerciseVideo.Completion += (_, _) =>
        {
            if (_freezeHoldAtEnd)
            {
                FreezeHoldOnFinalFrame();
            }
        };
    }

    private void LoadExerciseMedia(Exercise exercise, bool forceCacheRefresh = false)
    {
        bool holdDuringMove =
            exercise.Mode == ExerciseMode.Hold && _workoutPhase == WorkoutPhase.Move;
        bool holdDuringRest =
            exercise.Mode == ExerciseMode.Hold && _workoutPhase == WorkoutPhase.Rest;
        _mediaLoadGeneration++;
        _mediaReady = false;
        _loopExerciseVideo = !holdDuringMove && !holdDuringRest;
        _freezeHoldAtEnd = holdDuringMove || holdDuringRest;
        _activeMediaPlayer = null;

        if (holdDuringRest)
        {
            _exerciseVideo.Pause();
            _mediaErrorPanel.Visibility = ViewStates.Gone;
            _mediaLoadingText.Visibility = ViewStates.Gone;
            ShowHoldFrame(exercise.Id);
            return;
        }

        ClearHoldFrame();
        _exerciseVideo.StopPlayback();
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingText.Visibility = ViewStates.Visible;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Move)
        {
            _speedUpButton.Enabled = false;
            _speedUpButton.Alpha = 0.5f;
        }

        try
        {
            _exerciseVideo.SetVideoPath(
                CacheVideoAsset(exercise.Video, forceCacheRefresh));
        }
        catch (Exception)
        {
            ShowMediaError();
        }
    }

    private string CacheVideoAsset(string assetPath, bool forceRefresh)
    {
        string cacheRoot = System.IO.Path.Combine(CacheDir!.AbsolutePath, "exercise-videos-v4");
        Directory.CreateDirectory(cacheRoot);
        string cachedPath = System.IO.Path.Combine(cacheRoot, System.IO.Path.GetFileName(assetPath));
        string temporaryPath = cachedPath + ".tmp";

        using Stream source = Assets!.Open(assetPath);
        long expectedLength = source.CanSeek ? source.Length : -1;

        if (!forceRefresh &&
            File.Exists(cachedPath) &&
            new FileInfo(cachedPath).Length > 0 &&
            (expectedLength < 0 || new FileInfo(cachedPath).Length == expectedLength))
        {
            return cachedPath;
        }

        try
        {
            using (FileStream destination = File.Create(temporaryPath))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
                if (expectedLength >= 0 && destination.Length != expectedLength)
                {
                    throw new IOException("The cached exercise video is incomplete.");
                }
            }

            File.Move(temporaryPath, cachedPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        return cachedPath;
    }

    private void SetStartAvailability(bool available)
    {
        if (_workoutPhase != WorkoutPhase.Ready)
        {
            return;
        }

        _startButton.Enabled = available;
        _startButton.Alpha = available ? 1f : 0.5f;
        _startButton.Text = available
            ? GetString(Resource.String.start)
            : GetString(Resource.String.media_loading);
        _startButton.ContentDescription = available
            ? GetString(Resource.String.start)
            : GetString(Resource.String.media_loading);
    }

    private void RevealExerciseMedia()
    {
        if (_mediaErrorPanel.Visibility == ViewStates.Visible)
        {
            return;
        }

        int revealGeneration = _mediaLoadGeneration;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        _mediaLoadingText.Visibility = ViewStates.Gone;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaScrim.Animate()?.Cancel();
        if (_mediaScrim.Animate() is { } animator)
        {
            animator
                .Alpha(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _mediaScrim.PostDelayed(
            () =>
            {
                if (revealGeneration == _mediaLoadGeneration &&
                    _mediaLoadingText.Visibility != ViewStates.Visible &&
                    _mediaErrorPanel.Visibility != ViewStates.Visible)
                {
                    _mediaScrim.Alpha = 0f;
                    _mediaScrim.Visibility = ViewStates.Gone;
                }
            },
            PhaseMotionDurationMilliseconds + 20L);
    }

    private void ShowMediaError()
    {
        _mediaReady = false;
        if (_workoutPhase == WorkoutPhase.Move &&
            (_countdownActive || _countdownPaused))
        {
            if (_countdownActive)
            {
                PauseCountdown();
            }
            _countdownPausedForMediaError = true;
        }

        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Ready)
        {
            _startButton.Text = GetString(Resource.String.media_error);
            _startButton.ContentDescription = GetString(Resource.String.media_error);
        }
        _speedUpButton.Enabled = false;
        _speedUpButton.Alpha = 0.5f;
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingText.Visibility = ViewStates.Gone;
        _mediaErrorPanel.Visibility = ViewStates.Visible;
        AnnouncePhaseForAccessibility(
            _mediaErrorPanel,
            GetString(Resource.String.media_error));
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

        try
        {
            string assetPath = $"exercise_hold_frames/exercise_{exerciseId:D4}.png";
            using Stream stream = Assets!.Open(assetPath);
            Android.Graphics.Bitmap bitmap = Android.Graphics.BitmapFactory.DecodeStream(stream)
                ?? throw new InvalidOperationException(
                    $"Unable to decode the reviewed hold frame for exercise {exerciseId}.");

            _mediaReady = true;
            _holdFrameBitmap = bitmap;
            _holdFrameImage.SetImageBitmap(bitmap);
            _holdFrameImage.Visibility = ViewStates.Visible;
            RevealExerciseMedia();
        }
        catch (Exception)
        {
            ShowMediaError();
        }
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

        string muscleGroupName = GetMuscleGroupDisplayName(_currentMuscleGroup);
        _workoutProgressText.Text = $"{position:D2}  /  {_state.ActiveWorkoutMinutes:D2}";
        _workoutProgressText.ContentDescription =
            $"Round {position} of {_state.ActiveWorkoutMinutes}";
        _workoutProgressBar.Max = _state.ActiveWorkoutMinutes;
        _workoutProgressBar.Progress = position;
        _muscleGroupName.Text = muscleGroupName.ToUpperInvariant();
        _exerciseName.Text = exercise.Name;
        _exerciseName.ContentDescription = exercise.Mode == ExerciseMode.Hold
            ? $"{exercise.Name}. Hold."
            : $"{exercise.Name}. Repetition.";
        _exerciseModeBadge.Visibility = exercise.Mode == ExerciseMode.Hold
            ? ViewStates.Visible
            : ViewStates.Gone;
        ShowAppScreen(AppScreen.Workout);
        ShowStartButton();
        LoadExerciseMedia(exercise);
        ResizeMediaCard();
        AnnouncePhaseForAccessibility(
            _workoutHeader,
            $"Round {position} of {_state.ActiveWorkoutMinutes}. " +
            $"{muscleGroupName}. {exercise.Name}. " +
            (exercise.Mode == ExerciseMode.Hold ? "Hold." : "Repetition."));
    }

    private void ShowStartButton()
    {
        _startButton.Enabled = true;
        _startButton.Alpha = 1f;
        _startButton.Text = GetString(Resource.String.start);
        _startButton.ContentDescription = GetString(Resource.String.start);
        ShowWorkoutPhase(WorkoutPhase.Ready);
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
        ShowWorkoutPhase(WorkoutPhase.Move);
        UpdateMoveCountdown(CountdownSeconds);
        AnnouncePhaseForAccessibility(_countdownPanel, "Move, 45 seconds.");
        StartCountdownTimer(CountdownSeconds * 1000L);
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
        _countdownPaused = false;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownMillisecondsRemaining = 0;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        UpdateMoveCountdown(0);
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        PlayBeep(Android.Media.Tone.PropBeep2);

        BeginRest();
    }

    private void CancelCountdown(bool resetToStart)
    {
        if (!_countdownActive && !_countdownPaused)
        {
            return;
        }

        _countdownActive = false;
        _countdownPaused = false;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownMillisecondsRemaining = 0;
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

    private void PauseCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        _countdownMillisecondsRemaining = Math.Max(
            1,
            _countdownEndsAtElapsedMilliseconds -
                Android.OS.SystemClock.ElapsedRealtime());
        _countdownActive = false;
        _countdownPaused = true;
        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    private void ResumeCountdown()
    {
        if (!_countdownPaused || _countdownMillisecondsRemaining <= 0)
        {
            return;
        }

        if (_holdFrameImage.Visibility != ViewStates.Visible)
        {
            _exerciseVideo.Start();
        }

        StartCountdownTimer(_countdownMillisecondsRemaining);
    }

    private void StartCountdownTimer(long millisecondsRemaining)
    {
        _countdownActive = true;
        _countdownPaused = false;
        _countdownMillisecondsRemaining = millisecondsRemaining;
        _countdownEndsAtElapsedMilliseconds =
            Android.OS.SystemClock.ElapsedRealtime() + millisecondsRemaining;
        UpdateMoveCountdown((int)Math.Ceiling(millisecondsRemaining / 1000d));

        _countdownTimer = new WorkoutCountDownTimer(
            millisecondsRemaining,
            1000L,
            UpdateMoveCountdown,
            CompleteCountdown);
        _countdownTimer.Start();
    }

    private void UpdateMoveCountdown(int secondsRemaining)
    {
        int seconds = Math.Clamp(secondsRemaining, 0, CountdownSeconds);
        _countdownText.Text = seconds.ToString();
        _countdownText.ContentDescription = $"{seconds} seconds remaining";
        _countdownProgress.Progress = seconds;
    }

    private void BeginRest()
    {
        _state.PendingRestMuscleGroup = _currentMuscleGroup;
        _state.PendingRestEndsAtUnixMilliseconds =
            DateTimeOffset.UtcNow.AddSeconds(RestSeconds).ToUnixTimeMilliseconds();
        _state.PendingRestKept = false;
        _stateStore.Save(_state);

        _restActive = true;
        _exerciseVideo.Pause();
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        ShowRestPanel();
        AnnouncePhaseForAccessibility(
            _restPanel,
            "Rest, 15 seconds. Tap to keep this exercise.");
        ResumeRestCountdown();
    }

    private void ShowRestPanel()
    {
        ShowWorkoutPhase(WorkoutPhase.Rest);
        _keepButton.Enabled = !_state.PendingRestKept;
        _keepButton.Alpha = 1f;
        _keepButton.Text = _state.PendingRestKept
            ? GetString(Resource.String.kept)
            : GetString(Resource.String.tap_to_keep);
        _keepButton.SetBackgroundResource(_state.PendingRestKept
            ? Resource.Drawable.kept_button_background
            : Resource.Drawable.keep_button_background);
        _keepButton.SetTextColor(new Android.Graphics.Color(GetColor(
            _state.PendingRestKept
                ? Resource.Color.accent_text
                : Resource.Color.white)));
        _keepButton.ContentDescription = _state.PendingRestKept
            ? "Exercise kept for the next session"
            : GetString(Resource.String.tap_to_keep_description);
        _restHelperText.Text = _state.PendingRestKept
            ? GetString(Resource.String.rest_kept_helper)
            : GetString(Resource.String.rest_helper);

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
        _restCountdownText.Text = secondsRemaining.ToString();
        _restCountdownText.ContentDescription =
            $"Rest, {secondsRemaining} seconds remaining";
        _restProgress.Progress = secondsRemaining;
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
        _keepButton.Alpha = 1f;
        _keepButton.Text = GetString(Resource.String.kept);
        _keepButton.SetBackgroundResource(Resource.Drawable.kept_button_background);
        _keepButton.SetTextColor(new Android.Graphics.Color(
            GetColor(Resource.Color.accent_text)));
        _keepButton.ContentDescription = "Exercise kept for the next session";
        _restHelperText.Text = GetString(Resource.String.rest_kept_helper);
        _keepButton.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        AnnouncePhaseForAccessibility(_keepButton, "Exercise kept.");
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

        SaveStateAndScore(keep ? null : exercise);
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

        _congratulationsDuration.Text =
            $"{_state.ActiveWorkoutMinutes} minute session";
        _congratulationsSummary.Text =
            $"{kept} kept  ·  {replaced} rotating next time";
        ShowAppScreen(AppScreen.Completion);
        _completionMark.Animate()?.Cancel();
        _completionMark.Alpha = 0f;
        _completionMark.ScaleX = 0.86f;
        _completionMark.ScaleY = 0.86f;
        if (_completionMark.Animate() is { } animator)
        {
            animator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(220L)
                .Start();
        }
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

    private sealed class VideoErrorListener(Func<bool> onError)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnErrorListener
    {
        public bool OnError(
            Android.Media.MediaPlayer? mediaPlayer,
            Android.Media.MediaError what,
            int extra)
        {
            return onError();
        }
    }

    private sealed class VideoInfoListener(Func<Android.Media.MediaInfo, bool> onInfo)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnInfoListener
    {
        public bool OnInfo(
            Android.Media.MediaPlayer? mediaPlayer,
            Android.Media.MediaInfo what,
            int extra)
        {
            return onInfo(what);
        }
    }

    private sealed class DurationSeekAccessibilityDelegate(
        Func<int> getMinutes,
        Action<int> setMinutes) : View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(
            View host,
            Android.Views.Accessibility.AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            int minutes = getMinutes();
#pragma warning disable CA1422 // Obtain is required for the supported API 24-29 range.
            info.SetRangeInfo(
                Android.Views.Accessibility.AccessibilityNodeInfo.RangeInfo.Obtain(
                    Android.Views.Accessibility.RangeType.Int,
                    ExerciseSessionService.MinimumWorkoutMinutes,
                    ExerciseSessionService.MaximumWorkoutMinutes,
                    minutes));
#pragma warning restore CA1422
            info.ContentDescription =
                $"Workout duration, {minutes} minutes. Range 3 to 20 minutes";
        }

        public override bool PerformAccessibilityAction(
            View host,
            Android.Views.Accessibility.Action action,
            Bundle? arguments)
        {
            if ((int)action == Android.Resource.Id.AccessibilityActionSetProgress &&
                arguments is not null)
            {
                float requestedMinutes = arguments.GetFloat(
                    Android.Views.Accessibility.AccessibilityNodeInfo
                        .ActionArgumentProgressValue);
                setMinutes((int)MathF.Round(requestedMinutes));
                return true;
            }

            return base.PerformAccessibilityAction(host, action, arguments);
        }
    }
}
