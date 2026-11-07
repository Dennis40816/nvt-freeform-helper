// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImageExportSettingsIntegratedLayoutTests
{
    [AvaloniaFact]
    public async Task FileCountBesideFileModeFollowsExportSelectionAndStaysVisibleWhenCombinedAsync()
    {
        var context = LayerImageExportTestData.CreateContext();
        using var owner = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
        owner.FileSettings.OneFilePerLayer = true;
        owner.SearchText = "nc";
        var model = new LayerImageExportSettingsViewModel(owner, context.PickSavePathAsync);

        await WithDialogAsync(model, async dialog =>
        {
            var toggle = Assert.IsType<CheckBox>(dialog.FindControl<CheckBox>("OneFilePerLayerCheckBox"));
            var count = Assert.IsType<TextBlock>(dialog.FindControl<TextBlock>("ExpectedFileCountText"));
            var row = Assert.IsType<Grid>(toggle.Parent);
            Assert.Equal("One file per layer", toggle.Content);
            Assert.Same(row, count.Parent);
            Assert.True(Grid.GetColumn(count) > Grid.GetColumn(toggle));
            Assert.True(count.Bounds.Left >= toggle.Bounds.Right);
            Assert.True(count.IsEffectivelyVisible);
            Assert.Equal(3, owner.BuildRequest(model.FileSettings).Layers.Count);
            Assert.Equal(2, owner.VisibleRows.Count);
            Assert.Equal("3 image files", count.Text);

            toggle.IsChecked = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(model.FileSettings.OneFilePerLayer);
            Assert.True(count.IsEffectivelyVisible);
            Assert.Equal("1 image file", count.Text);

            toggle.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("3 image files", count.Text);
            owner.VisibleRows[0].IsChecked = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(2, owner.BuildRequest(model.FileSettings).Layers.Count);
            Assert.Equal("2 image files", count.Text);
            owner.VisibleRows[1].IsChecked = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("1 image file", count.Text);
        });
    }

    [AvaloniaFact]
    public async Task ErrorAreaAppearsOnlyAfterPickerFailureAndClearsOnRetryAsync()
    {
        using var owner = new LayerImageExportViewModel(
            LayerImageExportTestData.CreateContext(), LayerImageExportTestData.CreateClock());
        int pickCount = 0;
        var model = new LayerImageExportSettingsViewModel(owner, (_, _) => ++pickCount == 1
            ? Task.FromException<string?>(new IOException("Picker unavailable."))
            : Task.FromResult<string?>(null));

        await WithDialogAsync(model, async dialog =>
        {
            var error = Assert.IsType<TextBlock>(dialog.FindControl<TextBlock>("SaveErrorText"));
            Assert.False(error.IsEffectivelyVisible);

            await model.SaveCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(error.IsEffectivelyVisible);
            Assert.Equal("Could not choose the export path.", error.Text);
            Assert.Equal(model.SaveErrorText, error.Text);
            Assert.Null(model.Request);

            await model.SaveCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(error.IsEffectivelyVisible);
            Assert.Null(model.SaveErrorText);
            Assert.Null(model.Request);
            Assert.True(dialog.IsVisible);
        });
    }

    [AvaloniaFact]
    public async Task FooterKeepsOutputSizeAndSaveAsTheOnlyPrimaryWithNeutralBackAsync()
    {
        var context = LayerImageExportTestData.CreateContext(6480, 848);
        using var owner = new LayerImageExportViewModel(context, LayerImageExportTestData.CreateClock());
        var model = new LayerImageExportSettingsViewModel(owner, context.PickSavePathAsync);

        await WithDialogAsync(model, dialog =>
        {
            var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
            var save = Assert.Single(buttons, static button => button.Classes.Contains("actionPrimary"));
            Assert.Same(model.SaveCommand, save.Command);
            Assert.Equal("Save…", save.Content);
            Assert.True(save.IsDefault);
            var back = Assert.Single(buttons, button => ReferenceEquals(button.Command, model.BackCommand));
            Assert.Equal("Back", back.Content);
            Assert.Contains("actionNeutral", back.Classes);
            Assert.DoesNotContain("actionPrimary", back.Classes);
            Assert.Same(save.Parent, back.Parent);
            Assert.Same(model.BackCommand, Assert.Single(dialog.KeyBindings, static binding => binding.Gesture?.Key == Key.Escape).Command);

            var footer = Assert.IsType<TextBlock>(dialog.FindControl<TextBlock>("FooterText"));
            Assert.True(footer.IsEffectivelyVisible);
            Assert.Equal("6,480 × 848 px", footer.Text);
            Assert.Equal(owner.PreviewSettings.OutputText, footer.Text);
            Assert.Equal(model.FooterText, footer.Text);
            var footerRow = Assert.IsType<Grid>(footer.Parent);
            Assert.Same(footerRow, save.Parent?.Parent);
            return Task.CompletedTask;
        });
    }

    private static async Task WithDialogAsync(
        LayerImageExportSettingsViewModel model, Func<LayerImageExportSettingsDialog, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dialog = new LayerImageExportSettingsDialog { DataContext = model };
        try
        {
            dialog.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(dialog);
        }
        finally
        {
            dialog.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }
}
