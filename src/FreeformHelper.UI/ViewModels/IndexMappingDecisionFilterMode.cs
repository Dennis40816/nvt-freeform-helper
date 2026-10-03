namespace FreeformHelper.UI.ViewModels;

public enum IndexMappingDecisionFilterMode
{
    All = 0,
    ChangedByMask = 1,
    RemovedByMask = 2,
    Ambiguous = 3,
    DuplicateDiff = 4,
    LowConfidence = 5,
    Unmapped = 6,
    CountMismatch = 7,
}
