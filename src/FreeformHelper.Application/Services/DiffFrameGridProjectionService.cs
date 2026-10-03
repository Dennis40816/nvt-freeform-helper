using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class DiffFrameGridProjectionService
{
    public static DiffFrameGridProjectionResult ProjectFrame(
        DiffFrameCsvFrame frame,
        RegularGrid grid,
        DiffFrameGridRowOrigin rowOrigin = DiffFrameGridRowOrigin.TopRowIsRegularRow0)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(grid);

        if (frame.RowCount != grid.Rows || frame.ColCount != grid.Cols)
        {
            return DiffFrameGridProjectionResult.Incompatible(
                $"Frame dimensions {frame.RowCount}x{frame.ColCount} do not match grid {grid.Rows}x{grid.Cols}.");
        }

        var cells = new List<DiffFrameGridCellValue>(frame.RowCount * frame.ColCount);
        for (var frameRow = 0; frameRow < frame.RowCount; frameRow++)
        {
            var regularRow = rowOrigin == DiffFrameGridRowOrigin.TopRowIsRegularRow0
                ? frameRow
                : grid.Rows - 1 - frameRow;

            var values = frame.Rows[frameRow];
            for (var frameCol = 0; frameCol < frame.ColCount; frameCol++)
            {
                var pad = grid.GetPad(regularRow, frameCol);
                cells.Add(new DiffFrameGridCellValue(
                    frameRow,
                    frameCol,
                    regularRow,
                    frameCol,
                    pad.RegularPadId,
                    values[frameCol]));
            }
        }

        return new DiffFrameGridProjectionResult(true, null, cells);
    }
}
