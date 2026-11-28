// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class DisplaySettingsCanvasScene : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
    private readonly Window _window;

    private DisplaySettingsCanvasScene()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Model = new FreeformHelperViewModel(new AppGeneralSettingsStore(Path.Combine(_directory.FullName, "app-settings.json")))
        {
            ApplyAppVisualPreferencesOnProjectLoad = false,
        };
        var view = new FreeformHelperView { DataContext = Model };
        Surface = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("PadCanvas"));
        _window = new Window { Content = view, Width = 1200, Height = 800 };
    }

    public FreeformHelperViewModel Model { get; }
    public PadCanvas Surface { get; }

    public static async Task<DisplaySettingsCanvasScene> LoadAsync()
    {
        var scene = new DisplaySettingsCanvasScene();
        try
        {
            var path = Path.Combine(scene._directory.FullName, "project.json");
            await new JsonProjectStore().SaveAsync(path, CreateProject(), TestContext.Current.CancellationToken);
            var result = await scene.Model.LoadProjectFromPathAsync(path);
            Assert.True(result.IsLoaded, result.Message);
            await scene.Model.WaitForGridRebuildIdleAsync();
            Assert.Equal(3, scene.Model.CadPads.Count);
            Assert.Equal(2, scene.Model.RegularPads.Count);
            scene.Model.ShowCad = true;
            scene.Model.ShowRegular = true;
            scene.Model.ColorCadByArea = false;
            scene.Model.HighlightFreeform = false;
            scene.Model.HighlightUnmatched = false;
            scene.Model.ShowDiffIndexOverlay = false;
            scene.Model.ShowNotchCanvasPreview = false;
            scene.Model.ShowNotchToRegularLabels = false;
            scene.Surface.ShowAxisLabels = false;
            scene._window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            scene.Surface.ClearSelection();
            scene.Surface.FitToContent();
            await HeadlessUiPump.FlushQueueAsync();
            return scene;
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }

    public async Task<GeometryDrawing[]> DrawPadsAsync(bool cad)
    {
        await HeadlessUiPump.FlushQueueAsync();
        var bounds = cad
            ? Model.CadPads.Select(pad => ToRect(pad.Bounds)).ToHashSet()
            : Model.RegularPads.Select(pad => ToRect(pad.Bounds)).ToHashSet();
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            Surface.Render(context);
        }

        Assert.False(Surface.GetVisibleDrawPerfSnapshot().LowDetailMode);
        return Geometries(group)
            .Where(drawing => drawing.Pen is not null && drawing.Geometry is { } geometry && bounds.Contains(geometry.Bounds))
            .ToArray();
    }

    public async Task<Dictionary<Rect, Color>> FillColorsAsync(bool cad)
    {
        var drawings = await DrawPadsAsync(cad);
        var colors = drawings.Where(drawing => drawing.Brush is not null)
            .ToDictionary(
                drawing => drawing.Geometry!.Bounds,
                drawing => Assert.IsAssignableFrom<ISolidColorBrush>(drawing.Brush).Color);
        Assert.Equal(cad ? Model.CadPads.Count : Model.RegularPads.Count, colors.Count);
        return colors;
    }

    public void Dispose()
    {
        _window.Close();
        _directory.Delete(recursive: true);
    }

    public static Rect ToRect(Rect2 bounds) => new(bounds.MinX, bounds.MinY, bounds.Width, bounds.Height);

    private static IEnumerable<GeometryDrawing> Geometries(Drawing drawing)
    {
        if (drawing is GeometryDrawing geometry)
        {
            yield return geometry;
        }
        else if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                foreach (var descendant in Geometries(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static ProjectFile CreateProject()
    {
        return new ProjectFile
        {
            EmbeddedDxfName = "display-settings.dxf",
            EmbeddedDxf = Encoding.UTF8.GetBytes(CreateDxf()),
            Settings = new ProjectSettings
            {
                Grid = new GridSettings { XChannels = 2, YChannels = 1 },
                View = new ViewSettings { AutoReplayStep2AfterProjectLoad = false },
            },
            UiSnapshot = new ProjectUiSnapshot
            {
                Grid = new UiGridSnapshot
                {
                    CascadeCount = 1,
                    IcX = [2],
                    IcY = [1],
                    TotalX = 2,
                    TotalY = 1,
                    ActiveAreaWidthMm = 20,
                    ActiveAreaHeightMm = 10,
                },
            },
        };
    }

    private static string CreateDxf()
    {
        var text = new StringBuilder("0\nSECTION\n2\nENTITIES\n");
        for (var index = 0; index < 3; index++)
        {
            text.Append("0\nLWPOLYLINE\n8\nsignal\n70\n1\n90\n4\n");
            var polygon = TestGeometryFactory.CreateRect(index * 5, 0, index * 5 + index + 1, 2);
            foreach (var point in polygon.Vertices)
            {
                text.Append("10\n").Append(point.X.ToString(CultureInfo.InvariantCulture));
                text.Append("\n20\n").Append(point.Y.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        return text.Append("0\nENDSEC\n0\nEOF\n").ToString();
    }
}
