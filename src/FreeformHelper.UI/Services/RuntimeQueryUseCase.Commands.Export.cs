using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private RuntimeQueryResponseEnvelope QuerySetToFull(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetBoolArg(args, "enable", out var enable, out var error) || error is not null)
        {
            return error ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--enable' is required for query set-tofull.");
        }

        var helper = _shellViewModel.FreeformHelper;
        helper.EnableToFull = enable;
        var workflow = helper.GetWorkflowStateSnapshot();
        return RuntimeQueryResponseEnvelope.Success(new
        {
            enableToFull = helper.EnableToFull,
            statusText = helper.StatusText,
            workflow = RuntimeQueryResponseBuilder.BuildWorkflowPayload(workflow),
            notchPreview = RuntimeQueryResponseBuilder.BuildNotchPreviewPayload(helper)
        });
    }

    private async Task<RuntimeQueryResponseEnvelope> QueryExportNotchAsync(IReadOnlyDictionary<string, string>? args)
    {
        if (!TryParseExportNotchArguments(args, out var arguments, out var argumentsError))
        {
            return argumentsError;
        }

        var helper = _shellViewModel.FreeformHelper;
        var option = helper.NotchExportFileTypeOptions.FirstOrDefault(candidate =>
            candidate.Value == arguments.FileType);
        var normalizedFormat = arguments.NormalizedFormat;

        if (string.IsNullOrWhiteSpace(option.Extension))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Export format '{normalizedFormat}' is not available.");
        }

        var target = PrepareExportNotchTarget(arguments.OutputPath);
        var resolvedPath = target.ResolvedPath;

        var previousOption = helper.SelectedNotchExportFileTypeOption;
        var previousSavePathPicker = helper.PickSaveNotchPathAsync;
        var previousSelectionDialog = helper.OpenNotchExportSelectionAsync;
        var exportStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            helper.SelectedNotchExportFileTypeOption = option;
            helper.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(resolvedPath);
            helper.OpenNotchExportSelectionAsync = viewModel =>
            {
                var selectedTable = viewModel.BuildSelectedTable();

                // The format pins the transient selection model to one version. Reset the
                // selection after materializing the table so the shared UI command does not
                // persist that version choice back into project settings.
                viewModel.SelectedVersionOption = viewModel.VersionOptions[0];
                return Task.FromResult<NotchTable?>(selectedTable);
            };
            await helper.ExportNotchCommand.ExecuteAsync(null);
        }
        finally
        {
            exportStopwatch.Stop();
            helper.OpenNotchExportSelectionAsync = previousSelectionDialog;
            helper.PickSaveNotchPathAsync = previousSavePathPicker;
            helper.SelectedNotchExportFileTypeOption = previousOption;
        }

        var summaryText = helper.NotchExportSummary;
        if (!TryVerifyExportNotchOutput(target, summaryText, out var fileInfo, out var outputError))
        {
            return outputError;
        }

        return RuntimeQueryResponseEnvelope.Success(new
        {
            format = normalizedFormat,
            formatKind = NotchExportFileTypeMetadata.GetExportKindLabel(option),
            path = resolvedPath,
            fileSize = fileInfo.Length,
            fileLastWriteUtc = fileInfo.LastWriteTimeUtc,
            elapsedMs = exportStopwatch.ElapsedMilliseconds,
            statusText = helper.StatusText,
            summaryText,
            workflow = RuntimeQueryResponseBuilder.BuildWorkflowPayload(helper.GetWorkflowStateSnapshot())
        });
    }

    private static bool TryParseExportNotchArguments(
        IReadOnlyDictionary<string, string>? args,
        out ExportNotchArguments arguments,
        [NotNullWhen(false)] out RuntimeQueryResponseEnvelope? error)
    {
        arguments = default;
        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "format", out var formatText, out var formatError) || formatError is not null)
        {
            error = formatError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--format' is required for query export-notch. Use csv (CSV review)|c-v21|c-v22.");
            return false;
        }

        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "path", out var outputPath, out var pathError) || pathError is not null)
        {
            error = pathError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--path' is required for query export-notch.");
            return false;
        }

        var requestedFormat = formatText.Trim().ToLowerInvariant();
        var mappedType = requestedFormat switch
        {
            "csv" => FreeformHelperViewModel.NotchExportFileType.Csv,
            "c-v21" => FreeformHelperViewModel.NotchExportFileType.Cv21,
            "c-v22" => FreeformHelperViewModel.NotchExportFileType.Cv22,
            _ => (FreeformHelperViewModel.NotchExportFileType?)null,
        };
        if (!mappedType.HasValue)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Unsupported export format '{formatText}'. Use csv (CSV review)|c-v21|c-v22.");
            return false;
        }

        var normalizedFormat = mappedType.Value switch
        {
            FreeformHelperViewModel.NotchExportFileType.Csv => "csv",
            FreeformHelperViewModel.NotchExportFileType.Cv21 => "c-v21",
            FreeformHelperViewModel.NotchExportFileType.Cv22 => "c-v22",
            _ => requestedFormat,
        };
        arguments = new ExportNotchArguments(outputPath, mappedType.Value, normalizedFormat);
        error = null;
        return true;
    }

    private static ExportNotchTarget PrepareExportNotchTarget(string outputPath)
    {
        var resolvedPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var fileExistedBefore = File.Exists(resolvedPath);
        var previousWriteUtc = fileExistedBefore
            ? File.GetLastWriteTimeUtc(resolvedPath)
            : DateTime.MinValue;
        var previousFileSize = fileExistedBefore
            ? new FileInfo(resolvedPath).Length
            : -1L;
        return new ExportNotchTarget(resolvedPath, fileExistedBefore, previousWriteUtc, previousFileSize);
    }

    private static bool TryVerifyExportNotchOutput(
        ExportNotchTarget target,
        string summaryText,
        [NotNullWhen(true)] out FileInfo? fileInfo,
        [NotNullWhen(false)] out RuntimeQueryResponseEnvelope? error)
    {
        fileInfo = null;
        if (!File.Exists(target.ResolvedPath))
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not produce output file. summary={summaryText}");
            return false;
        }

        fileInfo = new FileInfo(target.ResolvedPath);
        var isFileUpdated = !target.FileExistedBefore ||
                            fileInfo.Length != target.PreviousFileSize ||
                            fileInfo.LastWriteTimeUtc > target.PreviousWriteUtc;
        if (!isFileUpdated)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not update output file. summary={summaryText}");
            return false;
        }

        error = null;
        return true;
    }

    private readonly record struct ExportNotchArguments(
        string OutputPath,
        FreeformHelperViewModel.NotchExportFileType FileType,
        string NormalizedFormat);

    private readonly record struct ExportNotchTarget(
        string ResolvedPath,
        bool FileExistedBefore,
        DateTime PreviousWriteUtc,
        long PreviousFileSize);
}
