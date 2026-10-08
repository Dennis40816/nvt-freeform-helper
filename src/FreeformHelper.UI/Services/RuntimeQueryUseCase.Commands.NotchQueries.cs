using FreeformHelper.Application.Services;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private RuntimeQueryResponseEnvelope QueryPad(IReadOnlyDictionary<string, string>? args)
    {
        var hasCadId = RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadError);
        if (cadError is not null)
        {
            return cadError;
        }

        var hasRegularId = RuntimeQueryArgumentParser.TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularPadId, out var regularError);
        if (regularError is not null)
        {
            return regularError;
        }

        if (hasCadId == hasRegularId)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use exactly one of --cad-id or --regular-id.");
        }

        return hasCadId
            ? QueryCadPad(cadPadId)
            : QueryRegularPad(regularPadId);
    }

    private RuntimeQueryResponseEnvelope QueryNotch(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadIdError) || cadIdError is not null)
        {
            return cadIdError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--cad-id' is required for query notch.");
        }

        var limit = DefaultNotchRegularLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "limit", 1, MaxNotchRegularLimit, out var parsedLimit, out var limitError))
        {
            limit = parsedLimit;
        }
        else if (limitError is not null)
        {
            return limitError;
        }

        var targetLimit = DefaultNotchTargetLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "target-limit", 1, MaxNotchTargetLimit, out var parsedTargetLimit, out var targetLimitError))
        {
            targetLimit = parsedTargetLimit;
        }
        else if (targetLimitError is not null)
        {
            return targetLimitError;
        }

        var polygonLimit = DefaultNotchPolygonLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "polygon-limit", 1, MaxNotchPolygonLimit, out var parsedPolygonLimit, out var polygonLimitError))
        {
            polygonLimit = parsedPolygonLimit;
        }
        else if (polygonLimitError is not null)
        {
            return polygonLimitError;
        }

        return QueryCadNotch(cadPadId, limit, targetLimit, polygonLimit);
    }

    private RuntimeQueryResponseEnvelope QueryMultiOwner(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadIdError) || cadIdError is not null)
        {
            return cadIdError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--cad-id' is required for query multi-owner.");
        }

        var limit = DefaultNotchRegularLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "limit", 1, MaxNotchRegularLimit, out var parsedLimit, out var limitError))
        {
            limit = parsedLimit;
        }
        else if (limitError is not null)
        {
            return limitError;
        }

        var hasOverlapPercentOverride = RuntimeQueryArgumentParser.TryGetDoubleArg(
            args,
            "overlap-percent",
            0.0,
            100.0,
            out var overlapPercentOverride,
            out var overlapPercentError);
        if (overlapPercentError is not null)
        {
            return overlapPercentError;
        }

        var strictOverlapRatioOverride = hasOverlapPercentOverride
            ? NotchV22CompensationService.NormalizeStrictOverlapRatio(overlapPercentOverride / 100.0)
            : (double?)null;
        return QueryCadMultiOwner(cadPadId, limit, strictOverlapRatioOverride, hasOverlapPercentOverride ? overlapPercentOverride : null);
    }

    private RuntimeQueryResponseEnvelope QueryNotchStage(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadIdError) || cadIdError is not null)
        {
            return cadIdError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--cad-id' is required for query notch-stage.");
        }

        var polygonLimit = DefaultNotchPolygonLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "polygon-limit", 1, MaxNotchPolygonLimit, out var parsedPolygonLimit, out var polygonLimitError))
        {
            polygonLimit = parsedPolygonLimit;
        }
        else if (polygonLimitError is not null)
        {
            return polygonLimitError;
        }

        return QueryCadNotchStage(cadPadId, polygonLimit);
    }

    private RuntimeQueryResponseEnvelope QueryNotchValidation(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularPadId, out var regularIdError) || regularIdError is not null)
        {
            return regularIdError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--regular-id' is required for query notch-validation.");
        }

        var helper = _shellViewModel.FreeformHelper;
        if (!helper.TryGetNotchValidationReportForRegularPad(
                regularPadId,
                out var report,
                out var nullDiffValue,
                out var failureCode,
                out var failureMessage))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: string.IsNullOrWhiteSpace(failureCode) ? "NOT_READY" : failureCode,
                message: string.IsNullOrWhiteSpace(failureMessage)
                    ? "Validation data is unavailable."
                    : failureMessage);
        }

        var trace = NotchValidationTraceService.BuildTrace(report, helper.RegularPads, nullDiffValue);
        var traceDirectRows = trace.DirectRows
            .Select(RuntimeQueryResponseBuilder.BuildValidationTraceRowPayload)
            .ToList();
        var traceIncomingRows = trace.IncomingRows
            .Select(RuntimeQueryResponseBuilder.BuildValidationTraceRowPayload)
            .ToList();
        var traceOutgoingRows = trace.OutgoingRows
            .Select(RuntimeQueryResponseBuilder.BuildValidationTraceRowPayload)
            .ToList();

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "notch-validation",
            regularPad = new
            {
                report.RegularPadId,
                report.IcIndex,
                report.DiffIndex,
                nullDiffValue
            },
            summary = new
            {
                directRowCount = traceDirectRows.Count,
                incomingRowCount = traceIncomingRows.Count,
                outgoingRowCount = traceOutgoingRows.Count
            },
            rows = new
            {
                direct = traceDirectRows,
                incoming = traceIncomingRows,
                outgoing = traceOutgoingRows
            }
        });
    }
}
