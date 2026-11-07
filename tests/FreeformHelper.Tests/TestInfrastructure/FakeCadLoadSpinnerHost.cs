// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class FakeCadLoadSpinnerHost : ICadLoadSpinnerHost
{
    public int WarmupCallCount { get; private set; }
    public int ShowCallCount { get; private set; }
    public int HideCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }
    public Window? LastOwner { get; private set; }

    public IDisposable Install()
    {
        var originalFactory = FreeformHelperView.CadLoadSpinnerHostFactory;
        FreeformHelperView.CadLoadSpinnerHostFactory = () => this;
        return new FactoryOverride(originalFactory);
    }

    public void Warmup()
    {
        WarmupCallCount++;
    }

    public void Show(Window owner)
    {
        ShowCallCount++;
        LastOwner = owner;
    }

    public void Hide()
    {
        HideCallCount++;
    }

    public void Dispose()
    {
        DisposeCallCount++;
    }

    private sealed class FactoryOverride(Func<ICadLoadSpinnerHost> originalFactory) : IDisposable
    {
        public void Dispose()
        {
            FreeformHelperView.CadLoadSpinnerHostFactory = originalFactory;
        }
    }
}
