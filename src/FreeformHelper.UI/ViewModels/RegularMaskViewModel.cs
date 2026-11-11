// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Contracts;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the mask input, presentation and commands for one workspace. UI thread only.</summary>
public sealed partial class RegularMaskViewModel : ObservableObject, IRegularMaskPresentation
{
    private const string NotLoadedSummary =
        "Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 geometry seed and Simulation.";

    internal RegularMaskViewModel(Func<IRegularMaskPresentation, IRegularMaskWorkflow> createWorkflow)
    {
        Workflow = createWorkflow(this);
    }

    internal IRegularMaskWorkflow Workflow { get; }

    /// <summary>Gets or sets whether the imported mask constrains Step 4 and Simulation.</summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>Gets or sets the last mask presentation, including rejected import diagnostics.</summary>
    [ObservableProperty] private string _summary = "Regular visibility mask: not loaded. Import Regular Visibility Mask (SeeRegular.csv) in Step 1 to constrain Step 4 and Simulation.";

    /// <summary>Gets or sets the picker attached by the view and cleared when it detaches.</summary>
    public Func<Task<string?>>? PickOpenRegularVisibilityMaskPathAsync { get; set; }

    private (IAsyncRelayCommand Import, IRelayCommand Clear)? _commands;

    /// <summary>Gets the command to import a regular visibility mask.</summary>
    public IAsyncRelayCommand ImportRegularVisibilityMaskCommand =>
        _commands?.Import ?? throw new InvalidOperationException("Mask commands have not been initialized.");

    /// <summary>Gets the command to clear the imported regular visibility mask.</summary>
    public IRelayCommand ClearRegularVisibilityMaskCommand =>
        _commands?.Clear ?? throw new InvalidOperationException("Mask commands have not been initialized.");

    // Register once at the existing position in the project editing command registry.
    internal void InitializeCommands(
        Func<Func<Task>, IAsyncRelayCommand> createAsyncCommand,
        Func<Action, IRelayCommand> createCommand)
    {
        _commands = (
            createAsyncCommand(() => Workflow.ImportAsync(PickOpenRegularVisibilityMaskPathAsync)),
            createCommand(Workflow.Clear));
    }

    void IRegularMaskPresentation.UpdateSummary(RegularVisibilityMaskResult result, RegularGrid? grid, bool isEnabled)
        => Summary = BuildSummary(result, grid, isEnabled);

    partial void OnIsEnabledChanged(bool value) => Workflow.OnEnabledChanged(value);

    private static string BuildSummary(RegularVisibilityMaskResult result, RegularGrid? grid, bool isEnabled)
    {
        return result.Status switch
        {
            RegularVisibilityMaskStatus.Loaded when isEnabled =>
                $"Regular visibility mask enabled: {Path.GetFileName(result.SourcePath)} loaded, active={result.ActiveRegularCount}, inactive={result.InactiveRegularCount}, frames={result.FrameCount}. Step 4 geometry seed only uses active regulars.",
            RegularVisibilityMaskStatus.Loaded =>
                $"Regular visibility mask loaded but disabled: {Path.GetFileName(result.SourcePath)}. Step 4 uses pure geometry seed.",
            RegularVisibilityMaskStatus.Incompatible =>
                $"Regular visibility mask: {Path.GetFileName(result.SourcePath)} incompatible with current grid {grid?.Rows ?? 0}x{grid?.Cols ?? 0}. Ignored. {result.Diagnostic}",
            RegularVisibilityMaskStatus.Failed =>
                $"Regular visibility mask: failed to load {Path.GetFileName(result.SourcePath)}. Ignored. {result.Diagnostic}",
            _ => NotLoadedSummary,
        };
    }
}
