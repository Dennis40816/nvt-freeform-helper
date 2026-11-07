// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportHiddenRowsHintWindowTests
{
    [AvaloniaFact]
    public async Task HiddenRowsHintVisibilityTracksDisplayModeAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        NotchTableRow[] rows =
        [
            new(0, 8, 100, 200, new NotchV22Node(8, 100, 65535, 0, 65535, 0, 0), "no-op"),
            new(0, 9, 101, 201, new NotchV22Node(9, 95, 10, 5, 65535, 0, 0), "transfer"),
        ];
        var viewModel = new NotchExportSelectionViewModel(new NotchTable(rows));
        var window = new NotchExportSelectionWindow { DataContext = viewModel };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var hint = Assert.Single(window.GetVisualDescendants().OfType<SelectableTextBlock>(),
                text => text.Text == "1 row hidden but still exported");

            Assert.True(hint.IsVisible);
            Assert.True(hint.IsEffectivelyVisible);

            viewModel.SelectedRowDisplayModeOption = viewModel.RowDisplayModeOptions
                .Single(option => option.Value == NotchExportRowDisplayMode.AllRows);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(0, viewModel.HiddenSelectedRowCount);
            Assert.False(hint.IsVisible);
            Assert.False(hint.IsEffectivelyVisible);

            viewModel.SelectedRowDisplayModeOption = viewModel.RowDisplayModeOptions
                .Single(option => option.Value == NotchExportRowDisplayMode.TransferOnly);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal("1 row hidden but still exported", hint.Text);
            Assert.True(hint.IsVisible);
            Assert.True(hint.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
