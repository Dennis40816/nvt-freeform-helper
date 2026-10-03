using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;

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
        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "format", out var formatText, out var formatError) || formatError is not null)
        {
            return formatError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--format' is required for query export-notch. Use csv (CSV review)|c-v21|c-v22.");
        }

        if (!RuntimeQueryArgumentParser.TryGetStringArg(args, "path", out var outputPath, out var pathError) || pathError is not null)
        {
            return pathError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--path' is required for query export-notch.");
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
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: $"Unsupported export format '{formatText}'. Use csv (CSV review)|c-v21|c-v22.");
        }

        var helper = _shellViewModel.FreeformHelper;
        var option = helper.NotchExportFileTypeOptions.FirstOrDefault(candidate =>
            candidate.Value == mappedType.Value);
        var normalizedFormat = mappedType.Value switch
        {
            FreeformHelperViewModel.NotchExportFileType.Csv => "csv",
            FreeformHelperViewModel.NotchExportFileType.Cv21 => "c-v21",
            FreeformHelperViewModel.NotchExportFileType.Cv22 => "c-v22",
            _ => requestedFormat,
        };

        if (string.IsNullOrWhiteSpace(option.Extension))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Export format '{normalizedFormat}' is not available.");
        }

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
        if (!File.Exists(resolvedPath))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not produce output file. summary={summaryText}");
        }

        var fileInfo = new FileInfo(resolvedPath);
        var isFileUpdated = !fileExistedBefore ||
                            fileInfo.Length != previousFileSize ||
                            fileInfo.LastWriteTimeUtc > previousWriteUtc;
        if (!isFileUpdated)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "STEP_EXECUTION_FAILED",
                message: $"Notch export did not update output file. summary={summaryText}");
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
}
