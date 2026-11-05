// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Logging;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Step-one state and commands. All mutable state is UI thread only.</summary>
public sealed class LayerImageExportViewModel : ObservableObject, IDisposable
{
    private readonly LayerImageExportContext _context;
    private readonly LayerSelectionModel _selection;
    private readonly Dictionary<string, LayerImageLayerRow> _rows;
    private readonly LatestRequestGate _previewGate = new();
    private readonly LayerImageExportSeams _seams;
    private readonly UiEventRunner _events = new(EmergencyLogSink.Report, EmergencyLogSink.Report);
    private IReadOnlyList<LayerImageLayerRow> _visibleRows = Array.Empty<LayerImageLayerRow>();
    private byte[]? _previewPng;
    private string? _previewErrorText;
    private bool _isPreviewUpdating;
    private bool _disposed;
    private Task _previewTask = Task.CompletedTask;

    public LayerImageExportViewModel(
        LayerImageExportContext context,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _seams = new LayerImageExportSeams(timeProvider ?? TimeProvider.System);
        _selection = new LayerSelectionModel(context.Entries, context.InitialCheckedLayers);
        _rows = _selection.Entries.ToDictionary(entry => entry.Name,
            entry => new LayerImageLayerRow(entry, _selection, OnSelectionChanged), StringComparer.Ordinal);
        PreviewSettings = new LayerImagePreviewSettings(context.InitialSize, context.LineWidthPixels, context.UseDarkTheme);
        FileSettings = new LayerImageFileSettings { Format = context.Format };
        ToggleAllLayersCommand = new RelayCommand(ToggleAllLayers);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        ConfirmCommand = new RelayCommand(() => RequestExportSettings?.Invoke(), () => _selection.CheckedCount > 0);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        RefreshVisibleRows();
        PreviewSettings.Changed += OnPreviewChanged;
        SchedulePreview();
    }

    public event Action? RequestExportSettings;

    public event Action<bool>? RequestClose;

    public LayerImagePreviewSettings PreviewSettings { get; }

    public LayerImageFileSettings FileSettings { get; }

    public IReadOnlyList<LayerImageLayerRow> VisibleRows => _visibleRows;

    public string SearchText
    {
        get => _selection.Query;
        set
        {
            if (string.Equals(_selection.Query, value, StringComparison.Ordinal))
            {
                return;
            }

            _selection.SetQuery(value);
            RefreshVisibleRows();
            OnPropertyChanged();
            OnPropertyChanged(nameof(AllLayersState));
        }
    }

    public AllLayersState AllLayersState => _selection.AllLayersState;

    public string CountText => string.Create(CultureInfo.InvariantCulture, $"{_selection.CheckedCount:N0} / {_selection.TotalCount:N0}");

    public string FooterText => $"{LayerImageSizeText.FormatSelection(_selection.CheckedCount)} · {PreviewSettings.OutputText}";

    public bool HasLayers => _selection.TotalCount > 0;

    public byte[]? PreviewPng => _previewPng;

    public string? PreviewErrorText
    {
        get => _previewErrorText;
        private set => SetProperty(ref _previewErrorText, value);
    }

    public bool IsPreviewUpdating
    {
        get => _isPreviewUpdating;
        private set => SetProperty(ref _isPreviewUpdating, value);
    }

    public IRelayCommand ToggleAllLayersCommand { get; }

    public IRelayCommand ClearSearchCommand { get; }

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public LayerImageExportRequest? CompletedRequest { get; private set; }

    public LayerImageExportRequest BuildRequest(LayerImageFileSettings fileSettings)
    {
        ArgumentNullException.ThrowIfNull(fileSettings);
        return new LayerImageExportRequest(_selection.CheckedNames, PreviewSettings.CurrentSize,
            PreviewSettings.LineWidthPixels, PreviewSettings.UseDarkTheme, fileSettings.Format, fileSettings.OneFilePerLayer);
    }

    public void CompleteExport(LayerImageExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompletedRequest = request;
        RequestClose?.Invoke(true);
    }

    internal Task WaitForPreviewAsync() => _previewTask.WaitAsync(CancellationToken.None);

    private void RefreshVisibleRows()
    {
        var terms = LayerListSearch.ParseTerms(_selection.Query);
        _visibleRows = Array.AsReadOnly(_selection.VisibleEntries.Select(entry => _rows[entry.Name]).ToArray());
        foreach (var row in _visibleRows)
        {
            row.UpdateHighlights(terms);
        }

        OnPropertyChanged(nameof(VisibleRows));
    }

    private void ToggleAllLayers()
    {
        _selection.ToggleAllVisible();
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        foreach (var row in _rows.Values)
        {
            row.RefreshSelection();
        }

        OnPropertyChanged(nameof(AllLayersState));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(FooterText));
        ConfirmCommand.NotifyCanExecuteChanged();
        bool updated = _selection.CheckedCount > 0 && PreviewSettings.CurrentSize.Source != LayerImageSizeSource.Custom &&
            PreviewSettings.UpdateDefaultSize(_context.CreateDefaultSize(_selection.CheckedNames));
        if (!updated)
        {
            SchedulePreview();
        }
    }

    private void OnPreviewChanged()
    {
        OnPropertyChanged(nameof(FooterText));
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        if (_disposed)
        {
            return;
        }

        _previewGate.Cancel();
        var ticket = _previewGate.Begin();
        var token = ticket.Source.Token;
        var layers = Array.AsReadOnly(_selection.CheckedNames
            .Select(name => new LayerImageLayer(name, _context.PadsOfLayer(name))).ToArray());
        var request = LayerImageRenderRequest.FromSize(layers, PreviewSettings.CurrentSize,
            PreviewSettings.LineWidthPixels, PreviewSettings.UseDarkTheme);
        _previewTask = _events.RunAsync("LayerImagePreview", _ =>
            UpdatePreviewAsync(request, ticket.RequestId, ticket.Source, token), token);
    }

    private async Task UpdatePreviewAsync(
        LayerImageRenderRequest request, long requestId, CancellationTokenSource source, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), _seams.TimeProvider, token);
            if (!IsCurrentPreview(requestId, token))
            {
                return;
            }

            IsPreviewUpdating = true;
            PreviewErrorText = null;
            var png = await _context.RenderPngAsync(request, 1200, token);
            if (IsCurrentPreview(requestId, token))
            {
                ReplacePreview(png);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A later edit or window close superseded this preview.
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            if (IsCurrentPreview(requestId, token))
            {
                ReplacePreview(null);
                PreviewErrorText = "Could not update preview.";
                EmergencyLogSink.Report("LayerImagePreview", ex);
            }
        }
        finally
        {
            if (IsCurrentPreview(requestId, token))
            {
                IsPreviewUpdating = false;
            }

            _previewGate.Release(source);
        }
    }

    private bool IsCurrentPreview(long requestId, CancellationToken token) =>
        !_disposed && !token.IsCancellationRequested && requestId == _previewGate.LatestRequestId;

    private void ReplacePreview(byte[]? png)
    {
        _previewPng = png;
        OnPropertyChanged(nameof(PreviewPng));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PreviewSettings.Changed -= OnPreviewChanged;
        _previewGate.Cancel();
        IsPreviewUpdating = false;
        ReplacePreview(null);
        GC.SuppressFinalize(this);
    }
}

internal sealed record LayerImageExportSeams(TimeProvider TimeProvider);
