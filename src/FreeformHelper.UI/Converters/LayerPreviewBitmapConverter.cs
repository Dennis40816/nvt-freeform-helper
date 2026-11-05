// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace FreeformHelper.UI.Converters;

/// <summary>UI-thread cache owned by one window. Retired images outlive the binding's source replacement.</summary>
public sealed class LayerPreviewBitmapConverter : IValueConverter, IDisposable
{
    private byte[]? _bytes;
    private Bitmap? _bitmap;
    private bool _disposed;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bytes = value as byte[];
        if (ReferenceEquals(bytes, _bytes))
        {
            return _bitmap;
        }

        var dispatcher = global::Avalonia.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Preview conversion requires the UI application.");
        dispatcher.VerifyAccess();
        var previous = _bitmap;
        if (bytes is null)
        {
            _bitmap = null;
        }
        else
        {
            using var stream = new MemoryStream(bytes, writable: false);
            _bitmap = new Bitmap(stream);
        }

        _bytes = bytes;
        if (previous is not null)
        {
            // The Image binding must release the previous Source before it is disposed.
            dispatcher.Post(previous.Dispose, DispatcherPriority.Background);
        }

        return _bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bitmap?.Dispose();
        _bitmap = null;
        _bytes = null;
        GC.SuppressFinalize(this);
    }
}
