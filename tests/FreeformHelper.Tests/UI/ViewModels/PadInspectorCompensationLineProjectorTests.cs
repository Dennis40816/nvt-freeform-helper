// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorCompensationLineProjectorTests
{
    [Fact]
    public void Build_WithoutValues_ReturnsOnlyUnavailableAndNotchRows()
    {
        const string rowSummary = "Anchor REG4808, freeform=XYWay.";

        var lines = PadInspectorCompensationLineProjector.Build(rowSummary, null);

        PadInspectorDisplayLine[] expected =
        [
            new("Notch 2.2", "Unavailable", IsEmphasized: true, Tone: PadInspectorLineTone.Warning),
            new("Notch rows", rowSummary),
        ];
        Assert.Equal(expected, lines);
    }

    [Theory]
    [InlineData(true, false, PadInspectorLineTone.Key, PadInspectorLineTone.Normal)]
    [InlineData(true, true, PadInspectorLineTone.Key, PadInspectorLineTone.Warning)]
    [InlineData(false, false, PadInspectorLineTone.Warning, PadInspectorLineTone.Normal)]
    [InlineData(false, true, PadInspectorLineTone.Warning, PadInspectorLineTone.Warning)]
    public void Build_WithReasonAndFreeform_PreservesLineOrderValuesAndIndependentTones(
        bool isToFullEnabled,
        bool hasOverflowRisk,
        PadInspectorLineTone toFullTone,
        PadInspectorLineTone combinedTone)
    {
        const string rowSummary = "Anchor REG4808, freeform=XYWay.";
        const string reason = "Expansion applied on clear path. (EXPAND_CLEAR_PATH)";
        var toFullValue = isToFullEnabled ? "150.0 % (Enabled)" : "100.0 % (Disabled)";
        var combinedValue = hasOverflowRisk ? "300.00 % (overflow risk >255%)" : "120.00 %";
        var values = new PadInspectorCompensationValues(
            IsToFullEnabled: isToFullEnabled,
            ToFullValueText: toFullValue,
            ToFullReasonFullText: reason,
            ToRegularValueText: "80.0 %",
            CombinedValueText: combinedValue,
            HasCombinedOverflowRisk: hasOverflowRisk);

        var lines = PadInspectorCompensationLineProjector.Build(rowSummary, values);

        PadInspectorDisplayLine[] expected =
        [
            new("To Full", toFullValue, IsEmphasized: true, Tone: toFullTone),
            new("Reason", reason, IsEmphasized: true, Tone: toFullTone),
            new("Freeform", "XYWay", IsEmphasized: true, Tone: PadInspectorLineTone.Key,
                FreeformDirection: PadInspectorFreeformDirection.XYWay),
            new("To Regular", "80.0 %"),
            new("Combined", combinedValue, IsEmphasized: hasOverflowRisk, Tone: combinedTone),
            new("Notch rows", rowSummary),
        ];
        Assert.Equal(expected, lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Build_WithBlankReason_OmitsReasonAndKeepsFreeform(string reason)
    {
        const string rowSummary = "Anchor REG4808, freeform=XYWay.";
        var values = new PadInspectorCompensationValues(
            IsToFullEnabled: true,
            ToFullValueText: "150.0 % (Enabled)",
            ToFullReasonFullText: reason,
            ToRegularValueText: "80.0 %",
            CombinedValueText: "120.00 %",
            HasCombinedOverflowRisk: false);

        var lines = PadInspectorCompensationLineProjector.Build(rowSummary, values);

        PadInspectorDisplayLine[] expected =
        [
            new("To Full", "150.0 % (Enabled)", IsEmphasized: true, Tone: PadInspectorLineTone.Key),
            new("Freeform", "XYWay", IsEmphasized: true, Tone: PadInspectorLineTone.Key,
                FreeformDirection: PadInspectorFreeformDirection.XYWay),
            new("To Regular", "80.0 %"),
            new("Combined", "120.00 %"),
            new("Notch rows", rowSummary),
        ];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Build_WithoutFreeformDirection_OmitsFreeformAndKeepsReason()
    {
        const string rowSummary = "Notch rows: 1 (v2.2)";
        const string reason = "Expansion applied on clear path. (EXPAND_CLEAR_PATH)";
        var values = new PadInspectorCompensationValues(
            IsToFullEnabled: true,
            ToFullValueText: "150.0 % (Enabled)",
            ToFullReasonFullText: reason,
            ToRegularValueText: "80.0 %",
            CombinedValueText: "120.00 %",
            HasCombinedOverflowRisk: false);

        var lines = PadInspectorCompensationLineProjector.Build(rowSummary, values);

        PadInspectorDisplayLine[] expected =
        [
            new("To Full", "150.0 % (Enabled)", IsEmphasized: true, Tone: PadInspectorLineTone.Key),
            new("Reason", reason, IsEmphasized: true, Tone: PadInspectorLineTone.Key),
            new("To Regular", "80.0 %"),
            new("Combined", "120.00 %"),
            new("Notch rows", rowSummary),
        ];
        Assert.Equal(expected, lines);
    }
}
