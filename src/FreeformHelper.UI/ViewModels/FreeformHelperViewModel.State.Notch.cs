using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly bool _showInternalLegacyNotchFields;

    // Notch settings
    /// <summary>
    /// Gets or sets a value indicating whether Notch Algorithm Version 2.1 is enabled.
    /// </summary>
    [ObservableProperty] private bool _enableV21;
    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 output path is enabled.
    /// (Internally mapped to enum value 30 for compatibility.)
    /// </summary>
    [ObservableProperty] private bool _enableV22;
    /// <summary>
    /// Gets or sets the length scale factor for notch calculations.
    /// </summary>
    [ObservableProperty] private decimal _lenScale;
    /// <summary>
    /// Gets a value indicating whether LenScale should be visible in UI.
    /// LenScale is now treated as an internal legacy parameter and stays hidden.
    /// </summary>
    public bool IsLenScaleVisible => EnableV21 && _showInternalLegacyNotchFields;
    /// <summary>
    /// Gets or sets the null value used in notch tables.
    /// </summary>
    [ObservableProperty] private decimal _nullValue;
    /// <summary>
    /// Gets or sets the v2.1 threshold gate in Q7 (/128).
    /// </summary>
    [ObservableProperty] private decimal _notchThresholdQ7;
    /// <summary>
    /// Gets or sets the v2.2 threshold gate in percent.
    /// </summary>
    [ObservableProperty] private decimal _notchThresholdPercent;
    /// <summary>
    /// Gets or sets a value indicating whether v2.2 threshold should be linked to v2.1 threshold.
    /// </summary>
    [ObservableProperty] private bool _linkNotchThresholds = true;
    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Regular compensation is enabled.
    /// </summary>
    [ObservableProperty] private bool _enableToRegular = true;
    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Full compensation is enabled.
    /// </summary>
    [ObservableProperty] private bool _enableToFull = true;
    /// <summary>
    /// Gets or sets strict overlap threshold (%) used by Notch 2.2 multi-owner gating.
    /// Example: 0.1 means overlap must exceed 0.1% of regular area to count as owner.
    /// </summary>
    [ObservableProperty] private decimal _toFullStrictOverlapPercent = 0.1m;
    /// <summary>
    /// Gets or sets whether Step3 should use split To Full rule engine path.
    /// </summary>
    [ObservableProperty] private bool _enableToFullRuleEngine = true;
    /// <summary>
    /// Gets or sets whether Step3 should keep per-regular To Full rule trace entries.
    /// </summary>
    [ObservableProperty] private bool _enableToFullRuleTrace;
    /// <summary>
    /// Gets or sets whether Step3 caps virtual To Full area before it contributes to gain/allocation.
    /// </summary>
    [ObservableProperty] private bool _enableBoundaryVirtualAreaCap = true;
    /// <summary>
    /// Gets or sets maximum virtual To Full area as a percent of the inside overlap area.
    /// </summary>
    [ObservableProperty] private decimal _boundaryVirtualAreaCapPercent = 100m;
    /// <summary>
    /// Gets or sets whether Step3 caps target-side coverage after CurrentGain row selection.
    /// </summary>
    [ObservableProperty] private bool _enableTargetCoverageGuard = true;
    /// <summary>
    /// Gets or sets the maximum target-side coverage percent for uniform-field safety.
    /// </summary>
    [ObservableProperty] private decimal _targetCoverageCapPercent = 120m;
    /// <summary>
    /// Gets or sets selected Step3 notch compensation model option.
    /// </summary>
    [ObservableProperty] private NotchCompensationModelOption _selectedNotchCompensationModelOption;
    /// <summary>
    /// Gets or sets selected file type used by Step 5 export.
    /// </summary>
    [ObservableProperty] private NotchExportFileTypeOption _selectedNotchExportFileTypeOption;
    /// <summary>
    /// Gets or sets selected C-export profile used by Step 5 C initializer export.
    /// </summary>
    [ObservableProperty] private NotchExportProfileOption _selectedNotchExportProfileOption;
    /// <summary>
    /// Gets a value indicating whether current Step 5 export type is C initializer.
    /// </summary>
    public bool IsNotchCExportType => NotchExportFileTypeMetadata.IsCExportType(SelectedNotchExportFileTypeOption.Value);

    /// <summary>
    /// Gets or sets the latest notch export summary (Step 5 result).
    /// </summary>
    [ObservableProperty] private string _notchExportSummary = "Notch export: not run.";
    /// <summary>
    /// Gets or sets a value indicating whether notch export is currently running.
    /// </summary>
    [ObservableProperty] private bool _isNotchExporting;
    /// <summary>
    /// Gets or sets notch export progress value in range [0,1].
    /// </summary>
    [ObservableProperty] private double _notchExportProgress;
    /// <summary>
    /// Gets a value indicating whether Step 5 export progress UI should be visible.
    /// Hidden once progress reaches completion even if command cleanup is still finishing.
    /// </summary>
    public bool ShouldShowNotchExportProgress => IsNotchExporting && NotchExportProgress < 0.9999;
    /// <summary>
    /// Gets or sets current notch export progress text.
    /// </summary>
    [ObservableProperty] private string _notchExportProgressText = "Idle.";
    /// <summary>
    /// Gets or sets target REG id used by Step 6 validation quick trace.
    /// </summary>
    [ObservableProperty] private decimal _notchValidationRegularPadId;
    /// <summary>
    /// Gets or sets quick trace summary text for Step 6 validation.
    /// </summary>
    [ObservableProperty] private string _notchValidationSummaryText = "Validation: select REG, then run Analyze.";
    /// <summary>
    /// Gets or sets merged validation rows for Step 6 quick trace.
    /// </summary>
    [ObservableProperty] private ObservableCollection<NotchValidationDisplayItem> _notchValidationItems = new();
    /// <summary>
    /// Gets or sets direct row count for Step 6 validation.
    /// </summary>
    [ObservableProperty] private int _notchValidationDirectCount;
    /// <summary>
    /// Gets or sets incoming row count for Step 6 validation.
    /// </summary>
    [ObservableProperty] private int _notchValidationIncomingCount;
    /// <summary>
    /// Gets or sets outgoing row count for Step 6 validation.
    /// </summary>
    [ObservableProperty] private int _notchValidationOutgoingCount;
    /// <summary>
    /// Gets a value indicating whether Step 6 merged validation list contains rows.
    /// </summary>
    public bool HasNotchValidationItems => NotchValidationItems.Count > 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 merged validation list is empty.
    /// </summary>
    public bool IsNotchValidationItemsEmpty => NotchValidationItems.Count == 0;

    /// <summary>
    /// Gets or sets direct row items for Step 6 validation.
    /// </summary>
    [ObservableProperty] private ObservableCollection<NotchValidationDisplayItem> _notchValidationDirectItems = new();
    /// <summary>
    /// Gets or sets incoming allocation items for Step 6 validation.
    /// </summary>
    [ObservableProperty] private ObservableCollection<NotchValidationDisplayItem> _notchValidationIncomingItems = new();
    /// <summary>
    /// Gets or sets outgoing allocation items for Step 6 validation.
    /// </summary>
    [ObservableProperty] private ObservableCollection<NotchValidationDisplayItem> _notchValidationOutgoingItems = new();
    /// <summary>
    /// Gets a value indicating whether Step 6 validation has direct rows.
    /// </summary>
    public bool HasNotchValidationDirectItems => NotchValidationDirectItems.Count > 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 validation direct section is empty.
    /// </summary>
    public bool IsNotchValidationDirectEmpty => NotchValidationDirectItems.Count == 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 validation has incoming allocations.
    /// </summary>
    public bool HasNotchValidationIncomingItems => NotchValidationIncomingItems.Count > 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 validation incoming section is empty.
    /// </summary>
    public bool IsNotchValidationIncomingEmpty => NotchValidationIncomingItems.Count == 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 validation has outgoing allocations.
    /// </summary>
    public bool HasNotchValidationOutgoingItems => NotchValidationOutgoingItems.Count > 0;
    /// <summary>
    /// Gets a value indicating whether Step 6 validation outgoing section is empty.
    /// </summary>
    public bool IsNotchValidationOutgoingEmpty => NotchValidationOutgoingItems.Count == 0;

}
