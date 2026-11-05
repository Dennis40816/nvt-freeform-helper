// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageOutputPathsTests
{
    private const string Folder = "out";

    [Fact]
    public void OneJobWritesToTheChosenPath()
    {
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "A" }, LayerImageOutputMode.OneImage, "panel");
        string chosen = Path.Combine(Folder, "panel.png");

        var paths = LayerImageOutputPaths.Resolve(jobs, Folder, ".png", chosen, static _ => true);

        Assert.Equal(new List<string> { chosen }, paths);
    }

    [Fact]
    public void SeveralJobsKeepTheirNamesWhenNothingExists()
    {
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "A", "B" }, LayerImageOutputMode.OnePerLayer, "panel");

        var paths = LayerImageOutputPaths.Resolve(jobs, Folder, ".png", Path.Combine(Folder, "panel.png"), static _ => false);

        Assert.Equal(jobs.Select(job => Path.Combine(Folder, job.FileStem + ".png")), paths);
    }

    [Fact]
    public void AnExistingFileGetsANumberSuffixAndIsNotReused()
    {
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "A", "B" }, LayerImageOutputMode.OnePerLayer, "panel");
        string taken = Path.Combine(Folder, jobs[0].FileStem + ".png");
        string takenAgain = Path.Combine(Folder, jobs[0].FileStem + " (2).png");

        var paths = LayerImageOutputPaths.Resolve(
            jobs, Folder, ".png", Path.Combine(Folder, "panel.png"), path => path == taken || path == takenAgain);

        Assert.Equal(Path.Combine(Folder, jobs[0].FileStem + " (3).png"), paths[0]);
        Assert.Equal(Path.Combine(Folder, jobs[1].FileStem + ".png"), paths[1]);
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
