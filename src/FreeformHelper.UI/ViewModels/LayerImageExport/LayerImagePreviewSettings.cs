// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Bindable preview inputs. All mutable state is UI thread only.</summary>
public sealed class LayerImagePreviewSettings : ObservableObject
{
    private LayerImageSize _size;
    private decimal _lineWidthPixels;
    private bool _useDarkTheme;

    public LayerImagePreviewSettings(LayerImageSize size, decimal lineWidthPixels = 1m, bool useDarkTheme = false)
    {
        _size = size ?? throw new ArgumentNullException(nameof(size));
        _lineWidthPixels = Math.Clamp(lineWidthPixels, 0.1m, 100m);
        _useDarkTheme = useDarkTheme;
        ResetToProjectSizeCommand = new RelayCommand(() => ApplySize(_size.Reset()));
        ToggleThemeCommand = new RelayCommand(() => UseDarkTheme = !UseDarkTheme);
    }

    public event Action? Changed;

    public LayerImageSize CurrentSize => _size;

    public string OutputText => LayerImageSizeText.Format(_size);

    public decimal PixelsX
    {
        get => _size.PixelsX;
        set => ApplySize(_size.WithPixelsX(ToInteger(value)));
    }

    public decimal PixelsY
    {
        get => _size.PixelsY;
        set => ApplySize(_size.WithPixelsY(ToInteger(value)));
    }

    public decimal PaddingX
    {
        get => _size.PaddingX;
        set => ApplySize(_size.WithPaddingX(ToInteger(value)));
    }

    public decimal PaddingY
    {
        get => _size.PaddingY;
        set => ApplySize(_size.WithPaddingY(ToInteger(value)));
    }

    public bool KeepRatio
    {
        get => _size.KeepRatio;
        set => ApplySize(_size.WithKeepRatio(value));
    }

    public decimal LineWidthPixels
    {
        get => _lineWidthPixels;
        set
        {
            if (SetProperty(ref _lineWidthPixels, Math.Clamp(value, 0.1m, 100m)))
            {
                Changed?.Invoke();
            }
        }
    }

    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set
        {
            if (SetProperty(ref _useDarkTheme, value))
            {
                Changed?.Invoke();
            }
        }
    }

    public IRelayCommand ResetToProjectSizeCommand { get; }

    public IRelayCommand ToggleThemeCommand { get; }

    internal bool UpdateDefaultSize(LayerImageSize size) =>
        _size.Source != LayerImageSizeSource.Custom && ApplySize(size.WithKeepRatio(_size.KeepRatio));

    private bool ApplySize(LayerImageSize size)
    {
        if (_size == size)
        {
            return false;
        }

        _size = size;
        OnPropertyChanged(nameof(PixelsX));
        OnPropertyChanged(nameof(PixelsY));
        OnPropertyChanged(nameof(PaddingX));
        OnPropertyChanged(nameof(PaddingY));
        OnPropertyChanged(nameof(KeepRatio));
        OnPropertyChanged(nameof(CurrentSize));
        OnPropertyChanged(nameof(OutputText));
        Changed?.Invoke();
        return true;
    }

    private static int ToInteger(decimal value) =>
        (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), int.MinValue, int.MaxValue);
}
