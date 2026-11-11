// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadExport;
using FreeformHelper.UI.Services;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageFileWriterTests
{
    [Theory]
    [InlineData(LayerImageOutputMode.OneImage)]
    [InlineData(LayerImageOutputMode.OnePerLayer)]
    public async Task WriteSingleJobUsesExactlyTheChosenPath(LayerImageOutputMode mode)
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-single-", ".png");
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "A" }, mode, Path.GetFileNameWithoutExtension(chosenPath));
        var size = CreateSize();
        try
        {
            var paths = LayerImageFileWriter.Write(jobs, CreatePads, size, 2m, false, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath);

            Assert.Equal(chosenPath, Assert.Single(paths));
            Assert.True(File.Exists(chosenPath));
            using var bitmap = SKBitmap.Decode(chosenPath);
            Assert.NotNull(bitmap);
            Assert.Equal(size.OutputWidth, bitmap.Width);
            Assert.Equal(size.OutputHeight, bitmap.Height);
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(chosenPath);
        }
    }

    [Theory]
    [InlineData(DxfLayerImageFormat.Png, ".png")]
    [InlineData(DxfLayerImageFormat.Bmp, ".bmp")]
    [InlineData(DxfLayerImageFormat.Jpg, ".jpg")]
    public async Task WriteThreeJobsUsesOrderedStemsAndFormatExtension(DxfLayerImageFormat format, string extension)
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-stems-", ".chosen");
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "B", "A", "C" }, LayerImageOutputMode.OnePerLayer, Path.GetFileNameWithoutExtension(chosenPath));
        string folder = Path.GetDirectoryName(chosenPath)!;
        var expected = jobs.Select(job => Path.Combine(folder, job.FileStem + extension)).ToArray();
        try
        {
            var paths = LayerImageFileWriter.Write(jobs, CreatePads, CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(format), chosenPath);

            Assert.Equal(expected, paths);
            Assert.All(paths, path => Assert.True(File.Exists(path)));
            Assert.False(File.Exists(chosenPath));
        }
        finally
        {
            foreach (string file in expected)
            {
                LayerImageTestSupport.DeleteTempFile(file);
            }

            LayerImageTestSupport.DeleteTempFile(chosenPath);
        }
    }

    [Fact]
    public void WriteKeepsAnExistingSiblingFileAndUsesAnUnusedName()
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-keep-", ".png");
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "A", "B" }, LayerImageOutputMode.OnePerLayer, Path.GetFileNameWithoutExtension(chosenPath));
        string folder = Path.GetDirectoryName(chosenPath)!;
        string existing = Path.Combine(folder, jobs[0].FileStem + ".png");
        byte[] original = [1, 2, 3];
        TestFiles.WriteBytes(existing, original);
        var written = new List<string>();
        try
        {
            var paths = LayerImageFileWriter.Write(
                jobs, CreatePads, CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath);
            written.AddRange(paths);

            Assert.Equal(original, TestFiles.ReadBytes(existing));
            Assert.Equal(Path.Combine(folder, jobs[0].FileStem + " (2).png"), paths[0]);
            Assert.Equal(Path.Combine(folder, jobs[1].FileStem + ".png"), paths[1]);
            Assert.All(paths, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(existing);
            foreach (string file in written)
            {
                LayerImageTestSupport.DeleteTempFile(file);
            }
        }
    }

    [Fact]
    public void WriteKeepsASiblingFileThatAppearsAfterTheNamesAreChosen()
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-race-", ".png");
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "A", "B" }, LayerImageOutputMode.OnePerLayer, Path.GetFileNameWithoutExtension(chosenPath));
        string folder = Path.GetDirectoryName(chosenPath)!;
        string racing = Path.Combine(folder, jobs[0].FileStem + ".png");
        byte[] original = [4, 5, 6];
        bool created = false;
        var written = new List<string>();
        try
        {
            // The pad lookup runs after the destinations were resolved and before the file is opened.
            var paths = LayerImageFileWriter.Write(
                jobs,
                name =>
                {
                    if (!created)
                    {
                        TestFiles.WriteBytes(racing, original);
                        created = true;
                    }

                    return CreatePads(name);
                },
                CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath);
            written.AddRange(paths);

            Assert.Equal(original, TestFiles.ReadBytes(racing));
            Assert.Equal(Path.Combine(folder, jobs[0].FileStem + " (2).png"), paths[0]);
            Assert.Equal(Path.Combine(folder, jobs[1].FileStem + ".png"), paths[1]);
            Assert.All(paths, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(racing);
            foreach (string file in written)
            {
                LayerImageTestSupport.DeleteTempFile(file);
            }
        }
    }

    [Theory]
    [InlineData(DxfLayerImageFormat.Bmp, ".bmp", new byte[] { 0x42, 0x4D })]
    [InlineData(DxfLayerImageFormat.Jpg, ".jpg", new byte[] { 0xFF, 0xD8 })]
    public async Task WriteEncodesTheRequestedFormat(DxfLayerImageFormat format, string extension, byte[] signature)
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-format-", extension);
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "A" }, LayerImageOutputMode.OneImage, Path.GetFileNameWithoutExtension(chosenPath));
        try
        {
            LayerImageFileWriter.Write(jobs, CreatePads, CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(format), chosenPath);

            await using var stream = File.OpenRead(chosenPath);
            var actual = new byte[signature.Length];
            await stream.ReadExactlyAsync(actual, TestContext.Current.CancellationToken);
            Assert.Equal(signature, actual);
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(chosenPath);
        }
    }

    [Fact]
    public async Task WriteCombinedJobMatchesRendererAndPreservesLayerOrder()
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-combined-", ".png");
        var names = new List<string> { "B", "A" };
        var jobs = LayerImageOutputPlanner.Plan(names, LayerImageOutputMode.OneImage, Path.GetFileNameWithoutExtension(chosenPath));
        var visited = new List<string>();
        var size = CreateSize();
        try
        {
            LayerImageFileWriter.Write(jobs, name =>
            {
                visited.Add(name);
                return CreatePads(name);
            }, size, 3.5m, true, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath);

            Assert.Equal(names, visited);
            var layers = names.Select(name => new LayerImageLayer(name, CreatePads(name))).ToArray();
            using var expected = LayerImageRenderer.Render(LayerImageRenderRequest.FromSize(layers, size, 3.5m, true));
            using var actual = SKBitmap.Decode(chosenPath);
            Assert.NotNull(actual);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.Pixels, actual.Pixels);
        }
        finally
        {
            LayerImageTestSupport.DeleteTempFile(chosenPath);
        }
    }

    [Fact]
    public async Task WriteCreatesTheDestinationFolder()
    {
        var directory = TestFiles.CreateTempDirectory();
        string chosenPath = Path.Combine(directory.FullName, "new", "chosen.png");
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "A" }, LayerImageOutputMode.OneImage, "chosen");
        try
        {
            LayerImageFileWriter.Write(jobs, CreatePads, CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath);

            Assert.True(File.Exists(chosenPath));
        }
        finally
        {
            LayerImageTestSupport.DeleteTempDirectory(directory.FullName);
        }
    }

    [Fact]
    public async Task WriteLeavesEarlierFilesAndPropagatesTheOriginalFailure()
    {
        string chosenPath = TestFiles.NewTempFilePath("layer-image-failure-", ".png");
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "A", "B", "C" }, LayerImageOutputMode.OnePerLayer, Path.GetFileNameWithoutExtension(chosenPath));
        var expected = jobs.Select(job => Path.Combine(Path.GetDirectoryName(chosenPath)!, job.FileStem + ".png")).ToArray();
        var failure = new InvalidOperationException("Layer lookup failed.");
        try
        {
            var actual = Assert.Throws<InvalidOperationException>(() => LayerImageFileWriter.Write(
                jobs, name => name switch { "B" => throw failure, _ => CreatePads(name) },
                CreateSize(), 2m, false, LayerImageTestSupport.EncodingFor(DxfLayerImageFormat.Png), chosenPath));

            Assert.Same(failure, actual);
            Assert.True(File.Exists(expected[0]));
            Assert.False(File.Exists(expected[1]));
            Assert.False(File.Exists(expected[2]));
        }
        finally
        {
            foreach (string file in expected)
            {
                LayerImageTestSupport.DeleteTempFile(file);
            }

            LayerImageTestSupport.DeleteTempFile(chosenPath);
        }
    }

    private static LayerImageSize CreateSize() =>
        LayerImageSize.CreateDefault(2, 1, 64, 48).WithPaddingX(5).WithPaddingY(3);

    private static IReadOnlyList<CadPad> CreatePads(string layer) => new[]
    {
        LayerImageTestSupport.CreateSquare(1, layer, layer switch { "B" => 20, "C" => 40, _ => 0 }, 0, 10),
    };
}
