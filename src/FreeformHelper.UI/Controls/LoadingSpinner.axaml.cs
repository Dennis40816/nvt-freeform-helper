using System.Diagnostics;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;

namespace FreeformHelper.UI.Controls;

public sealed partial class LoadingSpinner : UserControl
{
    private const double FlowOffsetStep = 0.45d;
    private const double FlowUnitsPerSecond = 27d;
    private const double FlowPatternLength = 23d;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);
    public static readonly StyledProperty<bool> IsAnimationActiveProperty =
        AvaloniaProperty.Register<LoadingSpinner, bool>(nameof(IsAnimationActive), true);

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _animationClock = new();
    private Ellipse? _flowEllipse;
    private bool _isAttached;
    private bool _isAnimating;
    private long _lastTickMs;

    public bool IsAnimationActive
    {
        get => GetValue(IsAnimationActiveProperty);
        set => SetValue(IsAnimationActiveProperty, value);
    }

    public LoadingSpinner()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += OnTick;

        _flowEllipse = this.FindControl<Ellipse>("SpinnerFlow");

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        this.GetObservable(IsAnimationActiveProperty).Subscribe(_ => UpdateAnimationState());
        this.GetObservable(IsVisibleProperty).Subscribe(_ => UpdateAnimationState());
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        UpdateAnimationState();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        StopAnimation();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isAnimating || !_isAttached)
        {
            StopAnimation();
            return;
        }

        if (_flowEllipse is null)
        {
            return;
        }

        var nowMs = _animationClock.ElapsedMilliseconds;
        var deltaMs = nowMs - _lastTickMs;
        if (deltaMs <= 0)
        {
            return;
        }

        _lastTickMs = nowMs;
        var deltaOffset = FlowUnitsPerSecond * (deltaMs / 1000d);
        var nextOffset = _flowEllipse.StrokeDashOffset - deltaOffset;
        if (nextOffset <= -FlowPatternLength)
        {
            nextOffset += FlowPatternLength;
        }

        _flowEllipse.StrokeDashOffset = nextOffset;
    }

    private void UpdateAnimationState()
    {
        if (_isAttached && IsAnimationActive)
        {
            StartAnimation();
            return;
        }

        StopAnimation();
    }

    private void StartAnimation()
    {
        _isAnimating = true;
        if (!_animationClock.IsRunning)
        {
            _animationClock.Restart();
            _lastTickMs = _animationClock.ElapsedMilliseconds;
        }

        if (_flowEllipse is not null && _flowEllipse.StrokeDashOffset == 0d)
        {
            _flowEllipse.StrokeDashOffset = -FlowOffsetStep;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void StopAnimation()
    {
        _isAnimating = false;
        _timer.Stop();
        _animationClock.Reset();
        _lastTickMs = 0;
        if (_flowEllipse is not null)
        {
            _flowEllipse.StrokeDashOffset = 0d;
        }
    }
}
