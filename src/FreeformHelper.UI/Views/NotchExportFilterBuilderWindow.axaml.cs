using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportFilterBuilderWindow : Window
{
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

        FuzzyTextBox.Text = query ?? string.Empty;
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

    private string BuildQuery()
    {
        if (!HasInputControls())
        {
            return string.Empty;
        }

        var segments = new List<string>();
        Append(segments, "ic", IcTextBox.Text);
        Append(segments, "diff", DiffTextBox.Text);
        Append(segments, "reg", RegTextBox.Text);
        Append(segments, "cad", CadTextBox.Text);
        Append(segments, "a", AnchorTextBox.Text);

        if (StatusComboBox.SelectedItem is ComboBoxItem item)
        {
            var status = item.Content?.ToString() ?? string.Empty;
            var normalized = status switch
            {
                "Linked" => "linked",
                "Warning" => "warning",
                "No CAD" => "nocad",
                "Legacy" => "legacy",
                _ => string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                segments.Add($"status={normalized}");
            }
        }

        var fuzzy = FuzzyTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(fuzzy))
        {
            segments.Add(NeedsQuote(fuzzy) ? $"\"{fuzzy}\"" : fuzzy);
        }

        return string.Join(' ', segments);
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

    private static void Append(List<string> segments, string key, string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        segments.Add($"{key}={text}");
    }

    private static bool NeedsQuote(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                return true;
            }
        }

        return false;
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
