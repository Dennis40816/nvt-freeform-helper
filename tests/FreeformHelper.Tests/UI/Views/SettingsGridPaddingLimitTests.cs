// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsGridPaddingLimitTests
{
    private const decimal SavedPaddingLimitPercent = 50m;

    [AvaloniaFact]
    public async Task GridPaddingInput_AllowsTheSameRangeAsTheSavedValue()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new FreeformHelperViewModel { GridPaddingPercent = 37m };
        var view = new SettingsGeneralSectionView { DataContext = viewModel.CreateSettingsWindowViewModel() };
        var window = new Window { Width = 900, Height = 900, Content = view };

        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            var scrubber = Assert.Single(
                view.GetVisualDescendants().OfType<NumberScrubber>(),
                static candidate => candidate.Value == 37m);
            Assert.Equal(0m, scrubber.Minimum);
            Assert.Equal(SavedPaddingLimitPercent, scrubber.Maximum);
        }
        finally
        {
            window.Close();
        }
    }
}
