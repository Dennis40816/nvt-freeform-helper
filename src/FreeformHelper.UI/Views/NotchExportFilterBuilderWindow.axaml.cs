using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportFilterBuilderWindow : Window
{
    private string _initialQuery = string.Empty;

    public NotchExportFilterBuilderWindow()
    {
        InitializeComponent();
        UpdatePreview();
    }

    public string GeneratedQuery { get; private set; } = string.Empty;

    public void SetInitialQuery(string query)
    {
        if (FuzzyTextBox is null)
        {
            return;
        }

        _initialQuery = query ?? string.Empty;
        FuzzyTextBox.Text = _initialQuery;
        UpdatePreview();
    }

    private void FilterInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void FilterInput_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!HasInputControls())
        {
            return;
        }

        IcTextBox.Text = string.Empty;
        DiffTextBox.Text = string.Empty;
        RegTextBox.Text = string.Empty;
        CadTextBox.Text = string.Empty;
        AnchorTextBox.Text = string.Empty;
        FuzzyTextBox.Text = string.Empty;
        StatusComboBox.SelectedIndex = 0;
        UpdatePreview();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        GeneratedQuery = BuildQuery();
        Close(true);
    }

    private void UpdatePreview()
    {
        var query = BuildQuery();
        GeneratedQuery = query;
        if (PreviewTextBox is not null)
        {
            PreviewTextBox.Text = string.IsNullOrWhiteSpace(query) ? "(empty)" : query;
        }
    }

    private bool HasInputControls()
    {
        return IcTextBox is not null &&
               DiffTextBox is not null &&
               RegTextBox is not null &&
               CadTextBox is not null &&
               AnchorTextBox is not null &&
               FuzzyTextBox is not null &&
               StatusComboBox is not null;
    }
}
