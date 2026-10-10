using Avalonia.Controls;
using Avalonia.Interactivity;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views.SettingsSections;

public sealed partial class SettingsGeneralSectionView : UserControl
{
    private UiEventRunner? _uiEvents => _seams?.UiEvents ?? (DataContext as FreeformHelper.UI.ViewModels.SettingsWindowViewModel)?.UiEvents;

    private readonly SettingsGeneralSectionSeams? _seams;

    public SettingsGeneralSectionView() : this(null)
    {
    }

    internal SettingsGeneralSectionView(SettingsGeneralSectionSeams? seams)
    {
        _seams = seams;
        InitializeComponent();
    }

    private void EditCascadeDetailsButton_Click(object? sender, RoutedEventArgs e)
    {
        _uiEvents?.Run("Settings.CascadeDetails", _ => ShowCascadeDetailsAsync(), CancellationToken.None);
    }

    private async Task ShowCascadeDetailsAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        if (_seams is not null)
        {
            await _seams.ShowCascadeDetailsAsync(owner, DataContext);
            return;
        }

        var dialog = new CascadeIcSettingsWindow
        {
            DataContext = DataContext,
        };
        await dialog.ShowDialog(owner);
    }
}

internal sealed record SettingsGeneralSectionSeams(UiEventRunner UiEvents, Func<Window, object?, Task> ShowCascadeDetailsAsync);
