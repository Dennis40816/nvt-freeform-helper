// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Owns the real dialog, model, clock and synthetic CAD sample for headless view tests.</summary>
internal sealed class LayerImageExportViewTestSession : IDisposable
{
    // A failure guard for a preview that never settles, not a number of rounds to wait.
    private const int MaxPreviewRounds = 5;

    public LayerImageExportViewTestSession(LayerImageExportContext? context = null)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Context = context ?? CreateContext();
        Model = new LayerImageExportViewModel(Context, Clock);
        Window = new LayerImageExportWindow { DataContext = Model };
        Model.RequestClose += Close;
    }

    public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UnixEpoch);
    public LayerImageExportContext Context { get; }
    public LayerImageExportViewModel Model { get; }
    public LayerImageExportWindow Window { get; }

    public async Task ShowAsync()
    {
        Window.Show();
        await HeadlessUiPump.FlushQueueAsync();
    }

    /// <summary>Runs the preview debounce on the manual clock until the newest preview has finished.</summary>
    public async Task CompletePreviewAsync()
    {
        // A preview that starts while an earlier one is still running cancels it and waits on a new timer.
        // Advancing once would leave that new timer unfired, so repeat until no newer preview is pending.
        for (var round = 0; round < MaxPreviewRounds; round++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(150));
            await Model.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
            if (Model.WaitForPreviewAsync().IsCompleted)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"The export preview kept restarting after {MaxPreviewRounds} rounds (updating={Model.IsPreviewUpdating}, error='{Model.PreviewErrorText}').");
    }

    public static LayerImageExportContext CreateContext()
    {
        var entries = new List<LayerListEntry>
        {
            new("NC.drawing", 882), new("PGT.drawing", 66), new("PINC.drawing", 80),
            new("Ref1.drawing", 188), new("Ref2.drawing", 3386), new("Aux.drawing", 120),
            new("GND.drawing", 410), new("PGB.drawing", 54), new("TOP.drawing", 212),
        }.AsReadOnly();
        var checkedNames = new List<string> { "NC.drawing", "PGT.drawing", "Ref1.drawing", "Ref2.drawing", "GND.drawing" }.AsReadOnly();
        var size = LayerImageSize.CreateDefault(915.52, 119.59, 6480, 848);
        var pads = CreatePreviewPads();
        return new LayerImageExportContext(entries, checkedNames, size, 1m, true, DxfLayerImageFormat.Bmp,
            name => pads.GetValueOrDefault(name) ?? Array.Empty<CadPad>(), _ => size,
            static (_, _) => Task.FromResult<string?>(null), LayerImagePreviewService.RenderPngAsync);
    }

    public static LayerImageExportContext CreateManyLayersContext(int count)
    {
        var entries = Enumerable.Range(0, count)
            .Select(index => new LayerListEntry(string.Create(CultureInfo.InvariantCulture, $"Layer{index:D4}"), index + 1))
            .ToList().AsReadOnly();
        return CreateContext() with
        {
            Entries = entries,
            InitialCheckedLayers = entries.Select(static entry => entry.Name).ToList().AsReadOnly(),
            PadsOfLayer = static _ => Array.Empty<CadPad>(),
        };
    }

    private static Dictionary<string, IReadOnlyList<CadPad>> CreatePreviewPads()
    {
        var outer = new List<CadPad>();
        var inner = new List<CadPad>();
        for (int index = 0; index < 5; index++)
        {
            double x = 18 + index * 124;
            outer.Add(Rectangle(index, "PGT.drawing", x, 12, 112, 60));
            if (index < 4)
            {
                inner.Add(Rectangle(index + 10, "Ref1.drawing", x + 20, 25, 72, 34));
            }
        }

        return new Dictionary<string, IReadOnlyList<CadPad>>(StringComparer.Ordinal)
        {
            ["NC.drawing"] = new List<CadPad> { Rectangle(30, "NC.drawing", 0, 0, 648, 84.8) }.AsReadOnly(),
            ["PGT.drawing"] = outer.AsReadOnly(),
            ["Ref1.drawing"] = inner.AsReadOnly(),
            ["Ref2.drawing"] = new List<CadPad> { Rectangle(31, "Ref2.drawing", 19, 13, 110, 58) }.AsReadOnly(),
            ["GND.drawing"] = new List<CadPad> { Rectangle(32, "GND.drawing", 20, 14, 108, 56) }.AsReadOnly(),
        };
    }

    private static CadPad Rectangle(int id, string layer, double x, double y, double width, double height)
    {
        var polygon = new Polygon2(new List<Point2>
        {
            new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height),
        });
        return new CadPad(id, string.Create(CultureInfo.InvariantCulture, $"CAD{id}"), layer, polygon);
    }

    private void Close(bool confirmed) => Window.Close(confirmed);

    public void Dispose()
    {
        Model.RequestClose -= Close;
        Window.Close();
        Model.Dispose();
    }
}
