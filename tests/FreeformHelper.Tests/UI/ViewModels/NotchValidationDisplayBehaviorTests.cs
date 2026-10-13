using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchValidationDisplayBehaviorTests
{
    private static readonly string[] _displayNames =
    [
        nameof(FreeformHelperViewModel.NotchValidationSummaryText),
        nameof(FreeformHelperViewModel.NotchValidationItems),
        nameof(FreeformHelperViewModel.HasNotchValidationItems),
        nameof(FreeformHelperViewModel.IsNotchValidationItemsEmpty),
        nameof(FreeformHelperViewModel.NotchValidationDirectItems),
        nameof(FreeformHelperViewModel.HasNotchValidationDirectItems),
        nameof(FreeformHelperViewModel.IsNotchValidationDirectEmpty),
        nameof(FreeformHelperViewModel.NotchValidationIncomingItems),
        nameof(FreeformHelperViewModel.HasNotchValidationIncomingItems),
        nameof(FreeformHelperViewModel.IsNotchValidationIncomingEmpty),
        nameof(FreeformHelperViewModel.NotchValidationOutgoingItems),
        nameof(FreeformHelperViewModel.HasNotchValidationOutgoingItems),
        nameof(FreeformHelperViewModel.IsNotchValidationOutgoingEmpty),
        nameof(FreeformHelperViewModel.NotchValidationDirectCount),
        nameof(FreeformHelperViewModel.NotchValidationIncomingCount),
        nameof(FreeformHelperViewModel.NotchValidationOutgoingCount),
    ];

    [Fact]
    public void NewViewModel_BeforeAnyAnalyze_ShowsTheAnalyzeHintAndNoRows()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal("Validation: select REG, then run Analyze.", vm.NotchValidationSummaryText);
        Assert.Empty(vm.NotchValidationItems);
        Assert.Empty(vm.NotchValidationDirectItems);
        Assert.Empty(vm.NotchValidationIncomingItems);
        Assert.Empty(vm.NotchValidationOutgoingItems);
        Assert.False(vm.HasNotchValidationItems);
        Assert.True(vm.IsNotchValidationItemsEmpty);
        Assert.Equal(0, vm.NotchValidationDirectCount);
    }

    [Fact]
    public void NotchValidationSummaryText_NewValue_RaisesOnlyTheSummary()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationSummaryText = "Validation: custom.";

        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationSummaryText)], changes);
        Assert.Equal("Validation: custom.", vm.NotchValidationSummaryText);
    }

    [Fact]
    public void NotchValidationSummaryText_SameValue_RaisesNothing()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationSummaryText = "Validation: select REG, then run Analyze.";

        Assert.Empty(changes);
    }

    [Fact]
    public void NotchValidationItems_NewCollection_RaisesTheFlagsBeforeTheCollection()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);
        var items = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { CreateItem("ALL") };

        vm.NotchValidationItems = items;

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.HasNotchValidationItems),
                nameof(FreeformHelperViewModel.IsNotchValidationItemsEmpty),
                nameof(FreeformHelperViewModel.NotchValidationItems),
            ],
            changes);
        Assert.Same(items, vm.NotchValidationItems);
    }

    [Fact]
    public void NotchValidationItems_SameCollection_RaisesNothing()
    {
        var vm = new FreeformHelperViewModel();
        var items = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { CreateItem("ALL") };
        vm.NotchValidationItems = items;
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationItems = items;

        Assert.Empty(changes);
    }

    [Fact]
    public void NotchValidationDirectItems_NewCollection_RaisesTheFlagsBeforeTheCollection()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationDirectItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("DIRECT"),
        };

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.HasNotchValidationDirectItems),
                nameof(FreeformHelperViewModel.IsNotchValidationDirectEmpty),
                nameof(FreeformHelperViewModel.NotchValidationDirectItems),
            ],
            changes);
        Assert.Equal(1, vm.NotchValidationDirectCount);
    }

    [Fact]
    public void NotchValidationIncomingItems_NewCollection_RaisesTheFlagsBeforeTheCollection()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationIncomingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("IN"),
        };

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.HasNotchValidationIncomingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationIncomingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationIncomingItems),
            ],
            changes);
        Assert.Equal(1, vm.NotchValidationIncomingCount);
    }

    [Fact]
    public void NotchValidationOutgoingItems_NewCollection_RaisesTheFlagsBeforeTheCollection()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationOutgoingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("OUT"),
        };

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.HasNotchValidationOutgoingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationOutgoingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationOutgoingItems),
            ],
            changes);
        Assert.Equal(1, vm.NotchValidationOutgoingCount);
    }

    [Fact]
    public void NotchValidationItems_NewCollection_RaisesPropertyChangingForTheCollectionOnly()
    {
        var vm = new FreeformHelperViewModel();
        var changing = new List<string?>();
        vm.PropertyChanging += (_, e) => changing.Add(e.PropertyName);

        vm.NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("ALL"),
        };

        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationItems)], changing);
    }

    [Fact]
    public void NotchValidationDirectItems_SameCollection_RaisesNothing()
    {
        var vm = new FreeformHelperViewModel();
        var items = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { CreateItem("DIRECT") };
        vm.NotchValidationDirectItems = items;
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationDirectItems = items;

        Assert.Empty(changes);
    }

    [Fact]
    public void NotchValidationDirectItems_NewCollection_RaisesPropertyChangingForTheCollectionOnly()
    {
        var vm = new FreeformHelperViewModel();
        var changing = new List<string?>();
        vm.PropertyChanging += (_, e) => changing.Add(e.PropertyName);

        vm.NotchValidationDirectItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("DIRECT"),
        };

        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationDirectItems)], changing);
    }

    [Fact]
    public void NotchValidationIncomingItems_SameCollection_RaisesNothing()
    {
        var vm = new FreeformHelperViewModel();
        var items = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { CreateItem("IN") };
        vm.NotchValidationIncomingItems = items;
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationIncomingItems = items;

        Assert.Empty(changes);
    }

    [Fact]
    public void NotchValidationIncomingItems_NewCollection_RaisesPropertyChangingForTheCollectionOnly()
    {
        var vm = new FreeformHelperViewModel();
        var changing = new List<string?>();
        vm.PropertyChanging += (_, e) => changing.Add(e.PropertyName);

        vm.NotchValidationIncomingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("IN"),
        };

        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationIncomingItems)], changing);
    }

    [Fact]
    public void NotchValidationOutgoingItems_SameCollection_RaisesNothing()
    {
        var vm = new FreeformHelperViewModel();
        var items = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { CreateItem("OUT") };
        vm.NotchValidationOutgoingItems = items;
        var changes = RecordNotifications(vm, _displayNames);

        vm.NotchValidationOutgoingItems = items;

        Assert.Empty(changes);
    }

    [Fact]
    public void NotchValidationOutgoingItems_NewCollection_RaisesPropertyChangingForTheCollectionOnly()
    {
        var vm = new FreeformHelperViewModel();
        var changing = new List<string?>();
        vm.PropertyChanging += (_, e) => changing.Add(e.PropertyName);

        vm.NotchValidationOutgoingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            CreateItem("OUT"),
        };

        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationOutgoingItems)], changing);
    }

    [Fact]
    public void ClearNotchValidationCommand_AfterRows_RestoresTheHintAndEmptiesEveryCollection()
    {
        var vm = CreateViewModelWithRows();

        vm.ClearNotchValidationCommand.Execute(null);

        Assert.Equal("Validation: select REG, then run Analyze.", vm.NotchValidationSummaryText);
        Assert.Empty(vm.NotchValidationItems);
        Assert.Empty(vm.NotchValidationDirectItems);
        Assert.Empty(vm.NotchValidationIncomingItems);
        Assert.Empty(vm.NotchValidationOutgoingItems);
    }

    [Fact]
    public void ClearNotchValidationCommand_AfterRows_RaisesTheSummaryThenEachGroupThenTheCounts()
    {
        var vm = CreateViewModelWithRows();
        vm.NotchValidationSummaryText = "REG 1 | rows=3";
        var changes = RecordNotifications(vm, _displayNames);

        vm.ClearNotchValidationCommand.Execute(null);

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.NotchValidationSummaryText),
                nameof(FreeformHelperViewModel.HasNotchValidationItems),
                nameof(FreeformHelperViewModel.IsNotchValidationItemsEmpty),
                nameof(FreeformHelperViewModel.NotchValidationItems),
                nameof(FreeformHelperViewModel.HasNotchValidationDirectItems),
                nameof(FreeformHelperViewModel.IsNotchValidationDirectEmpty),
                nameof(FreeformHelperViewModel.NotchValidationDirectItems),
                nameof(FreeformHelperViewModel.HasNotchValidationIncomingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationIncomingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationIncomingItems),
                nameof(FreeformHelperViewModel.HasNotchValidationOutgoingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationOutgoingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationOutgoingItems),
                nameof(FreeformHelperViewModel.NotchValidationDirectCount),
                nameof(FreeformHelperViewModel.NotchValidationIncomingCount),
                nameof(FreeformHelperViewModel.NotchValidationOutgoingCount),
            ],
            changes);
    }

    [Fact]
    public void UseSelectedRegularForNotchValidationCommand_NothingSelected_ShowsTheHintAndKeepsTheRows()
    {
        var vm = CreateViewModelWithRows();
        var items = vm.NotchValidationItems;
        var changes = RecordNotifications(vm, _displayNames);

        vm.UseSelectedRegularForNotchValidationCommand.Execute(null);

        Assert.Equal("Validation: select one REG first.", vm.NotchValidationSummaryText);
        Assert.Same(items, vm.NotchValidationItems);
        Assert.Equal([nameof(FreeformHelperViewModel.NotchValidationSummaryText)], changes);
    }

    [Fact]
    public void AnalyzeNotchValidationCommand_GridNotBuilt_ShowsTheFailureAndClearsTheRows()
    {
        var vm = CreateViewModelWithRows();

        vm.AnalyzeNotchValidationCommand.Execute(null);

        Assert.Equal("Validation: build grid first.", vm.NotchValidationSummaryText);
        Assert.Empty(vm.NotchValidationItems);
        Assert.Empty(vm.NotchValidationDirectItems);
        Assert.Empty(vm.NotchValidationIncomingItems);
        Assert.Empty(vm.NotchValidationOutgoingItems);
    }

    [Fact]
    public void AnalyzeNotchValidationCommand_GridNotBuilt_RaisesTheGroupsThenTheCountsThenTheSummary()
    {
        var vm = CreateViewModelWithRows();
        var changes = RecordNotifications(vm, _displayNames);

        vm.AnalyzeNotchValidationCommand.Execute(null);

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.HasNotchValidationItems),
                nameof(FreeformHelperViewModel.IsNotchValidationItemsEmpty),
                nameof(FreeformHelperViewModel.NotchValidationItems),
                nameof(FreeformHelperViewModel.HasNotchValidationDirectItems),
                nameof(FreeformHelperViewModel.IsNotchValidationDirectEmpty),
                nameof(FreeformHelperViewModel.NotchValidationDirectItems),
                nameof(FreeformHelperViewModel.HasNotchValidationIncomingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationIncomingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationIncomingItems),
                nameof(FreeformHelperViewModel.HasNotchValidationOutgoingItems),
                nameof(FreeformHelperViewModel.IsNotchValidationOutgoingEmpty),
                nameof(FreeformHelperViewModel.NotchValidationOutgoingItems),
                nameof(FreeformHelperViewModel.NotchValidationDirectCount),
                nameof(FreeformHelperViewModel.NotchValidationIncomingCount),
                nameof(FreeformHelperViewModel.NotchValidationOutgoingCount),
                nameof(FreeformHelperViewModel.NotchValidationSummaryText),
            ],
            changes);
    }

    [AvaloniaFact]
    public async Task AnalyzeNotchValidationCommand_RegWithAllGroups_ShowsTheRowSummaryAfterTheCollections()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            var regularPadId = vm.RegularPads[0].RegularPadId;
            vm.NotchValidationRegularPadId = regularPadId;
            var changes = RecordNotifications(vm, _displayNames);

            vm.AnalyzeNotchValidationCommand.Execute(null);

            Assert.Equal(
                $"REG {regularPadId} | IC1/diff{vm.NotchValidationItems[0].SourceDiffIndex} | rows=3 (direct=1, in=1, out=1)",
                vm.NotchValidationSummaryText);
            Assert.Equal(nameof(FreeformHelperViewModel.NotchValidationSummaryText), changes[^1]);
            Assert.Equal(3, vm.NotchValidationItems.Count);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    private static FreeformHelperViewModel CreateViewModelWithRows()
    {
        var vm = new FreeformHelperViewModel();
        var direct = CreateItem("DIRECT", isDirect: true);
        var incoming = CreateItem("IN", isIncoming: true);
        var outgoing = CreateItem("OUT", isOutgoing: true);
        vm.NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
        {
            direct,
            incoming,
            outgoing,
        };
        vm.NotchValidationDirectItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { direct };
        vm.NotchValidationIncomingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { incoming };
        vm.NotchValidationOutgoingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { outgoing };
        return vm;
    }

    private static FreeformHelperViewModel.NotchValidationDisplayItem CreateItem(
        string kindText,
        bool isDirect = false,
        bool isIncoming = false,
        bool isOutgoing = false)
    {
        return new FreeformHelperViewModel.NotchValidationDisplayItem(
            KindText: kindText,
            RowText: "#1",
            VersionText: "2.2",
            IcText: "IC 1",
            SourceText: "REG 1 / diff 10",
            TargetText: "REG 2 / diff 20",
            RatioText: "100%",
            AreaText: "1.0",
            ValuesText: "100,0,0",
            CommentText: string.Empty,
            NoteText: string.Empty,
            SourceRegularPadId: 1,
            TargetRegularPadId: 2,
            CadPadId: 1,
            IcIndex: 0,
            SourceDiffIndex: 10,
            TargetDiffIndex: 20,
            IsDirect: isDirect,
            IsIncoming: isIncoming,
            IsOutgoing: isOutgoing);
    }
}
