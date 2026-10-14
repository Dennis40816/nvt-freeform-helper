// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SelectionTimingRecorderTests
{
    [Fact]
    public void Record_ReplacesAllFourDurationsTogether()
    {
        var recorder = new SelectionTimingRecorder();
        Assert.Equal(SelectionTimings.Empty, recorder.Last);

        recorder.Record(summaryMs: 1, inspectorMs: 2, notchPreviewMs: 3, totalMs: 6);
        Assert.Equal(new SelectionTimings(1, 2, 3, 6), recorder.Last);

        recorder.Record(summaryMs: 4, inspectorMs: 5, notchPreviewMs: 6, totalMs: 15);
        Assert.Equal(new SelectionTimings(4, 5, 6, 15), recorder.Last);
    }
}
