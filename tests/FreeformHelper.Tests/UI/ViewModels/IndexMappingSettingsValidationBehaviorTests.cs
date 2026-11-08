// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingSettingsValidationBehaviorTests
{
    [Theory]
    [InlineData(nameof(IndexMappingSettings.CadOutputFwDiffAutoMode), 2, "CadOutputFwDiffAutoMode is invalid.")]
    [InlineData(nameof(IndexMappingSettings.CadOutputFwDiffStartIndex), -1, "CadOutputFwDiffStartIndex must be in [0,1000000].")]
    [InlineData(nameof(IndexMappingSettings.CadOutputFwDiffStartIndex), 1000001, "CadOutputFwDiffStartIndex must be in [0,1000000].")]
    [InlineData(nameof(IndexMappingSettings.WeightIou), -0.1, "WeightIou must be non-negative.")]
    [InlineData(nameof(IndexMappingSettings.WeightCentroidDistance), -0.1, "WeightCentroidDistance must be non-negative.")]
    [InlineData(nameof(IndexMappingSettings.WeightAreaRatio), -0.1, "WeightAreaRatio must be non-negative.")]
    [InlineData(nameof(IndexMappingSettings.LowConfidenceThreshold), -0.1, "LowConfidenceThreshold must be in [0,1].")]
    [InlineData(nameof(IndexMappingSettings.LowConfidenceThreshold), 1.1, "LowConfidenceThreshold must be in [0,1].")]
    [InlineData(nameof(IndexMappingSettings.AmbiguousMargin), -0.1, "AmbiguousMargin must be in [0,1].")]
    [InlineData(nameof(IndexMappingSettings.AmbiguousMargin), 1.1, "AmbiguousMargin must be in [0,1].")]
    [InlineData(nameof(IndexMappingSettings.CandidatePaddingCells), -1, "CandidatePaddingCells must be in [0,10].")]
    [InlineData(nameof(IndexMappingSettings.CandidatePaddingCells), 11, "CandidatePaddingCells must be in [0,10].")]
    [InlineData(nameof(IndexMappingSettings.CandidateNumber), -1, "CandidateNumber must be in [0,400].")]
    [InlineData(nameof(IndexMappingSettings.DiagnosticTopK), 0, "DiagnosticTopK must be in (0,10].")]
    [InlineData(nameof(IndexMappingSettings.DiagnosticTopK), 11, "DiagnosticTopK must be in (0,10].")]
    [InlineData(nameof(IndexMappingSettings.MaxSampleIssues), 0, "MaxSampleIssues must be in (0,1000].")]
    [InlineData(nameof(IndexMappingSettings.MaxSampleIssues), 1001, "MaxSampleIssues must be in (0,1000].")]
    [InlineData(nameof(IndexMappingSettings.MaxReportIssues), 0, "MaxReportIssues must be in (0,20000].")]
    [InlineData(nameof(IndexMappingSettings.MaxReportIssues), 20001, "MaxReportIssues must be in (0,20000].")]
    public void ValidateOrThrow_InvalidSetting_IdentifiesTheRejectedInput(string propertyName, double value, string expectedMessage)
    {
        var settings = CreateInvalidSettings(propertyName, value);

        var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateOrThrow_InclusiveLimits_AcceptsDisabledWeightsAndBothAssignmentModes(bool useUpperLimits)
    {
        var settings = new IndexMappingSettings
        {
            CadOutputFwDiffAutoMode = useUpperLimits ? CadOutputFwDiffAutoMode.BestMatchDirect : CadOutputFwDiffAutoMode.StrictUnique,
            CadOutputFwDiffStartIndex = useUpperLimits ? 1_000_000 : 0,
            WeightIou = 0,
            WeightCentroidDistance = 0,
            WeightAreaRatio = 0,
            LowConfidenceThreshold = useUpperLimits ? 1 : 0,
            AmbiguousMargin = useUpperLimits ? 1 : 0,
            CandidatePaddingCells = useUpperLimits ? 10 : 0,
            CandidateNumber = useUpperLimits ? 400 : 0,
            DiagnosticTopK = useUpperLimits ? 10 : 1,
            MaxSampleIssues = useUpperLimits ? 1000 : 1,
            MaxReportIssues = useUpperLimits ? 20000 : 1,
        };

        var error = Record.Exception(settings.ValidateOrThrow);

        Assert.Null(error);
    }

    private static IndexMappingSettings CreateInvalidSettings(string propertyName, double value)
    {
        return propertyName switch
        {
            nameof(IndexMappingSettings.CadOutputFwDiffAutoMode) => new() { CadOutputFwDiffAutoMode = (CadOutputFwDiffAutoMode)(int)value },
            nameof(IndexMappingSettings.CadOutputFwDiffStartIndex) => new() { CadOutputFwDiffStartIndex = (int)value },
            nameof(IndexMappingSettings.WeightIou) => new() { WeightIou = value },
            nameof(IndexMappingSettings.WeightCentroidDistance) => new() { WeightCentroidDistance = value },
            nameof(IndexMappingSettings.WeightAreaRatio) => new() { WeightAreaRatio = value },
            nameof(IndexMappingSettings.LowConfidenceThreshold) => new() { LowConfidenceThreshold = value },
            nameof(IndexMappingSettings.AmbiguousMargin) => new() { AmbiguousMargin = value },
            nameof(IndexMappingSettings.CandidatePaddingCells) => new() { CandidatePaddingCells = (int)value },
            nameof(IndexMappingSettings.CandidateNumber) => new() { CandidateNumber = (int)value },
            nameof(IndexMappingSettings.DiagnosticTopK) => new() { DiagnosticTopK = (int)value },
            nameof(IndexMappingSettings.MaxSampleIssues) => new() { MaxSampleIssues = (int)value },
            nameof(IndexMappingSettings.MaxReportIssues) => new() { MaxReportIssues = (int)value },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
        };
    }
}
