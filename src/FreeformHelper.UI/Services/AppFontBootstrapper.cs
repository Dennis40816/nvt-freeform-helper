// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Nvt.Core.Fonts;

namespace FreeformHelper.UI.Services;

/// <summary>Shares the Core font bootstrap between the desktop app and the headless test host.</summary>
internal static class AppFontBootstrapper
{
    // Core role styles name the font as "fonts:Inter#Inter", which needs the Inter collection.
    public static AppBuilder WithAppFonts(this AppBuilder builder) => builder.WithInterFont().WithNvtCoreFonts();
}
