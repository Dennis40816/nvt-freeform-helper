// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Provides parameter-only pad draw-density decisions.</summary>
public static class PadDrawDensityPolicy
{
    /// <summary>Returns the regular pad decimation step for the captured display settings and zoom.</summary>
    public static int RegularStep(bool showRegular, bool lowDetailMode, double threshold, double zoom, double maxStepResource)
    {
        if (!showRegular || !lowDetailMode)
        {
            return 1;
        }

        var maxStep = Math.Max(1, (int)Math.Round(maxStepResource));
        var zoomRatio = threshold / Math.Max(zoom, 1e-6);
        return Math.Clamp((int)Math.Ceiling(zoomRatio), 1, maxStep);
    }

    /// <summary>Returns the CAD decimation step for candidates that are neither selected nor highlighted.</summary>
    public static int CadStep(int unselectedCandidateCount, double maxVisibleUnselectedResource)
    {
        var maxVisibleUnselected = Math.Max(100, (int)Math.Round(maxVisibleUnselectedResource));
        return Math.Max(1, (int)Math.Ceiling(unselectedCandidateCount / (double)maxVisibleUnselected));
    }

    /// <summary>Reports whether an unselected regular pad is skipped by the row and column modulo rule.</summary>
    public static bool IsRegularDecimated(bool isSelected, int row, int col, int step)
    {
        if (!isSelected && step > 1)
        {
            if ((Math.Abs(row) % step) != 0 && (Math.Abs(col) % step) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
