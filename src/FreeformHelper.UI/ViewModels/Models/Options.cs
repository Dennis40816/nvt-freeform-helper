// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using NotchExportFileType = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.NotchExportFileType;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Represents an option for selecting a scan order in the UI.
/// </summary>
/// <param name="Value">The <see cref="ScanOrder"/> enumeration value.</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public readonly record struct ScanOrderOption(ScanOrder Value, string Display);

/// <summary>
/// Represents an option for selecting a grid alignment mode in the UI.
/// </summary>
/// <param name="Value">The <see cref="GridAlignmentMode"/> enumeration value.</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public readonly record struct GridAlignmentOption(GridAlignmentMode Value, string Display);

/// <summary>
/// Represents an option for selecting how regular pads are sourced.
/// </summary>
/// <param name="Value">The <see cref="RegularSourceMode"/> enumeration value.</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public readonly record struct RegularSourceModeOption(RegularSourceMode Value, string Display);

/// <summary>
/// Represents an option for selecting a DXF layer to use as grid bounds.
/// </summary>
/// <param name="Name">The layer name (null means auto/visible layers).</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public sealed record BoundLayerOption(string? Name, string Display);

/// <summary>
/// Represents an option for selecting a DXF layer as regular source.
/// </summary>
/// <param name="Name">The layer name (null means no layer selected).</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public sealed record RegularSourceLayerOption(string? Name, string Display);

/// <summary>
/// Represents an option for selecting the minimum application log level in the UI.
/// </summary>
/// <param name="Value">The NLog level name (for example, "Info").</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
public readonly record struct LogLevelOption(string Value, string Display);

/// <summary>
/// Represents an option for selecting CAD auto CAD Output FW Diff assignment mode.
/// </summary>
/// <param name="Value">The <see cref="CadOutputFwDiffAutoMode"/> value.</param>
/// <param name="Display">The human-readable string for display in the UI.</param>
/// <param name="Description">The rationale and tradeoff shown in hover/help text.</param>
public readonly record struct CadOutputFwDiffAutoModeOption(CadOutputFwDiffAutoMode Value, string Display, string Description);

/// <summary>
/// Represents an option for selecting notch export file type.
/// </summary>
/// <param name="Value">Export file type value.</param>
/// <param name="Display">Human-readable label.</param>
/// <param name="Extension">File extension (without dot).</param>
/// <param name="PinnedVersion">Single-version export scope implied by this format, if any.</param>
public readonly record struct NotchExportFileTypeOption(
    NotchExportFileType Value,
    string Display,
    string Extension,
    NotchAlgorithmVersion? PinnedVersion = null);

/// <summary>
/// Represents an option for notch C-export profile.
/// </summary>
/// <param name="Value">Export profile value.</param>
/// <param name="Display">Human-readable label.</param>
public readonly record struct NotchExportProfileOption(NotchExportProfile Value, string Display);

/// <summary>
/// Represents an option for Step3 notch compensation model selection.
/// </summary>
/// <param name="Value">Compensation model value.</param>
/// <param name="Display">Human-readable label.</param>
/// <param name="Description">One-line behavior summary.</param>
public readonly record struct NotchCompensationModelOption(
    NotchCompensationModel Value,
    string Display,
    string Description);
