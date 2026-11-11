namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The option lists of the settings window draft and the option that each list selects when the window opens.
/// A selection that is not in its list, or has no display text, falls back to the first option of the list.
/// </summary>
internal sealed class SettingsWindowOptionSet
{
    public SettingsWindowOptionSet(FreeformHelperViewModel owner)
    {
        ScanOrder = owner.ScanOrderOptions.ToList();
        GridAlignment = owner.GridAlignmentOptions.ToList();
        RegularSourceMode = owner.RegularSourceModeOptions.ToList();
        RegularSourceLayer = owner.RegularSourceLayerOptions.ToList();
        LogLevel = owner.LogLevelOptions.ToList();
        CadOutputFwDiffAutoMode = owner.CadOutputFwDiffAutoModeOptions.ToList();
        NotchExportFileType = owner.NotchExportFileTypeOptions.ToList();
        NotchExportProfile = owner.NotchExportProfileOptions.ToList();
        NotchCompensationModel = owner.NotchCompensationModelOptions.ToList();

        SelectedScanOrder = PickOption(
            ScanOrder, o => o.Value == owner.SelectedScanOrderOption.Value, static o => o.Display);
        SelectedGridAlignment = PickOption(
            GridAlignment, o => o.Value == owner.SelectedGridAlignmentOption.Value, static o => o.Display);
        SelectedRegularSourceMode = PickOption(
            RegularSourceMode, o => o.Value == owner.SelectedRegularSourceModeOption.Value, static o => o.Display);
        SelectedRegularSourceLayer = RegularSourceLayer.FirstOrDefault(o =>
            string.Equals(o.Name, owner.SelectedRegularSourceLayerOption.Name, StringComparison.Ordinal))
            ?? GetFirstOrDefault(RegularSourceLayer)
            ?? new RegularSourceLayerOption(null, "Select layer");
        SelectedLogLevel = PickOption(
            LogLevel,
            o => string.Equals(o.Value, owner.SelectedLogLevelOption.Value, StringComparison.OrdinalIgnoreCase),
            static o => o.Display);
        SelectedCadOutputFwDiffAutoMode = PickOption(
            CadOutputFwDiffAutoMode, o => o.Value == owner.SelectedCadOutputFwDiffAutoModeOption.Value, static o => o.Display);
        SelectedNotchExportFileType = PickOption(
            NotchExportFileType, o => o.Value == owner.SelectedNotchExportFileTypeOption.Value, static o => o.Display);
        SelectedNotchExportProfile = PickOption(
            NotchExportProfile, o => o.Value == owner.SelectedNotchExportProfileOption.Value, static o => o.Display);
        SelectedNotchCompensationModel = PickOption(
            NotchCompensationModel, o => o.Value == owner.SelectedNotchCompensationModelOption.Value, static o => o.Display);
    }

    public IReadOnlyList<ScanOrderOption> ScanOrder { get; }

    public IReadOnlyList<GridAlignmentOption> GridAlignment { get; }

    public IReadOnlyList<RegularSourceModeOption> RegularSourceMode { get; }

    public IReadOnlyList<RegularSourceLayerOption> RegularSourceLayer { get; }

    public IReadOnlyList<LogLevelOption> LogLevel { get; }

    public IReadOnlyList<CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoMode { get; }

    public IReadOnlyList<NotchExportFileTypeOption> NotchExportFileType { get; }

    public IReadOnlyList<NotchExportProfileOption> NotchExportProfile { get; }

    public IReadOnlyList<NotchCompensationModelOption> NotchCompensationModel { get; }

    public ScanOrderOption SelectedScanOrder { get; }

    public GridAlignmentOption SelectedGridAlignment { get; }

    public RegularSourceModeOption SelectedRegularSourceMode { get; }

    public RegularSourceLayerOption SelectedRegularSourceLayer { get; }

    public LogLevelOption SelectedLogLevel { get; }

    public CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoMode { get; }

    public NotchExportFileTypeOption SelectedNotchExportFileType { get; }

    public NotchExportProfileOption SelectedNotchExportProfile { get; }

    public NotchCompensationModelOption SelectedNotchCompensationModel { get; }

    private static T PickOption<T>(IReadOnlyList<T> options, Func<T, bool> isMatch, Func<T, string?> display)
    {
        var picked = options.FirstOrDefault(isMatch);
        return string.IsNullOrWhiteSpace(display(picked!)) ? GetFirstOrDefault(options) : picked!;
    }

    private static T GetFirstOrDefault<T>(IReadOnlyList<T> list)
    {
        return list.Count > 0 ? list[0] : default!;
    }
}
