using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Contracts;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.CadImport;

/// <summary>Owns overlap presentation, progress and commands. UI thread only.</summary>
public sealed partial class DxfQualityViewModel : ObservableObject, IDxfQualityPresentation
{
    private readonly UiOperationProgressState _progress = new();
    private (IAsyncRelayCommand Check, IRelayCommand Select, IRelayCommand Clear, IAsyncRelayCommand Details)? _commands;

    internal DxfQualityViewModel(Func<IDxfQualityPresentation, IDxfQualityWorkflow> createWorkflow)
    {
        Workflow = createWorkflow(this);
    }

    internal IDxfQualityWorkflow Workflow { get; }
    public Func<DxfOverlapReportViewModel, Task>? OpenDxfOverlapReportAsync { get; set; }
    bool IDxfQualityPresentation.HasDetailsHandler => OpenDxfOverlapReportAsync is not null;
    Task IDxfQualityPresentation.OpenDetailsAsync(string summary, IReadOnlyList<string> issues) =>
        OpenDxfOverlapReportAsync!(new DxfOverlapReportViewModel(summary, issues));

    public IAsyncRelayCommand CheckDxfQualityCommand => Commands.Check;
    public IRelayCommand SelectDxfOverlapPadsCommand => Commands.Select;
    public IRelayCommand ClearDxfOverlapHighlightsCommand => Commands.Clear;
    public IAsyncRelayCommand ShowDxfOverlapDetailsCommand => Commands.Details;
    private (IAsyncRelayCommand Check, IRelayCommand Select, IRelayCommand Clear, IAsyncRelayCommand Details) Commands =>
        _commands ?? throw new InvalidOperationException("Quality commands have not been initialized.");

    internal void InitializeCommands(Func<Func<Task>, IAsyncRelayCommand> createAsync, Func<Action, IRelayCommand> create)
    {
        _commands = (createAsync(Workflow.CheckAsync), create(Workflow.SelectOverlapPads),
            create(Workflow.ClearHighlights), createAsync(Workflow.ShowDetailsAsync));
    }

    private void RaiseWhenChanged<T>(T oldValue, T newValue, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue)) OnPropertyChanged(name);
    }

    private void SetOperationBusy(UiOperationProgressState state, bool value, string name)
    {
        if (state.Current.IsBusy == value) return;
        OnPropertyChanging(name);
        state.SetBusy(value);
        OnPropertyChanged(name);
    }

    private void SetOperationProgress(UiOperationProgressState state, double value, string name)
    {
        if (EqualityComparer<double>.Default.Equals(state.Current.Progress, value)) return;
        OnPropertyChanging(name);
        state.SetProgress(value);
        OnPropertyChanged(name);
    }

    partial void OnDxfOverlapResultChanged(DxfOverlapResult? oldValue, DxfOverlapResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, nameof(DxfOverlapSummary));
        RaiseWhenChanged(oldValue.Issues, newValue.Issues, nameof(DxfOverlapIssues));
        RaiseWhenChanged(oldValue.Issues.Count, newValue.Issues.Count, nameof(DxfOverlapIssueCount));
        RaiseWhenChanged(oldValue.Issues.Count > 0, newValue.Issues.Count > 0, nameof(HasDxfOverlapIssues));
        RaiseWhenChanged(oldValue.HighlightedCadIds, newValue.HighlightedCadIds, nameof(DxfOverlapHighlightedCadIds));
    }

    // --- DXF Quality Check Properties ---

    /// <summary>
    /// Gets or sets the latest DXF overlap check result. The view model replaces the whole value.
    /// The properties below forward to it and raise their change notification when their own
    /// value changed.
    /// </summary>
    [ObservableProperty] private DxfOverlapResult _dxfOverlapResult = DxfOverlapResult.CreateNotRun();
    /// <summary>
    /// Gets the summary message for DXF overlap issues (e.g., "no overlaps detected").
    /// </summary>
    public string DxfOverlapSummary => DxfOverlapResult.Summary;

    /// <summary>
    /// Gets the observable collection of detailed DXF overlap issue messages.
    /// </summary>
    public ObservableCollection<string> DxfOverlapIssues => DxfOverlapResult.Issues;

    /// <summary>
    /// Gets the number of overlap issues currently loaded in details.
    /// </summary>
    public int DxfOverlapIssueCount => DxfOverlapResult.Issues.Count;

    /// <summary>
    /// Gets a value indicating whether any DXF overlap issues were found.
    /// </summary>
    public bool HasDxfOverlapIssues => DxfOverlapResult.Issues.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether DXF overlap checking is currently running.
    /// </summary>
    public bool IsCheckingDxfOverlap
    {
        get => _progress.Current.IsBusy;
        set => SetOperationBusy(_progress, value, nameof(IsCheckingDxfOverlap));
    }

    /// <summary>
    /// Gets or sets the current progress value (0-1) for DXF overlap checking.
    /// </summary>
    public double DxfOverlapProgress
    {
        get => _progress.Current.Progress;
        set => SetOperationProgress(_progress, value, nameof(DxfOverlapProgress));
    }

    /// <summary>
    /// Gets the CAD pad IDs highlighted by overlap checking.
    /// </summary>
    public ObservableCollection<int> DxfOverlapHighlightedCadIds => DxfOverlapResult.HighlightedCadIds;
}
