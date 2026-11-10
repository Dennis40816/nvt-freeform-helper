// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests.UI.Bindings;

internal static class MainWindowBindingCatalog
{
    // These are context assignments made by window constructors or their entry workflows, not failure exceptions.
    public static IReadOnlyList<(string File, string Context)> DetachedContexts { get; } =
    [
        Draft("SettingsWindow"),
        Draft("CascadeIcSettingsWindow"),
        Dialog("ConfirmDialog", "ConfirmDialogViewModel"),
        Dialog("WarningDialog", "WarningDialogViewModel"),
        Dialog("DxfEditChangeListWindow", "DxfEditChangeListViewModel"),
        Dialog("DxfOverlapReportWindow", "DxfOverlapReportViewModel"),
        Dialog("IndexMappingReportWindow", "IndexMappingReportViewModel"),
        Dialog("LayerImageExportWindow", "LayerImageExportViewModel"),
        Dialog("LayerImageExportSettingsDialog", "LayerImageExportSettingsViewModel"),
        Dialog("NotchDetailWindow", "NotchDetailViewModel"),
        Dialog("NotchExportSelectionWindow", "NotchExportSelectionViewModel"),
        Dialog("NotchExportColumnFilterWindow", "NotchExportColumnFilterDialogViewModel"),
        ("Views/CoordinateArtifactDetailWindow.axaml", "FreeformHelper.Application.Services.CoordinateArtifactRow"),
    ];

    public static IReadOnlyList<Type> ContentAlternatives(Type actual, string path, Type context) =>
        context == typeof(ShellViewModel) && path == nameof(ShellViewModel.CurrentViewModel)
            ? [typeof(FreeformHelperViewModel), typeof(HowToUseViewModel), typeof(DevViewModel),
                typeof(SimulationHostViewModel), typeof(CoordinatePlannerHostViewModel)]
            : [actual];

    public static void RequireOwningChildren(ShellViewModel shell, SettingsWindowViewModel draft)
    {
        // Only stable owning children are evaluated. Nullable selection/snapshot results are deliberately not getters in this list.
        BindingPathVerifier.RequireInitialized(shell, nameof(ShellViewModel.FreeformHelper));
        BindingPathVerifier.RequireInitialized(shell, nameof(ShellViewModel.FreeformHelper) + "." + nameof(FreeformHelperViewModel.Step4Index));
        BindingPathVerifier.RequireInitialized(shell, nameof(ShellViewModel.FreeformHelper) + "." + nameof(FreeformHelperViewModel.PadInspectorUi));
        BindingPathVerifier.RequireInitialized(shell, nameof(ShellViewModel.ConsoleSession) + ".Controller");
        BindingPathVerifier.RequireInitialized(draft, nameof(SettingsWindowViewModel.ActiveAreaPixels));
    }

    public static void CheckCodePaths(BindingTypeResolver types, BindingPathVerifier paths, BindingWalkReport report)
    {
        var root = TestPaths.FromRepo("src", "FreeformHelper.UI");
        var registrations = new Dictionary<(string File, string Member), (Type Source, Type Target, string Property)>
        {
            [("Views/FreeformHelperView.SettingsWindow.cs", "FreeformHelperViewModel.IsProjectEditingEnabled")] =
                (typeof(FreeformHelperViewModel), typeof(Window), nameof(Window.IsEnabled)),
            [("Views/SimulationWorkspaceView.Integrated.cs", "ToggleButton.IsChecked")] =
                (typeof(ToggleButton), typeof(Grid), nameof(Grid.IsVisible)),
        };
        var consumed = new HashSet<(string File, string Member)>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(static path => !path.Split(Path.DirectorySeparatorChar).Any(static part => part is "bin" or "obj")))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = TestFiles.ReadContent(file);
            foreach (Match call in Regex.Matches(text, @"\bnew\s+(?:[\w.]+\.)?(?:Binding|ReflectionBinding|CompiledBinding|MultiBinding)\s*[({]",
                         RegexOptions.CultureInvariant))
            {
                var owner = new XElement("CodeBinding");
                var site = new BindingSite(relative, owner, owner, "C# path", text[call.Index..Math.Min(text.Length, call.Index + 160)],
                    text.AsSpan(0, call.Index).Count('\n') + 1);
                var expression = Regex.Match(text[call.Index..], @"^new\s+Binding\s*\(\s*nameof\((?<member>\w+\.\w+)\)\s*\)",
                    RegexOptions.CultureInvariant);
                var key = (relative, expression.Groups["member"].Value);
                if (!expression.Success || !registrations.TryGetValue(key, out var registration) || !consumed.Add(key))
                {
                    report.Add(new BindingFinding(site, BindingCoverage.Uncovered, "unknown", site.Text, "C# source",
                        "New, duplicate or dynamic C# path requires explicit source/target provenance."));
                    continue;
                }
                try
                {
                    var path = key.Item2.Split('.')[^1];
                    var mode = types.Mode(registration.Target, registration.Property, null, owner);
                    paths.Follow(new BindingValue(registration.Source), path, owner,
                        mode is Avalonia.Data.BindingMode.TwoWay or Avalonia.Data.BindingMode.OneWayToSource);
                    report.Add(new BindingFinding(site, BindingCoverage.Checked, registration.Source.Name, path, string.Empty, "C# path checked."));
                }
                catch (BindingWalkException error)
                {
                    report.Add(new BindingFinding(site, error.Coverage, registration.Source.Name, key.Item2, error.Segment, error.Message));
                }
            }
            AuditBindingCalls(relative, text, report);
        }
        foreach (var entry in registrations.Keys.Where(key => !consumed.Contains(key)))
        {
            var owner = new XElement("CodeBinding");
            report.Add(new BindingFinding(new BindingSite(entry.File, owner, owner, "C# path", entry.Member),
                BindingCoverage.Uncovered, "unknown", entry.Member, "registration", "Registered source site was not found."));
        }
    }

    private static void AuditBindingCalls(string file, string text, BindingWalkReport report)
    {
        foreach (Match call in Regex.Matches(text, @"\.(?:Bind|SetBinding)\s*\(", RegexOptions.CultureInvariant))
        {
            var end = text.IndexOf(';', call.Index);
            var statement = text[call.Index..(end < 0 ? text.Length : end)];
            if (Regex.IsMatch(statement, @"\bnew\s+Binding\s*\(", RegexOptions.CultureInvariant))
            {
                continue;
            }
            var owner = new XElement("CodeBinding");
            report.Add(new BindingFinding(new BindingSite(file, owner, owner, "C# binding", statement, text.AsSpan(0, call.Index).Count('\n') + 1),
                BindingCoverage.Uncovered, "unknown", "(no registered path)", "Bind",
                "Non-path resource or indirect binding call requires separate runtime coverage."));
        }
    }

    private static (string, string) Draft(string window) => Dialog(window, nameof(SettingsWindowViewModel));
    private static (string, string) Dialog(string window, string model) => ($"Views/{window}.axaml", "FreeformHelper.UI.ViewModels." + model);
}
