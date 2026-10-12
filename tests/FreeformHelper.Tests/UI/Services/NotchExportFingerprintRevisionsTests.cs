// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportFingerprintRevisionsTests
{
    [Fact]
    public void Constructor_NewInstance_StartsEveryRevisionAtOne()
    {
        var revisions = new NotchExportFingerprintRevisions();

        Assert.Equal((1, 1, 1), (revisions.Cad, revisions.Grid, revisions.Index));
    }

    [Fact]
    public void BumpCad_Called_AdvancesOnlyTheCadRevision()
    {
        var revisions = new NotchExportFingerprintRevisions();

        revisions.BumpCad();

        Assert.Equal((2, 1, 1), (revisions.Cad, revisions.Grid, revisions.Index));
    }

    [Fact]
    public void BumpGrid_Called_AdvancesOnlyTheGridRevision()
    {
        var revisions = new NotchExportFingerprintRevisions();

        revisions.BumpGrid();

        Assert.Equal((1, 2, 1), (revisions.Cad, revisions.Grid, revisions.Index));
    }

    [Fact]
    public void BumpIndex_Called_AdvancesOnlyTheIndexRevision()
    {
        var revisions = new NotchExportFingerprintRevisions();

        revisions.BumpIndex();

        Assert.Equal((1, 1, 2), (revisions.Cad, revisions.Grid, revisions.Index));
    }
}
