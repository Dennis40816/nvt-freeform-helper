using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportColumnFilterOptionViewModel : ObservableObject
{
    public NotchExportColumnFilterOptionViewModel(string key, string display, bool isSelected)
    {
        Key = key ?? string.Empty;
        Display = display ?? string.Empty;
        _isSelected = isSelected;
    }

    public string Key { get; }
    public string Display { get; }

    [ObservableProperty] private bool _isSelected;
}

public sealed partial class NotchExportColumnFilterDialogViewModel : ObservableObject
{
    private const int RangeModeSuggestedThreshold = 48;
    private readonly ObservableCollection<NotchExportColumnFilterOptionViewModel> _options;

    public NotchExportColumnFilterDialogViewModel(
        NotchExportColumnFilterField field,
        string titleText,
        IReadOnlyList<NotchExportColumnFilterOptionViewModel> options)
    {
        Field = field;
        TitleText = titleText ?? "Column filter";
        SupportsRangeInput = field is NotchExportColumnFilterField.RowNumber or NotchExportColumnFilterField.Ic or NotchExportColumnFilterField.Diff;
        _options = new ObservableCollection<NotchExportColumnFilterOptionViewModel>(
            options ?? Array.Empty<NotchExportColumnFilterOptionViewModel>());
        Options = new ReadOnlyObservableCollection<NotchExportColumnFilterOptionViewModel>(_options);

        foreach (var option in Options)
        {
            option.PropertyChanged += (_, _) => UpdateSummary();
        }

        if (SupportsRangeInput && Options.Count >= RangeModeSuggestedThreshold)
        {
            if (TryGetNumericBounds(out var min, out var max))
            {
                _rangeFromText = min.ToString(CultureInfo.InvariantCulture);
                _rangeToText = max.ToString(CultureInfo.InvariantCulture);
                _useRangeFilter = true;
            }
        }

        UpdateSummary();
    }

    public NotchExportColumnFilterField Field { get; }
    public string TitleText { get; }
    public bool SupportsRangeInput { get; }
    public bool IsRangeSuggested => SupportsRangeInput && Options.Count >= RangeModeSuggestedThreshold;
    public bool CanShowValueList => !SupportsRangeInput || !UseRangeFilter;
    public ReadOnlyObservableCollection<NotchExportColumnFilterOptionViewModel> Options { get; }
    [ObservableProperty] private string _summaryText = "0/0 selected";
    [ObservableProperty] private bool _useRangeFilter;
    [ObservableProperty] private string _rangeFromText = string.Empty;
    [ObservableProperty] private string _rangeToText = string.Empty;

    public void SelectAll()
    {
        UseRangeFilter = false;
        foreach (var option in Options)
        {
            option.IsSelected = true;
        }

        UpdateSummary();
    }

    public void ClearAll()
    {
        UseRangeFilter = false;
        foreach (var option in Options)
        {
            option.IsSelected = false;
        }

        UpdateSummary();
    }

    public IReadOnlyList<string> GetSelectedKeys()
    {
        if (SupportsRangeInput && UseRangeFilter)
        {
            var hasMin = int.TryParse(RangeFromText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var min);
            var hasMax = int.TryParse(RangeToText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max);
            if (hasMin || hasMax)
            {
                return Options
                    .Where(option => TryExtractNumericValue(option.Key, out var value) &&
                                     (!hasMin || value >= min) &&
                                     (!hasMax || value <= max))
                    .Select(static option => option.Key)
                    .ToArray();
            }
        }

        return Options
            .Where(static option => option.IsSelected)
            .Select(static option => option.Key)
            .ToArray();
    }

    public IReadOnlyList<string> GetAllKeys()
    {
        return Options
            .Select(static option => option.Key)
            .ToArray();
    }

    private void UpdateSummary()
    {
        if (SupportsRangeInput && UseRangeFilter)
        {
            var fromText = string.IsNullOrWhiteSpace(RangeFromText) ? "-" : RangeFromText.Trim();
            var toText = string.IsNullOrWhiteSpace(RangeToText) ? "-" : RangeToText.Trim();
            SummaryText = $"Range: {fromText} ~ {toText}";
            return;
        }

        var selected = GetSelectedKeys().Count;
        SummaryText = $"{selected}/{Options.Count} selected";
    }

    partial void OnUseRangeFilterChanged(bool value)
    {
        OnPropertyChanged(nameof(CanShowValueList));
        UpdateSummary();
    }

    partial void OnRangeFromTextChanged(string value)
    {
        UpdateSummary();
    }

    partial void OnRangeToTextChanged(string value)
    {
        UpdateSummary();
    }

    private bool TryExtractNumericValue(string key, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var text = key.Trim();
        if (Field == NotchExportColumnFilterField.RowNumber)
        {
            text = text.TrimStart('#');
        }
        else if (Field == NotchExportColumnFilterField.Ic)
        {
            text = text.Replace("IC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private bool TryGetNumericBounds(out int min, out int max)
    {
        min = 0;
        max = 0;
        var found = false;
        foreach (var option in Options)
        {
            if (!TryExtractNumericValue(option.Key, out var value))
            {
                continue;
            }

            if (!found)
            {
                min = value;
                max = value;
                found = true;
                continue;
            }

            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        return found;
    }
}
