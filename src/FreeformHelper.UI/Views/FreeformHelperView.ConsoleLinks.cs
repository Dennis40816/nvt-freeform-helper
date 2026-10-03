using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using AvaloniaEdit;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    private void OnConsoleEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not TextEditor editor)
        {
            return;
        }

        var point = _consoleTextView is not null ? e.GetPosition(_consoleTextView) : default(Point?);
        TryHandleConsoleLinkActivation(editor, e, point);
    }

    private void TryHandleConsoleLinkActivation(TextEditor editor, PointerReleasedEventArgs e, Point? pointInTextView)
    {
        if (e.Handled)
        {
            return;
        }

        var isLeftClick = e.InitialPressMouseButton == MouseButton.Left || e.InitialPressMouseButton == MouseButton.None;
        if (!isLeftClick)
        {
            _consolePressedLink = null;
            return;
        }

        Logger.Info(CultureInfo.InvariantCulture, "Console link open requested (Click).");

        ConsoleLinkSpan? link = null;
        if (pointInTextView is Point point)
        {
            link = FindConsoleLinkAtPoint(editor, point);
        }

        link ??= _consolePressedLink;
        link ??= ConsoleLinkParser.FindAtOffset(_consoleLinks, editor.CaretOffset);
        _consolePressedLink = null;

        if (link is null)
        {
            Logger.Info(CultureInfo.InvariantCulture, "Console link open skipped: no link resolved under pointer/caret.");
            return;
        }

        Logger.Info(CultureInfo.InvariantCulture, "Console link resolved: raw={0}.", link.Target);
        if (TryOpenConsoleLink(link))
        {
            e.Handled = true;
        }
    }

    private ConsoleLinkSpan? FindConsoleLinkAtPoint(TextEditor editor, Point pointInTextView)
    {
        if (editor.Document is null)
        {
            return null;
        }

        var textView = editor.TextArea?.TextView;
        if (textView is null)
        {
            return null;
        }

        var position = textView.GetPosition(pointInTextView)
            ?? textView.GetPositionFloor(pointInTextView);
        if (position is null)
        {
            return null;
        }

        try
        {
            var offset = editor.Document.GetOffset(position.Value.Location);
            return ConsoleLinkParser.FindAtOffset(_consoleLinks, offset);
        }
        catch
        {
            return null;
        }
    }

    private bool TryOpenConsoleLink(ConsoleLinkSpan link)
    {
        if (link.IsUrl)
        {
            var openedUrl = TryOpenWithShell(link.Target);
            if (!openedUrl)
            {
                Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (url): target={0}.", link.Target);
            }
            else
            {
                Logger.Debug(CultureInfo.InvariantCulture, "Console link open success (url): target={0}.", link.Target);
            }

            return openedUrl;
        }

        var resolvedTarget = ResolveConsoleTargetPath(link.Target);
        if (resolvedTarget is null)
        {
            Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (path not found): raw={0}.", link.Target);
            return false;
        }

        if (Directory.Exists(resolvedTarget))
        {
            var openedDirectory = TryOpenDirectory(resolvedTarget);
            if (!openedDirectory)
            {
                Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (directory): target={0}.", resolvedTarget);
            }
            else
            {
                Logger.Debug(CultureInfo.InvariantCulture, "Console link open success (directory): target={0}.", resolvedTarget);
            }

            return openedDirectory;
        }

        if (!File.Exists(resolvedTarget))
        {
            Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (file not found after resolve): raw={0}, resolved={1}.", link.Target, resolvedTarget);
            return false;
        }

        var openedFile = TryOpenFileInExplorer(resolvedTarget);
        if (!openedFile)
        {
            Logger.Warn(CultureInfo.InvariantCulture, "Console link open failed (file): target={0}.", resolvedTarget);
        }
        else
        {
            Logger.Debug(CultureInfo.InvariantCulture, "Console link open success (file): target={0}.", resolvedTarget);
        }

        return openedFile;
    }

    private static bool TryOpenDirectory(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
            return process is not null;
        }
        catch
        {
            return TryOpenWithShell(path);
        }
    }

    private static bool TryOpenFileInExplorer(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
            return process is not null;
        }
        catch
        {
            return TryOpenWithShell(path);
        }
    }

    private static bool TryOpenWithShell(string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private string? ResolveConsoleTargetPath(string rawTarget)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return null;
        }

        var target = rawTarget.Trim().Trim('"');
        if (Path.IsPathRooted(target))
        {
            return File.Exists(target) || Directory.Exists(target)
                ? Path.GetFullPath(target)
                : null;
        }

        var currentDirectoryCandidate = Path.GetFullPath(target, Environment.CurrentDirectory);
        if (File.Exists(currentDirectoryCandidate) || Directory.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        var appBaseCandidate = Path.GetFullPath(target, AppContext.BaseDirectory);
        if (File.Exists(appBaseCandidate) || Directory.Exists(appBaseCandidate))
        {
            return appBaseCandidate;
        }

        var repoRoot = FindConsoleRepoRoot();
        if (repoRoot is null)
        {
            return null;
        }

        var normalized = target.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var repoCandidate = Path.GetFullPath(normalized, repoRoot);
        if (File.Exists(repoCandidate) || Directory.Exists(repoCandidate))
        {
            return repoCandidate;
        }

        if (!target.Contains('/') && !target.Contains('\\'))
        {
            var filenameOnlyMatches = EnumerateRepoMatches(repoRoot, target);
            var shortest = filenameOnlyMatches
                .OrderBy(static path => path.Length)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(shortest))
            {
                return shortest;
            }
        }

        return null;
    }

    private string? FindConsoleRepoRoot()
    {
        if (!string.IsNullOrEmpty(_consoleRepoRoot) && Directory.Exists(_consoleRepoRoot))
        {
            return _consoleRepoRoot;
        }

        foreach (var seed in GetRepoRootSearchSeeds())
        {
            var root = WalkUpToRepoRoot(seed);
            if (root is null)
            {
                continue;
            }

            _consoleRepoRoot = root;
            return _consoleRepoRoot;
        }

        return null;
    }

    private static IEnumerable<string> GetRepoRootSearchSeeds()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userDirectory))
        {
            yield return userDirectory;
        }
    }

    private static string? WalkUpToRepoRoot(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed) || !Directory.Exists(seed))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(seed));
        while (directory is not null)
        {
            var hasSln = File.Exists(Path.Combine(directory.FullName, "FreeformHelper.sln"));
            var hasGit = Directory.Exists(Path.Combine(directory.FullName, ".git"));
            if (hasSln || hasGit)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string[] EnumerateRepoMatches(string repoRoot, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(repoRoot, fileName, SearchOption.AllDirectories)
                .Take(32)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

}

