// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Represents the latest selection pipeline timing breakdown captured by the ViewModel.
/// </summary>
public readonly record struct SelectionTimingSnapshot(
    long SummaryMs,
    long InspectorMs,
    long NotchPreviewMs,
    long TotalMs);
