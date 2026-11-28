// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Plans replacement inside the load session and completes a requested save after that session ends.</summary>
internal static class ReplaceProjectWorkflow
{
    internal static async Task<ReplaceProjectPlan> PrepareAsync(
        Func<Task<string?>> pickPathAsync,
        Func<bool> hasUnsavedWork,
        Func<Task<string?>> backUpAsync,
        Func<Func<string?, Task<bool?>>?> getPrompt)
    {
        var path = await pickPathAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ReplaceProjectPlan(ReplaceProjectOutcome.Cancel, null, null);
        }

        string? backupPath = null;
        if (hasUnsavedWork())
        {
            // The backup comes first, so a wrong click in the prompt can never lose the unsaved work.
            backupPath = await backUpAsync();
            var prompt = getPrompt();
            if (prompt is not null)
            {
                var saveFirst = await prompt(backupPath);
                var outcome = saveFirst switch
                {
                    null => ReplaceProjectOutcome.Cancel,
                    true => ReplaceProjectOutcome.SaveThenLoad,
                    false => ReplaceProjectOutcome.Load
                };
                return new ReplaceProjectPlan(outcome, path, backupPath);
            }
        }

        return new ReplaceProjectPlan(ReplaceProjectOutcome.Load, path, backupPath);
    }

    // Call only after the first load session ends: saving needs the editing session.
    // True means save succeeded and load was attempted, even when the load delegate reports failure.
    internal static async Task<bool> SaveThenLoadAsync(
        string path,
        string? backupPath,
        Func<Task<bool>> saveAsync,
        Action<string?> deleteBackup,
        Func<string, Task> loadAsync)
    {
        if (!await saveAsync())
        {
            return false;
        }

        deleteBackup(backupPath);
        await loadAsync(path);
        return true;
    }
}

internal enum ReplaceProjectOutcome
{
    Cancel,
    Load,
    SaveThenLoad
}

internal sealed record ReplaceProjectPlan(ReplaceProjectOutcome Outcome, string? Path, string? BackupPath)
{
    internal string Message => Outcome switch
    {
        ReplaceProjectOutcome.Cancel when Path is null => "Load cancelled.",
        ReplaceProjectOutcome.Cancel => "Load cancelled: unsaved changes were kept.",
        ReplaceProjectOutcome.SaveThenLoad => "Saving before load.",
        _ => string.Empty
    };
}
