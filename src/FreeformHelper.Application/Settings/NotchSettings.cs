using FreeformHelper.Application.Export;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Settings;

/// <summary>
/// Defines settings related to the generation of notch tables.
/// </summary>
public sealed class NotchSettings
{
    private static readonly NotchAlgorithmVersion[] DefaultEnabledVersions =
    {
        NotchAlgorithmVersion.V21,
        NotchAlgorithmVersion.V22,
    };

    private HashSet<NotchAlgorithmVersion> _enabledVersions = new(DefaultEnabledVersions);

    /// <summary>
    /// Gets or sets the notch row computation mode.
    /// Default uses CAD-allocation flow (Notch 2.2 baseline).
    /// </summary>
    public NotchComputationMode ComputationMode { get; set; } = NotchComputationMode.CadAllocation;

    /// <summary>
    /// Gets or sets a hash set of <see cref="NotchAlgorithmVersion"/> values that are currently enabled.
    /// Only algorithms specified in this set will be used when generating the notch table.
    /// By default v2.1 and v2.2 are enabled together.
    /// Unsupported legacy values are dropped during normalization.
    /// </summary>
    public HashSet<NotchAlgorithmVersion> EnabledVersions
    {
        get => _enabledVersions;
        set => _enabledVersions = NormalizeEnabledVersions(value);
    }

    public void ReplaceEnabledVersions(IEnumerable<NotchAlgorithmVersion>? versions)
    {
        _enabledVersions = NormalizeEnabledVersions(versions);
    }

    /// <summary>
    /// Gets or sets the scale factor applied to calculated notch lengths before they are stored in the notch table.
    /// This is typically used to convert floating-point lengths into integer representations.
    /// The reference system often uses a scale of 10.
    /// </summary>
    public int LenScale { get; set; } = 10;

    /// <summary>
    /// Gets or sets the integer value used to represent a "NULL" or undefined entry
    /// within the generated notch tables. The value must fit the firmware UINT16 domain.
    /// </summary>
    public int NullValue { get; set; } = 65535;

    internal static void ValidateNullValueOrThrow(int nullValue)
    {
        if (nullValue < 0 || nullValue > ushort.MaxValue)
        {
            throw new InvalidOperationException("NullValue must be in [0,65535].");
        }
    }

    /// <summary>
    /// Gets or sets the v2.1 gate threshold in Q7 (x/128).
    /// A value of 0 disables v2.1 threshold filtering.
    /// </summary>
    public int ThresholdQ7 { get; set; }

    /// <summary>
    /// Gets or sets the v2.2 gate threshold in percent (0..100).
    /// A value of 0 disables v2.2 threshold filtering.
    /// </summary>
    public double ThresholdPercentV22 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether v2.2 threshold should be linked to v2.1 threshold.
    /// When true, v2.2 threshold is derived from v2.1 Q7 value (Q7/128*100).
    /// </summary>
    public bool LinkVersionThresholds { get; set; } = true;

    /// <summary>
    /// Gets or sets Step3 compensation model preset.
    /// </summary>
    public NotchCompensationModel CompensationModel { get; set; } = NotchCompensationModel.CurrentGain;

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 Undo NF (To Regular) compensation is enabled.
    /// When disabled, To Regular ratio is fixed at 1.0 (100%).
    /// </summary>
    public bool EnableToRegular { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Notch 2.2 To Full compensation is enabled.
    /// When disabled, To Full ratio is fixed at 1.0 (100%).
    /// </summary>
    public bool EnableToFull { get; set; } = true;

    /// <summary>
    /// Gets or sets strict overlap threshold (%) used by multi-owner gating in Notch 2.2.
    /// Example: 0.1 means overlap must exceed 0.1% of regular-pad area to count as an owner.
    /// </summary>
    public double MultiOwnerStrictOverlapPercent { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets a value indicating whether the split To Full rule-engine path is enabled.
    /// When disabled, Step3 uses legacy inline gate resolution.
    /// </summary>
    public bool EnableToFullRuleEngine { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether per-regular To Full rule trace should be retained.
    /// </summary>
    public bool EnableToFullRuleTrace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether To Full virtual expansion area is capped
    /// before it contributes to Stage3 gain/allocation.
    /// </summary>
    public bool EnableBoundaryVirtualAreaCap { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum virtual expansion area relative to the original overlap area.
    /// Example: 1.0 means virtual To Full area cannot exceed the inside overlap area.
    /// </summary>
    public double BoundaryVirtualAreaCapRatio { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether target-side coverage should be capped
    /// after CurrentGain rows are selected.
    /// </summary>
    public bool EnableTargetCoverageGuard { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum target-side uniform-field coverage percent.
    /// Example: 120 means uniform 400 should not exceed 480 from notch redistribution.
    /// </summary>
    public int TargetCoverageCapPercent { get; set; } = 120;

    /// <summary>
    /// Gets or sets C-export profile.
    /// Release prunes no-op rows; Debug keeps full trace rows.
    /// </summary>
    public NotchExportProfile ExportProfile { get; set; } = NotchExportProfile.Release;

    /// <summary>
    /// Resolves effective To Regular/To Full switches for current compensation model.
    /// </summary>
    public (bool EnableToRegular, bool EnableToFull) ResolveStep3Switches()
    {
        return CompensationModel switch
        {
            NotchCompensationModel.CurrentGain => (EnableToRegular, EnableToFull),
            NotchCompensationModel.ConservativeNoGain => (true, true),
            NotchCompensationModel.Disabled => (false, false),
            _ => (EnableToRegular, EnableToFull),
        };
    }

    /// <summary>
    /// Resolves the legacy/CAD-level combine diagnostic for the current compensation model.
    /// V2.2 CurrentGain and ConservativeNoGain row payloads override this with target-regular coverage.
    /// </summary>
    public int ResolveCombinedPercent(int toRegularPercent, int toFullPercent) =>
        ResolveCombinedPercent(CompensationModel, toRegularPercent, toFullPercent);

    internal static int ResolveCombinedPercent(
        NotchCompensationModel model,
        int toRegularPercent,
        int toFullPercent) =>
        model switch
        {
            NotchCompensationModel.CurrentGain => (int)Math.Round((toRegularPercent * toFullPercent) / 100.0),
            NotchCompensationModel.ConservativeNoGain => toRegularPercent,
            NotchCompensationModel.Disabled => 100,
            _ => 100,
        };

    /// <summary>
    /// Resolves effective combined ratio for display/diagnostics under current compensation model.
    /// </summary>
    public double ResolveCombinedRatio(double toRegularRatio, double toFullRatio)
    {
        return CompensationModel switch
        {
            NotchCompensationModel.CurrentGain => toRegularRatio * toFullRatio,
            NotchCompensationModel.ConservativeNoGain => toRegularRatio,
            NotchCompensationModel.Disabled => 1.0,
            _ => 1.0,
        };
    }

    private static HashSet<NotchAlgorithmVersion> NormalizeEnabledVersions(IEnumerable<NotchAlgorithmVersion>? versions)
    {
        var normalized = versions is null
            ? new HashSet<NotchAlgorithmVersion>()
            : new HashSet<NotchAlgorithmVersion>(
                versions.Where(static version => version is NotchAlgorithmVersion.V21 or NotchAlgorithmVersion.V22));
        if (normalized.Count == 0)
        {
            normalized.Add(NotchAlgorithmVersion.V21);
            normalized.Add(NotchAlgorithmVersion.V22);
        }

        return normalized;
    }
}
