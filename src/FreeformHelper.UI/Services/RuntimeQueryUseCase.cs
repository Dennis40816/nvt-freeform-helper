using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private const int DefaultTerminalTail = 120;
    private const int MaxTerminalTail = 5000;
    private const int DefaultTerminalLinkLimit = 200;
    private const int MaxTerminalLinkLimit = 2000;
    private const int DefaultNotchRegularLimit = 200;
    private const int MaxNotchRegularLimit = 5000;
    private const int DefaultNotchTargetLimit = 120;
    private const int MaxNotchTargetLimit = 5000;
    private const int DefaultNotchPolygonLimit = 64;
    private const int MaxNotchPolygonLimit = 2000;
    private static readonly string[] QueryHelpExamples =
    [
        "freeformhelper.exe query help",
        "freeformhelper.exe query status",
        "freeformhelper.exe query terminal --tail 300",
        "freeformhelper.exe query terminal-links --tail 300 --limit 300",
        "freeformhelper.exe query pad --cad-id 4808",
        "freeformhelper.exe query notch --cad-id 4808 --limit 400 --target-limit 128 --polygon-limit 128",
        "freeformhelper.exe query multi-owner --cad-id 4808 --overlap-percent 1.0",
        "freeformhelper.exe query notch-stage --cad-id 4808 --polygon-limit 96",
        "freeformhelper.exe query notch-validation --regular-id 4978",
        "freeformhelper.exe query load-project --path example/BOE36.35/project_3635.json",
        "freeformhelper.exe query run-step --step 1 --timeout-ms 120000",
        "freeformhelper.exe query select-cad --cad-id 4767",
        "freeformhelper.exe query run-step --step 3",
        "freeformhelper.exe query run-step --step 4",
        "freeformhelper.exe query set-tofull --enable false",
        "freeformhelper.exe query export-notch --format csv --path build/perf/notch_review.csv",
        "freeformhelper.exe query export-notch --format c-v21 --path build/perf/notch_v2.1.c",
        "freeformhelper.exe query export-notch --format c-v22 --path build/perf/notch_v2.2.c",
        "freeformhelper.exe query simulation --regular-id 644",
        "freeformhelper.exe query clear-step --step 3"
    ];

    private readonly ShellViewModel _shellViewModel;
    private readonly NotchValidationTraceService _notchValidationTraceService = new();
    private readonly NotchV22TargetAllocationService _targetAllocationService = new();
    private readonly RuntimeQueryNotchCacheService _notchQueryCacheService = new();
    private readonly RuntimeQueryCommandRouter _commandRouter;

    public RuntimeQueryUseCase(ShellViewModel shellViewModel)
    {
        _shellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
        _commandRouter = new RuntimeQueryCommandRouter(new Dictionary<string, Func<IReadOnlyDictionary<string, string>?, Task<RuntimeQueryResponseEnvelope>>>(StringComparer.Ordinal)
        {
            ["help"] = _ => Task.FromResult(QueryHelp()),
            ["status"] = _ => Task.FromResult(QueryStatus()),
            ["selection"] = _ => Task.FromResult(QuerySelection()),
            ["terminal"] = args => Task.FromResult(QueryTerminal(args)),
            ["terminal-links"] = args => Task.FromResult(QueryTerminalLinks(args)),
            ["pad"] = args => Task.FromResult(QueryPad(args)),
            ["notch"] = args => Task.FromResult(QueryNotch(args)),
            ["multi-owner"] = args => Task.FromResult(QueryMultiOwner(args)),
            ["notch-stage"] = args => Task.FromResult(QueryNotchStage(args)),
            ["notch-validation"] = args => Task.FromResult(QueryNotchValidation(args)),
            ["load-project"] = QueryLoadProjectAsync,
            ["run-step"] = QueryRunStepAsync,
            ["clear-step"] = args => Task.FromResult(QueryClearStep(args)),
            ["select-cad"] = args => Task.FromResult(QuerySelectCad(args)),
            ["select-regular"] = args => Task.FromResult(QuerySelectRegular(args)),
            ["clear-selection"] = _ => Task.FromResult(QueryClearSelection()),
            ["set-tofull"] = args => Task.FromResult(QuerySetToFull(args)),
            ["simulation"] = QuerySimulationAsync,
            ["export-notch"] = QueryExportNotchAsync
        });
    }

    public async Task<RuntimeQueryResponseEnvelope> ExecuteAsync(RuntimeQueryRequest? request)
    {
        if (request is null)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_REQUEST",
                message: "Request is null.");
        }

        if (!string.Equals(request.Version, RuntimeQueryProtocol.Version, StringComparison.Ordinal))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "UNSUPPORTED_VERSION",
                message: $"Unsupported request version '{request.Version}'. Expected '{RuntimeQueryProtocol.Version}'.");
        }

        return await _commandRouter.RouteAsync(request.Command, request.Args);
    }

    private static RuntimeQueryResponseEnvelope QueryHelp()
    {
        return RuntimeQueryResponseEnvelope.Success(new
        {
            commands = new object[]
            {
                new
                {
                    name = "help",
                    description = "List supported runtime query commands and arguments.",
                    args = Array.Empty<object>()
                },
                new
                {
                    name = "status",
                    description = "Return app/workflow/notch preview status.",
                    args = Array.Empty<object>()
                },
                new
                {
                    name = "selection",
                    description = "Return current CAD/Regular selection.",
                    args = Array.Empty<object>()
                },
                new
                {
                    name = "terminal",
                    description = "Return recent in-app log lines.",
                    args = new object[]
                    {
                        new { key = "tail", required = false, value = "1..5000", defaultValue = DefaultTerminalTail }
                    }
                },
                new
                {
                    name = "terminal-links",
                    description = "Return parsed clickable links from recent terminal lines.",
                    args = new object[]
                    {
                        new { key = "tail", required = false, value = "1..5000", defaultValue = DefaultTerminalTail },
                        new { key = "limit", required = false, value = $"1..{MaxTerminalLinkLimit}", defaultValue = DefaultTerminalLinkLimit }
                    }
                },
                new
                {
                    name = "pad",
                    description = "Return one pad snapshot (same source as Popover/Inspector).",
                    args = new object[]
                    {
                        new { key = "cad-id", required = false, value = ">=0", note = "Use exactly one of cad-id / regular-id." },
                        new { key = "regular-id", required = false, value = ">=0", note = "Use exactly one of cad-id / regular-id." }
                    }
                },
                new
                {
                    name = "notch",
                    description = "Return Notch 2.2 compensation debug payload for one CAD pad.",
                    args = new object[]
                    {
                        new { key = "cad-id", required = true, value = ">=0" },
                        new { key = "limit", required = false, value = $"1..{MaxNotchRegularLimit}", defaultValue = DefaultNotchRegularLimit },
                        new { key = "target-limit", required = false, value = $"1..{MaxNotchTargetLimit}", defaultValue = DefaultNotchTargetLimit },
                        new { key = "polygon-limit", required = false, value = $"1..{MaxNotchPolygonLimit}", defaultValue = DefaultNotchPolygonLimit }
                    }
                },
                new
                {
                    name = "multi-owner",
                    description = "Return regular pads gated by multi-owner rule and owner CAD ids.",
                    args = new object[]
                    {
                        new { key = "cad-id", required = true, value = ">=0" },
                        new { key = "limit", required = false, value = $"1..{MaxNotchRegularLimit}", defaultValue = DefaultNotchRegularLimit },
                        new { key = "overlap-percent", required = false, value = "0..100", note = "Override strict owner overlap threshold (%) for this query only." }
                    }
                },
                new
                {
                    name = "notch-stage",
                    description = "Return stage-1/2/3 notch overlay polygons for one CAD pad.",
                    args = new object[]
                    {
                        new { key = "cad-id", required = true, value = ">=0" },
                        new { key = "polygon-limit", required = false, value = $"1..{MaxNotchPolygonLimit}", defaultValue = DefaultNotchPolygonLimit }
                    }
                },
                new
                {
                    name = "notch-validation",
                    description = "Return Step 5 regular-centric validation rows (direct/incoming/outgoing).",
                    args = new object[]
                    {
                        new { key = "regular-id", required = true, value = ">=0" }
                    }
                },
                new
                {
                    name = "load-project",
                    description = "Load a project JSON file into the running app instance.",
                    args = new object[]
                    {
                        new { key = "path", required = true, value = "absolute or relative file path" }
                    }
                },
                new
                {
                    name = "run-step",
                    description = "Execute workflow step (1=match, 2=freeform detect, 3=refresh notch preview, 4=index diagnostics).",
                    args = new object[]
                    {
                        new { key = "step", required = true, value = "1|2|3|4" }
                    }
                },
                new
                {
                    name = "clear-step",
                    description = "Clear workflow step result (and dependent downstream state).",
                    args = new object[]
                    {
                        new { key = "step", required = true, value = "1|2|3|4|5" }
                    }
                },
                new
                {
                    name = "select-cad",
                    description = "Programmatically select CAD pads in canvas/inspector context.",
                    args = new object[]
                    {
                        new { key = "cad-id", required = false, value = ">=0", note = "single id" },
                        new { key = "cad-ids", required = false, value = "comma-separated >=0", note = "multiple ids" }
                    }
                },
                new
                {
                    name = "select-regular",
                    description = "Programmatically select regular pads by pad-id or index.",
                    args = new object[]
                    {
                        new { key = "regular-id", required = false, value = ">=0", note = "single regular pad id" },
                        new { key = "regular-ids", required = false, value = "comma-separated >=0", note = "multiple regular pad ids" },
                        new { key = "regular-index", required = false, value = ">=0", note = "single grid index" },
                        new { key = "regular-indices", required = false, value = "comma-separated >=0", note = "multiple grid indices" }
                    }
                },
                new
                {
                    name = "clear-selection",
                    description = "Clear CAD/regular selection.",
                    args = Array.Empty<object>()
                },
                new
                {
                    name = "set-tofull",
                    description = "Toggle Notch 2.2 To Full enable flag.",
                    args = new object[]
                    {
                        new { key = "enable", required = true, value = "true|false|1|0|on|off" }
                    }
                },
                new
                {
                    name = "simulation",
                    description = "Return current Simulation workspace state and optionally one regular pad snapshot.",
                    args = new object[]
                    {
                        new { key = "regular-id", required = false, value = ">=0", note = "specific regular pad to inspect in current simulation workspace" }
                    }
                },
                new
                {
                    name = "export-notch",
                    description = "Generate Step 5 notch table and export to a CSV review or C FW file (non-interactive).",
                    args = new object[]
                    {
                        new { key = "format", required = true, value = "csv (CSV review)|c-v21|c-v22" },
                        new { key = "path", required = true, value = "output file path" }
                    }
                }
            },
            commonArgs = new object[]
            {
                new { key = "timeout-ms", required = false, value = "1..120000", defaultValue = 1500 },
                new { key = "json-pretty/json-compact", required = false, value = "output format" }
            },
            examples = QueryHelpExamples
        });
    }


}
