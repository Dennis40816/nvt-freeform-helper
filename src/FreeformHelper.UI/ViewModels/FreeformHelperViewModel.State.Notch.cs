using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly bool _showInternalLegacyNotchFields;

    // Notch settings
    // The sixteen notch algorithm settings are stored once. The properties below forward to the
    // record and raise the same notifications and callbacks as the stored properties they replaced.
    private NotchAlgorithmSettings _notchAlgorithm = NotchAlgorithmSettings.Initial;

    /// <summary>
    /// Gets or sets a value indicating whether Notch Algorithm Version 2.1 is enabled.
    /// </summary>
    public bool EnableV21
    {
        get => _notchAlgorithm.EnableV21;
        set
        {
            if (_notchAlgorithm.EnableV21 == value)
            {
                return;
            }

            OnEnableV21Changing(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableV21 = value };
            OnEnableV21Changed(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableV21Changing(bool value);

    partial void OnEnableV21Changed(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 output path is enabled.
    /// (Internally mapped to enum value 30 for compatibility.)
    /// </summary>
    public bool EnableV22
    {
        get => _notchAlgorithm.EnableV22;
        set
        {
            if (_notchAlgorithm.EnableV22 == value)
            {
                return;
            }

            OnEnableV22Changing(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableV22 = value };
            OnPropertyChanged();
        }
    }

    partial void OnEnableV22Changing(bool value);

    /// <summary>
    /// Gets or sets the length scale factor for notch calculations.
    /// </summary>
    public decimal LenScale
    {
        get => _notchAlgorithm.LenScale;
        set
        {
            if (_notchAlgorithm.LenScale == value)
            {
                return;
            }

            OnLenScaleChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { LenScale = value };
            OnPropertyChanged();
        }
    }

    partial void OnLenScaleChanging(decimal value);

    /// <summary>
    /// Gets a value indicating whether LenScale should be visible in UI.
    /// LenScale is now treated as an internal legacy parameter and stays hidden.
    /// </summary>
    public bool IsLenScaleVisible => EnableV21 && _showInternalLegacyNotchFields;
    /// <summary>
    /// Gets or sets the null value used in notch tables.
    /// </summary>
    public decimal NullValue
    {
        get => _notchAlgorithm.NullValue;
        set
        {
            if (_notchAlgorithm.NullValue == value)
            {
                return;
            }

            OnNullValueChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { NullValue = value };
            OnPropertyChanged();
        }
    }

    partial void OnNullValueChanging(decimal value);

    /// <summary>
    /// Gets or sets the v2.1 threshold gate in Q7 (/128).
    /// </summary>
    public decimal NotchThresholdQ7
    {
        get => _notchAlgorithm.NotchThresholdQ7;
        set
        {
            if (_notchAlgorithm.NotchThresholdQ7 == value)
            {
                return;
            }

            OnNotchThresholdQ7Changing(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { NotchThresholdQ7 = value };
            OnNotchThresholdQ7Changed(value);
            OnPropertyChanged();
        }
    }

    partial void OnNotchThresholdQ7Changing(decimal value);

    partial void OnNotchThresholdQ7Changed(decimal value);

    /// <summary>
    /// Gets or sets the v2.2 threshold gate in percent.
    /// </summary>
    public decimal NotchThresholdPercent
    {
        get => _notchAlgorithm.NotchThresholdPercent;
        set
        {
            if (_notchAlgorithm.NotchThresholdPercent == value)
            {
                return;
            }

            OnNotchThresholdPercentChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { NotchThresholdPercent = value };
            OnNotchThresholdPercentChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnNotchThresholdPercentChanging(decimal value);

    partial void OnNotchThresholdPercentChanged(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether v2.2 threshold should be linked to v2.1 threshold.
    /// </summary>
    public bool LinkNotchThresholds
    {
        get => _notchAlgorithm.LinkNotchThresholds;
        set
        {
            if (_notchAlgorithm.LinkNotchThresholds == value)
            {
                return;
            }

            OnLinkNotchThresholdsChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { LinkNotchThresholds = value };
            OnLinkNotchThresholdsChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnLinkNotchThresholdsChanging(bool value);

    partial void OnLinkNotchThresholdsChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Regular compensation is enabled.
    /// </summary>
    public bool EnableToRegular
    {
        get => _notchAlgorithm.EnableToRegular;
        set
        {
            if (_notchAlgorithm.EnableToRegular == value)
            {
                return;
            }

            OnEnableToRegularChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableToRegular = value };
            OnEnableToRegularChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableToRegularChanging(bool value);

    partial void OnEnableToRegularChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Full compensation is enabled.
    /// </summary>
    public bool EnableToFull
    {
        get => _notchAlgorithm.EnableToFull;
        set
        {
            if (_notchAlgorithm.EnableToFull == value)
            {
                return;
            }

            OnEnableToFullChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableToFull = value };
            OnEnableToFullChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableToFullChanging(bool value);

    partial void OnEnableToFullChanged(bool value);

    /// <summary>
    /// Gets or sets strict overlap threshold (%) used by Notch 2.2 multi-owner gating.
    /// Example: 0.1 means overlap must exceed 0.1% of regular area to count as owner.
    /// </summary>
    public decimal ToFullStrictOverlapPercent
    {
        get => _notchAlgorithm.ToFullStrictOverlapPercent;
        set
        {
            if (_notchAlgorithm.ToFullStrictOverlapPercent == value)
            {
                return;
            }

            OnToFullStrictOverlapPercentChanging(value);
            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { ToFullStrictOverlapPercent = value };
            OnToFullStrictOverlapPercentChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnToFullStrictOverlapPercentChanging(decimal value);

    partial void OnToFullStrictOverlapPercentChanged(decimal value);

    /// <summary>
    /// Gets or sets whether Step3 should use split To Full rule engine path.
    /// </summary>
    public bool EnableToFullRuleEngine
    {
        get => _notchAlgorithm.EnableToFullRuleEngine;
        set
        {
            if (_notchAlgorithm.EnableToFullRuleEngine == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableToFullRuleEngine = value };
            OnEnableToFullRuleEngineChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableToFullRuleEngineChanged(bool value);

    /// <summary>
    /// Gets or sets whether Step3 should keep per-regular To Full rule trace entries.
    /// </summary>
    public bool EnableToFullRuleTrace
    {
        get => _notchAlgorithm.EnableToFullRuleTrace;
        set
        {
            if (_notchAlgorithm.EnableToFullRuleTrace == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableToFullRuleTrace = value };
            OnEnableToFullRuleTraceChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableToFullRuleTraceChanged(bool value);

    /// <summary>
    /// Gets or sets whether Step3 caps virtual To Full area before it contributes to gain/allocation.
    /// </summary>
    public bool EnableBoundaryVirtualAreaCap
    {
        get => _notchAlgorithm.EnableBoundaryVirtualAreaCap;
        set
        {
            if (_notchAlgorithm.EnableBoundaryVirtualAreaCap == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableBoundaryVirtualAreaCap = value };
            OnEnableBoundaryVirtualAreaCapChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableBoundaryVirtualAreaCapChanged(bool value);

    /// <summary>
    /// Gets or sets maximum virtual To Full area as a percent of the inside overlap area.
    /// </summary>
    public decimal BoundaryVirtualAreaCapPercent
    {
        get => _notchAlgorithm.BoundaryVirtualAreaCapPercent;
        set
        {
            if (_notchAlgorithm.BoundaryVirtualAreaCapPercent == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { BoundaryVirtualAreaCapPercent = value };
            OnBoundaryVirtualAreaCapPercentChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnBoundaryVirtualAreaCapPercentChanged(decimal value);

    /// <summary>
    /// Gets or sets whether Step3 caps target-side coverage after CurrentGain row selection.
    /// </summary>
    public bool EnableTargetCoverageGuard
    {
        get => _notchAlgorithm.EnableTargetCoverageGuard;
        set
        {
            if (_notchAlgorithm.EnableTargetCoverageGuard == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { EnableTargetCoverageGuard = value };
            OnEnableTargetCoverageGuardChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableTargetCoverageGuardChanged(bool value);

    /// <summary>
    /// Gets or sets the maximum target-side coverage percent for uniform-field safety.
    /// </summary>
    public decimal TargetCoverageCapPercent
    {
        get => _notchAlgorithm.TargetCoverageCapPercent;
        set
        {
            if (_notchAlgorithm.TargetCoverageCapPercent == value)
            {
                return;
            }

            OnPropertyChanging();
            _notchAlgorithm = _notchAlgorithm with { TargetCoverageCapPercent = value };
            OnTargetCoverageCapPercentChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnTargetCoverageCapPercentChanged(decimal value);

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
    public bool IsNotchExporting
    {
        get => _notchOperationState.Current.IsBusy;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_notchOperationState.Current.IsBusy, value))
            {
                return;
            }

            OnPropertyChanging(nameof(IsNotchExporting));
            _notchOperationState.SetBusy(value);
            OnIsNotchExportingChanged(value);
            OnPropertyChanged(nameof(IsNotchExporting));
        }
    }
    /// <summary>
    /// Gets or sets notch export progress value in range [0,1].
    /// </summary>
    public double NotchExportProgress
    {
        get => _notchOperationState.Current.Progress;
        set
        {
            if (EqualityComparer<double>.Default.Equals(_notchOperationState.Current.Progress, value))
            {
                return;
            }

            OnPropertyChanging(nameof(NotchExportProgress));
            _notchOperationState.SetProgress(value);
            OnNotchExportProgressChanged(value);
            OnPropertyChanged(nameof(NotchExportProgress));
        }
    }
    /// <summary>
    /// Gets a value indicating whether Step 5 export progress UI should be visible.
    /// Hidden once progress reaches completion even if command cleanup is still finishing.
    /// </summary>
    public bool ShouldShowNotchExportProgress => IsNotchExporting && NotchExportProgress < 0.9999;
    /// <summary>
    /// Gets or sets current notch export progress text.
    /// </summary>
    public string NotchExportProgressText
    {
        get => _notchOperationState.Current.Text;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_notchOperationState.Current.Text, value))
            {
                return;
            }

            OnPropertyChanging(nameof(NotchExportProgressText));
            _notchOperationState.SetText(value);
            OnPropertyChanged(nameof(NotchExportProgressText));
        }
    }
    /// <summary>
    /// Gets or sets target REG id used by Step 6 validation quick trace.
    /// </summary>
    [ObservableProperty] private decimal _notchValidationRegularPadId;
    // The Step 6 quick trace display is stored once. The summary and the four row collections below
    // forward to it. Each setter raises the notifications of the stored property it replaced.
    private NotchValidationDisplayState _notchValidationDisplay = NotchValidationDisplayState.CreateInitial();

    /// <summary>
    /// Gets or sets quick trace summary text for Step 6 validation.
    /// </summary>
    public string NotchValidationSummaryText
    {
        get => _notchValidationDisplay.SummaryText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_notchValidationDisplay.SummaryText, value))
            {
                return;
            }

            OnPropertyChanging();
            _notchValidationDisplay = _notchValidationDisplay with { SummaryText = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets merged validation rows for Step 6 quick trace.
    /// </summary>
    public ObservableCollection<NotchValidationDisplayItem> NotchValidationItems
    {
        get => _notchValidationDisplay.Items;
        set
        {
            if (EqualityComparer<ObservableCollection<NotchValidationDisplayItem>>.Default.Equals(_notchValidationDisplay.Items, value))
            {
                return;
            }

            OnPropertyChanging();
            _notchValidationDisplay = _notchValidationDisplay with { Items = value };
            OnPropertyChanged(nameof(HasNotchValidationItems));
            OnPropertyChanged(nameof(IsNotchValidationItemsEmpty));
            OnPropertyChanged();
        }
    }
    /// <summary>
    /// Gets direct row count for Step 6 validation.
    /// </summary>
    public int NotchValidationDirectCount => NotchValidationDirectItems.Count;
    /// <summary>
    /// Gets incoming row count for Step 6 validation.
    /// </summary>
    public int NotchValidationIncomingCount => NotchValidationIncomingItems.Count;
    /// <summary>
    /// Gets outgoing row count for Step 6 validation.
    /// </summary>
    public int NotchValidationOutgoingCount => NotchValidationOutgoingItems.Count;
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
    public ObservableCollection<NotchValidationDisplayItem> NotchValidationDirectItems
    {
        get => _notchValidationDisplay.DirectItems;
        set
        {
            if (EqualityComparer<ObservableCollection<NotchValidationDisplayItem>>.Default.Equals(_notchValidationDisplay.DirectItems, value))
            {
                return;
            }

            OnPropertyChanging();
            _notchValidationDisplay = _notchValidationDisplay with { DirectItems = value };
            OnPropertyChanged(nameof(HasNotchValidationDirectItems));
            OnPropertyChanged(nameof(IsNotchValidationDirectEmpty));
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets incoming allocation items for Step 6 validation.
    /// </summary>
    public ObservableCollection<NotchValidationDisplayItem> NotchValidationIncomingItems
    {
        get => _notchValidationDisplay.IncomingItems;
        set
        {
            if (EqualityComparer<ObservableCollection<NotchValidationDisplayItem>>.Default.Equals(_notchValidationDisplay.IncomingItems, value))
            {
                return;
            }

            OnPropertyChanging();
            _notchValidationDisplay = _notchValidationDisplay with { IncomingItems = value };
            OnPropertyChanged(nameof(HasNotchValidationIncomingItems));
            OnPropertyChanged(nameof(IsNotchValidationIncomingEmpty));
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets outgoing allocation items for Step 6 validation.
    /// </summary>
    public ObservableCollection<NotchValidationDisplayItem> NotchValidationOutgoingItems
    {
        get => _notchValidationDisplay.OutgoingItems;
        set
        {
            if (EqualityComparer<ObservableCollection<NotchValidationDisplayItem>>.Default.Equals(_notchValidationDisplay.OutgoingItems, value))
            {
                return;
            }

            OnPropertyChanging();
            _notchValidationDisplay = _notchValidationDisplay with { OutgoingItems = value };
            OnPropertyChanged(nameof(HasNotchValidationOutgoingItems));
            OnPropertyChanged(nameof(IsNotchValidationOutgoingEmpty));
            OnPropertyChanged();
        }
    }
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
