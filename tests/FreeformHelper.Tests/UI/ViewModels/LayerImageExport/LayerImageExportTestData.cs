// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;

namespace FreeformHelper.Tests;

internal static class LayerImageExportTestData
{
    internal static ManualTimeProvider CreateClock() => new(DateTimeOffset.UnixEpoch);

    internal static LayerImageExportContext CreateContext(int? pixelsX = null, int? pixelsY = null)
    {
        var source = new LayerImageExportSource(new List<CadPad>
        {
            CreateSquareCad(1, "nc.drawing", 0, 0, 10),
            CreateSquareCad(2, "nc.pads", 0, 20, 10),
            CreateSquareCad(3, "other.drawing", 20, 0, 10),
        }, pixelsX, pixelsY);
        var names = source.Entries.Select(entry => entry.Name).ToList().AsReadOnly();
        return new LayerImageExportContext(source.Entries, names, source.CreateDefaultSize(names), 1m, false,
            DxfLayerImageFormat.Png, source.PadsOfLayer, source.CreateDefaultSize,
            static (_, _) => Task.FromResult<string?>(null),
            static (_, _, _) => Task.FromResult<byte[]?>(null));
    }

    internal static CadPad CreateSquareCad(int id, string layer, double x, double y, double size)
    {
        var polygon = new Polygon2(new List<Point2>
        {
            new(x, y), new(x + size, y), new(x + size, y + size), new(x, y + size),
        });
        return new CadPad(id, string.Create(CultureInfo.InvariantCulture, $"CAD{id}"), layer, polygon);
    }
}
