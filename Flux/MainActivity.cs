using Android.Views;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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
        Android.Content.PM.ConfigChanges.SmallestScreenSize |
        Android.Content.PM.ConfigChanges.UiMode |
        Android.Content.PM.ConfigChanges.Locale |
        Android.Content.PM.ConfigChanges.LayoutDirection |
        Android.Content.PM.ConfigChanges.FontScale)]
public class MainActivity : Activity
{
    private const int CountdownSeconds = 45;
    private const int DirectionSecondPhaseOffsetMilliseconds = 20_000;
    private const int DirectionSegmentDurationMilliseconds = 20_000;
    private const int RestSeconds = 15;
    private const long PhaseMotionDurationMilliseconds = 160L;
    private const long HueMotionDurationMilliseconds = 120L;
    private const long ModifierFeedbackHoldMilliseconds = 560L;
    private const float PlaybackControlEnabledAlpha = 1f;
    private const float PlaybackControlDisabledAlpha = 0.35f;

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
    private WorkoutGroup _currentWorkoutGroup = null!;
    private Exercise? _currentExercise;
    private int _selectedWorkoutMinutes = ExerciseSessionService.DefaultWorkoutMinutes;
    private WorkoutModifiers _selectedWorkoutModifiers =
        ExerciseSessionService.DefaultWorkoutModifiers;

    private View _durationScreen = null!;
    private LinearLayout _durationInsetContent = null!;
    private ScrollView _durationScroll = null!;
    private LinearLayout _durationContent = null!;
    private LinearLayout _durationIdentity = null!;
    private ImageView _durationAppIcon = null!;
    private View _durationDial = null!;
    private LinearLayout _durationControls = null!;
    private LinearLayout _durationStepRow = null!;
    private FrameLayout _durationOptionLabels = null!;
    private GridLayout _durationModifierGrid = null!;
    private CheckBox _insectModifierButton = null!;
    private CheckBox _silenceModifierButton = null!;
    private CheckBox _mirrorModifierButton = null!;
    private TextView _durationModifierFeedback = null!;
    private int _modifierFeedbackGeneration;
    private FrameLayout _durationActionBar = null!;
    private TextView _durationMinutesValue = null!;
    private Button _durationDecreaseButton = null!;
    private SeekBar _durationSeekBar = null!;
    private Button _durationIncreaseButton = null!;
    private LinearLayout _durationOptionSegments = null!;
    private Button _beginWorkoutButton = null!;
    private View _workoutScreen = null!;
    private View _workoutPhaseSurface = null!;
    private View _workoutPhaseLeft = null!;
    private View _workoutPhaseRight = null!;
    private LinearLayout _workoutInsetContent = null!;
    private LinearLayout _workoutHeader = null!;
    private TextView _workoutProgressText = null!;
    private ProgressBar _workoutProgressBar = null!;
    private View _congratulationsScreen = null!;
    private LinearLayout _completionInsetContent = null!;
    private FrameLayout _completionHero = null!;
    private View _completionHalo = null!;
    private FrameLayout _completionActionBar = null!;
    private TextView _exerciseName = null!;
    private TextView _exerciseModeBadge = null!;
    private FrameLayout _executionSignifier = null!;
    private ImageView _executionSignifierIcon = null!;
    private FrameLayout _exerciseMediaArea = null!;
    private View _exerciseMediaCard = null!;
    private VideoView _exerciseVideo = null!;
    private ImageView _holdFrameImage = null!;
    private View _mediaScrim = null!;
    private ProgressBar _mediaLoadingIndicator = null!;
    private View _mediaErrorPanel = null!;
    private Button _mediaRetryButton = null!;
    private FrameLayout _workoutActionHost = null!;
    private LinearLayout _readyPanel = null!;
    private ImageButton _shuffleButton = null!;
    private ImageButton _startButton = null!;
    private LinearLayout _countdownPanel = null!;
    private TextView _countdownText = null!;
    private ProgressBar _countdownProgress = null!;
    private ImageButton _repeatAction = null!;
    private ImageButton _playbackAction = null!;
    private ImageButton _nextAction = null!;
    private LinearLayout _restPanel = null!;
    private TextView _restCountdownText = null!;
    private ProgressBar _restProgress = null!;
    private ImageButton _keepButton = null!;
    private ImageView _completionMark = null!;
    private Button _doneButton = null!;
    private SystemBarsController[] _systemBarsControllers = [];
    private DurationSeekAccessibilityDelegate? _durationSeekAccessibilityDelegate;

    private WorkoutCountDownTimer? _countdownTimer;
    private WorkoutCountDownTimer? _restTimer;
    private Android.Media.SoundPool? _whistleSoundPool;
    private int _movementStartWhistleId;
    private int _sideChangeWhistleId;
    private int _restStartWhistleId;
    private int _workoutCompleteWhistleId;
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
    private bool _countdownPausedByUser;
    private int _mediaLoadGeneration;
    private int _revealedMediaGeneration = -1;
    private bool _hasRenderedScreen;
    private string? _exerciseVideoCacheRoot;
    private AppScreen _appScreen = AppScreen.Duration;
    private WorkoutPhase _workoutPhase = WorkoutPhase.Ready;
    private MovementPhase? _lastMovementPhase;

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
        ConfigureWhistleCues();

        _exerciseDatabase = new SqliteExerciseDatabase(this);
        _sessionService = new ExerciseSessionService(_exerciseDatabase.Exercises);
        _stateStore = new SharedPreferencesWorkoutStateStore(this);
        _state = _stateStore.Load();
        _sessionService.Initialize(_state);
        RecoverPendingScoreUpdate();

        WorkoutGroup? pendingMovementGroup =
            _sessionService.GetPendingMovementGroup(_state);
        WorkoutGroup? pendingRestGroup =
            _sessionService.GetPendingRestGroup(_state);

        if (!_state.WorkoutCompleted &&
            _state.ActiveWorkoutMinutes != 0 &&
            pendingMovementGroup is null &&
            pendingRestGroup is null)
        {
            FinishInterruptedWorkout();
        }
        else
        {
            _stateStore.Save(_state);
        }

        _selectedWorkoutMinutes = _state.LastWorkoutMinutes;
        _selectedWorkoutModifiers = _state.LastWorkoutModifiers;

        if (_state.WorkoutCompleted && !_state.CompletionAcknowledged)
        {
            ShowCongratulations();
        }
        else if (pendingMovementGroup is not null)
        {
            RestorePendingMovement(pendingMovementGroup);
        }
        else if (pendingRestGroup is not null)
        {
            RestorePendingRest(pendingRestGroup);
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
                 !_countdownPausedByUser &&
                 (!_countdownPausedForMediaError || _mediaReady))
        {
            _countdownPausedForMediaError = false;
            ResumeCountdown();
        }
        else if (_currentExercise is not null && ShouldExerciseVideoBePlaying())
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
        ConfigureResponsiveText();
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
        _whistleSoundPool?.Release();
        _whistleSoundPool?.Dispose();
        _whistleSoundPool = null;
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
        _durationInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.duration_inset_content);
        _durationScroll = FindRequiredView<ScrollView>(Resource.Id.duration_scroll);
        _durationContent = FindRequiredView<LinearLayout>(Resource.Id.duration_content);
        _durationIdentity = FindRequiredView<LinearLayout>(Resource.Id.duration_identity);
        _durationAppIcon = FindRequiredView<ImageView>(Resource.Id.duration_app_icon);
        _durationDial = FindRequiredView<View>(Resource.Id.duration_dial);
        _durationControls = FindRequiredView<LinearLayout>(Resource.Id.duration_controls);
        _durationStepRow = FindRequiredView<LinearLayout>(Resource.Id.duration_step_row);
        _durationOptionLabels = FindRequiredView<FrameLayout>(
            Resource.Id.duration_option_labels);
        _durationModifierGrid = FindRequiredView<GridLayout>(
            Resource.Id.duration_modifier_grid);
        _insectModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.insect_modifier_button);
        _silenceModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.silence_modifier_button);
        _mirrorModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.mirror_modifier_button);
        _durationModifierFeedback = FindRequiredView<TextView>(
            Resource.Id.duration_modifier_feedback);
        _durationActionBar = FindRequiredView<FrameLayout>(
            Resource.Id.duration_action_bar);
        _durationMinutesValue = FindRequiredView<TextView>(
            Resource.Id.duration_minutes_value);
        _durationDecreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_decrease_button);
        _durationSeekBar = FindRequiredView<SeekBar>(Resource.Id.duration_seek_bar);
        _durationIncreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_increase_button);
        _durationOptionSegments = FindRequiredView<LinearLayout>(
            Resource.Id.duration_option_segments);
        _beginWorkoutButton = FindRequiredView<Button>(Resource.Id.begin_workout_button);
        _workoutScreen = FindRequiredView<View>(Resource.Id.workout_screen);
        _workoutPhaseSurface = FindRequiredView<View>(
            Resource.Id.workout_phase_surface);
        _workoutPhaseLeft = FindRequiredView<View>(Resource.Id.workout_phase_left);
        _workoutPhaseRight = FindRequiredView<View>(Resource.Id.workout_phase_right);
        _workoutInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.workout_inset_content);
        _workoutHeader = FindRequiredView<LinearLayout>(Resource.Id.workout_header);
        _workoutProgressText = FindRequiredView<TextView>(Resource.Id.workout_progress_text);
        _workoutProgressBar = FindRequiredView<ProgressBar>(Resource.Id.workout_progress_bar);
        _congratulationsScreen = FindRequiredView<View>(Resource.Id.congratulations_screen);
        _completionInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.completion_inset_content);
        _completionHero = FindRequiredView<FrameLayout>(Resource.Id.completion_hero);
        _completionHalo = FindRequiredView<View>(Resource.Id.completion_halo);
        _completionActionBar = FindRequiredView<FrameLayout>(
            Resource.Id.completion_action_bar);
        _exerciseName = FindRequiredView<TextView>(Resource.Id.exercise_name);
        _exerciseModeBadge = FindRequiredView<TextView>(Resource.Id.exercise_mode_badge);
        _executionSignifier = FindRequiredView<FrameLayout>(
            Resource.Id.execution_signifier);
        _executionSignifierIcon = FindRequiredView<ImageView>(
            Resource.Id.execution_signifier_icon);
        _exerciseMediaArea = FindRequiredView<FrameLayout>(
            Resource.Id.exercise_media_area);
        _exerciseMediaCard = FindRequiredView<View>(Resource.Id.exercise_media_card);
        _exerciseVideo = FindRequiredView<VideoView>(Resource.Id.exercise_video);
        _holdFrameImage = FindRequiredView<ImageView>(Resource.Id.hold_frame_image);
        _mediaScrim = FindRequiredView<View>(Resource.Id.media_scrim);
        _mediaLoadingIndicator = FindRequiredView<ProgressBar>(
            Resource.Id.media_loading_indicator);
        _mediaErrorPanel = FindRequiredView<View>(Resource.Id.media_error_panel);
        _mediaRetryButton = FindRequiredView<Button>(Resource.Id.media_retry_button);
        _workoutActionHost = FindRequiredView<FrameLayout>(
            Resource.Id.workout_action_host);
        _readyPanel = FindRequiredView<LinearLayout>(Resource.Id.ready_panel);
        _shuffleButton = FindRequiredView<ImageButton>(Resource.Id.shuffle_button);
        _startButton = FindRequiredView<ImageButton>(Resource.Id.start_button);
        _countdownPanel = FindRequiredView<LinearLayout>(Resource.Id.countdown_panel);
        _countdownText = FindRequiredView<TextView>(Resource.Id.countdown_text);
        _countdownProgress = FindRequiredView<ProgressBar>(Resource.Id.countdown_progress);
        _repeatAction = FindRequiredView<ImageButton>(Resource.Id.repeat_action);
        _playbackAction = FindRequiredView<ImageButton>(Resource.Id.playback_action);
        _nextAction = FindRequiredView<ImageButton>(Resource.Id.next_action);
        _restPanel = FindRequiredView<LinearLayout>(Resource.Id.rest_panel);
        _restCountdownText = FindRequiredView<TextView>(Resource.Id.rest_countdown_text);
        _restProgress = FindRequiredView<ProgressBar>(Resource.Id.rest_progress);
        _keepButton = FindRequiredView<ImageButton>(Resource.Id.keep_button);
        _completionMark = FindRequiredView<ImageView>(Resource.Id.completion_mark);
        _doneButton = FindRequiredView<Button>(Resource.Id.done_button);

        _exerciseMediaArea.LayoutChange += (_, _) => ResizeMediaCard();
        _completionHero.LayoutChange += (_, _) => ResizeCompletionHalo();
        _durationSeekBar.LayoutChange += (_, _) => AlignDurationOptionLabels();
        _durationOptionLabels.LayoutChange += (_, _) =>
            AlignDurationOptionLabels();
    }

    private void BindEvents()
    {
        _durationDecreaseButton.Click += (_, _) =>
            StepSelectedWorkoutMinutes(-1);
        _durationIncreaseButton.Click += (_, _) =>
            StepSelectedWorkoutMinutes(1);
        _durationSeekBar.ProgressChanged += (_, eventArgs) =>
        {
            if (eventArgs.FromUser)
            {
                SetSelectedWorkoutMinutes(
                    ExerciseSessionService.SupportedWorkoutMinutes[eventArgs.Progress],
                    userInitiated: true);
            }
        };
        _insectModifierButton.Click += (_, _) =>
        {
            bool enabled = _insectModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Insect,
                enabled,
                _insectModifierButton,
                Resource.String.insect_modifier_description,
                Resource.String.insect_modifier_on,
                Resource.String.insect_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Insect,
                enabled));
        };
        _silenceModifierButton.Click += (_, _) =>
        {
            bool enabled = _silenceModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Silence,
                enabled,
                _silenceModifierButton,
                Resource.String.silence_modifier_description,
                Resource.String.silence_modifier_on,
                Resource.String.silence_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Silence,
                enabled));
        };
        _mirrorModifierButton.Click += (_, _) =>
        {
            MirrorEquipment nextEquipment = WorkoutModifierPolicy
                .GetMirrorEquipment(_selectedWorkoutModifiers) switch
            {
                MirrorEquipment.None => MirrorEquipment.Compact,
                MirrorEquipment.Compact => MirrorEquipment.Tall,
                MirrorEquipment.Tall => MirrorEquipment.None,
                _ => throw new InvalidOperationException(
                    "Unknown mirror equipment state."),
            };
            SetSelectedMirrorEquipment(nextEquipment, userInitiated: true);
            ShowModifierFeedback(
                GetMirrorFeedbackResourceId(nextEquipment));
        };
        _beginWorkoutButton.Click += (_, _) => StartSelectedWorkout();
        _shuffleButton.Click += (_, _) => ShuffleCurrentExercise();
        _startButton.Click += (_, _) => StartCountdown();
        _repeatAction.Click += (_, _) => RepeatExercise();
        _playbackAction.Click += (_, _) => TogglePlayback();
        _nextAction.Click += (_, _) => GoToNextExercise();
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
        bool landscape = IsLandscape();
        _exerciseName.SetMaxLines(landscape ? 3 : 4);
        _countdownText.SetMaxLines(1);
        _restCountdownText.SetMaxLines(1);
        _durationDecreaseButton.SetMaxLines(1);
        _durationIncreaseButton.SetMaxLines(1);
        _doneButton.SetMaxLines(2);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _durationMinutesValue.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 36 : 56,
                landscape ? 88 : 108,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _exerciseName.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 11 : 16,
                landscape ? 19 : 23,
                1,
                (int)Android.Util.ComplexUnitType.Sp);
            _countdownText.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 28 : 32,
                landscape ? 56 : 60,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _restCountdownText.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 24 : 28,
                40,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _beginWorkoutButton.SetAutoSizeTextTypeUniformWithConfiguration(
                24,
                34,
                1,
                (int)Android.Util.ComplexUnitType.Sp);
            foreach (Button stepButton in new[]
                     {
                         _durationDecreaseButton,
                         _durationIncreaseButton,
                     })
            {
                stepButton.SetAutoSizeTextTypeUniformWithConfiguration(
                    18,
                    28,
                    1,
                    (int)Android.Util.ComplexUnitType.Sp);
            }
            _doneButton.SetAutoSizeTextTypeUniformWithConfiguration(
                14,
                20,
                1,
                (int)Android.Util.ComplexUnitType.Sp);

            return;
        }

        // Native TextView auto-sizing starts at API 26. Keep the same fitted
        // hierarchy on API 24-25 by compensating for large system font scales;
        // the values remain at least as large as the auto-size minimums above.
        float fontScale = Math.Max(
            1f,
            Resources?.Configuration?.FontScale ?? 1f);
        SetResponsiveTextSize(
            _durationMinutesValue,
            landscape ? 36f : 56f,
            landscape ? 88f : 108f,
            fontScale);
        SetResponsiveTextSize(
            _exerciseName,
            landscape ? 11f : 16f,
            landscape ? 19f : 23f,
            fontScale);
        SetResponsiveTextSize(
            _countdownText,
            landscape ? 28f : 32f,
            landscape ? 56f : 60f,
            fontScale);
        SetResponsiveTextSize(
            _restCountdownText,
            landscape ? 24f : 28f,
            40f,
            fontScale);
        SetResponsiveTextSize(_beginWorkoutButton, 24f, 34f, fontScale);
        SetResponsiveTextSize(_durationDecreaseButton, 18f, 28f, fontScale);
        SetResponsiveTextSize(_durationIncreaseButton, 18f, 28f, fontScale);
        SetResponsiveTextSize(_doneButton, 14f, 20f, fontScale);
    }

    private void ApplyResponsiveDimensions()
    {
        bool landscape = IsLandscape();
        _exerciseMediaArea.SetMinimumHeight(
            Resources!.GetDimensionPixelSize(Resource.Dimension.workout_media_min_height));
        _readyPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.ready_panel_min_height));
        _countdownPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.move_panel_min_height));
        _restPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.rest_panel_min_height));

        ApplyDurationLayout(landscape);
        ApplyWorkoutLayout(landscape);
        ApplyCompletionLayout(landscape);
        _exerciseMediaArea.Post(ResizeMediaCard);
        _completionHero.Post(ResizeCompletionHalo);
    }

    private static void SetResponsiveTextSize(
        TextView view,
        float minimumSp,
        float preferredSp,
        float fontScale)
    {
        view.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            Math.Max(minimumSp, preferredSp / fontScale));
    }

    private bool IsLandscape()
    {
        return Resources?.Configuration?.Orientation ==
            Android.Content.Res.Orientation.Landscape;
    }

    private void ApplyDurationLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;
        var resources = Resources!;
        bool compactLandscape = landscape &&
            resources.Configuration!.ScreenWidthDp < 640;
        int iconSize = compactLandscape
            ? DpInt(32)
            : resources.GetDimensionPixelSize(Resource.Dimension.duration_icon_size);
        int dialSize = compactLandscape
            ? DpInt(140)
            : resources.GetDimensionPixelSize(Resource.Dimension.duration_dial_size);

        _durationInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _durationContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _durationContent.SetGravity(landscape
            ? GravityFlags.Center
            : GravityFlags.CenterHorizontal);
        _durationIdentity.Orientation = Orientation.Vertical;
        _durationIdentity.SetGravity(GravityFlags.Center);

        _durationAppIcon.LayoutParameters = new LinearLayout.LayoutParams(
            iconSize,
            iconSize);
        var dialLayout = new LinearLayout.LayoutParams(dialSize, dialSize);
        dialLayout.TopMargin = DpInt(
            compactLandscape ? 4 : landscape ? 8 : 22);
        _durationDial.LayoutParameters = dialLayout;

        if (landscape)
        {
            _durationScroll.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1f);
            _durationActionBar.LayoutParameters = new LinearLayout.LayoutParams(
                compactLandscape
                    ? DpInt(80)
                    : resources.GetDimensionPixelSize(
                        Resource.Dimension.duration_landscape_action_width),
                matchParent);
            _durationActionBar.SetBackgroundResource(
                Resource.Drawable.duration_action_rail_background);
            _durationActionBar.SetPadding(
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16));
            _durationContent.SetPadding(
                DpInt(compactLandscape ? 8 : 16),
                DpInt(compactLandscape ? 6 : 10),
                DpInt(compactLandscape ? 8 : 16),
                DpInt(compactLandscape ? 6 : 10));

            var identityLayout = new LinearLayout.LayoutParams(
                dialSize + DpInt(compactLandscape ? 20 : 28),
                wrapContent)
            {
                Gravity = GravityFlags.CenterVertical,
            };
            _durationIdentity.LayoutParameters = identityLayout;

            var controlsLayout = new LinearLayout.LayoutParams(
                0,
                wrapContent,
                1f)
            {
                Gravity = GravityFlags.CenterVertical,
            };
            controlsLayout.SetMargins(DpInt(compactLandscape ? 8 : 12), 0, 0, 0);
            _durationControls.LayoutParameters = controlsLayout;

            var stepRowLayout = new LinearLayout.LayoutParams(
                matchParent,
                DpInt(compactLandscape ? 48 : 56));
            _durationStepRow.LayoutParameters = stepRowLayout;
            _durationDecreaseButton.LayoutParameters =
                new LinearLayout.LayoutParams(
                    DpInt(compactLandscape ? 48 : 56),
                    DpInt(compactLandscape ? 48 : 56));
            _durationIncreaseButton.LayoutParameters =
                new LinearLayout.LayoutParams(
                    DpInt(compactLandscape ? 48 : 56),
                    DpInt(compactLandscape ? 48 : 56));
            _durationOptionLabels.SetPadding(0, 0, 0, 0);

            var modifierGridLayout = new LinearLayout.LayoutParams(
                wrapContent,
                wrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
            };
            modifierGridLayout.TopMargin = DpInt(compactLandscape ? 10 : 16);
            _durationModifierGrid.LayoutParameters = modifierGridLayout;
            _durationModifierGrid.ColumnCount = 3;
            SetModifierTileSizes(DpInt(compactLandscape ? 48 : 56));

            var segmentLayout = new LinearLayout.LayoutParams(
                matchParent,
                DpInt(24));
            segmentLayout.TopMargin = DpInt(compactLandscape ? 12 : 20);
            _durationOptionSegments.LayoutParameters = segmentLayout;
            _beginWorkoutButton.LayoutParameters = new FrameLayout.LayoutParams(
                DpInt(compactLandscape ? 60 : 68),
                DpInt(compactLandscape ? 60 : 68),
                GravityFlags.Center);
            _durationOptionLabels.Post(AlignDurationOptionLabels);
            return;
        }

        _durationScroll.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            0,
            1f);
        _durationActionBar.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _durationActionBar.SetBackgroundResource(
            Resource.Drawable.duration_action_background);
        _durationActionBar.SetPadding(
            DpInt(24),
            DpInt(12),
            DpInt(24),
            DpInt(16));
        _durationContent.SetPadding(
            DpInt(24),
            DpInt(24),
            DpInt(24),
            DpInt(24));
        _durationIdentity.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _durationControls.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);

        var portraitStepRowLayout = new LinearLayout.LayoutParams(
            matchParent,
            DpInt(64));
        portraitStepRowLayout.TopMargin = DpInt(28);
        _durationStepRow.LayoutParameters = portraitStepRowLayout;
        _durationDecreaseButton.LayoutParameters =
            new LinearLayout.LayoutParams(DpInt(64), DpInt(64));
        _durationIncreaseButton.LayoutParameters =
            new LinearLayout.LayoutParams(DpInt(64), DpInt(64));
        _durationOptionLabels.SetPadding(0, 0, 0, 0);

        var portraitModifierGridLayout = new LinearLayout.LayoutParams(
            wrapContent,
            wrapContent)
        {
            Gravity = GravityFlags.CenterHorizontal,
        };
        portraitModifierGridLayout.TopMargin = DpInt(32);
        _durationModifierGrid.LayoutParameters = portraitModifierGridLayout;
        _durationModifierGrid.ColumnCount = 3;
        SetModifierTileSizes(DpInt(64));

        var portraitSegmentLayout = new LinearLayout.LayoutParams(
            matchParent,
            DpInt(24));
        portraitSegmentLayout.TopMargin = DpInt(36);
        _durationOptionSegments.LayoutParameters = portraitSegmentLayout;
        _beginWorkoutButton.LayoutParameters = new FrameLayout.LayoutParams(
            matchParent,
            DpInt(68));
        _durationOptionLabels.Post(AlignDurationOptionLabels);
    }

    private void AlignDurationOptionLabels()
    {
        int optionCount = ExerciseSessionService.SupportedWorkoutMinutes.Count;
        if (_durationOptionLabels.Width <= 0 ||
            _durationSeekBar.Width <= 0 ||
            _durationOptionLabels.ChildCount != optionCount ||
            optionCount < 2)
        {
            return;
        }

        int[] seekLocation = new int[2];
        int[] labelsLocation = new int[2];
        _durationSeekBar.GetLocationInWindow(seekLocation);
        _durationOptionLabels.GetLocationInWindow(labelsLocation);

        float trackStart =
            seekLocation[0] - labelsLocation[0] + _durationSeekBar.PaddingLeft;
        float trackEnd =
            seekLocation[0] - labelsLocation[0] +
            _durationSeekBar.Width - _durationSeekBar.PaddingRight;
        bool rightToLeft =
            _durationSeekBar.LayoutDirection == LayoutDirection.Rtl;
        int labelSlotWidth = Math.Max(
            1,
            (int)Math.Floor(
                (trackEnd - trackStart) / (optionCount - 1)));

        for (int index = 0; index < optionCount; index++)
        {
            View label = _durationOptionLabels.GetChildAt(index)
                ?? throw new InvalidOperationException(
                    "A duration option label is missing.");
            if (label.LayoutParameters is FrameLayout.LayoutParams layout &&
                layout.Width != labelSlotWidth)
            {
                layout.Width = labelSlotWidth;
                label.LayoutParameters = layout;
            }

            float fraction = index / (float)(optionCount - 1);
            if (rightToLeft)
            {
                fraction = 1f - fraction;
            }

            float center = trackStart + ((trackEnd - trackStart) * fraction);
            float targetLeft = center - (labelSlotWidth / 2f);
            label.TranslationX = targetLeft - label.Left;
        }
    }

    private void ApplyWorkoutLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;
        int gap = Resources!.GetDimensionPixelSize(
            Resource.Dimension.landscape_content_gap);

        _workoutInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _workoutHeader.SetGravity(landscape
            ? GravityFlags.CenterVertical
            : GravityFlags.Top);

        if (landscape)
        {
            _workoutHeader.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                0.92f);

            var mediaLayout = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1.28f);
            mediaLayout.SetMargins(gap, 0, gap, 0);
            _exerciseMediaArea.LayoutParameters = mediaLayout;

            _workoutActionHost.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                0.96f);
        }
        else
        {
            _workoutHeader.LayoutParameters = new LinearLayout.LayoutParams(
                matchParent,
                wrapContent);

            var mediaLayout = new LinearLayout.LayoutParams(
                matchParent,
                0,
                1f);
            mediaLayout.SetMargins(0, DpInt(12), 0, DpInt(12));
            _exerciseMediaArea.LayoutParameters = mediaLayout;

            _workoutActionHost.LayoutParameters = new LinearLayout.LayoutParams(
                matchParent,
                Resources.GetDimensionPixelSize(
                    Resource.Dimension.workout_action_height));
        }

        foreach (LinearLayout panel in new[]
                 {
                     _readyPanel,
                     _countdownPanel,
                     _restPanel,
                 })
        {
            panel.LayoutParameters = new FrameLayout.LayoutParams(
                matchParent,
                matchParent,
                GravityFlags.Center);
        }
    }

    private void ApplyCompletionLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;

        _completionInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;

        if (landscape)
        {
            _completionHero.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1f);
            _completionActionBar.LayoutParameters = new LinearLayout.LayoutParams(
                Resources!.GetDimensionPixelSize(
                    Resource.Dimension.completion_landscape_action_width),
                matchParent);
            _completionActionBar.SetPadding(
                DpInt(16),
                DpInt(16),
                DpInt(16),
                DpInt(16));
            _doneButton.LayoutParameters = new FrameLayout.LayoutParams(
                matchParent,
                DpInt(68),
                GravityFlags.Center);
            return;
        }

        _completionHero.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            0,
            1f);
        _completionActionBar.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _completionActionBar.SetPadding(
            DpInt(24),
            DpInt(12),
            DpInt(24),
            DpInt(16));
        _doneButton.LayoutParameters = new FrameLayout.LayoutParams(
            matchParent,
            DpInt(68));
    }

    private void ConfigureAccessibility()
    {
        _durationSeekBar.Max =
            ExerciseSessionService.SupportedWorkoutMinutes.Count - 1;
        _durationSeekAccessibilityDelegate = new DurationSeekAccessibilityDelegate(
            () => _selectedWorkoutMinutes,
            () => GetSupportedMinuteIndex(_selectedWorkoutMinutes),
            optionIndex => SetSelectedWorkoutMinutes(
                ExerciseSessionService.SupportedWorkoutMinutes[optionIndex],
                userInitiated: true));
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
            AnimateViewIn(target, 4f);
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
                     _durationDial,
                     _durationMinutesValue,
                     _workoutScreen,
                     _workoutPhaseSurface,
                     _congratulationsScreen,
                     _workoutHeader,
                     _exerciseMediaCard,
                     _readyPanel,
                     _countdownPanel,
                     _restPanel,
                     _completionHalo,
                     _completionMark,
                     _doneButton,
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

        bool mediaIsResting =
            _appScreen == AppScreen.Workout &&
            (_workoutPhase == WorkoutPhase.Rest ||
             (_workoutPhase == WorkoutPhase.Move &&
              _lastMovementPhase is MovementPhase.Preparation or
                  MovementPhase.ChangeSides));
        _exerciseMediaCard.Alpha = mediaIsResting ? 0.92f : 1f;
        _exerciseMediaCard.ScaleX = mediaIsResting ? 0.985f : 1f;
        _exerciseMediaCard.ScaleY = mediaIsResting ? 0.985f : 1f;

        int selectedDurationIndex = GetSupportedMinuteIndex(
            _selectedWorkoutMinutes);
        for (int index = 0; index < _durationOptionLabels.ChildCount; index++)
        {
            View? label = _durationOptionLabels.GetChildAt(index);
            label?.Animate()?.Cancel();
            if (label is null)
            {
                continue;
            }

            bool selected = index == selectedDurationIndex;
            label.Alpha = selected ? 1f : 0.72f;
            label.ScaleX = selected ? 1.08f : 1f;
            label.ScaleY = selected ? 1.08f : 1f;
        }

        _mediaScrim.Animate()?.Cancel();
        if (_mediaLoadingIndicator.Visibility == ViewStates.Visible ||
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

    private int DpInt(float value)
    {
        return (int)Math.Round(Dp(value));
    }

    private GridLayout.LayoutParams CreateModifierTileLayout(int size)
    {
        var layout = new GridLayout.LayoutParams
        {
            Width = size,
            Height = size,
        };
        layout.SetGravity(GravityFlags.Center);
        int margin = DpInt(6);
        layout.SetMargins(margin, margin, margin, margin);
        return layout;
    }

    private void SetModifierTileSizes(int size)
    {
        int padding = GetModifierTilePadding(size);
        for (int index = 0; index < _durationModifierGrid.ChildCount; index++)
        {
            View tile = _durationModifierGrid.GetChildAt(index)
                ?? throw new InvalidOperationException(
                    "A duration modifier tile is missing.");
            tile.LayoutParameters = CreateModifierTileLayout(size);
            tile.SetPadding(padding, padding, padding, padding);
        }

        UpdateMirrorModifierPresentation(
            WorkoutModifierPolicy.GetMirrorEquipment(
                _selectedWorkoutModifiers),
            size);
    }

    private int GetModifierTilePadding(int size) =>
        size <= DpInt(48)
            ? DpInt(10)
            : size <= DpInt(56)
                ? DpInt(12)
                : DpInt(16);

    private void UpdateMirrorModifierPresentation(
        MirrorEquipment equipment,
        int? tileSize = null)
    {
        int drawableResourceId = equipment switch
        {
            MirrorEquipment.Compact => Resource.Drawable.ic_mirror_compact,
            MirrorEquipment.Tall => Resource.Drawable.ic_mirror_tall,
            _ => Resource.Drawable.ic_mirror,
        };
        _mirrorModifierButton.SetCompoundDrawablesWithIntrinsicBounds(
            0,
            drawableResourceId,
            0,
            0);
        _mirrorModifierButton.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            0f);
        int size = tileSize ?? _mirrorModifierButton.LayoutParameters?.Width
            ?? DpInt(64);
        int padding = GetModifierTilePadding(size);
        _mirrorModifierButton.SetPadding(
            padding,
            padding,
            padding,
            padding);
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

    private void ResizeCompletionHalo()
    {
        int availableSize = Math.Min(
            _completionHero.Width,
            _completionHero.Height);
        int maximumSize = Resources!.GetDimensionPixelSize(
            Resource.Dimension.completion_halo_max_size);
        int size = Math.Min(maximumSize, availableSize - DpInt(20));
        if (size <= 0)
        {
            return;
        }

        int padding = Math.Max(DpInt(16), (int)Math.Round(size * 0.16d));
        _completionHalo.SetPadding(padding, padding, padding, padding);
        if (_completionHalo.LayoutParameters?.Width == size &&
            _completionHalo.LayoutParameters.Height == size)
        {
            return;
        }

        _completionHalo.LayoutParameters = new FrameLayout.LayoutParams(
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
        ResetMovementVisuals();
        _currentExercise = null;
        _beginWorkoutButton.Enabled = true;
        _beginWorkoutButton.Alpha = 1f;

        ShowAppScreen(AppScreen.Duration);
        SetSelectedWorkoutMinutes(_state.LastWorkoutMinutes);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Insect,
            (_state.LastWorkoutModifiers & WorkoutModifiers.Insect) != 0,
            _insectModifierButton,
            Resource.String.insect_modifier_description,
            Resource.String.insect_modifier_on,
            Resource.String.insect_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Silence,
            (_state.LastWorkoutModifiers & WorkoutModifiers.Silence) != 0,
            _silenceModifierButton,
            Resource.String.silence_modifier_description,
            Resource.String.silence_modifier_on,
            Resource.String.silence_modifier_off);
        SetSelectedMirrorEquipment(
            WorkoutModifierPolicy.GetMirrorEquipment(
                _state.LastWorkoutModifiers));
    }

    private void SetSelectedWorkoutModifier(
        WorkoutModifiers modifier,
        bool enabled,
        CheckBox button,
        int descriptionResourceId,
        int enabledStateResourceId,
        int disabledStateResourceId,
        bool userInitiated = false)
    {
        _selectedWorkoutModifiers = enabled
            ? _selectedWorkoutModifiers | modifier
            : _selectedWorkoutModifiers & ~modifier;
        _selectedWorkoutModifiers =
            WorkoutModifierPolicy.Normalize(_selectedWorkoutModifiers);
        button.Checked = enabled;
        button.ContentDescription = GetString(descriptionResourceId);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            button.TooltipText = GetString(
                GetModifierFeedbackResourceId(modifier, enabled));
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            button.StateDescription = GetString(enabled
                ? enabledStateResourceId
                : disabledStateResourceId);
        }

        if (!userInitiated)
        {
            return;
        }

        AnimateModifierTile(button);
    }

    private void SetSelectedMirrorEquipment(
        MirrorEquipment equipment,
        bool userInitiated = false)
    {
        _selectedWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            _selectedWorkoutModifiers,
            equipment);
        _mirrorModifierButton.Checked = equipment != MirrorEquipment.None;
        _mirrorModifierButton.Text = string.Empty;
        UpdateMirrorModifierPresentation(equipment);
        int stateResourceId = equipment switch
        {
            MirrorEquipment.Compact => Resource.String.mirror_modifier_compact,
            MirrorEquipment.Tall => Resource.String.mirror_modifier_tall,
            _ => Resource.String.mirror_modifier_off,
        };
        _mirrorModifierButton.ContentDescription =
            $"{GetString(Resource.String.mirror_modifier_description)}: " +
            GetString(stateResourceId);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _mirrorModifierButton.TooltipText = GetString(
                GetMirrorFeedbackResourceId(equipment));
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            _mirrorModifierButton.StateDescription = GetString(stateResourceId);
        }

        if (userInitiated)
        {
            AnimateModifierTile(_mirrorModifierButton);
        }
    }

    private static int GetMirrorFeedbackResourceId(MirrorEquipment equipment) =>
        equipment switch
        {
            MirrorEquipment.None =>
                Resource.String.mirror_equipment_disabled_feedback,
            MirrorEquipment.Compact =>
                Resource.String.compact_mirror_equipment_enabled_feedback,
            MirrorEquipment.Tall =>
                Resource.String.tall_mirror_equipment_enabled_feedback,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };

    private static void AnimateModifierTile(CheckBox button)
    {
        button.PerformHapticFeedback(FeedbackConstants.ClockTick);
        button.Animate()?.Cancel();
        button.ScaleX = 0.92f;
        button.ScaleY = 0.92f;
        if (button.Animate() is { } animator)
        {
            animator
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private static int GetModifierFeedbackResourceId(
        WorkoutModifiers modifier,
        bool enabled) => modifier switch
    {
        WorkoutModifiers.Insect => enabled
            ? Resource.String.insect_mode_enabled_feedback
            : Resource.String.insect_mode_disabled_feedback,
        WorkoutModifiers.Silence => enabled
            ? Resource.String.noisy_exercises_disabled_feedback
            : Resource.String.noisy_exercises_enabled_feedback,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, null),
    };

    private void ShowModifierFeedback(int messageResourceId)
    {
        int generation = ++_modifierFeedbackGeneration;
        TextView feedback = _durationModifierFeedback;
        feedback.Animate()?.Cancel();
        feedback.Text = GetString(messageResourceId);
        feedback.Visibility = ViewStates.Visible;
        feedback.Alpha = 0f;
        feedback.ScaleX = 0.82f;
        feedback.ScaleY = 0.82f;

        feedback.Animate()?
            .Alpha(1f)
            .ScaleX(1f)
            .ScaleY(1f)
            .SetDuration(140L)
            .WithEndAction(new Java.Lang.Runnable(() =>
                _ = feedback.PostDelayed(
                    new Java.Lang.Runnable(() =>
                    {
                        if (generation != _modifierFeedbackGeneration)
                        {
                            return;
                        }

                        feedback.Animate()?
                            .Alpha(0f)
                            .ScaleX(1.08f)
                            .ScaleY(1.08f)
                            .SetDuration(180L)
                            .WithEndAction(new Java.Lang.Runnable(() =>
                            {
                                if (generation == _modifierFeedbackGeneration)
                                {
                                    feedback.Visibility = ViewStates.Gone;
                                }
                            }))
                            .Start();
                    }),
                    ModifierFeedbackHoldMilliseconds)))
            .Start();
    }

    private void SetSelectedWorkoutMinutes(int minutes, bool userInitiated = false)
    {
        int previousOptionIndex = GetSupportedMinuteIndex(_selectedWorkoutMinutes);
        int normalizedMinutes = ExerciseSessionService.NormalizeLastWorkoutMinutes(minutes);
        _selectedWorkoutMinutes = normalizedMinutes;
        int optionIndex = GetSupportedMinuteIndex(normalizedMinutes);

        if (_durationSeekBar.Progress != optionIndex)
        {
            _durationSeekBar.Progress = optionIndex;
        }

        const string minuteLabel = "minutes";

        _durationMinutesValue.Text = normalizedMinutes.ToString();
        _durationMinutesValue.ContentDescription =
            $"{normalizedMinutes} minutes selected";
        _beginWorkoutButton.ContentDescription =
            $"Continue with a {normalizedMinutes} {minuteLabel} workout";
        _durationSeekBar.ContentDescription =
            $"Workout duration, {normalizedMinutes} {minuteLabel}. " +
            "Options: 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90 minutes";
        _durationOptionSegments.ContentDescription =
            $"{normalizedMinutes} minute workout selected";

        _durationDecreaseButton.Enabled = optionIndex > 0;
        _durationIncreaseButton.Enabled =
            optionIndex < ExerciseSessionService.SupportedWorkoutMinutes.Count - 1;
        _durationDecreaseButton.Alpha = _durationDecreaseButton.Enabled ? 1f : 0.42f;
        _durationIncreaseButton.Alpha = _durationIncreaseButton.Enabled ? 1f : 0.42f;

        if (userInitiated)
        {
            _durationMinutesValue.PerformHapticFeedback(FeedbackConstants.ClockTick);
        }

        for (int index = 0; index < _durationOptionSegments.ChildCount; index++)
        {
            View segment = _durationOptionSegments.GetChildAt(index)
                ?? throw new InvalidOperationException("A duration route segment is missing.");
            segment.SetBackgroundResource(index <= optionIndex
                ? Resource.Drawable.duration_segment_active
                : Resource.Drawable.duration_segment_inactive);

            if (_durationOptionLabels.GetChildAt(index) is TextView label)
            {
                label.Animate()?.Cancel();
                bool selected = index == optionIndex;
                label.SetTextColor(new Android.Graphics.Color(GetColor(
                    selected
                        ? Resource.Color.primary_text
                        : Resource.Color.secondary_text)));
                label.Alpha = selected ? 1f : 0.72f;
                label.ScaleX = selected ? 1.08f : 1f;
                label.ScaleY = selected ? 1.08f : 1f;
            }
        }

        if (userInitiated && previousOptionIndex != optionIndex)
        {
            AnimateDurationSelectionChange(optionIndex);
        }
    }

    private void AnimateDurationSelectionChange(int optionIndex)
    {
        _durationDial.Animate()?.Cancel();
        _durationDial.ScaleX = 0.98f;
        _durationDial.ScaleY = 0.98f;
        if (_durationDial.Animate() is { } dialAnimator)
        {
            dialAnimator
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _durationMinutesValue.Animate()?.Cancel();
        _durationMinutesValue.Alpha = 0.62f;
        _durationMinutesValue.ScaleX = 0.9f;
        _durationMinutesValue.ScaleY = 0.9f;
        if (_durationMinutesValue.Animate() is { } valueAnimator)
        {
            valueAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        if (_durationOptionLabels.GetChildAt(optionIndex) is not TextView label)
        {
            return;
        }

        label.Animate()?.Cancel();
        label.Alpha = 0.72f;
        label.ScaleX = 0.92f;
        label.ScaleY = 0.92f;
        if (label.Animate() is { } labelAnimator)
        {
            labelAnimator
                .Alpha(1f)
                .ScaleX(1.08f)
                .ScaleY(1.08f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void StepSelectedWorkoutMinutes(int direction)
    {
        int currentIndex = GetSupportedMinuteIndex(_selectedWorkoutMinutes);
        int nextIndex = Math.Clamp(
            currentIndex + direction,
            0,
            ExerciseSessionService.SupportedWorkoutMinutes.Count - 1);
        SetSelectedWorkoutMinutes(
            ExerciseSessionService.SupportedWorkoutMinutes[nextIndex],
            userInitiated: true);
    }

    private static int GetSupportedMinuteIndex(int minutes)
    {
        for (int index = 0;
             index < ExerciseSessionService.SupportedWorkoutMinutes.Count;
             index++)
        {
            if (ExerciseSessionService.SupportedWorkoutMinutes[index] == minutes)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(minutes), minutes, null);
    }

    private void StartSelectedWorkout()
    {
        if (!_beginWorkoutButton.Enabled)
        {
            return;
        }

        _beginWorkoutButton.Enabled = false;
        _beginWorkoutButton.Alpha = 0.6f;
        try
        {
            _sessionService.StartWorkout(
                _state,
                _selectedWorkoutMinutes,
                _selectedWorkoutModifiers);
            _stateStore.Save(_state);
            ShowNextExercise();
        }
        catch
        {
            _beginWorkoutButton.Enabled = true;
            _beginWorkoutButton.Alpha = 1f;
            throw;
        }
    }

    private void FinishInterruptedWorkout()
    {
        IReadOnlyList<Exercise> scoreUpdates =
            _sessionService.FinishInterruptedWorkoutWithScoreUpdates(_state);
        SaveStateAndScores(scoreUpdates);
    }

    private void RecoverPendingScoreUpdate()
    {
        if (_state.PendingScoreExerciseId > 0)
        {
            _state.PendingScoreUpdates.TryAdd(
                _state.PendingScoreExerciseId,
                _state.PendingScoreValue);
        }

        foreach ((int exerciseId, int score) in
                 _state.PendingScoreUpdates.ToArray())
        {
            Exercise? exercise = _exerciseDatabase.Exercises.SingleOrDefault(
                candidate => candidate.Id == exerciseId);
            if (exercise is not null)
            {
                exercise.Score = score;
                _exerciseDatabase.UpdateScore(exercise);
            }

            _state.PendingScoreUpdates.Remove(exerciseId);
        }

        _state.PendingScoreExerciseId = 0;
        _state.PendingScoreValue = 0;
        // OnCreate saves after legacy conversion/interruption finalization. Saving
        // here would serialize away the compatibility-only legacy fields.
    }

    private void SaveStateAndScores(IReadOnlyList<Exercise> scoreUpdates)
    {
        Exercise[] distinctUpdates = scoreUpdates
            .DistinctBy(exercise => exercise.Id)
            .ToArray();
        foreach (Exercise exercise in distinctUpdates)
        {
            _state.PendingScoreUpdates[exercise.Id] = exercise.Score;
        }
        _state.PendingScoreExerciseId = 0;
        _state.PendingScoreValue = 0;

        _stateStore.Save(_state);

        if (distinctUpdates.Length == 0)
        {
            return;
        }

        foreach (Exercise exercise in distinctUpdates)
        {
            _exerciseDatabase.UpdateScore(exercise);
        }
        _state.PendingScoreUpdates.Clear();
        _stateStore.Save(_state);
    }

    private void ConfigureVideoView()
    {
        _videoPreparedListener = new VideoPreparedListener(mediaPlayer =>
        {
            _mediaReady = true;
            _activeMediaPlayer = mediaPlayer;
            try
            {
                _activeMediaPlayer.Looping = _loopExerciseVideo;
                _activeMediaPlayer.SetVolume(0f, 0f);
            }
            catch (Java.Lang.IllegalStateException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            catch (ObjectDisposedException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            if (_countdownPausedForMediaError &&
                !_countdownPausedByUser &&
                _activityResumed)
            {
                _countdownPausedForMediaError = false;
                ResumeCountdown();
            }
            SetPlaybackControlsAvailability(
                _workoutPhase == WorkoutPhase.Move &&
                (_countdownActive || _countdownPausedByUser));
            ApplyCurrentMediaPlaybackState();

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
            if (_currentExercise?.DirectionSequence !=
                ExerciseDirectionSequence.None)
            {
                if (_workoutPhase == WorkoutPhase.Ready)
                {
                    _exerciseVideo.SeekTo(0);
                    ApplyCurrentMediaPlaybackState();
                    return;
                }

                if (_workoutPhase == WorkoutPhase.Move &&
                    _lastMovementPhase is MovementPhase.FirstSide or
                        MovementPhase.SecondSide)
                {
                    RestartExerciseMediaForPhase(_lastMovementPhase.Value);
                    return;
                }
            }

            if (_freezeHoldAtEnd)
            {
                FreezeHoldOnFinalFrame();
            }
        };
    }

    private void LoadExerciseMedia(Exercise exercise, bool forceCacheRefresh = false)
    {
        bool usesStill = exercise.Presentation == ExercisePresentation.Still;
        bool holdDuringMove =
            exercise.Mode == ExerciseMode.Hold && _workoutPhase == WorkoutPhase.Move;
        bool holdDuringRest =
            exercise.Mode == ExerciseMode.Hold && _workoutPhase == WorkoutPhase.Rest;
        _mediaLoadGeneration++;
        _mediaReady = false;
        _loopExerciseVideo =
            !holdDuringMove &&
            !holdDuringRest &&
            exercise.DirectionSequence == ExerciseDirectionSequence.None;
        _freezeHoldAtEnd = holdDuringMove || holdDuringRest;
        _activeMediaPlayer = null;

        if (usesStill)
        {
            ClearHoldFrame();
            _exerciseVideo.Pause();
            _exerciseVideo.StopPlayback();
            _mediaScrim.Animate()?.Cancel();
            _mediaScrim.Alpha = 1f;
            _mediaScrim.Visibility = ViewStates.Visible;
            _mediaErrorPanel.Visibility = ViewStates.Gone;
            _mediaLoadingIndicator.Visibility = ViewStates.Gone;
            ShowHoldFrame(exercise.Id);
            SetStartAvailability(available: _mediaReady);
            if (_workoutPhase == WorkoutPhase.Move && _mediaReady)
            {
                SetPlaybackControlsAvailability(
                    _countdownActive || _countdownPausedByUser);
            }
            if (_mediaReady &&
                _countdownPausedForMediaError &&
                !_countdownPausedByUser &&
                _activityResumed)
            {
                _countdownPausedForMediaError = false;
                ResumeCountdown();
                SetPlaybackControlsAvailability(available: true);
            }
            return;
        }

        if (holdDuringRest)
        {
            _exerciseVideo.Pause();
            _mediaErrorPanel.Visibility = ViewStates.Gone;
            _mediaLoadingIndicator.Visibility = ViewStates.Gone;
            ShowHoldFrame(exercise.Id);
            return;
        }

        ClearHoldFrame();
        _exerciseVideo.StopPlayback();
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingIndicator.Visibility = ViewStates.Visible;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Move)
        {
            SetPlaybackControlsAvailability(available: false);
        }

        try
        {
            _exerciseVideo.SetVideoPath(
                CacheVideoAsset(
                    GetExerciseVideoAssetPath(exercise),
                    forceCacheRefresh));
        }
        catch (Exception)
        {
            ShowMediaError();
        }
    }

    private string CacheVideoAsset(string assetPath, bool forceRefresh)
    {
        string cacheRoot = GetVersionedVideoCacheRoot();
        Directory.CreateDirectory(cacheRoot);
        string assetKind = assetPath.StartsWith(
            "exercise_direction_videos/",
            StringComparison.Ordinal)
            ? "directions-"
            : "standard-";
        long expectedLength;
        string assetFingerprint;
        using (Stream fingerprintSource = Assets!.Open(assetPath))
        {
            expectedLength = fingerprintSource.CanSeek ? fingerprintSource.Length : -1;
            assetFingerprint = Convert.ToHexString(
                SHA256.HashData(fingerprintSource)).ToLowerInvariant();
        }

        string assetFileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string assetExtension = System.IO.Path.GetExtension(assetPath);
        string cachedPath = System.IO.Path.Combine(
            cacheRoot,
            $"{assetKind}{assetFileName}-{assetFingerprint}{assetExtension}");
        string temporaryPath = cachedPath + ".tmp";

        if (!forceRefresh &&
            File.Exists(cachedPath) &&
            new FileInfo(cachedPath).Length > 0 &&
            (expectedLength < 0 || new FileInfo(cachedPath).Length == expectedLength))
        {
            return cachedPath;
        }

        try
        {
            using Stream source = Assets!.Open(assetPath);
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

    private string GetVersionedVideoCacheRoot()
    {
        if (_exerciseVideoCacheRoot is not null)
        {
            return _exerciseVideoCacheRoot;
        }

        long versionCode = GetInstalledVersionCode();
        string cacheParent = System.IO.Path.Combine(
            CacheDir!.AbsolutePath,
            "exercise-videos");
        string versionRoot = System.IO.Path.Combine(
            cacheParent,
            versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(versionRoot);

        foreach (string staleRoot in Directory.EnumerateDirectories(cacheParent))
        {
            if (string.Equals(staleRoot, versionRoot, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(staleRoot, recursive: true);
            }
            catch (IOException)
            {
                // Android may still have an old cached video open briefly.
            }
            catch (UnauthorizedAccessException)
            {
                // Cache cleanup is best-effort and must not block playback.
            }
        }

        _exerciseVideoCacheRoot = versionRoot;
        return versionRoot;
    }

    private long GetInstalledVersionCode()
    {
        Android.Content.PM.PackageManager packageManager = PackageManager!;
        string packageName = PackageName!;

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Android.Content.PM.PackageInfo packageInfo = packageManager.GetPackageInfo(
                packageName,
                Android.Content.PM.PackageManager.PackageInfoFlags.Of(0L))
                ?? throw new InvalidOperationException(
                    "Android could not resolve the installed Flux package.");
            return packageInfo.LongVersionCode;
        }

#pragma warning disable CS0618
        Android.Content.PM.PackageInfo legacyPackageInfo = packageManager.GetPackageInfo(
            packageName,
            Android.Content.PM.PackageInfoFlags.Activities)
            ?? throw new InvalidOperationException(
                "Android could not resolve the installed Flux package.");
#pragma warning restore CS0618
        return OperatingSystem.IsAndroidVersionAtLeast(28)
            ? legacyPackageInfo.LongVersionCode
#pragma warning disable CS0618
            : legacyPackageInfo.VersionCode;
#pragma warning restore CS0618
    }

    private static string GetExerciseVideoAssetPath(Exercise exercise)
    {
        return exercise.DirectionSequence == ExerciseDirectionSequence.None
            ? exercise.Video
            : $"exercise_direction_videos/exercise_{exercise.Id:D4}.mp4";
    }

    private void SetStartAvailability(bool available)
    {
        if (_workoutPhase != WorkoutPhase.Ready)
        {
            return;
        }

        _startButton.Enabled = available;
        _startButton.Alpha = available ? 1f : 0.5f;
        _startButton.SetImageResource(Resource.Drawable.ic_phase_active);
        _startButton.ContentDescription = available
            ? GetString(Resource.String.start)
            : GetString(Resource.String.media_loading);
    }

    private void SetPlaybackControlsAvailability(bool available)
    {
        SetPlaybackControlAvailability(_repeatAction, available);
        SetPlaybackControlAvailability(_playbackAction, available);

        // A missing or buffering demonstration must never trap the workout.
        // Repeat and play/pause require ready media; Next only requires an
        // active movement that can be rejected and advanced.
        bool nextAvailable = _workoutPhase == WorkoutPhase.Move &&
            (_countdownActive || _countdownPaused);
        SetPlaybackControlAvailability(_nextAction, nextAvailable);
    }

    private static void SetPlaybackControlAvailability(
        ImageButton control,
        bool available)
    {
        control.Enabled = available;
        control.Alpha = available
            ? PlaybackControlEnabledAlpha
            : PlaybackControlDisabledAlpha;
    }

    private void UpdatePlaybackActionVisual()
    {
        bool paused = _countdownPausedByUser;
        _playbackAction.SetImageResource(paused
            ? Resource.Drawable.ic_phase_active
            : Resource.Drawable.ic_phase_pause);
        _playbackAction.ContentDescription = GetString(paused
            ? Resource.String.resume_exercise_description
            : Resource.String.pause_exercise_description);
    }

    private void RevealExerciseMedia()
    {
        if (_mediaErrorPanel.Visibility == ViewStates.Visible ||
            _revealedMediaGeneration == _mediaLoadGeneration)
        {
            return;
        }

        _revealedMediaGeneration = _mediaLoadGeneration;
        int revealGeneration = _mediaLoadGeneration;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        _mediaLoadingIndicator.Visibility = ViewStates.Gone;
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
                    _mediaLoadingIndicator.Visibility != ViewStates.Visible &&
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
        if (_workoutPhase == WorkoutPhase.Move && _countdownActive)
        {
            PauseCountdown();
            _countdownPausedForMediaError = true;
        }

        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Ready)
        {
            _startButton.ContentDescription = GetString(Resource.String.media_error);
        }
        SetPlaybackControlsAvailability(available: false);
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingIndicator.Visibility = ViewStates.Gone;
        _mediaErrorPanel.Visibility = ViewStates.Visible;
        AnnouncePhaseForAccessibility(
            _mediaErrorPanel,
            GetString(Resource.String.media_error));
    }

    private void PlayHoldOnce()
    {
        if (_currentExercise?.Presentation == ExercisePresentation.Still)
        {
            ShowHoldFrame(_currentExercise.Id);
            return;
        }

        ClearHoldFrame();
        _loopExerciseVideo = false;
        _freezeHoldAtEnd = true;
        if (_activeMediaPlayer is not null)
        {
            try
            {
                _activeMediaPlayer.Looping = false;
            }
            catch (Java.Lang.IllegalStateException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            catch (ObjectDisposedException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
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
        bool continuingWorkout =
            _appScreen == AppScreen.Workout &&
            _workoutScreen.Visibility == ViewStates.Visible;
        WorkoutGroup? nextGroup = _sessionService.GetNextGroup(_state);

        if (nextGroup is null)
        {
            ShowCongratulations();
            return;
        }

        _currentWorkoutGroup = nextGroup;
        Exercise exercise = _sessionService.GetSelectedExercise(
            _state,
            _currentWorkoutGroup);
        _currentExercise = exercise;
        _lastMovementPhase = null;
        SetExerciseMediaMirrored(mirrored: false);
        int position = _currentWorkoutGroup.Order;
        int totalRounds = _sessionService.GetActiveGroups(_state).Count;
        int countdownDurationMilliseconds = GetCurrentCountdownDurationMilliseconds();

        _workoutProgressText.Text = $"{position:D2}  /  {totalRounds:D2}";
        _workoutProgressText.ContentDescription =
            $"Round {position} of {totalRounds}";
        _workoutProgressBar.Max = totalRounds;
        _workoutProgressBar.SetProgress(position, continuingWorkout);
        _countdownProgress.Max = countdownDurationMilliseconds;
        _countdownProgress.Progress = countdownDurationMilliseconds;
        _exerciseName.Text = exercise.Name;
        _exerciseName.ContentDescription = exercise.Mode == ExerciseMode.Hold
            ? $"{exercise.Name}. Hold."
            : exercise.DirectionSequence != ExerciseDirectionSequence.None
                ? $"{exercise.Name}. First direction, change, then opposite direction."
                : $"{exercise.Name}. Repetition.";
        RenderExecutionSignifier(exercise);
        _exerciseModeBadge.Visibility = exercise.Mode == ExerciseMode.Hold
            ? ViewStates.Visible
            : ViewStates.Gone;
        ShowAppScreen(AppScreen.Workout);
        ShowStartButton();
        LoadExerciseMedia(exercise);
        ResizeMediaCard();
        if (continuingWorkout)
        {
            AnimateExerciseChange();
        }
        AnnouncePhaseForAccessibility(
            _workoutHeader,
            $"Round {position} of {totalRounds}. " +
            $"{exercise.Name}. " +
            (exercise.Mode == ExerciseMode.Hold ? "Hold." : "Repetition."));
    }

    private void RestorePendingMovement(WorkoutGroup pendingGroup)
    {
        ShowNextExercise();
        if (_currentWorkoutGroup?.Id != pendingGroup.Id)
        {
            throw new InvalidOperationException(
                "The persisted movement is not the next workout round.");
        }

        long millisecondsRemaining =
            _sessionService.GetPendingMovementMillisecondsRemaining(
                _state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (millisecondsRemaining <= 0)
        {
            throw new InvalidOperationException(
                "The persisted movement has no remaining time.");
        }

        _countdownActive = false;
        _countdownPaused = true;
        _countdownMillisecondsRemaining = millisecondsRemaining;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownPausedByUser = _state.PendingMovementPausedByUser;
        _countdownPausedForMediaError =
            !_countdownPausedByUser && !_mediaReady;
        _sessionService.PauseMovement(
            _state,
            pendingGroup,
            millisecondsRemaining,
            _countdownPausedByUser);
        _stateStore.Save(_state);
        _lastMovementPhase = null;
        ShowWorkoutPhase(WorkoutPhase.Move);
        UpdateMoveCountdown(millisecondsRemaining);
        SetPlaybackControlsAvailability(
            _mediaReady && _countdownPausedByUser);
        UpdatePlaybackActionVisual();
    }

    private void RestorePendingRest(WorkoutGroup pendingGroup)
    {
        ShowNextExercise();
        if (_currentWorkoutGroup?.Id != pendingGroup.Id)
        {
            throw new InvalidOperationException(
                "The persisted rest is not for the next workout round.");
        }

        _restActive = true;
        _exerciseVideo.Pause();
        ShowRestPanel();
        AnnouncePhaseForAccessibility(_restPanel, GetRestDescription());
        ResumeRestCountdown();
    }

    private void RenderExecutionSignifier(Exercise exercise)
    {
        bool isUnilateral = exercise.SideSequence.UsesTimedSides();
        bool isBidirectional = exercise.DirectionSequence !=
            ExerciseDirectionSequence.None;
        if (!isUnilateral && !isBidirectional)
        {
            _executionSignifier.Visibility = ViewStates.Gone;
            _executionSignifier.ContentDescription = null;
            return;
        }

        _executionSignifierIcon.SetImageResource(isUnilateral
            ? Resource.Drawable.ic_unilateral_asymmetry
            : Resource.Drawable.ic_bidirectional_execution);
        _executionSignifier.ContentDescription =
            exercise.SideSequence.UsesTimedLeadStances()
                ? "Unilateral exercise. Match the shown lead stance, change stance, " +
                    "then repeat from the opposite lead stance."
                : isUnilateral
                    ? "Unilateral exercise. Work one side, change, then the other."
                    : "Bidirectional exercise. Complete the shown direction, change " +
                        "direction, then complete the opposite direction.";
        _executionSignifier.Visibility = ViewStates.Visible;
    }

    private void AnimateExerciseChange()
    {
        _workoutHeader.Animate()?.Cancel();
        _workoutHeader.Alpha = 0.2f;
        _workoutHeader.TranslationY = Dp(4f);
        if (_workoutHeader.Animate() is { } headerAnimator)
        {
            headerAnimator
                .Alpha(1f)
                .TranslationY(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _exerciseMediaCard.Animate()?.Cancel();
        _exerciseMediaCard.Alpha = 0.72f;
        _exerciseMediaCard.ScaleX = 0.985f;
        _exerciseMediaCard.ScaleY = 0.985f;
        if (_exerciseMediaCard.Animate() is { } mediaAnimator)
        {
            mediaAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void ShowStartButton()
    {
        _countdownPausedByUser = false;
        _startButton.Enabled = true;
        _startButton.Alpha = 1f;
        _startButton.SetImageResource(Resource.Drawable.ic_phase_active);
        _startButton.ContentDescription = GetString(Resource.String.start);
        UpdateShuffleAvailability();
        SetPlaybackControlsAvailability(available: false);
        UpdatePlaybackActionVisual();
        ResetMovementVisuals();
        ShowWorkoutPhase(WorkoutPhase.Ready);
    }

    private void UpdateShuffleAvailability()
    {
        bool available = _currentWorkoutGroup is not null &&
            _sessionService.CanShuffleNextExercise(_state, _currentWorkoutGroup);
        _shuffleButton.Enabled = available;
        _shuffleButton.Visibility = available
            ? ViewStates.Visible
            : ViewStates.Gone;
        if (_startButton.LayoutParameters is LinearLayout.LayoutParams layoutParameters)
        {
            layoutParameters.MarginStart = available ? DpInt(12) : 0;
            _startButton.LayoutParameters = layoutParameters;
        }
    }

    private void ShuffleCurrentExercise()
    {
        if (_workoutPhase != WorkoutPhase.Ready ||
            _currentWorkoutGroup is null ||
            !_shuffleButton.Enabled)
        {
            return;
        }

        _shuffleButton.Enabled = false;
        ShuffledExerciseResult? result = _sessionService.ShuffleNextExercise(
            _state,
            _currentWorkoutGroup);
        if (result is null)
        {
            UpdateShuffleAvailability();
            return;
        }

        SaveStateAndScores(result.ScoreUpdates);
        ShowNextExercise();
        AnnouncePhaseForAccessibility(
            _workoutHeader,
            $"Rejected {result.RejectedExercise.Name}. " +
            $"Changed to {result.ReplacementExercise.Name}.");
    }

    private void StartCountdown()
    {
        if (_countdownActive || _countdownPaused || !_mediaReady)
        {
            return;
        }

        _countdownPausedByUser = false;
        _lastMovementPhase = null;
        ShowWorkoutPhase(WorkoutPhase.Move);
        StartCountdownTimer(GetCurrentCountdownDurationMilliseconds());
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
    }

    private void TogglePlayback()
    {
        if (_countdownPausedByUser)
        {
            if (!_mediaReady)
            {
                return;
            }

            _countdownPausedByUser = false;
            ResumeCountdown();
            SetPlaybackControlsAvailability(available: true);
            UpdatePlaybackActionVisual();
            AnnouncePhaseForAccessibility(_countdownPanel, "Exercise resumed.");
            return;
        }

        if (!_countdownActive)
        {
            return;
        }

        _countdownPausedByUser = true;
        PauseCountdown();
        _exerciseVideo.Pause();
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
        AnnouncePhaseForAccessibility(_countdownPanel, "Exercise paused.");
    }

    private void RepeatExercise()
    {
        if ((!_countdownActive && !_countdownPausedByUser) ||
            !_mediaReady ||
            _currentExercise is null)
        {
            return;
        }

        StopCountdownTimer();
        _exerciseVideo.Pause();
        SetExerciseMediaMirrored(mirrored: false);
        if (_currentExercise.Presentation != ExercisePresentation.Still)
        {
            ClearHoldFrame();
            _exerciseVideo.SeekTo(0);
        }
        _lastMovementPhase = null;
        StartCountdownTimer(GetCurrentCountdownDurationMilliseconds());
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
        AnnouncePhaseForAccessibility(
            _countdownPanel,
            "Exercise restarted from the beginning.");
    }

    private void GoToNextExercise()
    {
        if (!_countdownActive && !_countdownPaused)
        {
            return;
        }

        StopCountdownTimer();
        SetPlaybackControlsAvailability(available: false);
        FinalizeCurrentRound(keep: false);
    }

    private void CompleteCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        StopCountdownTimer();
        UpdateMoveCountdown(0L);
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        PlayWhistleCue(_restStartWhistleId);

        BeginRest();
    }

    private void CancelCountdown(bool resetToStart)
    {
        if (!_countdownActive && !_countdownPaused)
        {
            return;
        }

        StopCountdownTimer();

        if (resetToStart && !_state.WorkoutCompleted)
        {
            if (_currentExercise?.Mode == ExerciseMode.Hold)
            {
                LoadExerciseMedia(_currentExercise);
            }
            ShowStartButton();
        }
    }

    private void StopCountdownTimer()
    {
        _countdownActive = false;
        _countdownPaused = false;
        _countdownPausedForMediaError = false;
        _countdownPausedByUser = false;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownMillisecondsRemaining = 0;
        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        SetPlaybackControlsAvailability(available: false);
        UpdatePlaybackActionVisual();
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
        if (_currentWorkoutGroup is not null)
        {
            _sessionService.PauseMovement(
                _state,
                _currentWorkoutGroup,
                _countdownMillisecondsRemaining,
                _countdownPausedByUser);
            _stateStore.Save(_state);
        }
    }

    private void ResumeCountdown()
    {
        if (!_countdownPaused || _countdownMillisecondsRemaining <= 0)
        {
            return;
        }

        StartCountdownTimer(_countdownMillisecondsRemaining);
        SetPlaybackControlsAvailability(available: _mediaReady);
        UpdatePlaybackActionVisual();
    }

    private void StartCountdownTimer(long millisecondsRemaining)
    {
        if (_currentWorkoutGroup is null)
        {
            throw new InvalidOperationException(
                "A movement timer requires a current workout group.");
        }

        _sessionService.BeginMovement(
            _state,
            _currentWorkoutGroup,
            millisecondsRemaining,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + millisecondsRemaining);
        _stateStore.Save(_state);
        _countdownActive = true;
        _countdownPaused = false;
        _countdownMillisecondsRemaining = millisecondsRemaining;
        _countdownEndsAtElapsedMilliseconds =
            Android.OS.SystemClock.ElapsedRealtime() + millisecondsRemaining;
        UpdateMoveCountdown(millisecondsRemaining);
        UpdatePlaybackActionVisual();

        if (!_countdownActive)
        {
            return;
        }

        _countdownTimer = new WorkoutCountDownTimer(
            millisecondsRemaining,
            250L,
            UpdateMoveCountdown,
            CompleteCountdown);
        ApplyCurrentMediaPlaybackState();
        _countdownTimer.Start();
    }

    private void UpdateMoveCountdown(long millisecondsRemaining)
    {
        int countdownDurationMilliseconds = GetCurrentCountdownDurationMilliseconds();
        long boundedMilliseconds = Math.Clamp(
            millisecondsRemaining,
            0L,
            countdownDurationMilliseconds);
        _countdownMillisecondsRemaining = boundedMilliseconds;
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            boundedMilliseconds,
            UsesTimedPair(),
            _currentWorkoutGroup.UsesFullSideTiming);
        string secondsText = state.SecondsRemaining.ToString();
        if (_countdownText.Text != secondsText ||
            state.Phase != _lastMovementPhase)
        {
            _countdownText.Text = secondsText;
            _countdownText.ContentDescription =
                GetMovementCountdownDescription(state);
        }
        _countdownProgress.Progress = (int)boundedMilliseconds;
        ApplyMovementPhase(state);
        EnforceDirectionMediaSegment(state.Phase);
    }

    private bool UsesTimedPair()
    {
        Exercise? exercise = _currentExercise;
        return exercise is not null &&
            MovementPhasePresentationPolicy.UsesTimedPair(
                exercise.SideSequence,
                exercise.DirectionSequence);
    }

    private int GetCurrentMovementDurationMilliseconds() =>
        (_currentWorkoutGroup?.UsesFullSideTiming == true
            ? MovementPhaseSchedule.FullSideTotalDurationSeconds
            : CountdownSeconds) * 1_000;

    private int GetCurrentCountdownDurationMilliseconds() =>
        GetCurrentMovementDurationMilliseconds() +
        MovementPhaseSchedule.PreparationDurationSeconds * 1_000;

    private string GetMovementCountdownDescription(
        MovementPhaseState state)
    {
        if (state.Phase == MovementPhase.Preparation)
        {
            return $"Prepare, {state.SecondsRemaining} seconds remaining";
        }

        MovementDirectionCue cue = GetCurrentMovementPresentation(state.Phase).Cue;
        string phaseDescription = state.Phase == MovementPhase.ChangeSides
            ? GetPairChangeDescription()
            : GetMovementCueDescription(cue);
        return state.Phase switch
        {
            MovementPhase.Preparation or MovementPhase.Continuous or MovementPhase.FirstSide or
                MovementPhase.ChangeSides or MovementPhase.SecondSide =>
                $"{phaseDescription}, {state.SecondsRemaining} seconds remaining",
            MovementPhase.Complete => "Movement complete",
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private void ApplyMovementPhase(MovementPhaseState state)
    {
        if (state.Phase == MovementPhase.Complete ||
            state.Phase == _lastMovementPhase)
        {
            return;
        }

        MovementPhase? previousPhase = _lastMovementPhase;
        _lastMovementPhase = state.Phase;

        if (state.Phase == MovementPhase.Preparation)
        {
            SetExerciseMediaMirrored(mirrored: false);
            RenderPreparationPhase();
            RenderFullWorkoutPhase(Resource.Color.rest_surface);
            AnimateMediaPhase(resting: true);
            _exerciseVideo.Pause();
            AnnouncePhaseForAccessibility(
                _countdownPanel,
                $"Prepare, {MovementPhaseSchedule.PreparationDurationSeconds} seconds.");
            return;
        }

        MovementPhasePresentation presentation =
            GetCurrentMovementPresentation(state.Phase);
        RenderCountdownPhase(presentation.Cue);

        switch (state.Phase)
        {
            case MovementPhase.Continuous:
                SetExerciseMediaMirrored(mirrored: false);
                RenderFullWorkoutPhase(Resource.Color.move_surface);
                AnimateMediaPhase(resting: false);
                if (previousPhase == MovementPhase.Preparation)
                {
                    RestartExerciseMediaForPhase(state.Phase);
                    CueMovementRestart();
                }
                else
                {
                    RestartHoldOrResumeRepetition();
                }
                break;

            case MovementPhase.FirstSide:
                SetExerciseMediaMirrored(presentation.MirrorMedia);
                RenderTimedPairWorkoutPhase(presentation);
                AnimateMediaPhase(resting: false);
                RestartExerciseMediaForPhase(state.Phase);
                if (previousPhase == MovementPhase.Preparation)
                {
                    CueMovementRestart();
                }
                break;

            case MovementPhase.ChangeSides:
                _exerciseVideo.Pause();
                RenderFullWorkoutPhase(Resource.Color.rest_surface);
                AnimateMediaPhase(resting: true);
                CueSideChange();
                break;

            case MovementPhase.SecondSide:
                SetExerciseMediaMirrored(presentation.MirrorMedia);
                RenderTimedPairWorkoutPhase(presentation);
                AnimateMediaPhase(resting: false);
                RestartExerciseMediaForPhase(state.Phase);
                CueMovementRestart();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }

        int timedPairWorkSeconds = _currentWorkoutGroup?.UsesFullSideTiming == true
            ? MovementPhaseSchedule.FullSideDurationSeconds
            : MovementPhaseSchedule.SideDurationSeconds;
        int timedPairChangeSeconds = _currentWorkoutGroup?.UsesFullSideTiming == true
            ? MovementPhaseSchedule.FullSideChangeDurationSeconds
            : MovementPhaseSchedule.SideChangeDurationSeconds;
        string? announcement = state.Phase switch
        {
            MovementPhase.Continuous when previousPhase is null or MovementPhase.Preparation =>
                "Move, 45 seconds.",
            MovementPhase.FirstSide when previousPhase is null or MovementPhase.Preparation =>
                $"{GetMovementCueDescription(presentation.Cue)}, " +
                    $"{timedPairWorkSeconds} seconds.",
            MovementPhase.ChangeSides =>
                $"{GetPairChangeDescription()}, {timedPairChangeSeconds} seconds.",
            MovementPhase.SecondSide =>
                $"{GetMovementCueDescription(presentation.Cue)}, " +
                    $"{timedPairWorkSeconds} seconds.",
            _ => null,
        };
        if (announcement is not null)
        {
            AnnouncePhaseForAccessibility(_countdownPanel, announcement);
        }
    }

    private void RestartHoldOrResumeRepetition()
    {
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            PlayHoldOnce();
            return;
        }

        ApplyCurrentMediaPlaybackState();
    }

    private void RestartExerciseMediaForPhase(MovementPhase phase)
    {
        Exercise exercise = _currentExercise
            ?? throw new InvalidOperationException(
                "A timed movement phase requires a current exercise.");
        if (exercise.Presentation == ExercisePresentation.Still)
        {
            ShowHoldFrame(exercise.Id);
            return;
        }

        ClearHoldFrame();
        _exerciseVideo.Pause();
        int positionMilliseconds =
            exercise.DirectionSequence != ExerciseDirectionSequence.None &&
            phase == MovementPhase.SecondSide
                ? DirectionSecondPhaseOffsetMilliseconds
                : 0;
        _exerciseVideo.SeekTo(positionMilliseconds);
        RestartHoldOrResumeRepetition();
    }

    private void EnforceDirectionMediaSegment(MovementPhase phase)
    {
        if (_currentExercise?.DirectionSequence ==
                ExerciseDirectionSequence.None ||
            _activeMediaPlayer is null ||
            !_mediaReady ||
            phase is not (MovementPhase.FirstSide or MovementPhase.SecondSide))
        {
            return;
        }

        int segmentStartMilliseconds = phase == MovementPhase.SecondSide
            ? DirectionSecondPhaseOffsetMilliseconds
            : 0;
        int segmentEndMilliseconds =
            segmentStartMilliseconds + DirectionSegmentDurationMilliseconds;
        int positionMilliseconds;
        try
        {
            positionMilliseconds = _activeMediaPlayer.CurrentPosition;
        }
        catch (Java.Lang.IllegalStateException)
        {
            RecoverInvalidMediaPlayerState();
            return;
        }
        catch (ObjectDisposedException)
        {
            RecoverInvalidMediaPlayerState();
            return;
        }
        if (positionMilliseconds >= segmentStartMilliseconds &&
            positionMilliseconds < segmentEndMilliseconds)
        {
            return;
        }

        _exerciseVideo.Pause();
        _exerciseVideo.SeekTo(segmentStartMilliseconds);
        ApplyCurrentMediaPlaybackState();
    }

    private void RecoverInvalidMediaPlayerState()
    {
        _mediaReady = false;
        _activeMediaPlayer = null;
        if (_workoutPhase == WorkoutPhase.Move && _countdownActive)
        {
            PauseCountdown();
        }
        if (_workoutPhase == WorkoutPhase.Move &&
            _countdownPaused &&
            !_countdownPausedByUser)
        {
            _countdownPausedForMediaError = true;
        }

        SetStartAvailability(available: false);
        SetPlaybackControlsAvailability(available: false);
        if (_currentExercise is not null)
        {
            LoadExerciseMedia(_currentExercise);
        }
        else
        {
            ShowMediaError();
        }
    }

    private void CueSideChange()
    {
        PlayWhistleCue(_sideChangeWhistleId);
        _countdownPanel.PerformHapticFeedback(FeedbackConstants.ClockTick);
    }

    private void CueMovementRestart()
    {
        PlayWhistleCue(_movementStartWhistleId);
        _countdownPanel.PerformHapticFeedback(FeedbackConstants.ClockTick);
    }

    private void RenderCountdownPhase(MovementDirectionCue cue)
    {
        bool changingPair = cue == MovementDirectionCue.Switch;
        int textColorResource = changingPair
            ? Resource.Color.rest_text
            : Resource.Color.move_text;
        int progressResource = changingPair
            ? Resource.Drawable.rest_progress_track
            : Resource.Drawable.move_progress_track;
        int actionBackgroundResource = changingPair
            ? Resource.Drawable.phase_rest_chip_background
            : Resource.Drawable.phase_move_chip_background;
        var textColor = new Android.Graphics.Color(GetColor(textColorResource));
        _countdownText.SetTextColor(textColor);
        StylePlaybackControls(textColor, actionBackgroundResource);
        _countdownProgress.ProgressDrawable = GetDrawable(progressResource);
    }

    private void RenderPreparationPhase()
    {
        var textColor = new Android.Graphics.Color(
            GetColor(Resource.Color.rest_text));
        _countdownText.SetTextColor(textColor);
        StylePlaybackControls(
            textColor,
            Resource.Drawable.phase_rest_chip_background);
        _countdownProgress.ProgressDrawable =
            GetDrawable(Resource.Drawable.rest_progress_track);
    }

    private void StylePlaybackControls(
        Android.Graphics.Color tint,
        int backgroundResource)
    {
        Android.Content.Res.ColorStateList tintList =
            Android.Content.Res.ColorStateList.ValueOf(tint);
        foreach (ImageButton control in new[]
                 {
                     _repeatAction,
                     _playbackAction,
                     _nextAction,
                 })
        {
            control.ImageTintList = tintList;
            control.SetBackgroundResource(backgroundResource);
        }
    }

    private bool ShouldExerciseVideoBePlaying()
    {
        if (!_activityResumed || !_mediaReady ||
            _holdFrameImage.Visibility == ViewStates.Visible)
        {
            return false;
        }

        return _workoutPhase == WorkoutPhase.Ready ||
            (_workoutPhase == WorkoutPhase.Move &&
                _countdownActive &&
                _lastMovementPhase is (MovementPhase.Continuous or
                    MovementPhase.FirstSide or MovementPhase.SecondSide));
    }

    private void ApplyCurrentMediaPlaybackState()
    {
        bool mirrorMedia = _workoutPhase == WorkoutPhase.Move &&
            _lastMovementPhase is MovementPhase phase &&
            phase != MovementPhase.Preparation &&
            GetCurrentMovementPresentation(phase).MirrorMedia;
        SetExerciseMediaMirrored(mirrorMedia);

        if (ShouldExerciseVideoBePlaying())
        {
            _exerciseVideo.Start();
        }
        else
        {
            _exerciseVideo.Pause();
        }
    }

    private void SetExerciseMediaMirrored(bool mirrored)
    {
        float scale = mirrored ? -1f : 1f;
        _exerciseVideo.ScaleX = scale;
        _holdFrameImage.ScaleX = scale;
    }

    private MovementPhasePresentation GetCurrentMovementPresentation(
        MovementPhase phase)
    {
        Exercise exercise = _currentExercise
            ?? throw new InvalidOperationException(
                "A movement phase requires a current exercise.");
        return MovementPhasePresentationPolicy.GetPresentation(
            exercise.SideSequence,
            exercise.DirectionSequence,
            phase);
    }

    private string GetPairChangeDescription()
    {
        if (_currentExercise?.SideSequence.UsesTimedLeadStances() == true)
        {
            return "Change stance";
        }

        return _currentExercise?.DirectionSequence == ExerciseDirectionSequence.None
            ? "Change sides"
            : "Change direction";
    }

    private static string GetMovementCueDescription(MovementDirectionCue cue)
    {
        return cue switch
        {
            MovementDirectionCue.None => "Movement complete",
            MovementDirectionCue.Move => "Move",
            MovementDirectionCue.Switch => "Change",
            MovementDirectionCue.ScreenLeft => "Left side",
            MovementDirectionCue.ScreenRight => "Right side",
            MovementDirectionCue.ShownLeadStance => "Shown lead stance",
            MovementDirectionCue.OppositeLeadStance => "Opposite lead stance",
            MovementDirectionCue.Forward => "Forward",
            MovementDirectionCue.Backward => "Backward",
            MovementDirectionCue.Clockwise => "Clockwise",
            MovementDirectionCue.Counterclockwise => "Counterclockwise",
            MovementDirectionCue.Inward => "Inward",
            MovementDirectionCue.Outward => "Outward",
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
        };
    }

    private void RenderTimedPairWorkoutPhase(MovementPhasePresentation presentation)
    {
        if (presentation.ActiveScreenSide is ScreenSide activeSide)
        {
            RenderSplitWorkoutPhase(activeSide == ScreenSide.Left);
            return;
        }

        RenderFullWorkoutPhase(Resource.Color.move_surface);
    }

    private void RenderSplitWorkoutPhase(bool activeLeft)
    {
        _workoutPhaseSurface.Visibility = ViewStates.Visible;
        SetWorkoutPhaseHalf(_workoutPhaseLeft, active: activeLeft);
        SetWorkoutPhaseHalf(_workoutPhaseRight, active: !activeLeft);
        SetExerciseMediaPhase(resting: false);
        AnimatePhaseSurface();
    }

    private void SetWorkoutPhaseHalf(View half, bool active)
    {
        half.SetBackgroundColor(new Android.Graphics.Color(GetColor(
            active
                ? Resource.Color.move_surface
                : Resource.Color.rest_surface)));
    }

    private void RenderFullWorkoutPhase(int colorResource)
    {
        var color = new Android.Graphics.Color(GetColor(colorResource));
        _workoutPhaseSurface.Visibility = ViewStates.Visible;
        _workoutPhaseLeft.SetBackgroundColor(color);
        _workoutPhaseRight.SetBackgroundColor(color);
        SetExerciseMediaPhase(
            resting: colorResource == Resource.Color.rest_surface);
        AnimatePhaseSurface();
    }

    private void SetExerciseMediaPhase(bool resting)
    {
        SetExerciseMediaBackground(resting
            ? Resource.Drawable.media_card_rest_background
            : Resource.Drawable.media_card_move_background);
    }

    private void SetExerciseMediaBackground(int drawableResource)
    {
        _exerciseMediaCard.SetBackgroundResource(drawableResource);
        int mediaPadding = DpInt(4f);
        _exerciseMediaCard.SetPadding(
            mediaPadding,
            mediaPadding,
            mediaPadding,
            mediaPadding);
    }

    private void AnimatePhaseSurface()
    {
        _workoutPhaseSurface.Animate()?.Cancel();
        _workoutPhaseSurface.Alpha = 0.86f;
        if (_workoutPhaseSurface.Animate() is { } animator)
        {
            animator
                .Alpha(1f)
                .SetDuration(HueMotionDurationMilliseconds)
                .Start();
        }
    }

    private void AnimateMediaPhase(bool resting)
    {
        _exerciseMediaCard.Animate()?.Cancel();
        if (_exerciseMediaCard.Animate() is not { } animator)
        {
            return;
        }

        animator
            .Alpha(resting ? 0.92f : 1f)
            .ScaleX(resting ? 0.985f : 1f)
            .ScaleY(resting ? 0.985f : 1f)
            .SetDuration(HueMotionDurationMilliseconds)
            .Start();
    }

    private void RenderRestVisuals()
    {
        SetExerciseMediaMirrored(mirrored: false);
        RenderFullWorkoutPhase(Resource.Color.rest_surface);
        AnimateMediaPhase(resting: true);
    }

    private void ResetMovementVisuals()
    {
        _lastMovementPhase = null;
        RenderCountdownPhase(MovementDirectionCue.Move);
        SetExerciseMediaMirrored(mirrored: false);
        _exerciseMediaCard.Animate()?.Cancel();
        _exerciseMediaCard.Alpha = 1f;
        _exerciseMediaCard.ScaleX = 1f;
        _exerciseMediaCard.ScaleY = 1f;
        SetExerciseMediaBackground(Resource.Drawable.media_card_background);
        _workoutPhaseSurface.Animate()?.Cancel();
        _workoutPhaseSurface.Alpha = 1f;
        _workoutPhaseSurface.Visibility = ViewStates.Gone;
    }

    private void BeginRest()
    {
        _sessionService.BeginRest(
            _state,
            _currentWorkoutGroup,
            DateTimeOffset.UtcNow.AddSeconds(RestSeconds).ToUnixTimeMilliseconds());
        _stateStore.Save(_state);

        _restActive = true;
        _exerciseVideo.Pause();
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        ShowRestPanel();
        AnnouncePhaseForAccessibility(_restPanel, GetRestDescription());
        ResumeRestCountdown();
    }

    private string GetRestDescription() =>
        _currentWorkoutGroup.IsDirectionPairLead
            ? "Rest, 15 seconds. The other direction is next."
            : "Rest, 15 seconds. Tap the heart to keep this exercise.";

    private void ShowRestPanel()
    {
        RenderRestVisuals();
        ShowWorkoutPhase(WorkoutPhase.Rest);
        UpdateKeepButtonState();
        UpdateRestCountdownText();
    }

    private void UpdateKeepButtonState()
    {
        if (_currentWorkoutGroup.IsDirectionPairLead)
        {
            _keepButton.Enabled = false;
            _keepButton.Visibility = ViewStates.Gone;
            return;
        }

        _keepButton.Visibility = ViewStates.Visible;
        _keepButton.Enabled = !_state.PendingRestKept;
        _keepButton.Alpha = 1f;
        _keepButton.SetBackgroundResource(_state.PendingRestKept
            ? Resource.Drawable.kept_button_background
            : Resource.Drawable.rest_button_background);
        _keepButton.SetColorFilter(new Android.Graphics.Color(GetColor(
            _state.PendingRestKept
                ? Resource.Color.accent_text
                : Resource.Color.white)));
        _keepButton.ContentDescription = _state.PendingRestKept
            ? "Exercise kept for the next session"
            : GetString(Resource.String.keep_exercise_description);
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
        string secondsText = secondsRemaining.ToString();
        if (_restCountdownText.Text != secondsText)
        {
            _restCountdownText.Text = secondsText;
            _restCountdownText.ContentDescription =
                $"Rest, {secondsRemaining} seconds remaining";
        }
        _restProgress.Progress = (int)Math.Min(
            millisecondsRemaining,
            RestSeconds * 1000L);
    }

    private void PauseRestCountdown()
    {
        _restTimer?.Cancel();
        _restTimer?.Dispose();
        _restTimer = null;
    }

    private void KeepCurrentExercise()
    {
        if (!_restActive ||
            _state.PendingRestKept ||
            _currentWorkoutGroup.IsDirectionPairLead)
        {
            return;
        }

        _state.PendingRestKept = true;
        _stateStore.Save(_state);
        _keepButton.Enabled = false;
        _keepButton.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        CompleteRest();
    }

    private void CompleteRest()
    {
        if (!_restActive || _state.PendingRestGroupId != _currentWorkoutGroup.Id)
        {
            return;
        }

        _restActive = false;
        PauseRestCountdown();
        if (_currentWorkoutGroup.IsDirectionPairLead)
        {
            _sessionService.AdvanceDirectionPair(_state, _currentWorkoutGroup);
            _sessionService.ClearPendingRest(_state);
            _stateStore.Save(_state);
            ShowNextExercise();
            return;
        }

        bool keep = _state.PendingRestKept;
        FinalizeCurrentRound(keep);
    }

    private void FinalizeCurrentRound(bool keep)
    {
        RecordedWorkoutOutcome result = _sessionService.RecordOutcomeWithScoreUpdates(
            _state,
            _currentWorkoutGroup,
            keep);
        _sessionService.ClearPendingRest(_state);

        SaveStateAndScores(result.ScoreUpdates);
        if (_state.WorkoutCompleted)
        {
            PlayWhistleCue(_workoutCompleteWhistleId);
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
        ResetMovementVisuals();
        _currentExercise = null;
        ShowAppScreen(AppScreen.Completion);

        _completionHalo.Animate()?.Cancel();
        _completionHalo.Alpha = 0f;
        _completionHalo.ScaleX = 0.88f;
        _completionHalo.ScaleY = 0.88f;
        if (_completionHalo.Animate() is { } haloAnimator)
        {
            haloAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(240L)
                .Start();
        }

        _completionMark.Animate()?.Cancel();
        _completionMark.Alpha = 0f;
        _completionMark.ScaleX = 0.8f;
        _completionMark.ScaleY = 0.8f;
        if (_completionMark.Animate() is { } markAnimator)
        {
            markAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetStartDelay(40L)
                .SetDuration(200L)
                .Start();
        }

        _doneButton.Animate()?.Cancel();
        _doneButton.Alpha = 0f;
        _doneButton.TranslationY = Dp(5f);
        if (_doneButton.Animate() is { } doneAnimator)
        {
            doneAnimator
                .Alpha(1f)
                .TranslationY(0f)
                .SetStartDelay(60L)
                .SetDuration(180L)
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

    private void ConfigureWhistleCues()
    {
        try
        {
            ConfigureWhistleCuesCore();
        }
        catch (Exception)
        {
            _whistleSoundPool?.Release();
            _whistleSoundPool?.Dispose();
            _whistleSoundPool = null;
            _movementStartWhistleId = 0;
            _sideChangeWhistleId = 0;
            _restStartWhistleId = 0;
            _workoutCompleteWhistleId = 0;
        }
    }

    private void ConfigureWhistleCuesCore()
    {
        using var attributesBuilder = new Android.Media.AudioAttributes.Builder();
        Android.Media.AudioAttributes.Builder configuredAttributesBuilder =
            attributesBuilder.SetUsage(
                Android.Media.AudioUsageKind.Media)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle audio usage.");
        configuredAttributesBuilder = configuredAttributesBuilder.SetContentType(
            Android.Media.AudioContentType.Sonification)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle audio content.");
        using Android.Media.AudioAttributes attributes =
            configuredAttributesBuilder.Build()
            ?? throw new InvalidOperationException(
                "Unable to create whistle audio attributes.");
        using var soundPoolBuilder = new Android.Media.SoundPool.Builder();
        Android.Media.SoundPool.Builder configuredSoundPoolBuilder =
            soundPoolBuilder.SetMaxStreams(1)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle playback streams.");
        configuredSoundPoolBuilder = configuredSoundPoolBuilder.SetAudioAttributes(
            attributes)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle playback attributes.");
        Android.Media.SoundPool soundPool = configuredSoundPoolBuilder.Build()
            ?? throw new InvalidOperationException(
                "Unable to create whistle playback.");
        _whistleSoundPool = soundPool;
        _movementStartWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_start,
            1);
        _sideChangeWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_side_change,
            1);
        _restStartWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_rest,
            1);
        _workoutCompleteWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_complete,
            1);
    }

    private void PlayWhistleCue(int soundId)
    {
        try
        {
            if (soundId > 0)
            {
                _whistleSoundPool?.Play(
                    soundId,
                    0.78f,
                    0.78f,
                    1,
                    0,
                    1f);
            }
        }
        catch (Exception)
        {
            // A missing or unavailable audio output should not stop a workout.
        }
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
        private readonly Action<long> _onTick;
        private readonly Action _onFinish;

        public WorkoutCountDownTimer(
            long millisInFuture,
            long countDownInterval,
            Action<long> onTick,
            Action onFinish)
            : base(millisInFuture, countDownInterval)
        {
            _onTick = onTick;
            _onFinish = onFinish;
        }

        public override void OnTick(long millisUntilFinished)
        {
            _onTick(millisUntilFinished);
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
        Func<int> getOptionIndex,
        Action<int> setOptionIndex) : View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(
            View host,
            Android.Views.Accessibility.AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            int minutes = getMinutes();
            int optionIndex = getOptionIndex();
#pragma warning disable CA1422 // Obtain is required for the supported API 24-29 range.
            info.SetRangeInfo(
                Android.Views.Accessibility.AccessibilityNodeInfo.RangeInfo.Obtain(
                    Android.Views.Accessibility.RangeType.Int,
                    0,
                    ExerciseSessionService.SupportedWorkoutMinutes.Count - 1,
                    optionIndex));
#pragma warning restore CA1422
            info.ContentDescription =
                $"Workout duration, {minutes} minutes. " +
                "Options: 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90 minutes";
        }

        public override bool PerformAccessibilityAction(
            View host,
            Android.Views.Accessibility.Action action,
            Bundle? arguments)
        {
            if ((int)action == Android.Resource.Id.AccessibilityActionSetProgress &&
                arguments is not null)
            {
                float requestedOptionIndex = arguments.GetFloat(
                    Android.Views.Accessibility.AccessibilityNodeInfo
                        .ActionArgumentProgressValue);
                int optionIndex = Math.Clamp(
                    (int)MathF.Round(requestedOptionIndex),
                    0,
                    ExerciseSessionService.SupportedWorkoutMinutes.Count - 1);
                setOptionIndex(optionIndex);
                return true;
            }

            return base.PerformAccessibilityAction(host, action, arguments);
        }
    }
}
