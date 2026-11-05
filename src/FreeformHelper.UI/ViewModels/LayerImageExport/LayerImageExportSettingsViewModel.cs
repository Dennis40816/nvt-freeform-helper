// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Step-two file choices and save-picker commands. Mutable state is UI thread only.</summary>
public sealed class LayerImageExportSettingsViewModel : ObservableObject
{
    private readonly LayerImageExportViewModel _owner;
    private readonly Func<string, string, Task<string?>> _pickSavePathAsync;
    private string? _saveErrorText;

    public LayerImageExportSettingsViewModel(
        LayerImageExportViewModel owner, Func<string, string, Task<string?>> pickSavePathAsync)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _pickSavePathAsync = pickSavePathAsync ?? throw new ArgumentNullException(nameof(pickSavePathAsync));
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => owner.ConfirmCommand.CanExecute(null));
        BackCommand = new RelayCommand(() => RequestClose?.Invoke(false), () => !SaveCommand.IsRunning);
        SaveCommand.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SaveCommand.IsRunning))
            {
                BackCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public event Action<bool>? RequestClose;

    public LayerImageFileSettings FileSettings => _owner.FileSettings;

    public string FooterText => _owner.PreviewSettings.OutputText;

    public LayerImageExportRequest? Request { get; private set; }

    public string? SaveErrorText
    {
        get => _saveErrorText;
        private set => SetProperty(ref _saveErrorText, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand BackCommand { get; }

    private async Task SaveAsync()
    {
        try
        {
            SaveErrorText = null;
            var request = _owner.BuildRequest(FileSettings);
            string suggestedName = request.Layers.Count == 1 ? "dxf_" + request.Layers[0] : "dxf_layers";
            string? path = await _pickSavePathAsync(suggestedName, request.ToRememberedRequest().FileExtension);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Request = request with { SavePath = path };
            RequestClose?.Invoke(true);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            SaveErrorText = "Could not choose the export path.";
            EmergencyLogSink.Report("LayerImageSavePath", ex);
        }
    }
}
