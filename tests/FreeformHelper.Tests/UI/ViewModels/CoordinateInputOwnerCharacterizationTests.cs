// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel;

namespace FreeformHelper.Tests;

public sealed class CoordinateInputOwnerCharacterizationTests
{
    [Fact]
    public void SameInputValues_KeepRenderAndEmitNoEvents()
    {
        var vm = CreateViewModel();
        var snapshot = vm.Snapshot;
        var events = new List<string>();
        vm.PropertyChanging += (_, e) => events.Add($"changing:{e.PropertyName}");
        vm.PropertyChanged += (_, e) => events.Add($"changed:{e.PropertyName}");
        vm.WorkspacePreferencesChanged += _ => events.Add("preferences");
        string[] inputs =
        [
            "SelectedAaBoundsOption", "MachineOriginX", "MachineOriginY", "MachineWidth", "MachineHeight",
            "PixelWidth", "PixelHeight", "CopperPillarDiameter", "ShowBistRectangle", "SelectedGuideBasisOption",
            "SelectedHorizontalGuideGenerationOption", "SelectedVerticalGuideGenerationOption",
            "HorizontalGuideCount", "VerticalGuideCount", "HorizontalGuidePitch", "VerticalGuidePitch",
            "HorizontalGuideInset", "VerticalGuideInset", "HorizontalGuidePositionListText", "VerticalGuidePositionListText",
        ];

        foreach (var name in inputs)
        {
            var property = typeof(CoordinatePlannerWorkspaceViewModel).GetProperty(name);
            Assert.NotNull(property);
            property.SetValue(vm, property.GetValue(vm));
        }

        Assert.Empty(events);
        Assert.Same(snapshot, vm.Snapshot);
    }

    [Theory]
    [InlineData("PixelWidth")]
    [InlineData("PixelHeight")]
    [InlineData("SelectedAaBoundsOption")]
    public void PreferenceInputs_PublishRenderThenPreferencesThenInputChanged(string name)
    {
        var vm = CreateViewModel();
        var property = typeof(CoordinatePlannerWorkspaceViewModel).GetProperty(name);
        Assert.NotNull(property);
        var oldInput = property.GetValue(vm);
        var oldSnapshot = vm.Snapshot;
        var order = new List<string>();
        vm.PropertyChanging += (sender, e) =>
        {
            if (e.PropertyName != name) return;
            Assert.Same(vm, sender);
            Assert.Equal(oldInput, property.GetValue(vm));
            Assert.Same(oldSnapshot, vm.Snapshot);
            order.Add("changing");
        };
        vm.PropertyChanged += (sender, e) =>
        {
            Assert.Same(vm, sender);
            Assert.NotSame(oldSnapshot, vm.Snapshot);
            if (e.PropertyName == "GuideSourceSummaryText") order.Add("render");
            if (e.PropertyName == name) order.Add("changed");
        };
        vm.WorkspacePreferencesChanged += preferences =>
        {
            Assert.NotSame(oldSnapshot, vm.Snapshot);
            Assert.Equal(vm.BuildWorkspacePreferences(), preferences);
            order.Add("preferences");
        };

        if (name == "PixelWidth") vm.PixelWidth = 2.5m;
        else if (name == "PixelHeight") vm.PixelHeight = -0.5m;
        else vm.SelectedAaBoundsOption = vm.AaBoundsOptions[1];

        Assert.Equal(["changing", "render", "preferences", "changed"], order);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GuideMode_PublishesRenderThenCountPitchExplicitFlags(bool horizontal)
    {
        var vm = CreateViewModel();
        var oldSnapshot = vm.Snapshot;
        var oldMode = vm.GuideGenerationOptions[0];
        var prefix = horizontal ? "Horizontal" : "Vertical";
        var input = $"Selected{prefix}GuideGenerationOption";
        var order = new List<string>();
        vm.PropertyChanging += (sender, e) =>
        {
            if (e.PropertyName != input) return;
            Assert.Same(vm, sender);
            Assert.Same(oldSnapshot, vm.Snapshot);
            Assert.Equal(oldMode, horizontal ? vm.SelectedHorizontalGuideGenerationOption : vm.SelectedVerticalGuideGenerationOption);
            order.Add("changing");
        };
        vm.PropertyChanged += (sender, e) =>
        {
            Assert.Same(vm, sender);
            Assert.NotSame(oldSnapshot, vm.Snapshot);
            if (e.PropertyName == "GuideSourceSummaryText" || e.PropertyName == input ||
                e.PropertyName?.StartsWith($"Is{prefix}Guide", StringComparison.Ordinal) == true)
            {
                order.Add(e.PropertyName!);
            }
        };

        if (horizontal) vm.SelectedHorizontalGuideGenerationOption = vm.GuideGenerationOptions[1];
        else vm.SelectedVerticalGuideGenerationOption = vm.GuideGenerationOptions[1];

        Assert.Equal(["changing", "GuideSourceSummaryText", $"Is{prefix}GuideCountMode",
            $"Is{prefix}GuidePitchMode", $"Is{prefix}GuideExplicitMode", input], order);
    }

    [Fact]
    public void Sync_AssignsSixInputsSequentiallyBeforeActivatingSafeCorners()
    {
        var vm = CreateViewModel();
        vm.MachineOriginX = 12m;
        vm.MachineOriginY = 8m;
        vm.MachineWidth = 40m;
        vm.MachineHeight = 20m;
        vm.PixelWidth = 200m;
        vm.PixelHeight = 100m;
        vm.CopperPillarDiameter = 10m;
        vm.HorizontalGuidePositionListText = "unchanged draft";
        var values = new List<(decimal X, decimal Y, decimal Width, decimal Height, decimal PixelsX, decimal PixelsY)>();
        var order = new List<string>();
        string[] inputs = ["MachineOriginX", "MachineOriginY", "MachineWidth", "MachineHeight", "PixelWidth", "PixelHeight"];
        vm.PropertyChanged += (_, e) =>
        {
            if (inputs.Contains(e.PropertyName, StringComparer.Ordinal))
            {
                order.Add(e.PropertyName!);
                values.Add((vm.MachineOriginX, vm.MachineOriginY, vm.MachineWidth, vm.MachineHeight, vm.PixelWidth, vm.PixelHeight));
            }
            if (e.PropertyName == "ShowCustomArray") order.Add(e.PropertyName);
        };

        vm.SyncFromWorkspaceCommand.Execute(null);

        Assert.Equal(inputs.Append("ShowCustomArray"), order);
        Assert.Equal([(0m, 8m, 40m, 20m, 200m, 100m), (0m, 0m, 40m, 20m, 200m, 100m),
            (0m, 0m, 100m, 20m, 200m, 100m), (0m, 0m, 100m, 50m, 200m, 100m),
            (0m, 0m, 100m, 50m, 1000m, 100m), (0m, 0m, 100m, 50m, 1000m, 500m)], values);
        Assert.Equal(5m, vm.CustomArrayTopLeftMachineX);
        Assert.Equal(5m, vm.CustomArrayTopLeftMachineY);
        Assert.Equal(95m, vm.CustomArrayBottomRightMachineX);
        Assert.Equal(45m, vm.CustomArrayBottomRightMachineY);
        Assert.Equal(10m, vm.CopperPillarDiameter);
        Assert.Equal("unchanged draft", vm.HorizontalGuidePositionListText);
        Assert.Equal("Coordinate planner synced from current workspace.", vm.StatusText);
    }

    [Fact]
    public void InvalidGuideTextAndInactiveInputs_RemainEditableWhileRequestsNormalize()
    {
        var vm = CreateViewModel();
        vm.HorizontalGuideCount = -0.5m;
        vm.HorizontalGuidePitch = -2m;
        vm.HorizontalGuideInset = -3m;
        vm.HorizontalGuidePositionListText = "bad; -5, 25,25 100";
        vm.SelectedHorizontalGuideGenerationOption = vm.GuideGenerationOptions.Single(static option =>
            option.Mode == CoordinatePlannerGuideGenerationMode.ExplicitPositions);
        vm.PixelWidth = 2.5m;
        vm.PixelHeight = -0.5m;

        Assert.Equal("bad; -5, 25,25 100", vm.HorizontalGuidePositionListText);
        Assert.Equal(-0.5m, vm.HorizontalGuideCount);
        Assert.Equal(-2m, vm.HorizontalGuidePitch);
        Assert.Equal(-3m, vm.HorizontalGuideInset);
        Assert.Equal([0d, 25d], vm.Snapshot.Lines.Where(static line =>
            line.Kind == CoordinatePlannerLineKind.HorizontalGuide).Select(static line => line.StartMachineY));
        Assert.Equal("Guides: H 4 explicit, inset 0 / V 5 count, inset 0", vm.GuideSummaryText);
        Assert.Equal(2.5m, vm.PixelWidth);
        Assert.Equal(-0.5m, vm.PixelHeight);
        Assert.Equal(3, vm.BuildWorkspacePreferences().PixelWidth);
        Assert.Equal(1, vm.BuildWorkspacePreferences().PixelHeight);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel() => new(
        new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceUseCase(),
        new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
            [TestGeometryFactory.CreateCadPad(1, "SIG", 2d, 1d, 8d, 4d)], ["SIG"],
            SourceRevision: 0, DefaultMachineWidth: 100d, DefaultMachineHeight: 50d,
            DefaultPixelWidth: 1000, DefaultPixelHeight: 500));
}
