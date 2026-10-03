using System.ComponentModel;
using System.Text;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class CoordinatePlannerWorkspaceView : UserControl
{
    private static readonly string[] PngFilePatterns = ["*.png"];
    private static readonly string[] AllFilesPatterns = ["*"];
    private PadCanvas? _canvas;
    private Canvas? _overlayCanvas;
    private Canvas? _overlayLabelCanvas;
    private Grid? _previewHost;
    private CanvasViewportAdapter? _viewportAdapter;
    private CoordinatePlannerWorkspaceViewModel? _attachedViewModel;
    private string? _draggedArrayCornerKey;

    public CoordinatePlannerWorkspaceView()
    {
        InitializeComponent();
        PointerMoved += OnArrayCornerPointerMoved;
        PointerReleased += OnArrayCornerPointerReleased;
        AttachedToVisualTree += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);
        DetachedFromVisualTree += (_, _) => AttachDataContext(null);
        DataContextChanged += (_, _) => AttachDataContext(DataContext as CoordinatePlannerWorkspaceViewModel);
    }

    private void AttachDataContext(CoordinatePlannerWorkspaceViewModel? viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        if (_attachedViewModel is not null)
        {
            var previousViewModel = _attachedViewModel;
            if (_viewportAdapter is not null)
            {
                _viewportAdapter.Detach(handler => previousViewModel.FitCanvasRequested -= handler);
            }

            _attachedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _attachedViewModel.ArtifactDetailRequested -= OnArtifactDetailRequested;
            _attachedViewModel.RequestExportPreviewPngAsync = null;
            _attachedViewModel.RequestSetClipboardTextAsync = null;
            _attachedViewModel.RequestSaveTextFileAsync = null;
        }

        _attachedViewModel = viewModel;
        if (_attachedViewModel is null)
        {
            if (_viewportAdapter is not null)
            {
                _viewportAdapter.SetFitBoundsOverride(null);
            }

            ClearOverlay();
            return;
        }

        _attachedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _attachedViewModel.ArtifactDetailRequested += OnArtifactDetailRequested;
        _attachedViewModel.RequestExportPreviewPngAsync = ExportPreviewPngAsync;
        _attachedViewModel.RequestSetClipboardTextAsync = SetClipboardTextAsync;
        _attachedViewModel.RequestSaveTextFileAsync = SaveTextFileAsync;
        var viewportAdapter = FindViewportAdapter();
        CanvasViewportAdapter.ApplyClipPolicy(
            FindPreviewHost(),
            FindOverlayCanvas(),
            FindOverlayLabelCanvas());
        viewportAdapter.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
        viewportAdapter.Attach(handler => _attachedViewModel.FitCanvasRequested += handler);
        Dispatcher.UIThread.Post(
            RenderOverlay,
            DispatcherPriority.Loaded);
    }

    private PadCanvas FindCanvas()
    {
        return _canvas ??= this.FindControl<PadCanvas>("CoordinatePadCanvas")
               ?? throw new InvalidOperationException("Coordinate planner pad canvas not found.");
    }

    private Canvas FindOverlayCanvas()
    {
        return _overlayCanvas ??= this.FindControl<Canvas>("CoordinateOverlayCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay canvas not found.");
    }

    private Canvas FindOverlayLabelCanvas()
    {
        return _overlayLabelCanvas ??= this.FindControl<Canvas>("CoordinateOverlayLabelCanvas")
               ?? throw new InvalidOperationException("Coordinate planner overlay label canvas not found.");
    }

    private Grid FindPreviewHost()
    {
        return _previewHost ??= this.FindControl<Grid>("CoordinatePreviewHost")
               ?? throw new InvalidOperationException("Coordinate preview host not found.");
    }

    private CanvasViewportAdapter FindViewportAdapter()
    {
        return _viewportAdapter ??= new CanvasViewportAdapter(FindCanvas(), RenderOverlay);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CoordinatePlannerWorkspaceViewModel.Snapshot) or
            nameof(CoordinatePlannerWorkspaceViewModel.CadPadsForCanvas) or
            nameof(CoordinatePlannerWorkspaceViewModel.SelectedArtifactRow) or
            nameof(CoordinatePlannerWorkspaceViewModel.SelectedOverlayModeOption))
        {
            if (e.PropertyName == nameof(CoordinatePlannerWorkspaceViewModel.Snapshot) &&
                _attachedViewModel is not null &&
                _viewportAdapter is not null)
            {
                _viewportAdapter.SetFitBoundsOverride(_attachedViewModel.PreviewBounds);
            }

            RenderOverlay();
        }
    }

    private async void OnArtifactDetailRequested(CoordinateArtifactRow row)
    {
        var dialog = new CoordinateArtifactDetailWindow
        {
            DataContext = row,
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
    }

    private async Task<bool> SetClipboardTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        await clipboard.SetTextAsync(text);
        return true;
    }

    private async Task<bool> SaveTextFileAsync(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest request)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = request.Title,
            DefaultExtension = request.DefaultExtension,
            SuggestedFileName = request.SuggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(request.FileTypeName) { Patterns = request.Patterns },
                new("All files") { Patterns = AllFilesPatterns },
            },
        });
        if (file is null)
        {
            return false;
        }

        await File.WriteAllTextAsync(file.Path.LocalPath, request.Content, Encoding.UTF8);
        return true;
    }

    private async Task<bool> ExportPreviewPngAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return false;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Coordinate Preview",
            DefaultExtension = "png",
            SuggestedFileName = "coordinate_planner.png",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("PNG") { Patterns = PngFilePatterns },
                new("All files") { Patterns = AllFilesPatterns },
            },
        });
        if (file is null)
        {
            return false;
        }

        var previewHost = FindPreviewHost();
        if (previewHost.Bounds.Width <= 1 || previewHost.Bounds.Height <= 1)
        {
            return false;
        }

        await Dispatcher.UIThread.InvokeAsync(RenderOverlay, DispatcherPriority.Render);
        var pixelSize = new PixelSize(
            Math.Max(2, (int)Math.Ceiling(previewHost.Bounds.Width)),
            Math.Max(2, (int)Math.Ceiling(previewHost.Bounds.Height)));
        var bitmap = new RenderTargetBitmap(pixelSize);
        bitmap.Render(previewHost);
        await using var stream = File.Open(file.Path.LocalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        bitmap.Save(stream);
        bitmap.Dispose();
        return true;
    }

    private void RenderOverlay()
    {
        var overlayShapes = FindOverlayCanvas();
        var overlayLabels = FindOverlayLabelCanvas();
        overlayShapes.Children.Clear();
        overlayLabels.Children.Clear();
        if (DataContext is not CoordinatePlannerWorkspaceViewModel viewModel)
        {
            return;
        }

        var viewportAdapter = FindViewportAdapter();
        var canvas = FindCanvas();
        if (canvas.Bounds.Width <= 1 || canvas.Bounds.Height <= 1)
        {
            return;
        }

        var viewFrame = viewportAdapter.GetViewFrameSnapshot();
        var snapshot = viewModel.Snapshot;
        var guideBrush = GetBrush("BrushCoordinatePlannerGuide", Brushes.DeepSkyBlue);
        var cornerBrush = GetBrush("BrushCoordinatePlannerPoint", Brushes.Gold);
        var bistBrush = GetBrush("BrushCoordinatePlannerBist", Brushes.Orange);
        var arrayBrush = GetBrush("BrushCoordinatePlannerArray", guideBrush);
        var mutedBrush = GetBrush("BrushTextMuted", Brushes.Gray);
        var labelBackground = GetBrush("BrushCoordinatePlannerLabelBackground", Brushes.Black);
        var labelForeground = GetBrush("BrushCoordinatePlannerLabelForeground", Brushes.White);
        var guideThickness = GetResourceDouble("CoordinatePlannerGuideStrokeThickness", 2d);
        var bistThickness = GetResourceDouble("CoordinatePlannerBistStrokeThickness", 2d);
        var pointThickness = GetResourceDouble("CoordinatePlannerPointStrokeThickness", 2d);
        var focusThickness = GetResourceDouble("CoordinatePlannerFocusStrokeThickness", 3d);
        var mutedOpacity = GetResourceDouble("CoordinatePlannerMutedOverlayOpacity", 0.34d);
        var focusOpacity = GetResourceDouble("CoordinatePlannerFocusOverlayOpacity", 0.96d);
        var labelFontSize = GetResourceDouble("CoordinatePlannerLabelFontSize", 12d);
        var labelOffset = GetResourceDouble("CoordinatePlannerLabelOffset", 10d);
        var minRadiusScreen = GetResourceDouble("CoordinatePlannerPointMinRadiusScreen", 5d);
        var arrayDotMinRadiusScreen = GetResourceDouble("CoordinatePlannerArrayPointMinRadiusScreen", 3d);
        var labelCornerRadius = GetResourceCornerRadius("RadiusSm", new CornerRadius(6));
        var labelPadding = GetResourceThickness("Inset6_2", new Thickness(6, 2));
        var overlayMode = viewModel.SelectedOverlayModeOption.Mode;
        var selectedKey = viewModel.SelectedArtifactRow?.Key;

        foreach (var rect in snapshot.Rectangles)
        {
            var isSelected = IsSelected(rect.Key, selectedKey);
            var isMuted = IsMutedOverlay(overlayMode, selectedKey, rect.Key);
            var brush = isMuted ? mutedBrush : rect.Kind == CoordinatePlannerRectangleKind.BistCenter ? bistBrush : guideBrush;
            var screenRect = viewportAdapter.ToOverlayRect(overlayShapes, rect.WorldBounds, viewFrame);
            var rectangle = new Rectangle
            {
                Width = screenRect.Width,
                Height = screenRect.Height,
                Stroke = brush,
                StrokeThickness = isSelected ? focusThickness : bistThickness,
                Fill = Brushes.Transparent,
                StrokeDashArray = new AvaloniaList<double> { 6, 4 },
                Opacity = isMuted ? mutedOpacity : focusOpacity,
            };
            Canvas.SetLeft(rectangle, screenRect.X);
            Canvas.SetTop(rectangle, screenRect.Y);
            overlayShapes.Children.Add(rectangle);
            if (ShouldLabelRectangle(overlayMode, isSelected))
            {
                overlayLabels.Children.Add(BuildLabel(
                    rect.Label,
                    new Point(screenRect.X + labelOffset, screenRect.Y - labelOffset),
                    labelBackground,
                    labelForeground,
                    labelFontSize,
                    labelCornerRadius,
                    labelPadding));
            }
        }

        foreach (var line in snapshot.Lines)
        {
            if (!ShouldRenderLine(line, overlayMode, selectedKey))
            {
                continue;
            }

            var isSelected = IsSelected(line.Key, selectedKey);
            var isMuted = IsMutedOverlay(overlayMode, selectedKey, line.Key);
            var colorBrush = line.Kind switch
            {
                CoordinatePlannerLineKind.HorizontalGuide => guideBrush,
                CoordinatePlannerLineKind.VerticalGuide => bistBrush,
                CoordinatePlannerLineKind.CustomArrayEdge => arrayBrush,
                _ => guideBrush,
            };
            var brush = isMuted ? mutedBrush : colorBrush;
            var start = viewportAdapter.ToOverlayPoint(overlayShapes, line.SafeStartWorld, viewFrame);
            var end = viewportAdapter.ToOverlayPoint(overlayShapes, line.SafeEndWorld, viewFrame);
            overlayShapes.Children.Add(new Line
            {
                StartPoint = start,
                EndPoint = end,
                Stroke = brush,
                StrokeThickness = isSelected ? focusThickness : guideThickness,
                Opacity = isMuted ? mutedOpacity : focusOpacity,
            });
            if (ShouldLabelLine(line, overlayMode, isSelected))
            {
                overlayLabels.Children.Add(BuildLabel(
                    line.Label,
                    new Point(start.X + labelOffset, start.Y - labelOffset),
                    labelBackground,
                    labelForeground,
                    labelFontSize,
                    labelCornerRadius,
                    labelPadding));
            }
        }

        foreach (var point in snapshot.Points)
        {
            if (!ShouldRenderPoint(point, overlayMode, selectedKey))
            {
                continue;
            }

            var isSelected = IsSelected(point.Key, selectedKey);
            var isMuted = IsMutedOverlay(overlayMode, selectedKey, point.Key);
            var (colorBrush, radiusFloor) = point.Kind switch
            {
                CoordinatePlannerPointKind.AaCorner => (cornerBrush, minRadiusScreen),
                CoordinatePlannerPointKind.BistCorner => (bistBrush, minRadiusScreen),
                CoordinatePlannerPointKind.CustomArrayCorner => (arrayBrush, minRadiusScreen),
                CoordinatePlannerPointKind.CustomArrayDot => (arrayBrush, arrayDotMinRadiusScreen),
                _ => (cornerBrush, minRadiusScreen),
            };
            var brush = isMuted ? mutedBrush : colorBrush;
            var center = viewportAdapter.ToOverlayPoint(overlayShapes, point.SafeWorld, viewFrame);
            var radiusX = ComputeScreenRadiusX(viewportAdapter, overlayShapes, viewFrame, point.SafeWorld, snapshot.WorldPillarRadiusX, radiusFloor);
            var radiusY = ComputeScreenRadiusY(viewportAdapter, overlayShapes, viewFrame, point.SafeWorld, snapshot.WorldPillarRadiusY, radiusFloor);
            var ellipse = new Ellipse
            {
                Width = radiusX * 2d,
                Height = radiusY * 2d,
                Stroke = brush,
                StrokeThickness = isSelected ? focusThickness : pointThickness,
                Fill = isSelected ? brush : Brushes.Transparent,
                Opacity = isMuted ? mutedOpacity : focusOpacity,
            };
            if (point.Kind == CoordinatePlannerPointKind.CustomArrayCorner)
            {
                ConfigureArrayCornerHandle(ellipse, point.Key, point.Label);
            }

            Canvas.SetLeft(ellipse, center.X - radiusX);
            Canvas.SetTop(ellipse, center.Y - radiusY);
            overlayShapes.Children.Add(ellipse);
            if (ShouldLabelPoint(point, overlayMode, isSelected))
            {
                overlayLabels.Children.Add(BuildLabel(
                    point.Label,
                    new Point(center.X + labelOffset, center.Y - labelOffset),
                    labelBackground,
                    labelForeground,
                    labelFontSize,
                    labelCornerRadius,
                    labelPadding));
            }
        }
    }

    private void ConfigureArrayCornerHandle(
        Ellipse handle,
        string key,
        string label)
    {
        handle.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(handle, label);
        handle.PointerPressed += (_, e) => OnArrayCornerHandlePointerPressed(key, e);
    }

    private void OnArrayCornerHandlePointerPressed(
        string key,
        PointerPressedEventArgs e)
    {
        if (DataContext is not CoordinatePlannerWorkspaceViewModel viewModel ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !viewModel.BeginCustomArrayCornerEdit(key))
        {
            return;
        }

        _draggedArrayCornerKey = key;
        e.Pointer.Capture(this);
        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void OnArrayCornerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CompleteDraggedArrayCorner(e);
            return;
        }

        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void OnArrayCornerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        MoveDraggedArrayCorner(e);
        CompleteDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void MoveDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null ||
            DataContext is not CoordinatePlannerWorkspaceViewModel viewModel)
        {
            return;
        }

        var canvas = FindCanvas();
        var worldPoint = canvas.ScreenToWorld(e.GetPosition(canvas));
        viewModel.MoveCustomArrayCornerToWorldPoint(_draggedArrayCornerKey, worldPoint);
    }

    private void CompleteDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (DataContext is CoordinatePlannerWorkspaceViewModel viewModel)
        {
            viewModel.CompleteCustomArrayCornerEdit(_draggedArrayCornerKey);
        }

        _draggedArrayCornerKey = null;
        e.Pointer.Capture(null);
    }

    private void ClearOverlay()
    {
        if (_overlayCanvas is not null)
        {
            _overlayCanvas.Children.Clear();
        }

        if (_overlayLabelCanvas is not null)
        {
            _overlayLabelCanvas.Children.Clear();
        }
    }

    private static double ComputeScreenRadiusX(
        CanvasViewportAdapter viewportAdapter,
        Canvas overlay,
        in PadCanvasViewFrameSnapshot viewFrame,
        Point2 center,
        double worldRadiusX,
        double fallback)
    {
        if (worldRadiusX <= 1e-9)
        {
            return fallback;
        }

        var centerPoint = viewportAdapter.ToOverlayPoint(overlay, center, viewFrame);
        var offsetPoint = viewportAdapter.ToOverlayPoint(overlay, new Point2(center.X + worldRadiusX, center.Y), viewFrame);
        return Math.Max(fallback, Math.Abs(offsetPoint.X - centerPoint.X));
    }

    private static double ComputeScreenRadiusY(
        CanvasViewportAdapter viewportAdapter,
        Canvas overlay,
        in PadCanvasViewFrameSnapshot viewFrame,
        Point2 center,
        double worldRadiusY,
        double fallback)
    {
        if (worldRadiusY <= 1e-9)
        {
            return fallback;
        }

        var centerPoint = viewportAdapter.ToOverlayPoint(overlay, center, viewFrame);
        var offsetPoint = viewportAdapter.ToOverlayPoint(overlay, new Point2(center.X, center.Y - worldRadiusY), viewFrame);
        return Math.Max(fallback, Math.Abs(offsetPoint.Y - centerPoint.Y));
    }

    private static bool ShouldRenderLine(
        CoordinatePlannerLine line,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey) =>
        mode switch
        {
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All => true,
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus => HasSelectedArtifact(selectedKey) || IsEssentialLine(line) || IsSelected(line.Key, selectedKey),
            _ => IsEssentialLine(line) || IsSelected(line.Key, selectedKey),
        };

    private static bool ShouldRenderPoint(
        CoordinatePlannerPoint point,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey) =>
        mode switch
        {
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All => true,
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus => HasSelectedArtifact(selectedKey) || IsEssentialPoint(point) || IsSelected(point.Key, selectedKey),
            _ => IsEssentialPoint(point) || IsSelected(point.Key, selectedKey),
        };

    private static bool ShouldLabelRectangle(
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        isSelected && mode != CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All;

    private static bool ShouldLabelLine(
        CoordinatePlannerLine line,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All ||
        isSelected ||
        line.Kind == CoordinatePlannerLineKind.CustomArrayEdge;

    private static bool ShouldLabelPoint(
        CoordinatePlannerPoint point,
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        bool isSelected) =>
        (point.Kind != CoordinatePlannerPointKind.CustomArrayDot &&
         mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.All) ||
        isSelected ||
        point.Kind is CoordinatePlannerPointKind.AaCorner or CoordinatePlannerPointKind.BistCorner;

    private static bool IsEssentialLine(CoordinatePlannerLine line) =>
        line.Kind is CoordinatePlannerLineKind.HorizontalGuide or CoordinatePlannerLineKind.VerticalGuide or CoordinatePlannerLineKind.CustomArrayEdge;

    private static bool IsEssentialPoint(CoordinatePlannerPoint point) =>
        point.Kind is CoordinatePlannerPointKind.AaCorner or CoordinatePlannerPointKind.BistCorner or CoordinatePlannerPointKind.CustomArrayCorner;

    private static bool IsMutedOverlay(
        CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode mode,
        string? selectedKey,
        string key) =>
        mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Focus &&
        HasSelectedArtifact(selectedKey) &&
        !IsSelected(key, selectedKey);

    private static bool IsSelected(string key, string? selectedKey) =>
        string.Equals(key, selectedKey, StringComparison.Ordinal);

    private static bool HasSelectedArtifact(string? selectedKey) =>
        !string.IsNullOrWhiteSpace(selectedKey);

    private static Border BuildLabel(
        string text,
        Point position,
        IBrush background,
        IBrush foreground,
        double fontSize,
        CornerRadius cornerRadius,
        Thickness padding)
    {
        var border = new Border
        {
            Background = background,
            CornerRadius = cornerRadius,
            Padding = padding,
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize,
                FontWeight = FontWeight.SemiBold,
            },
        };
        Canvas.SetLeft(border, position.X);
        Canvas.SetTop(border, position.Y);
        return border;
    }

    private IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        return UiResourceResolver.GetBrush(this, resourceKey, fallback, static color => new SolidColorBrush(color));
    }

    private CornerRadius GetResourceCornerRadius(string resourceKey, CornerRadius fallback)
    {
        return UiResourceResolver.GetCornerRadius(this, resourceKey, fallback);
    }

    private Thickness GetResourceThickness(string resourceKey, Thickness fallback)
    {
        return UiResourceResolver.GetThickness(this, resourceKey, fallback);
    }

    private double GetResourceDouble(string resourceKey, double fallback)
    {
        return UiResourceResolver.GetDouble(this, resourceKey, fallback);
    }
}
