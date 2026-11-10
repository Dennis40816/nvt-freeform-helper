// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Exercises the real view attachment with a recording storage feature on a headless window.</summary>
internal sealed class MainWindowFilePickerTestHost : IDisposable
{
    // Keep the original managed wrapper alive; Window owns the shared platform implementation's lifetime.
    private readonly Window _platformOwner;

    public MainWindowFilePickerTestHost(IStorageProvider provider)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        _platformOwner = new Window();
        ViewModel = new FreeformHelperViewModel();
        View = new FreeformHelperView { DataContext = ViewModel };
        Window = CreateWindow(_platformOwner, provider, View);
        Window.Show();
    }

    public FreeformHelperViewModel ViewModel { get; }
    public FreeformHelperView View { get; }
    public Window Window { get; }

    public void Dispose() => Window.Close();

    private static Window CreateWindow(Window platformOwner, IStorageProvider provider, FreeformHelperView view)
    {
        IWindowImpl? platform = null;
        try
        {
            platform = DispatchProxy.Create<IWindowImpl, FilePickerWindowPlatform>();
            var proxy = (FilePickerWindowPlatform)platform;
            proxy.Inner = platformOwner.PlatformImpl
                ?? throw new InvalidOperationException("Headless window has no platform implementation.");
            proxy.StorageProvider = provider;
            var window = new Window(platform) { Content = view };
            platform = null;
            return window;
        }
        finally
        {
            platform?.Dispose();
        }
    }
}

/// <summary>Forwards the real headless platform while substituting only its storage feature.</summary>
public class FilePickerWindowPlatform : DispatchProxy
{
    public IWindowImpl Inner { get; set; } = null!;
    public IStorageProvider StorageProvider { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == nameof(Avalonia.IOptionalFeatureProvider.TryGetFeature)
            && args is [Type feature] && feature == typeof(IStorageProvider))
        {
            return StorageProvider;
        }

        return targetMethod.Invoke(Inner, args);
    }
}
