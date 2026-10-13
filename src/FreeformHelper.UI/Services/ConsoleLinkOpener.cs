using System.Collections.Concurrent;
using System.Globalization;
using NLog;
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.LogConsole;
using Nvt.Core.SourceFileNavigation;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Gives the console its link capabilities. The console asks on the UI thread, so a path is resolved
/// on a worker and the answer is cached. The first answer for a path is "checking"; the next hover sees the result.
/// </summary>
internal sealed class ConsoleLinkOpener : IConsoleLinkOpener
{
    private const string PendingReason = "Checking the path...";
    private const string MissingReason = "The path does not exist.";
    private static readonly TimeSpan MissingRetryAge = TimeSpan.FromSeconds(2);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<string, (ConsoleTargetResolution Resolution, DateTimeOffset CheckedAt)> _resolved =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _pending = new(StringComparer.Ordinal);
    private readonly Func<string, ConsoleTargetResolution> _resolve;
    private readonly Action<LinkTarget, ConsoleTargetResolution> _open;
    private readonly Action<Action> _schedule;
    private readonly TimeProvider _time;

    public ConsoleLinkOpener()
        : this(
            ConsoleTargetResolver.Resolve,
            OpenWithShell,
            static work => _ = Task.Run(work),
            TimeProvider.System)
    {
    }

    public ConsoleLinkOpener(
        Func<string, ConsoleTargetResolution> resolve,
        Action<LinkTarget, ConsoleTargetResolution> open,
        Action<Action> schedule,
        TimeProvider time)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public string? GetUnavailableReason(LinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == LinkKind.Url)
        {
            return null;
        }

        if (_resolved.TryGetValue(target.Path, out var known))
        {
            if (known.Resolution.Exists)
            {
                return null;
            }

            if (_time.GetUtcNow() - known.CheckedAt >= MissingRetryAge)
            {
                StartResolve(target.Path);
            }

            return MissingReason;
        }

        StartResolve(target.Path);
        return PendingReason;
    }

    /// <inheritdoc />
    public void Open(LinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == LinkKind.Url)
        {
            _schedule(() => _open(target, default));
            return;
        }

        _schedule(() =>
        {
            var resolution = Resolve(target.Path);
            if (resolution.Exists)
            {
                _open(target, resolution);
            }
            else
            {
                Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (path not found): raw={0}.", target.Path);
            }
        });
    }

    private void StartResolve(string path)
    {
        if (!_pending.TryAdd(path, true))
        {
            return;
        }

        _schedule(() =>
        {
            try
            {
                Resolve(path);
            }
            finally
            {
                _pending.TryRemove(path, out _);
            }
        });
    }

    private ConsoleTargetResolution Resolve(string path)
    {
        var resolution = _resolve(path);
        _resolved[path] = (resolution, _time.GetUtcNow());
        return resolution;
    }

    private static void OpenWithShell(LinkTarget target, ConsoleTargetResolution resolution)
    {
        var opened = target.Kind == LinkKind.Url
            ? SourceFileNavigator.OpenDefault(target.Path).Opened
            : OpenInExplorer(resolution);
        if (opened)
        {
            Logger.Debug(CultureInfo.InvariantCulture, "Console link open success: target={0}.", target.Path);
        }
        else
        {
            Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed: target={0}.", target.Path);
        }
    }

    private static bool OpenInExplorer(ConsoleTargetResolution resolution)
    {
        var fullPath = resolution.FullPath!;
        var arguments = resolution.IsDirectory ? new[] { fullPath } : new[] { "/select," + fullPath };
        return SourceFileNavigator.TryStart("explorer.exe", arguments, out _)
            || SourceFileNavigator.OpenDefault(fullPath).Opened;
    }
}
