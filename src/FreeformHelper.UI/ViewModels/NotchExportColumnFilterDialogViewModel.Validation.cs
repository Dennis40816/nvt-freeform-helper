// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportColumnFilterDialogViewModel
{
    public string RangeErrorText => SupportsRangeInput && UseRangeFilter ? RangeInput.Error : string.Empty;
    public bool HasRangeError => RangeErrorText.Length > 0;
    public bool CanApply => !HasRangeError;

    private (int? From, int? To, string Error) RangeInput
    {
        get
        {
            var fromText = RangeFromText?.Trim() ?? string.Empty;
            var toText = RangeToText?.Trim() ?? string.Empty;
            var hasFrom = int.TryParse(fromText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var from);
            var hasTo = int.TryParse(toText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var to);
            var error = fromText.Length > 0 && !hasFrom ? "From must be a number"
                : toText.Length > 0 && !hasTo ? "To must be a number"
                : hasFrom && hasTo && from > to ? "From must be less than or equal to To"
                : string.Empty;
            return (hasFrom ? from : null, hasTo ? to : null, error);
        }
    }
}
