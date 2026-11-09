using System.Security.Cryptography;
using System.Text;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the text that the coordinate planner and the copper path replay hand to the host for saving and copying.
/// The input is a small synthetic project. The tests compare the whole text, so a change in one column, one number
/// format or one line ending fails here. Line endings are compared as written by the platform
/// (<see cref="Environment.NewLine"/>): the check normalizes them to LF only after it has checked that every line
/// break is the platform one.
/// </summary>
public sealed class ExportedFileContentCharacterizationTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] _copperDiffIndices = [10, 11];

    private static readonly string _expectedCopperClipboard = Lf("""
            Step	Center	Max After	Worst	Audit	Status	Diagnostics
            #1	(0.5, 0.5)	Max After 500	REG 0 · IC 1 · FW Diff 10	EMS 1 · net-flow 0 · coverage 0	EMS risk	Missing target diff IC1/diff11 in projected grid.
            #2	(1, 0.5)	Max After 499	REG 1 · IC 2 · FW Diff 11	EMS 1 · net-flow 0 · coverage 0	EMS risk	Missing target diff IC1/diff11 in projected grid.
            #3	(1.5, 0.5)	Max After 1000	REG 1 · IC 2 · FW Diff 11	EMS 1 · net-flow 0 · coverage 0	EMS risk	Missing target diff IC1/diff11 in projected grid.
            """);

    private static readonly string _expectedCopperCsv = Lf("""
            step_index,center_x,center_y,max_after,ems_violations,worst_regular_pad,worst_ic,worst_diff,net_flow_residuals,target_coverage_risks,status,diagnostics
            "0","0.5","0.5","500","1","0","0","10","0","0","EMS risk","Missing target diff IC1/diff11 in projected grid."
            "1","1","0.5","499","1","1","1","11","0","0","EMS risk","Missing target diff IC1/diff11 in projected grid."
            "2","1.5","0.5","1000","1","1","1","11","0","0","EMS risk","Missing target diff IC1/diff11 in projected grid."
            """);

    private static readonly string _expectedCopperJson = Lf("""
            {
              "stepCount": 3,
              "totalEmsViolationCount": 3,
              "totalNetFlowResidualCount": 0,
              "totalTargetCoverageRiskCount": 0,
              "worstStepIndex": 2,
              "rows": [
                {
                  "stepIndex": 0,
                  "centerX": 0.5,
                  "centerY": 0.5,
                  "maxAfterValue": 500,
                  "emsViolationCount": 1,
                  "worstRegularPadId": 0,
                  "worstIcIndex": 0,
                  "worstDiffIndex": 10,
                  "netFlowResidualCount": 0,
                  "targetCoverageRiskCount": 0,
                  "statusText": "EMS risk",
                  "diagnosticsText": "Missing target diff IC1/diff11 in projected grid."
                },
                {
                  "stepIndex": 1,
                  "centerX": 1,
                  "centerY": 0.5,
                  "maxAfterValue": 499,
                  "emsViolationCount": 1,
                  "worstRegularPadId": 1,
                  "worstIcIndex": 1,
                  "worstDiffIndex": 11,
                  "netFlowResidualCount": 0,
                  "targetCoverageRiskCount": 0,
                  "statusText": "EMS risk",
                  "diagnosticsText": "Missing target diff IC1/diff11 in projected grid."
                },
                {
                  "stepIndex": 2,
                  "centerX": 1.5,
                  "centerY": 0.5,
                  "maxAfterValue": 1000,
                  "emsViolationCount": 1,
                  "worstRegularPadId": 1,
                  "worstIcIndex": 1,
                  "worstDiffIndex": 11,
                  "netFlowResidualCount": 0,
                  "targetCoverageRiskCount": 0,
                  "statusText": "EMS risk",
                  "diagnosticsText": "Missing target diff IC1/diff11 in projected grid."
                }
              ]
            }
            """);

    [Fact]
    public async Task CoordinatePlanner_DefaultProject_ExportsCsvAndJsonWithPinnedContentAsync()
    {
        var viewModel = BuildCoordinateViewModel();
        var requests = new List<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest>();
        viewModel.RequestSaveTextFileAsync = request =>
        {
            requests.Add(request);
            return Task.FromResult(true);
        };

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);
        var csvStatus = viewModel.StatusText;
        await viewModel.ExportArtifactsJsonCommand.ExecuteAsync(null);

        Assert.Equal(2, requests.Count);
        Assert.Equal("Exported CSV 19 coordinate row(s).", csvStatus);
        Assert.Equal("Exported JSON 19 coordinate row(s).", viewModel.StatusText);

        var csvRequest = requests[0];
        Assert.Equal("Export Coordinate Artifacts CSV", csvRequest.Title);
        Assert.Equal("coordinate_artifacts.csv", csvRequest.SuggestedFileName);
        Assert.Equal("csv", csvRequest.DefaultExtension);
        Assert.Equal("CSV", csvRequest.FileTypeName);
        Assert.Equal(["*.csv"], csvRequest.Patterns);
        var csv = NormalizeLineBreaks(csvRequest.Content);
        Assert.Equal(
            Lf("""
            key,label,kind,recipe,geometry,pixel_x,pixel_y,pixel_end_x,pixel_end_y,machine_x,machine_y,machine_end_x,machine_end_y,safe_machine_x,safe_machine_y,safe_machine_end_x,safe_machine_end_y,world_x,world_y,world_end_x,world_end_y,unit,source_bounds,source,detail
            "aa-tl","AA TL","Point","AA corners","Point","0","0","","","0","0","","","0","0","","","0","5","","","pixel/mm/world","(0, 0) -> (10, 5)","AA outline: regular grid bounds","Pixel (0, 0); machine (0, 0); safe (0, 0); world (0, 5)."
            "aa-tr","AA TR","Point","AA corners","Point","1000","0","","","100","0","","","100","0","","","10","5","","","pixel/mm/world","(0, 0) -> (10, 5)","AA outline: regular grid bounds","Pixel (1000, 0); machine (100, 0); safe (100, 0); world (10, 5)."
            """),
            string.Join('\n', csv.Split('\n').Take(3)));
        Assert.Equal(6891, csv.Length);
        Assert.Equal(20, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal("b6c4b595483efb0fd8769f50ae7456189cc2447095fd303964196992fd1ce089", Sha256(csv));

        var jsonRequest = requests[1];
        Assert.Equal("Export Coordinate Artifacts JSON", jsonRequest.Title);
        Assert.Equal("coordinate_artifacts.json", jsonRequest.SuggestedFileName);
        Assert.Equal("json", jsonRequest.DefaultExtension);
        Assert.Equal("JSON", jsonRequest.FileTypeName);
        Assert.Equal(["*.json"], jsonRequest.Patterns);
        var json = NormalizeLineBreaks(jsonRequest.Content);
        Assert.StartsWith("{\n  \"activeAreaBounds\": {\n    \"minX\": 0,", json, StringComparison.Ordinal);
        Assert.Equal(17184, json.Length);
        Assert.Equal("7cdab2c82d2263625a04059783d2ccb498e7e8675b7db787ee95e7ba6d24d758", Sha256(json));
    }

    [Fact]
    public async Task CoordinatePlanner_WithCustomPointAndPath_ExportsCustomRowsAndClipboardWithPinnedContentAsync()
    {
        var viewModel = BuildCoordinateViewModel();
        var requests = new List<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest>();
        var clipboard = string.Empty;
        viewModel.RequestSaveTextFileAsync = request =>
        {
            requests.Add(request);
            return Task.FromResult(true);
        };
        viewModel.RequestSetClipboardTextAsync = text =>
        {
            clipboard = text;
            return Task.FromResult(true);
        };
        viewModel.CustomPointLabelText = "Probe A";
        viewModel.CustomPointMachineX = 12.5m;
        viewModel.CustomPointMachineY = 7.25m;
        viewModel.AddCustomPointCommand.Execute(null);
        viewModel.CustomPathLabelText = "Sweep 1";
        viewModel.CustomPathStartMachineX = 10m;
        viewModel.CustomPathStartMachineY = 10m;
        viewModel.CustomPathEndMachineX = 40m;
        viewModel.CustomPathEndMachineY = 20m;
        viewModel.CustomPathStepCount = 3;
        viewModel.AddCustomPathCommand.Execute(null);

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);
        await viewModel.ExportArtifactsJsonCommand.ExecuteAsync(null);
        await viewModel.CopyAllArtifactsCommand.ExecuteAsync(null);

        Assert.Equal("Copied 24 coordinate row(s).", viewModel.StatusText);
        var csv = NormalizeLineBreaks(requests[0].Content);
        Assert.Equal(
            Lf("""
            "custom-point-1","Probe A","Point","Custom point","Point","125","72.5","","","12.5","7.25","","","12.5","7.25","","","1.25","4.275","","","pixel/mm/world","(0, 0) -> (10, 5)","Custom coordinate recipe","Pixel (125, 72.5); machine (12.5, 7.25); safe (12.5, 7.25); world (1.25, 4.275)."
            "custom-path-1","Sweep 1","Path","Custom path","Line","100","100","400","200","10","10","40","20","10","10","40","20","1","4","4","3","pixel/mm/world","(0, 0) -> (10, 5)","Custom coordinate recipe","Source Custom coordinate recipe; bounds (0, 0) -> (10, 5); pixel (100, 100) -> (400, 200); machine (10, 10) -> (40, 20); safe (10, 10) -> (40, 20); world (1, 4) -> (4, 3)."
            "custom-path-1-step-1","Sweep 1 P1","Step","Custom path","Point","100","100","","","10","10","","","10","10","","","1","4","","","pixel/mm/world","(0, 0) -> (10, 5)","Custom coordinate recipe","Pixel (100, 100); machine (10, 10); safe (10, 10); world (1, 4)."
            "custom-path-1-step-2","Sweep 1 P2","Step","Custom path","Point","250","150","","","25","15","","","25","15","","","2.5","3.5","","","pixel/mm/world","(0, 0) -> (10, 5)","Custom coordinate recipe","Pixel (250, 150); machine (25, 15); safe (25, 15); world (2.5, 3.5)."
            "custom-path-1-step-3","Sweep 1 P3","Step","Custom path","Point","400","200","","","40","20","","","40","20","","","4","3","","","pixel/mm/world","(0, 0) -> (10, 5)","Custom coordinate recipe","Pixel (400, 200); machine (40, 20); safe (40, 20); world (4, 3)."
            """),
            string.Join('\n', csv.Split('\n').Skip(20).Take(5)));
        Assert.Equal(8336, csv.Length);
        Assert.Equal("acdf478ca93eceea792604dce3a25c471a7fa21ed170ef9a0f1a6b895ab48ed1", Sha256(csv));
        var json = NormalizeLineBreaks(requests[1].Content);
        Assert.Equal(21307, json.Length);
        Assert.Equal("21414dd60a7f9ace481a0cc9900e88bf7009e630a2ca6e0aaf697e90598724c8", Sha256(json));
        var table = NormalizeLineBreaks(clipboard);
        Assert.Equal(
            "Label\tKind\tRecipe\tPixel\tMachine\tSafe machine\tWorld\tSource bounds\tSource",
            table.Split('\n')[0]);
        Assert.Equal(3562, table.Length);
        Assert.Equal("a963755971161a8646acf2e2600eb50ae9f53d93da6f08fbbe4e60cd56898486", Sha256(table));
    }

    [Theory]
    [InlineData(true, "Exported CSV 19 coordinate row(s).")]
    [InlineData(false, "Coordinate CSV export cancelled.")]
    public async Task CoordinatePlanner_ExportCsv_ReportsHostResultAsync(bool hostResult, string expectedStatus)
    {
        var viewModel = BuildCoordinateViewModel();
        viewModel.RequestSaveTextFileAsync = _ => Task.FromResult(hostResult);

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);

        Assert.Equal(expectedStatus, viewModel.StatusText);
    }

    [Fact]
    public async Task CoordinatePlanner_ExportWithoutHost_ReportsUnavailableAsync()
    {
        var viewModel = BuildCoordinateViewModel();

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);
        var csvStatus = viewModel.StatusText;
        await viewModel.ExportArtifactsJsonCommand.ExecuteAsync(null);

        Assert.Equal("Coordinate text export is unavailable in the current host.", csvStatus);
        Assert.Equal("Coordinate text export is unavailable in the current host.", viewModel.StatusText);
    }

    [Fact]
    public async Task CopperPathReplay_ThreeSteps_CopiesClipboardTableWithPinnedContentAsync()
    {
        var clipboard = string.Empty;
        var viewModel = BuildReplayedCopperViewModel(
            text =>
            {
                clipboard = text;
                return Task.FromResult(true);
            },
            _ => Task.FromResult(true));

        await viewModel.CopyCopperPathReplayCommand.ExecuteAsync(null);

        Assert.Equal("Copied 3 copper path replay row(s).", viewModel.StatusText);
        Assert.Equal(_expectedCopperClipboard, NormalizeLineBreaks(clipboard).TrimEnd('\n'));
    }

    [Fact]
    public async Task CopperPathReplay_ThreeSteps_ExportsCsvAndJsonWithPinnedContentAsync()
    {
        var exports = new List<SimulationWorkspaceViewModel.SimulationTextExportRequest>();
        var viewModel = BuildReplayedCopperViewModel(
            _ => Task.FromResult(true),
            request =>
            {
                exports.Add(request);
                return Task.FromResult(true);
            });

        await viewModel.ExportCopperPathReplayCsvCommand.ExecuteAsync(null);
        var csvStatus = viewModel.StatusText;
        await viewModel.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);

        Assert.Equal("Exported CSV copper path replay row(s): 3.", csvStatus);
        Assert.Equal("Exported JSON copper path replay row(s): 3.", viewModel.StatusText);
        Assert.Equal(2, exports.Count);
        Assert.Equal("Export Copper Path Replay CSV", exports[0].Title);
        Assert.Equal("copper_path_replay.csv", exports[0].SuggestedFileName);
        Assert.Equal("csv", exports[0].DefaultExtension);
        Assert.Equal("Export Copper Path Replay JSON", exports[1].Title);
        Assert.Equal("copper_path_replay.json", exports[1].SuggestedFileName);
        Assert.Equal("json", exports[1].DefaultExtension);
        Assert.Equal(_expectedCopperCsv, NormalizeLineBreaks(exports[0].Content).TrimEnd('\n'));
        Assert.Equal(_expectedCopperJson, NormalizeLineBreaks(exports[1].Content).TrimEnd('\n'));
    }

    [Fact]
    public async Task CopperPathReplay_BeforeReplay_DoesNotRequestAnExportAsync()
    {
        var exports = new List<SimulationWorkspaceViewModel.SimulationTextExportRequest>();
        var viewModel = BuildCopperViewModel(
            _ => Task.FromResult(true),
            request =>
            {
                exports.Add(request);
                return Task.FromResult(true);
            });

        await viewModel.ExportCopperPathReplayCsvCommand.ExecuteAsync(null);
        var csvStatus = viewModel.StatusText;
        await viewModel.ExportCopperPathReplayJsonCommand.ExecuteAsync(null);

        Assert.Empty(exports);
        Assert.Equal("Run copper path replay before exporting CSV.", csvStatus);
        Assert.Equal("Run copper path replay before exporting JSON.", viewModel.StatusText);
    }

    private static CoordinatePlannerWorkspaceViewModel BuildCoordinateViewModel()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
    }

    private static SimulationWorkspaceViewModel BuildReplayedCopperViewModel(
        Func<string, Task<bool>> setClipboardText,
        Func<SimulationWorkspaceViewModel.SimulationTextExportRequest, Task<bool>> saveTextFile)
    {
        var viewModel = BuildCopperViewModel(setClipboardText, saveTextFile);
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperPeakValue = 1000d;
        viewModel.CopperPathStartX = 0.5m;
        viewModel.CopperPathStartY = 0.5m;
        viewModel.CopperPathEndX = 1.5m;
        viewModel.CopperPathEndY = 0.5m;
        viewModel.CopperPathStepCount = 3m;
        viewModel.ReplayCopperPathCommand.Execute(null);
        return viewModel;
    }

    private static SimulationWorkspaceViewModel BuildCopperViewModel(
        Func<string, Task<bool>> setClipboardText,
        Func<SimulationWorkspaceViewModel.SimulationTextExportRequest, Task<bool>> saveTextFile)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_copperDiffIndices, icIndexSelector: static col => col);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample"),
        });
        var session = new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection:
            [
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
            ],
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
            CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 1 });
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            RequestSetClipboardTextAsync = setClipboardText,
            RequestSaveTextFileAsync = saveTextFile,
        };
    }

    private static string NormalizeLineBreaks(string text)
    {
        var withoutPlatformBreaks = text.Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', withoutPlatformBreaks);
        Assert.DoesNotContain('\n', withoutPlatformBreaks);
        return text.Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
    }

    private static string Lf(string sourceText)
    {
        return sourceText.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string Sha256(string text)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
