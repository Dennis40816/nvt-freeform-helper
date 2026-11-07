using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CoordinatePlannerHostViewModel : ObservableObject
{
    private readonly string _placeholderTitle = "No coordinate plan yet";
    private readonly string _placeholderSubtitle = "Build from the current workspace, then inspect AA corners, guide lines, BIST rectangle, and copper-pillar-safe machine coordinates on the same canvas.";
    private readonly string _staleWorkspaceBannerText = "Refresh rebuilds the plan from the current workspace and clears custom points and paths.";

    [ObservableProperty]
    private CoordinatePlannerWorkspaceViewModel? _currentWorkspace;

    [ObservableProperty]
    private int _sourceRevision;

    public IAsyncRelayCommand OpenCoordinatePlannerCommand { get; }

    public Func<Task>? RequestBuildWorkspaceAsync { get; set; }
    public Func<Task>? RequestPrewarmWorkspaceAsync { get; set; }

    public CoordinatePlannerHostViewModel()
    {
        OpenCoordinatePlannerCommand = new AsyncRelayCommand(OpenCoordinatePlannerAsync);
    }

    public string PlaceholderTitle => _placeholderTitle;

    public string PlaceholderSubtitle => _placeholderSubtitle;

    public string StaleWorkspaceBannerText => _staleWorkspaceBannerText;

    public bool HasWorkspace => CurrentWorkspace is not null;

    public bool HasNoWorkspace => !HasWorkspace;

    public bool IsWorkspaceStale => CurrentWorkspace is not null && CurrentWorkspace.SourceRevision != SourceRevision;

    public bool CanOpenCoordinatePlanner => RequestBuildWorkspaceAsync is not null;

    public string SourceStatusText => HasNoWorkspace ? "No plan built" : IsWorkspaceStale ? "Source changed" : "Source up to date";

    public string SourceDimensionsTooltipText => CurrentWorkspace?.WorkspaceSummaryText ?? "Build a plan from the current Freeform workspace.";

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

        await RequestBuildWorkspaceAsync();
    }

    public async Task PrewarmWorkspaceAsync()
    {
        if (RequestPrewarmWorkspaceAsync is null)
        {
            return;
        }

        if (CurrentWorkspace is not null)
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

    partial void OnCurrentWorkspaceChanged(CoordinatePlannerWorkspaceViewModel? value)
    {
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(HasNoWorkspace));
        OnPropertyChanged(nameof(IsWorkspaceStale));
        OnPropertyChanged(nameof(SourceStatusText));
        OnPropertyChanged(nameof(SourceDimensionsTooltipText));
    }

    partial void OnSourceRevisionChanged(int value)
    {
        OnPropertyChanged(nameof(IsWorkspaceStale));
        OnPropertyChanged(nameof(SourceStatusText));
    }

    private async Task OpenCoordinatePlannerAsync()
    {
        if (RequestBuildWorkspaceAsync is null)
        {
            return;
        }

        await RequestBuildWorkspaceAsync();
    }
}
