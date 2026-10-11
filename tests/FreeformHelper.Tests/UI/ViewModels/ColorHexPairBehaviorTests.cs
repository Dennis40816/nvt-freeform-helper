// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ColorHexPairBehaviorTests
{
    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_DifferentColorWithLowercaseText_NormalizesAndRaisesNestedChanges(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "#aabbcc");

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, HexName(colorName), colorName, HexName(colorName));
        Assert.Equal<string?>([nameof(vm.CanUndo), HexName(colorName), colorName, HexName(colorName)], changes);
        Assert.True(vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_DifferentColorWithCanonicalText_RaisesColorThenHex(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "#AABBCC");

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, colorName, HexName(colorName));
        Assert.Equal<string?>([nameof(vm.CanUndo), colorName, HexName(colorName)], changes);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "#AABBCC", "#aabbcc")]
    [InlineData("CadLineColor", "#AABBCC", "#abc")]
    [InlineData("CadLineColor", "#112233", "#FF112233")]
    [InlineData("RegularLineColor", "#AABBCC", "#aabbcc")]
    [InlineData("RegularLineColor", "#AABBCC", "#abc")]
    [InlineData("RegularLineColor", "#112233", "#FF112233")]
    [InlineData("RegularSelectedColor", "#AABBCC", "#aabbcc")]
    [InlineData("RegularSelectedColor", "#AABBCC", "#abc")]
    [InlineData("RegularSelectedColor", "#112233", "#FF112233")]
    public void HexSetter_SameColorWithDifferentSpelling_PreservesTextAndRaisesHexOnce(
        string colorName, string originalHex, string typedHex)
    {
        var vm = new FreeformHelperViewModel();
        var color = Color.Parse(originalHex);
        SetColor(vm, colorName, color);
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, typedHex);

        AssertPair(vm, colorName, typedHex, color);
        AssertPairChanges(changes, HexName(colorName));
        Assert.Equal<string?>([nameof(vm.CanUndo), HexName(colorName)], changes);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "#12")]
    [InlineData("CadLineColor", "#GGGGGG")]
    [InlineData("CadLineColor", "reddish")]
    [InlineData("CadLineColor", "")]
    [InlineData("RegularLineColor", "#12")]
    [InlineData("RegularLineColor", "#GGGGGG")]
    [InlineData("RegularLineColor", "reddish")]
    [InlineData("RegularLineColor", "")]
    [InlineData("RegularSelectedColor", "#12")]
    [InlineData("RegularSelectedColor", "#GGGGGG")]
    [InlineData("RegularSelectedColor", "reddish")]
    [InlineData("RegularSelectedColor", "")]
    public void HexSetter_InvalidText_PreservesTextAndLastValidColor(string colorName, string typedHex)
    {
        var vm = new FreeformHelperViewModel();
        var color = Color.Parse("#112233");
        SetColor(vm, colorName, color);
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, typedHex);

        Assert.False(Color.TryParse(typedHex, out _));
        AssertPair(vm, colorName, typedHex, color);
        AssertPairChanges(changes, HexName(colorName));
        Assert.Equal<string?>([nameof(vm.CanUndo), HexName(colorName)], changes);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_RecognizedColorName_NormalizesAcceptedName(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "red");

        AssertPair(vm, colorName, "#FF0000", Colors.Red);
        AssertPairChanges(changes, HexName(colorName), colorName, HexName(colorName));
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_UnchangedText_RaisesNoChangesOrUndo(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "#112233");

        AssertPair(vm, colorName, "#112233", Color.Parse("#112233"));
        Assert.Empty(changes);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void ColorSetter_DifferentColor_NormalizesHexWithoutRecordingUndo(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetColor(vm, colorName, Color.Parse("#AABBCC"));

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        Assert.Equal<string?>([HexName(colorName), colorName], changes);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void ColorSetter_ExistingUndoEntry_DoesNotAddAnotherEntry(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        var originalShowCad = vm.ShowCad;
        vm.ShowCad = !originalShowCad;
        Assert.True(vm.CanUndo);
        var changes = ObserveChanges(vm);

        SetColor(vm, colorName, Color.Parse("#AABBCC"));

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        Assert.Equal<string?>([HexName(colorName), colorName], changes);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.False(vm.CanUndo);
        Assert.Equal(originalShowCad, vm.ShowCad);
        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, HexName(colorName), colorName);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void ColorSetter_SameColorAfterInvalidText_PreservesInvalidTextWithoutNotification(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        var color = Color.Parse("#112233");
        SetColor(vm, colorName, color);
        SetHex(vm, colorName, "#GGGGGG");
        var changes = ObserveChanges(vm);

        SetColor(vm, colorName, color);

        AssertPair(vm, colorName, "#GGGGGG", color);
        Assert.Empty(changes);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void UndoCommand_ValidHexEdit_RestoresPreviousTextAndColor(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        SetHex(vm, colorName, "#aabbcc");
        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        Assert.True(vm.CanUndo);
        var changes = ObserveChanges(vm);

        vm.UndoCommand.Execute(null);

        AssertPair(vm, colorName, "#112233", Color.Parse("#112233"));
        AssertPairChanges(changes, colorName, HexName(colorName));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void UndoCommand_InvalidHexEdit_RestoresPreviousTextWithoutChangingColor(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        SetHex(vm, colorName, "#GGGGGG");
        AssertPair(vm, colorName, "#GGGGGG", Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        vm.UndoCommand.Execute(null);

        AssertPair(vm, colorName, "#112233", Color.Parse("#112233"));
        AssertPairChanges(changes, HexName(colorName));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void UndoCommand_ValidEditFollowingInvalidText_RestoresInvalidTextAndKeepsNewColor(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        SetHex(vm, colorName, "#GGGGGG");
        SetHex(vm, colorName, "#aabbcc");
        var changes = ObserveChanges(vm);

        vm.UndoCommand.Execute(null);

        AssertPair(vm, colorName, "#GGGGGG", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, HexName(colorName));
        Assert.True(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void UndoCommand_PreviousShortHexWithDifferentColor_NormalizesRestoredText(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#AABBCC"));
        SetHex(vm, colorName, "#abc");
        SetHex(vm, colorName, "#112233");
        var changes = ObserveChanges(vm);

        vm.UndoCommand.Execute(null);

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, HexName(colorName), colorName, HexName(colorName));
        Assert.True(vm.CanUndo);
    }

    // There is no redo API in this ViewModel; reapplying an edit uses the public Hex setter.
    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_ValidEditReappliedAfterUndo_NormalizesAgain(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        SetHex(vm, colorName, "#aabbcc");
        vm.UndoCommand.Execute(null);
        AssertPair(vm, colorName, "#112233", Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "#aabbcc");

        AssertPair(vm, colorName, "#AABBCC", Color.Parse("#AABBCC"));
        AssertPairChanges(changes, HexName(colorName), colorName, HexName(colorName));
        Assert.True(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public void HexSetter_InvalidEditReappliedAfterUndo_PreservesTextAgain(string colorName)
    {
        var vm = new FreeformHelperViewModel();
        SetColor(vm, colorName, Color.Parse("#112233"));
        SetHex(vm, colorName, "#GGGGGG");
        vm.UndoCommand.Execute(null);
        AssertPair(vm, colorName, "#112233", Color.Parse("#112233"));
        var changes = ObserveChanges(vm);

        SetHex(vm, colorName, "#GGGGGG");

        AssertPair(vm, colorName, "#GGGGGG", Color.Parse("#112233"));
        AssertPairChanges(changes, HexName(colorName));
        Assert.True(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public async Task SaveProjectAsync_ValidHex_RoundTripsNormalizedTextAndColor(string colorName)
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = new FreeformHelperViewModel { PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            SetColor(vm, colorName, Color.Parse("#112233"));
            SetHex(vm, colorName, "#aabbcc");
            var loaded = new FreeformHelperViewModel
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            SetColor(loaded, colorName, Color.Parse("#445566"));
            var changes = ObserveChanges(loaded);

            Assert.True(await vm.SaveProjectAsync());
            var saved = JsonProjectStore.Load(projectPath);
            await loaded.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal("#AABBCC", GetSnapshotHex(saved.UiSnapshot.View, colorName));
            AssertPair(loaded, colorName, "#AABBCC", Color.Parse("#AABBCC"));
            AssertPairChanges(changes, colorName, HexName(colorName));
            await WaitForGridRebuildAsync(loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "#AABBCC", "#aabbcc")]
    [InlineData("CadLineColor", "#AABBCC", "#abc")]
    [InlineData("CadLineColor", "#112233", "#FF112233")]
    [InlineData("RegularLineColor", "#AABBCC", "#aabbcc")]
    [InlineData("RegularLineColor", "#AABBCC", "#abc")]
    [InlineData("RegularLineColor", "#112233", "#FF112233")]
    [InlineData("RegularSelectedColor", "#AABBCC", "#aabbcc")]
    [InlineData("RegularSelectedColor", "#AABBCC", "#abc")]
    [InlineData("RegularSelectedColor", "#112233", "#FF112233")]
    public async Task SaveProjectAsync_SameColorSpelling_RoundTripsTypedTextForSameReceivingColor(
        string colorName, string originalHex, string typedHex)
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = new FreeformHelperViewModel { PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            var color = Color.Parse(originalHex);
            SetColor(vm, colorName, color);
            SetHex(vm, colorName, typedHex);
            var loaded = new FreeformHelperViewModel
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            SetColor(loaded, colorName, color);
            var changes = ObserveChanges(loaded);

            Assert.True(await vm.SaveProjectAsync());
            var saved = JsonProjectStore.Load(projectPath);
            await loaded.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(typedHex, GetSnapshotHex(saved.UiSnapshot.View, colorName));
            AssertPair(loaded, colorName, typedHex, color);
            AssertPairChanges(changes, HexName(colorName));
            await WaitForGridRebuildAsync(loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor")]
    [InlineData("RegularLineColor")]
    [InlineData("RegularSelectedColor")]
    public async Task SaveProjectAsync_InvalidHex_RoundTripsTextAndKeepsReceivingColor(string colorName)
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = new FreeformHelperViewModel { PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            SetColor(vm, colorName, Color.Parse("#112233"));
            SetHex(vm, colorName, "#GGGGGG");
            var loaded = new FreeformHelperViewModel
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            SetColor(loaded, colorName, Color.Parse("#445566"));
            var changes = ObserveChanges(loaded);

            Assert.True(await vm.SaveProjectAsync());
            var saved = JsonProjectStore.Load(projectPath);
            await loaded.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal("#GGGGGG", GetSnapshotHex(saved.UiSnapshot.View, colorName));
            AssertPair(vm, colorName, "#GGGGGG", Color.Parse("#112233"));
            AssertPair(loaded, colorName, "#GGGGGG", Color.Parse("#445566"));
            AssertPairChanges(changes, HexName(colorName));
            await WaitForGridRebuildAsync(loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "")]
    [InlineData("CadLineColor", " ")]
    [InlineData("RegularLineColor", "")]
    [InlineData("RegularLineColor", " ")]
    [InlineData("RegularSelectedColor", "")]
    [InlineData("RegularSelectedColor", " ")]
    public async Task SaveProjectAsync_BlankHex_WritesTextButLoadLeavesReceivingPairUntouched(
        string colorName, string typedHex)
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = new FreeformHelperViewModel { PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            SetColor(vm, colorName, Color.Parse("#112233"));
            SetHex(vm, colorName, typedHex);
            var loaded = new FreeformHelperViewModel
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            SetColor(loaded, colorName, Color.Parse("#445566"));
            SetHex(loaded, colorName, "#GGGGGG");
            var changes = ObserveChanges(loaded);

            Assert.True(await vm.SaveProjectAsync());
            var saved = JsonProjectStore.Load(projectPath);
            await loaded.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(typedHex, GetSnapshotHex(saved.UiSnapshot.View, colorName));
            AssertPair(loaded, colorName, "#GGGGGG", Color.Parse("#445566"));
            AssertPairChanges(changes);
            await WaitForGridRebuildAsync(loaded);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "#aabbcc", "#AABBCC", "#AABBCC")]
    [InlineData("CadLineColor", "#GGGGGG", "#GGGGGG", "#112233")]
    [InlineData("RegularLineColor", "#aabbcc", "#AABBCC", "#AABBCC")]
    [InlineData("RegularLineColor", "#GGGGGG", "#GGGGGG", "#112233")]
    [InlineData("RegularSelectedColor", "#aabbcc", "#AABBCC", "#AABBCC")]
    [InlineData("RegularSelectedColor", "#GGGGGG", "#GGGGGG", "#112233")]
    public async Task LoadProjectCommand_ColorSnapshot_DoesNotRecordUndoOrMarkPairDirty(
        string colorName, string snapshotHex, string expectedHex, string expectedColor)
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var project = new ProjectFile();
            SetSnapshotHex(project.UiSnapshot.View, colorName, snapshotHex);
            await new JsonProjectStore().SaveAsync(projectPath, project, CancellationToken.None);
            var vm = new FreeformHelperViewModel
            {
                ApplyAppVisualPreferencesOnProjectLoad = false,
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            SetColor(vm, colorName, Color.Parse("#112233"));
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);
            var changes = ObserveChanges(vm);
            var statesAtPairChanges = new ConcurrentQueue<(bool CanUndo, bool Dirty)>();
            vm.PropertyChanged += (_, change) => RecordPairState(vm, change.PropertyName, statesAtPairChanges);

            await vm.LoadProjectCommand.ExecuteAsync(null);

            AssertPair(vm, colorName, expectedHex, Color.Parse(expectedColor));
            AssertLoadedPairChanges(changes, colorName, snapshotHex);
            Assert.NotEmpty(statesAtPairChanges);
            Assert.All(statesAtPairChanges, state => Assert.Equal((false, false), state));
            Assert.DoesNotContain(nameof(vm.CanUndo), changes);
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "ColorCanvasCadLine")]
    [InlineData("RegularLineColor", "ColorCanvasRegularLine")]
    [InlineData("RegularSelectedColor", "ColorCanvasRegularSelected")]
    public void ColorSetter_DifferentTokenDefaultAfterInvalidText_ReplacesTextWithoutAddingUndo(
        string colorName, string tokenName)
    {
        var vm = new FreeformHelperViewModel();
        var token = Assert.IsType<Color>(FreeformHelperViewModel.ResolveTokenColor(tokenName));
        SetColor(vm, colorName, Color.FromRgb((byte)(token.R ^ 0xFF), token.G, token.B));
        SetHex(vm, colorName, "#GGGGGG");
        var changes = ObserveChanges(vm);

        SetColor(vm, colorName, token);

        AssertPair(vm, colorName, $"#{token.R:X2}{token.G:X2}{token.B:X2}", token);
        Assert.Equal<string?>([HexName(colorName), colorName], changes);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("CadLineColor", "ColorCanvasCadLine")]
    [InlineData("RegularLineColor", "ColorCanvasRegularLine")]
    [InlineData("RegularSelectedColor", "ColorCanvasRegularSelected")]
    public void ColorSetter_SameTokenDefaultAfterInvalidText_KeepsInvalidText(
        string colorName, string tokenName)
    {
        var vm = new FreeformHelperViewModel();
        var token = Assert.IsType<Color>(FreeformHelperViewModel.ResolveTokenColor(tokenName));
        SetColor(vm, colorName, token);
        SetHex(vm, colorName, "#GGGGGG");
        var changes = ObserveChanges(vm);

        SetColor(vm, colorName, token);

        AssertPair(vm, colorName, "#GGGGGG", token);
        Assert.Empty(changes);
        Assert.True(vm.CanUndo);
    }

    private static ConcurrentQueue<string?> ObserveChanges(FreeformHelperViewModel vm)
    {
        var changes = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, change) => changes.Enqueue(change.PropertyName);
        return changes;
    }

    private static void RecordPairState(FreeformHelperViewModel vm, string? name,
        ConcurrentQueue<(bool CanUndo, bool Dirty)> states)
    {
        if (IsPairChange(name))
        {
            states.Enqueue((vm.CanUndo, vm.HasUnsavedChanges));
        }
    }

    private static void AssertPair(FreeformHelperViewModel vm, string colorName, string hex, Color color)
    {
        Assert.Equal(hex, GetHex(vm, colorName));
        Assert.Equal(color, GetColor(vm, colorName));
    }

    private static void AssertPairChanges(ConcurrentQueue<string?> changes, params string[] expected) =>
        Assert.Equal(expected, changes.ToArray().Where(IsPairChange));

    private static void AssertLoadedPairChanges(ConcurrentQueue<string?> changes, string colorName, string snapshotHex)
    {
        var expected = snapshotHex switch
        {
            "#aabbcc" => new[] { HexName(colorName), colorName, HexName(colorName) },
            "#GGGGGG" => [HexName(colorName)],
            _ => throw new ArgumentOutOfRangeException(nameof(snapshotHex)),
        };
        AssertPairChanges(changes, expected);
    }

    private static bool IsPairChange(string? name) => name is
        nameof(FreeformHelperViewModel.CadLineColor) or nameof(FreeformHelperViewModel.CadLineColorHex) or
        nameof(FreeformHelperViewModel.RegularLineColor) or nameof(FreeformHelperViewModel.RegularLineColorHex) or
        nameof(FreeformHelperViewModel.RegularSelectedColor) or nameof(FreeformHelperViewModel.RegularSelectedColorHex);

    private static string HexName(string colorName) => colorName + "Hex";

    private static Color GetColor(FreeformHelperViewModel vm, string colorName) => colorName switch
    {
        nameof(vm.CadLineColor) => vm.CadLineColor,
        nameof(vm.RegularLineColor) => vm.RegularLineColor,
        nameof(vm.RegularSelectedColor) => vm.RegularSelectedColor,
        _ => throw new ArgumentOutOfRangeException(nameof(colorName)),
    };

    private static string GetHex(FreeformHelperViewModel vm, string colorName) => colorName switch
    {
        nameof(vm.CadLineColor) => vm.CadLineColorHex,
        nameof(vm.RegularLineColor) => vm.RegularLineColorHex,
        nameof(vm.RegularSelectedColor) => vm.RegularSelectedColorHex,
        _ => throw new ArgumentOutOfRangeException(nameof(colorName)),
    };

    private static void SetColor(FreeformHelperViewModel vm, string colorName, Color color)
    {
        switch (colorName)
        {
            case nameof(vm.CadLineColor): vm.CadLineColor = color; break;
            case nameof(vm.RegularLineColor): vm.RegularLineColor = color; break;
            case nameof(vm.RegularSelectedColor): vm.RegularSelectedColor = color; break;
            default: throw new ArgumentOutOfRangeException(nameof(colorName));
        }
    }

    private static void SetHex(FreeformHelperViewModel vm, string colorName, string hex)
    {
        switch (colorName)
        {
            case nameof(vm.CadLineColor): vm.CadLineColorHex = hex; break;
            case nameof(vm.RegularLineColor): vm.RegularLineColorHex = hex; break;
            case nameof(vm.RegularSelectedColor): vm.RegularSelectedColorHex = hex; break;
            default: throw new ArgumentOutOfRangeException(nameof(colorName));
        }
    }

    private static string GetSnapshotHex(UiViewSnapshot snapshot, string colorName) => colorName switch
    {
        nameof(FreeformHelperViewModel.CadLineColor) => snapshot.CadLineColor,
        nameof(FreeformHelperViewModel.RegularLineColor) => snapshot.RegularLineColor,
        nameof(FreeformHelperViewModel.RegularSelectedColor) => snapshot.RegularSelectedColor,
        _ => throw new ArgumentOutOfRangeException(nameof(colorName)),
    };

    private static void SetSnapshotHex(UiViewSnapshot snapshot, string colorName, string hex)
    {
        switch (colorName)
        {
            case nameof(FreeformHelperViewModel.CadLineColor): snapshot.CadLineColor = hex; break;
            case nameof(FreeformHelperViewModel.RegularLineColor): snapshot.RegularLineColor = hex; break;
            case nameof(FreeformHelperViewModel.RegularSelectedColor): snapshot.RegularSelectedColor = hex; break;
            default: throw new ArgumentOutOfRangeException(nameof(colorName));
        }
    }
}
