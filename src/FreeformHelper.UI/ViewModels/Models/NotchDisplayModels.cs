// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Lightweight preview row for mapping a regular pad to generated notch rows.
/// </summary>
public readonly record struct NotchRowPreview(
    int RowNumber,
    NotchAlgorithmVersion Version,
    int IcIndex,
    int DiffIndex,
    int RegularPadId,
    int? CadPadId,
    string ValuesText,
    string Comment);

/// <summary>
/// One row item displayed in Step 5 validation sections.
/// </summary>
public readonly record struct NotchValidationDisplayItem(
    string KindText,
    string RowText,
    string VersionText,
    string IcText,
    string SourceText,
    string TargetText,
    string RatioText,
    string AreaText,
    string ValuesText,
    string CommentText,
    string NoteText,
    int SourceRegularPadId,
    int TargetRegularPadId,
    int CadPadId,
    int IcIndex,
    int SourceDiffIndex,
    int TargetDiffIndex,
    bool IsDirect,
    bool IsIncoming,
    bool IsOutgoing);
