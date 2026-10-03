using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.Views;

public sealed partial class CadLoadSpinnerWindow : Window, IDisposable
{
    private readonly CadLoadSpinnerWindowOptions? _options;
    private DispatcherTimer? _parentMonitorTimer;
    private Process? _parentProcess;
    private CancellationTokenSource? _ipcCts;
    private Task? _ipcLoopTask;

    public CadLoadSpinnerWindow()
        : this(null)
    {
    }

    internal CadLoadSpinnerWindow(CadLoadSpinnerWindowOptions? options)
    {
        _options = options;
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyWindowPresentationContract();
        StartParentMonitor();
        StartIpcServer();
        if (_options is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (IsVisible)
                {
                    Hide();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        StopIpcServer();
        StopParentMonitor();
    }

    private void PositionFromOwnerBounds(int ownerLeft, int ownerTop, int ownerWidth, int ownerHeight)
    {
        if (RenderScaling <= 0)
        {
            return;
        }

        var widthPx = Math.Max(1, (int)Math.Round(Bounds.Width * RenderScaling, MidpointRounding.AwayFromZero));
        var heightPx = Math.Max(1, (int)Math.Round(Bounds.Height * RenderScaling, MidpointRounding.AwayFromZero));
        var left = ownerLeft + Math.Max(0, (ownerWidth - widthPx) / 2);
        var top = ownerTop + Math.Max(0, (ownerHeight - heightPx) / 2);
        Position = new Avalonia.PixelPoint(left, top);
    }

    private void StartParentMonitor()
    {
        if (_options is null || _options.ParentProcessId <= 0)
        {
            return;
        }

        try
        {
            _parentProcess = Process.GetProcessById(_options.ParentProcessId);
        }
        catch
        {
            Close();
            return;
        }

        _parentMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _parentMonitorTimer.Tick += OnParentMonitorTick;
        _parentMonitorTimer.Start();
    }

    private void StopParentMonitor()
    {
        if (_parentMonitorTimer is not null)
        {
            _parentMonitorTimer.Tick -= OnParentMonitorTick;
            _parentMonitorTimer.Stop();
            _parentMonitorTimer = null;
        }

        _parentProcess?.Dispose();
        _parentProcess = null;
    }

    private void OnParentMonitorTick(object? sender, EventArgs e)
    {
        if (_parentProcess is null || _parentProcess.HasExited)
        {
            Close();
        }
    }

    private void StartIpcServer()
    {
        if (_options is null || string.IsNullOrWhiteSpace(_options.PipeName))
        {
            return;
        }

        _ipcCts = new CancellationTokenSource();
        _ipcLoopTask = Task.Run(() => RunIpcLoopAsync(_options.PipeName, _ipcCts.Token));
    }

    private void StopIpcServer()
    {
        var cts = _ipcCts;
        _ipcCts = null;
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private async Task RunIpcLoopAsync(string pipeName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var reader = new StreamReader(
                    pipe,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);
                var requestJson = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    continue;
                }

                var request = JsonSerializer.Deserialize<CadLoadSpinnerRequest>(requestJson, CadLoadSpinnerIpcProtocol.JsonOptions);
                if (request is null)
                {
                    continue;
                }

                if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
                {
                    break;
                }

                await dispatcher!.InvokeAsync(
                    () => ApplyRequest(request),
                    DispatcherPriority.Render,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Best-effort helper process. Ignore malformed requests and keep serving.
            }
        }
    }

    private void ApplyRequest(CadLoadSpinnerRequest request)
    {
        switch (request.Command)
        {
            case CadLoadSpinnerIpcProtocol.PingCommand:
                break;
            case CadLoadSpinnerIpcProtocol.ShowCommand:
                PositionFromOwnerBounds(request.OwnerLeft, request.OwnerTop, request.OwnerWidth, request.OwnerHeight);
                if (!IsVisible)
                {
                    Show();
                }

                ApplyWindowPresentationContract();
                Topmost = true;
                Opacity = 1;
                break;
            case CadLoadSpinnerIpcProtocol.HideCommand:
                if (IsVisible)
                {
                    Hide();
                }
                break;
            case CadLoadSpinnerIpcProtocol.ShutdownCommand:
                Close();
                break;
        }
    }

    public void Dispose()
    {
        StopIpcServer();
        StopParentMonitor();
        GC.SuppressFinalize(this);
    }

    private void ApplyWindowPresentationContract()
    {
        IsHitTestVisible = false;
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var platformHandle = TryGetPlatformHandle();
        if (platformHandle?.Handle is not nint hwnd || hwnd == nint.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        style &= ~(WsCaption | WsThickFrame | WsBorder | WsDlgFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
        SetWindowLongPtr(hwnd, GwlStyle, new nint(style));

        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        exStyle |= WsExTransparent | WsExNoActivate | WsExToolWindow | WsExLayered;
        SetWindowLongPtr(hwnd, GwlExStyle, new nint(exStyle));

        SetWindowPos(
            hwnd,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNomove | SwpNosize | SwpNozorder | SwpNoActivate | SwpFrameChanged);
    }

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsBorder = 0x00800000L;
    private const long WsDlgFrame = 0x00400000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;

    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
