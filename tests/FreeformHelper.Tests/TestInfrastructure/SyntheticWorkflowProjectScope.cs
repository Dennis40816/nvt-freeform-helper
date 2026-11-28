// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

internal sealed class SyntheticWorkflowProjectScope : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();

    public string PathFor(string name) => Path.Combine(_directory.FullName, name);

    public async Task<FreeformHelperViewModel> LoadAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var projectPath = PathFor("project.json");
        var project = new ProjectFile
        {
            EmbeddedDxfName = "synthetic.dxf",
            EmbeddedDxf = Encoding.UTF8.GetBytes(BuildDxf()),
            Settings = new ProjectSettings
            {
                Grid = new GridSettings
                {
                    XChannels = 3,
                    YChannels = 3,
                    ActiveAreaWidth = 30,
                    ActiveAreaHeight = 30,
                    AlignmentMode = GridAlignmentMode.FromPanelAa,
                },
            },
            UiSnapshot = new ProjectUiSnapshot
            {
                Grid = new UiGridSnapshot
                {
                    CascadeCount = 1,
                    IcX = [3],
                    IcY = [3],
                    TotalX = 3,
                    TotalY = 3,
                    ActiveAreaWidthMm = 30,
                    ActiveAreaHeightMm = 30,
                    GridPaddingPercent = 0,
                    AlignmentMode = nameof(GridAlignmentMode.FromPanelAa),
                },
                View = new UiViewSnapshot { NotchPreviewAutoPlayEnabled = false },
            },
        };
        await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(PathFor("app.json")))
        {
            PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            NotchPreviewAutoPlayEnabled = false,
        };
        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Equal(4, vm.CadPads.Count);
        Assert.Equal(9, vm.RegularPads.Count);
        return vm;
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    private static string BuildDxf()
    {
        (string Layer, int MinX, int MinY, int MaxX, int MaxY)[] pads =
        [
            ("frame", 0, 0, 1, 1),
            ("signal-a", 12, 12, 18, 18),
            ("signal-b", 22, 12, 28, 18),
            ("frame", 29, 29, 30, 30),
        ];
        var builder = new StringBuilder("0\nSECTION\n2\nENTITIES\n");
        foreach (var pad in pads)
        {
            builder.Append("0\nLWPOLYLINE\n8\n").AppendLine(pad.Layer);
            builder.Append("70\n1\n90\n4\n");
            AppendVertex(builder, pad.MinX, pad.MinY);
            AppendVertex(builder, pad.MaxX, pad.MinY);
            AppendVertex(builder, pad.MaxX, pad.MaxY);
            AppendVertex(builder, pad.MinX, pad.MaxY);
        }

        return builder.Append("0\nENDSEC\n0\nEOF\n").ToString();
    }

    private static void AppendVertex(StringBuilder builder, int x, int y)
    {
        builder.AppendLine("10").AppendLine(x.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("20").AppendLine(y.ToString(CultureInfo.InvariantCulture));
    }
}
