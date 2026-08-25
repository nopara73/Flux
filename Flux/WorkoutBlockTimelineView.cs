using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Flux.Services;

namespace Flux;

public sealed class WorkoutBlockTimelineView : View
{
    private const float MinimumWidthDp = 42f;
    private const float MaximumWidthDp = 166f;
    private const float HeightDp = 52f;
    private const float TrackTopDp = 12f;
    private const float TrackHeightDp = 32f;
    private const float BorderDp = 3f;
    private const float InnerPaddingDp = 3f;
    private const float PreferredGapDp = 3f;
    private const float SegmentWidthDp = 21f;

    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Android.Graphics.Path _playheadPath = new();
    private readonly int _graphiteColor;
    private readonly int _surfaceColor;
    private readonly int _blueColor;
    private readonly int _redColor;
    private readonly int _neutralColor;
    private WorkoutBlockAccent[] _blocks = [WorkoutBlockAccent.Neutral];
    private int _currentBlockIndex;

    public WorkoutBlockTimelineView(Context context)
        : this(context, null)
    {
    }

    public WorkoutBlockTimelineView(Context context, IAttributeSet? attrs)
        : base(context, attrs)
    {
        _graphiteColor = context.GetColor(Resource.Color.brand_graphite);
        _surfaceColor = context.GetColor(Resource.Color.surface);
        _blueColor = context.GetColor(Resource.Color.rest_accent);
        _redColor = context.GetColor(Resource.Color.move_accent);
        _neutralColor = context.GetColor(Resource.Color.brand_chartreuse);
        ImportantForAccessibility = ImportantForAccessibility.Yes;
    }

    public void SetTimeline(
        IReadOnlyList<WorkoutBlockAccent> blocks,
        int currentBlockIndex)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Count == 0)
        {
            throw new ArgumentException(
                "An execution timeline requires at least one work block.",
                nameof(blocks));
        }
        if (currentBlockIndex < 0 || currentBlockIndex >= blocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentBlockIndex));
        }

        _blocks = blocks.ToArray();
        _currentBlockIndex = currentBlockIndex;
        RequestLayout();
        Invalidate();
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        float desiredWidthDp = Math.Clamp(
            28f + _blocks.Length * SegmentWidthDp,
            MinimumWidthDp,
            MaximumWidthDp);
        SetMeasuredDimension(
            ResolveSize(Dp(desiredWidthDp), widthMeasureSpec),
            ResolveSize(Dp(HeightDp), heightMeasureSpec));
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        float trackTop = Dp(TrackTopDp);
        float trackBottom = trackTop + Dp(TrackHeightDp);
        float border = Dp(BorderDp);
        float innerPadding = Dp(InnerPaddingDp);
        float cornerRadius = Dp(11f);
        var outer = new RectF(0f, trackTop, Width, trackBottom);
        _paint.Color = new Color(_graphiteColor);
        canvas.DrawRoundRect(outer, cornerRadius, cornerRadius, _paint);

        var inner = new RectF(
            border,
            trackTop + border,
            Width - border,
            trackBottom - border);
        _paint.Color = new Color(_surfaceColor);
        canvas.DrawRoundRect(
            inner,
            Math.Max(0f, cornerRadius - border),
            Math.Max(0f, cornerRadius - border),
            _paint);

        float contentLeft = inner.Left + innerPadding;
        float contentRight = inner.Right - innerPadding;
        float contentTop = inner.Top + innerPadding;
        float contentBottom = inner.Bottom - innerPadding;
        float contentWidth = Math.Max(1f, contentRight - contentLeft);
        float gap = _blocks.Length == 1
            ? 0f
            : Math.Min(
                Dp(PreferredGapDp),
                contentWidth / (_blocks.Length * 2f));
        float segmentWidth = Math.Max(
            1f,
            (contentWidth - gap * (_blocks.Length - 1)) / _blocks.Length);
        float segmentRadius = Math.Min(Dp(4f), segmentWidth / 3f);

        for (int index = 0; index < _blocks.Length; index++)
        {
            float left = contentLeft + index * (segmentWidth + gap);
            var segment = new RectF(
                left,
                contentTop,
                left + segmentWidth,
                contentBottom);
            _paint.Color = new Color(GetBlockColor(_blocks[index]));
            canvas.DrawRoundRect(
                segment,
                segmentRadius,
                segmentRadius,
                _paint);
        }

        float currentCenter = contentLeft +
            _currentBlockIndex * (segmentWidth + gap) +
            segmentWidth / 2f;
        float markerTop = trackTop - Dp(10f);
        float markerBottom = trackTop - Dp(3f);
        float markerHalfWidth = Dp(5f);
        _playheadPath.Reset();
        _playheadPath.MoveTo(currentCenter - markerHalfWidth, markerTop);
        _playheadPath.LineTo(currentCenter + markerHalfWidth, markerTop);
        _playheadPath.LineTo(currentCenter, markerBottom);
        _playheadPath.Close();
        _paint.Color = new Color(_graphiteColor);
        canvas.DrawPath(_playheadPath, _paint);
    }

    private int GetBlockColor(WorkoutBlockAccent accent) => accent switch
    {
        WorkoutBlockAccent.Blue => _blueColor,
        WorkoutBlockAccent.Red => _redColor,
        WorkoutBlockAccent.Neutral => _neutralColor,
        _ => throw new ArgumentOutOfRangeException(nameof(accent), accent, null),
    };

    private int Dp(float value) =>
        (int)MathF.Round(value * Resources!.DisplayMetrics!.Density);
}
