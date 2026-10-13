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
    private const string UnsupportedSchemeReason = "Only http and https links can be opened.";
    private static readonly TimeSpan _missingRetryAge = TimeSpan.FromSeconds(2);

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<string, (ConsoleTargetResolution Resolution, DateTimeOffset CheckedAt)> _resolved =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _pending = new(StringComparer.Ordinal);
    private readonly Func<string, ConsoleTargetResolution> _resolve;
    private readonly Action<LinkTarget, ConsoleTargetResolution> _open;
    private readonly Action<Action> _schedule;
    private readonly Func<DateTimeOffset> _utcNow;

    public ConsoleLinkOpener()
        : this(
            ConsoleTargetResolver.Resolve,
            OpenWithShell,
            static work => _ = Task.Run(work),
            TimeProvider.System.GetUtcNow)
    {
    }

    public ConsoleLinkOpener(
        Func<string, ConsoleTargetResolution> resolve,
        Action<LinkTarget, ConsoleTargetResolution> open,
        Action<Action> schedule,
        Func<DateTimeOffset> utcNow)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    /// <inheritdoc />
    public string? GetUnavailableReason(LinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == LinkKind.Url)
        {
            return IsWebUrl(target.Path) ? null : UnsupportedSchemeReason;
        }

        if (_resolved.TryGetValue(target.Path, out var known))
        {
            if (known.Resolution.Exists)
            {
                return null;
            }

            if (_utcNow() - known.CheckedAt >= _missingRetryAge)
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
            if (!IsWebUrl(target.Path))
            {
                _logger.Warn(CultureInfo.InvariantCulture, "Console link open refused (not an http or https link): raw={0}.", target.Path);
                return;
            }

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
                _logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (path not found): raw={0}.", target.Path);
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
        _resolved[path] = (resolution, _utcNow());
        return resolution;
    }

    private static void OpenWithShell(LinkTarget target, ConsoleTargetResolution resolution)
    {
        var opened = target.Kind == LinkKind.Url
            ? SourceFileNavigator.OpenDefault(target.Path).Opened
            : OpenInExplorer(resolution);
        if (opened)
        {
            _logger.Debug(CultureInfo.InvariantCulture, "Console link open success: target={0}.", target.Path);
        }
        else
        {
            _logger.Warn(CultureInfo.InvariantCulture, "Console link open failed: target={0}.", target.Path);
        }
    }

    private static bool OpenInExplorer(ConsoleTargetResolution resolution)
    {
        var fullPath = resolution.FullPath!;
        var arguments = resolution.IsDirectory ? new[] { fullPath } : new[] { "/select," + fullPath };
        // Explorer only selects a file or opens a folder. It never runs the file.
        return SourceFileNavigator.TryStart("explorer.exe", arguments, out _);
    }

    private static bool IsWebUrl(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
