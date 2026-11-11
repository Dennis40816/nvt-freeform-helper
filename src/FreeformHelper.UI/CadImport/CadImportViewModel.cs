using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Infrastructure.Dxf;

namespace FreeformHelper.UI.CadImport;

/// <summary>Owns import choices and the attached picker for one workspace. UI thread only.</summary>
public sealed partial class CadImportViewModel : ObservableObject
{
    private readonly Action<bool, bool, Action<bool>, string> _trackUndo;
    private IAsyncRelayCommand? _openCommand;

    internal CadImportViewModel(Func<Func<DxfImportOptions>, ICadImportWorkflow> createWorkflow,
        Action<bool, bool, Action<bool>, string> trackUndo)
    {
        _trackUndo = trackUndo;
        Workflow = createWorkflow(BuildDxfOptions);
    }

    internal ICadImportWorkflow Workflow { get; }
    [ObservableProperty] private bool _importOnlyClosedPolylines = true;
    [ObservableProperty] private bool _importBlockPolylines = true;
    public Func<Task<string?>>? PickOpenDxfPathAsync { get; set; }
    public IAsyncRelayCommand OpenDxfCommand => _openCommand ?? throw new InvalidOperationException("Import command has not been initialized.");

    internal void InitializeCommand(Func<Func<Task>, IAsyncRelayCommand> createCommand) =>
        _openCommand = createCommand(() => Workflow.OpenAsync(PickOpenDxfPathAsync));

    internal DxfImportOptions BuildDxfOptions() => new()
    {
        OnlyClosedPolylines = ImportOnlyClosedPolylines,
        IncludeBlockPolylines = ImportBlockPolylines,
    };

    partial void OnImportOnlyClosedPolylinesChanging(bool value) =>
        _trackUndo(ImportOnlyClosedPolylines, value, v => ImportOnlyClosedPolylines = v, "Import closed polylines");

    partial void OnImportBlockPolylinesChanging(bool value) =>
        _trackUndo(ImportBlockPolylines, value, v => ImportBlockPolylines = v, "Import block polylines");
}
