// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal sealed record CoordinatePlannerRenderState(
    CoordinatePlannerSnapshot Snapshot,
    CoordinateArtifactSnapshot ArtifactSnapshot,
    Rect2 PreviewBounds,
    string ActiveAreaSourceText,
    string GuideSourceText,
    CadPad[] CadPads,
    IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerPointRow> AaCornerRows,
    IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerPointRow> CustomArrayCornerRows,
    IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerPointRow> CustomArrayPointRows,
    IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerLineRow> HorizontalGuideRows,
    IReadOnlyList<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerLineRow> VerticalGuideRows,
    IReadOnlyList<CoordinateArtifactRow> ArtifactRows);
