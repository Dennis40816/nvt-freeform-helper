// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.SettingsSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiEventFailureTests
{
    [AvaloniaFact]
    public void CascadeDetails_DialogFails_ReportsOnceAndKeepsOwnerUsable()
    {
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("dialog unavailable");
        var view = new SettingsGeneralSectionView(new SettingsGeneralSectionSeams(
            probe.Runner, (_, _) => Task.FromException(failure)));
        var owner = new Window { Content = view };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(owner);
        owner.Show();

        UiEventProbe.Click(view, "EditCascadeDetailsButton_Click");

        var report = Assert.Single(probe.Reports);
        Assert.Equal("Settings.CascadeDetails", report.Operation);
        Assert.Same(failure, report.Exception);
        Assert.True(owner.IsVisible);
        Assert.True(view.IsEnabled);
    }

    [AvaloniaFact]
    public void CopyOverlapReport_ClipboardFails_ReportsOnceAndPreservesReport()
    {
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("clipboard unavailable");
        var vm = new DxfOverlapReportViewModel("overlap", ["synthetic issue"]);
        var window = new DxfOverlapReportWindow(new DxfOverlapReportSeams(
            probe.Runner, _ => Task.FromException(failure)))
        { DataContext = vm };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.Show();

        UiEventProbe.Click(window, "CopyAll_Click");

        var report = Assert.Single(probe.Reports);
        Assert.Equal("DxfOverlap.CopyAll", report.Operation);
        Assert.Same(failure, report.Exception);
        Assert.Equal("synthetic issue", vm.ReportText);
        Assert.True(window.IsVisible);
        Assert.True(window.IsEnabled);
    }

    [AvaloniaFact]
    public void CopyOverlapReport_ProductionReporter_UsesExistingStatusRoute()
    {
        var helper = new FreeformHelperViewModel();
        var failure = new InvalidOperationException("clipboard unavailable");
        var window = new DxfOverlapReportWindow(new DxfOverlapReportSeams(
            helper.UiEvents, _ => Task.FromException(failure)))
        {
            DataContext = new DxfOverlapReportViewModel("overlap", ["synthetic issue"]),
        };
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);

        UiEventProbe.Click(window, "CopyAll_Click");

        Assert.Equal("DxfOverlap.CopyAll failed: clipboard unavailable", helper.StatusText);
    }
}
