// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

internal sealed record CoordinatePreviewImageInfo(int Width, int Height)
{
    public string SummaryText => string.Format(CultureInfo.InvariantCulture, "Preview: {0} × {1} px · PNG / BMP", Width, Height);
}
