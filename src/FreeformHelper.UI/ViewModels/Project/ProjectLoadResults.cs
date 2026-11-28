// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.UseCases.ProjectLoad;

namespace FreeformHelper.UI.ViewModels;

internal sealed record ProjectLoadCommandResult(
    bool IsLoaded,
    string Code,
    string Message,
    string? Path,
    long TotalElapsedMs,
    long PersistenceElapsedMs,
    long ApplyStateElapsedMs,
    long DxfElapsedMs,
    long RebuildElapsedMs,
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed)
{
    public static ProjectLoadCommandResult Loaded(
        string path,
        long totalElapsedMs,
        long persistenceElapsedMs,
        long applyStateElapsedMs,
        long dxfElapsedMs,
        long rebuildElapsedMs,
        long step1ReplayElapsedMs,
        long step2ReplayElapsedMs,
        bool step1Replayed,
        bool step2Replayed)
    {
        return new ProjectLoadCommandResult(
            IsLoaded: true,
            Code: "OK",
            Message: $"Project loaded: {path}",
            Path: path,
            TotalElapsedMs: totalElapsedMs,
            PersistenceElapsedMs: persistenceElapsedMs,
            ApplyStateElapsedMs: applyStateElapsedMs,
            DxfElapsedMs: dxfElapsedMs,
            RebuildElapsedMs: rebuildElapsedMs,
            Step1ReplayElapsedMs: step1ReplayElapsedMs,
            Step2ReplayElapsedMs: step2ReplayElapsedMs,
            Step1Replayed: step1Replayed,
            Step2Replayed: step2Replayed);
    }

    public static ProjectLoadCommandResult Failed(string code, string message, string? path = null)
    {
        return new ProjectLoadCommandResult(
            IsLoaded: false,
            Code: code,
            Message: message,
            Path: path,
            TotalElapsedMs: 0,
            PersistenceElapsedMs: 0,
            ApplyStateElapsedMs: 0,
            DxfElapsedMs: 0,
            RebuildElapsedMs: 0,
            Step1ReplayElapsedMs: 0,
            Step2ReplayElapsedMs: 0,
            Step1Replayed: false,
            Step2Replayed: false);
    }
}

internal sealed record ProjectLoadApplyTiming(
    long TotalElapsedMs,
    long ApplyStateElapsedMs,
    long DxfElapsedMs,
    long RebuildElapsedMs,
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed);

internal sealed record ProjectLoadCadReload(
    long DxfElapsedMs,
    long RebuildElapsedMs,
    ProjectLoadReplayTiming ReplayTiming,
    DxfCombinedGroupReplayResult? CombinedGroupReplay);

internal sealed record ProjectLoadReplayTiming(
    long Step1ReplayElapsedMs,
    long Step2ReplayElapsedMs,
    bool Step1Replayed,
    bool Step2Replayed)
{
    public static ProjectLoadReplayTiming Empty => new(
        Step1ReplayElapsedMs: 0,
        Step2ReplayElapsedMs: 0,
        Step1Replayed: false,
        Step2Replayed: false);
}
