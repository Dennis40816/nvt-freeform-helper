using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class SimulationHostViewModel : ObservableObject
{
    private readonly string _placeholderTitle = "Simulation";
    private readonly string _placeholderSubtitle = "Build from the current Freeform Helper workspace, then simulate regular-pad values directly on the AA canvas.";
    private readonly string _staleWorkspaceBannerText = "Freeform Helper workspace changed. Refresh simulation to use the latest project/grid state.";
    private const string DefaultBuildProgressText = "Preparing simulation workspace...";
    private int _buildProgressScopeCount;

    [ObservableProperty]
    private SimulationWorkspaceViewModel? _currentWorkspace;
    [ObservableProperty]
    private int _sourceRevision;
    [ObservableProperty]
    private string _buildFailureText = string.Empty;
    [ObservableProperty]
    private bool _isBuildingWorkspace;
    [ObservableProperty]
    private string _buildProgressText = string.Empty;

    public IAsyncRelayCommand OpenSimulationCommand { get; }

    public Func<Task>? RequestBuildWorkspaceAsync { get; set; }
    public Func<Task>? RequestPrewarmWorkspaceAsync { get; set; }

    public SimulationHostViewModel()
    {
        OpenSimulationCommand = new AsyncRelayCommand(OpenSimulationAsync);
    }

    public string PlaceholderTitle => _placeholderTitle;

    public string PlaceholderSubtitle => _placeholderSubtitle;

    public string StaleWorkspaceBannerText => _staleWorkspaceBannerText;

    public bool HasBuildFailure => !string.IsNullOrWhiteSpace(BuildFailureText);

    public bool HasWorkspace => CurrentWorkspace is not null;

    public bool HasNoWorkspace => !HasWorkspace;

    public bool HasWorkspaceFailureOverlay => HasWorkspace && HasBuildFailure;

    public bool HasBuildProgressOverlay => IsBuildingWorkspace;

    public bool HasBuildProgressText => !string.IsNullOrWhiteSpace(BuildProgressText);

    public bool IsWorkspaceStale => CurrentWorkspace is not null && CurrentWorkspace.SourceRevision != SourceRevision;

    public bool CanAutoRefreshCurrentWorkspace => CurrentWorkspace?.CanAutoRefreshFromSourceChange ?? true;

    public bool CanOpenSimulation => RequestBuildWorkspaceAsync is not null && !IsBuildingWorkspace;

    public async Task EnsureWorkspaceAsync()
    {
        if (RequestBuildWorkspaceAsync is null)
        {
            return;
        }

        if (CurrentWorkspace is not null && !IsWorkspaceStale)
        {
            return;
        }

        await RunWorkspaceBuildAsync(RequestBuildWorkspaceAsync);
    }

    public async Task PrewarmWorkspaceAsync()
    {
        if (RequestPrewarmWorkspaceAsync is null)
        {
            return;
        }

        if (CurrentWorkspace is not null &&
            (!IsWorkspaceStale || !CanAutoRefreshCurrentWorkspace))
        {
            return;
        }

        await RequestPrewarmWorkspaceAsync();
    }

    public void UpdateSourceRevision(int revision)
    {
        if (revision < 0 || SourceRevision == revision)
        {
            return;
        }

        SourceRevision = revision;
    }

    partial void OnCurrentWorkspaceChanged(SimulationWorkspaceViewModel? value)
    {
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(HasNoWorkspace));
        OnPropertyChanged(nameof(HasWorkspaceFailureOverlay));
        OnPropertyChanged(nameof(IsWorkspaceStale));
        OnPropertyChanged(nameof(CanAutoRefreshCurrentWorkspace));
        if (value is not null)
        {
            BuildFailureText = string.Empty;
        }
    }

    partial void OnSourceRevisionChanged(int value)
    {
        OnPropertyChanged(nameof(IsWorkspaceStale));
        OnPropertyChanged(nameof(CanAutoRefreshCurrentWorkspace));
    }

    partial void OnBuildFailureTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasBuildFailure));
        OnPropertyChanged(nameof(HasWorkspaceFailureOverlay));
    }

    public void SetBuildFailure(string? text)
    {
        BuildFailureText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    public void ReportBuildProgress(string? text)
    {
        var normalized = string.IsNullOrWhiteSpace(text)
            ? DefaultBuildProgressText
            : text.Trim();
        if (string.Equals(BuildProgressText, normalized, StringComparison.Ordinal))
        {
            return;
        }

        BuildProgressText = normalized;
    }

    public void BeginBuildProgress(string? text = null)
    {
        ReportBuildProgress(text);
        _buildProgressScopeCount++;
        IsBuildingWorkspace = true;
    }

    public void EndBuildProgress()
    {
        if (_buildProgressScopeCount > 0)
        {
            _buildProgressScopeCount--;
        }

        IsBuildingWorkspace = _buildProgressScopeCount > 0;
        if (!IsBuildingWorkspace)
        {
            BuildProgressText = string.Empty;
        }
    }

    partial void OnIsBuildingWorkspaceChanged(bool value)
    {
        OnPropertyChanged(nameof(HasBuildProgressOverlay));
        OnPropertyChanged(nameof(CanOpenSimulation));
    }

    partial void OnBuildProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasBuildProgressText));
    }

    private async Task OpenSimulationAsync()
    {
        if (RequestBuildWorkspaceAsync is null)
        {
            return;
        }

        await RunWorkspaceBuildAsync(RequestBuildWorkspaceAsync);
    }

    private async Task RunWorkspaceBuildAsync(Func<Task> requestAsync)
    {
        ArgumentNullException.ThrowIfNull(requestAsync);

        BeginBuildProgress();
        try
        {
            await requestAsync();
        }
        finally
        {
            EndBuildProgress();
        }
    }
}
