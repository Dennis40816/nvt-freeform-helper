// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using FreeformHelper.Tests.TestInfrastructure;
using Nvt.Core.Avalonia.Icons;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NvtIconParityTests
{
    [AvaloniaTheory]
    [InlineData("EXPAND_MORE", NvtIcons.ExpandMore)]
    [InlineData("EXPAND_LESS", NvtIcons.ExpandLess)]
    [InlineData("DELETE", NvtIcons.Delete)]
    [InlineData("REMOVE", NvtIcons.Remove)]
    [InlineData("ADD", NvtIcons.Add)]
    [InlineData("ARROW_DOWNWARD", NvtIcons.ArrowDownward)]
    [InlineData("SEARCH", NvtIcons.Search)]
    [InlineData("FILTER_ALT", NvtIcons.FilterAlt)]
    [InlineData("FILTER_ALT_OFF", NvtIcons.FilterAltOff)]
    [InlineData("CONTENT_COPY", NvtIcons.ContentCopy)]
    [InlineData("CLOSE", NvtIcons.Close)]
    [InlineData("VISIBILITY", NvtIcons.Visibility)]
    [InlineData("VISIBILITY_OFF", NvtIcons.VisibilityOff)]
    [InlineData("INFO", NvtIcons.Info)]
    [InlineData("WARNING", NvtIcons.Warning)]
    [InlineData("SHIELD", NvtIcons.Shield)]
    [InlineData("CONSTRUCTION", NvtIcons.Construction)]
    [InlineData("UNDO", NvtIcons.Undo)]
    [InlineData("MERGE", NvtIcons.Merge)]
    [InlineData("CALL_SPLIT", NvtIcons.CallSplit)]
    [InlineData("LAYERS", NvtIcons.Layers)]
    [InlineData("DRIVE_FILE_MOVE", NvtIcons.DriveFileMove)]
    [InlineData("ROTATE_RIGHT", NvtIcons.RotateRight)]
    [InlineData("ADD_CIRCLE", NvtIcons.AddCircle)]
    [InlineData("EDIT", NvtIcons.Edit)]
    [InlineData("FIT_SCREEN", NvtIcons.FitScreen)]
    [InlineData("RESTART_ALT", NvtIcons.RestartAlt)]
    [InlineData("CHEVRON_LEFT", NvtIcons.ChevronLeft)]
    [InlineData("CHEVRON_RIGHT", NvtIcons.ChevronRight)]
    [InlineData("PLAY_ARROW", NvtIcons.PlayArrow)]
    [InlineData("PAUSE", NvtIcons.Pause)]
    [InlineData("REPEAT", NvtIcons.Repeat)]
    [InlineData("SHUFFLE", NvtIcons.Shuffle)]
    [InlineData("SETTINGS", NvtIcons.Settings)]
    [InlineData("STRAIGHTEN", NvtIcons.Straighten)]
    [InlineData("CONTENT_PASTE_OFF", NvtIcons.ContentPasteOff)]
    [InlineData("CONTENT_PASTE", NvtIcons.ContentPaste)]
    [InlineData("DATA_OBJECT", NvtIcons.DataObject)]
    public void OldLigatureAndCoreNameResolveToTheSameGlyph(string oldLigature, string coreGlyph)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Assert.Equal(ShapeGlyph(oldLigature), ShapeGlyph(coreGlyph));
    }

    private static ushort ShapeGlyph(string value)
    {
        var resources = Avalonia.Application.Current!.Resources;
        Assert.True(resources.TryGetResource("Nvt.Font.Icon.Family", null, out var family));
        Assert.True(resources.TryGetResource("IconSizeMd", null, out var size));
        var text = new TextBlock
        {
            Text = value,
            FontFamily = Assert.IsType<FontFamily>(family),
            FontSize = Assert.IsType<double>(size),
            FontFeatures = new FontFeatureCollection
            {
                FontFeature.Parse("liga"),
                FontFeature.Parse("clig"),
            },
        };
        text.Measure(Avalonia.Size.Infinity);
        var run = Assert.Single(Assert.Single(text.TextLayout.TextLines).TextRuns.OfType<ShapedTextRun>());
        Assert.Equal("Material Symbols Outlined", run.GlyphRun.GlyphTypeface.FamilyName);
        var glyph = Assert.Single(run.GlyphRun.GlyphInfos);
        Assert.True(glyph.GlyphIndex != 0, $"Missing icon: {(value.Length == 1 ? $"U+{(int)value[0]:X4}" : value)}");
        return glyph.GlyphIndex;
    }
}
