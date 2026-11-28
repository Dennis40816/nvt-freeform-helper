using Avalonia.Controls;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>Handles coordinate planner detail, clipboard and text export requests.</summary>
internal sealed class CoordinatePlannerHostRequests(Func<TopLevel?> getTopLevel)
{
    public async Task ShowArtifactDetailAsync(CoordinateArtifactRow row)
    {
        var dialog = new CoordinateArtifactDetailWindow
        {
            DataContext = row,
        };

        if (getTopLevel() is Window owner)
        {
            await dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
    }

    public Task<bool> SetClipboardTextAsync(string text)
    {
        return TextExportHost.TrySetClipboardTextAsync(getTopLevel(), text);
    }

    public async Task<bool> SaveTextFileAsync(CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest request)
    {
        var outcome = await TextExportHost.SaveTextFileAsync(
            getTopLevel(),
            request.Title,
            request.DefaultExtension,
            request.SuggestedFileName,
            request.FileTypeName,
            request.Patterns,
            request.Content);
        return outcome == TextExportOutcome.Saved;
    }
}
