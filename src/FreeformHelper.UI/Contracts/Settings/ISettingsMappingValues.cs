// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Projects existing live values through their original setters; implementations add no storage.</summary>
internal interface ISettingsMappingValues
{
    decimal MatchThreshold { get; set; }
    decimal FreeformAxisThreshold { get; set; }
    decimal MappingWeightIou { get; set; }
    decimal MappingWeightCentroidDistance { get; set; }
    decimal MappingWeightAreaRatio { get; set; }
    decimal MappingLowConfidenceThreshold { get; set; }
    decimal MappingAmbiguousMargin { get; set; }
    decimal MappingCandidatePaddingCells { get; set; }
    decimal MappingCandidateNumber { get; set; }
    decimal CadOutputFwDiffIndexStart { get; set; }
    decimal CadOutputFwDiffIndexAnchorCadId { get; set; }
    CadOutputFwDiffAutoMode CadOutputFwDiffAutoMode { get; set; }
    bool EnableAutoDetectXy { get; set; }
    bool EnableFreeformEdgeSpecialization { get; set; }
    bool ShowDiffIndexOverlay { get; set; }
}
