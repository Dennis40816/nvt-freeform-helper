using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> contains nested types
/// that serve as data models or option structures specifically used within the UI layer.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Represents an option for selecting a scan order in the UI.
    /// </summary>
    /// <param name="Value">The <see cref="ScanOrder"/> enumeration value.</param>
    /// <param name="Display">The human-readable string for display in the UI.</param>
    public readonly record struct ScanOrderOption(ScanOrder Value, string Display);

    /// <summary>
    /// Represents an option for selecting a grid alignment mode in the UI.
    /// </summary>
    /// <param name="Value">The <see cref="GridAlignmentMode"/> enumeration value.</param>
    /// <param name="Display">The human-readable string for display in the UI.</param>
    public readonly record struct GridAlignmentOption(GridAlignmentMode Value, string Display);

    /// <summary>
    /// Represents an option for selecting how regular pads are sourced.
    /// </summary>
    /// <param name="Value">The <see cref="RegularSourceMode"/> enumeration value.</param>
    /// <param name="Display">The human-readable string for display in the UI.</param>
    public readonly record struct RegularSourceModeOption(RegularSourceMode Value, string Display);

    /// <summary>
    /// Represents the scope used by DXF geometry rotation.
    /// </summary>
    public enum DxfEditRotationScope
    {
        SelectedPads,
        TargetLayer,
    }

    /// <summary>
    /// Represents an option for DXF rotation scope selection.
    /// </summary>
    /// <param name="Value">Scope enum value.</param>
    /// <param name="Display">Human-readable label.</param>
    /// <param name="Description">Short behavior note for tooltip/help text.</param>
    public readonly record struct DxfEditRotationScopeOption(DxfEditRotationScope Value, string Display, string Description);

    /// <summary>
    /// Represents an option for selecting the minimum application log level in the UI.
    /// </summary>
    /// <param name="Value">The NLog level name (for example, "Info").</param>
    /// <param name="Display">The human-readable string for display in the UI.</param>
    public readonly record struct LogLevelOption(string Value, string Display);

    /// <summary>
    /// Represents an option for selecting CAD auto CAD Output FW Diff assignment mode.
    /// </summary>
    /// <param name="Value">The <see cref="CadOutputFwDiffAutoMode"/> value.</param>
    /// <param name="Display">The human-readable string for display in the UI.</param>
    /// <param name="Description">The rationale and tradeoff shown in hover/help text.</param>
    public readonly record struct CadOutputFwDiffAutoModeOption(CadOutputFwDiffAutoMode Value, string Display, string Description);

    /// <summary>
    /// Represents supported notch export file types.
    /// </summary>
    public enum NotchExportFileType
    {
        Csv,
        Cv21,
        Cv22,
    }

    /// <summary>
    /// Represents an option for selecting notch export file type.
    /// </summary>
    /// <param name="Value">Export file type value.</param>
    /// <param name="Display">Human-readable label.</param>
    /// <param name="Extension">File extension (without dot).</param>
    /// <param name="PinnedVersion">Single-version export scope implied by this format, if any.</param>
    public readonly record struct NotchExportFileTypeOption(
        NotchExportFileType Value,
        string Display,
        string Extension,
        NotchAlgorithmVersion? PinnedVersion = null);

    /// <summary>
    /// Represents an option for notch C-export profile.
    /// </summary>
    /// <param name="Value">Export profile value.</param>
    /// <param name="Display">Human-readable label.</param>
    public readonly record struct NotchExportProfileOption(NotchExportProfile Value, string Display);

    /// <summary>
    /// Represents an option for Step3 notch compensation model selection.
    /// </summary>
    /// <param name="Value">Compensation model value.</param>
    /// <param name="Display">Human-readable label.</param>
    /// <param name="Description">One-line behavior summary.</param>
    public readonly record struct NotchCompensationModelOption(
        NotchCompensationModel Value,
        string Display,
        string Description);

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
    /// Represents the latest selection pipeline timing breakdown captured by the ViewModel.
    /// </summary>
    public readonly record struct SelectionTimingSnapshot(
        long SummaryMs,
        long InspectorMs,
        long NotchPreviewMs,
        long TotalMs);

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

    /// <summary>
    /// Represents settings for a single cascaded IC, specifically its X and Y channel counts.
    /// This is used for binding in the UI to allow per-IC channel adjustments.
    /// </summary>
    public sealed partial class CascadeIcSetting : ObservableObject
    {
        /// <summary>
        /// Gets the zero-based index of the IC.
        /// </summary>
        [ObservableProperty] private int _icIndex;
        /// <summary>
        /// Gets or sets the number of X channels for this IC.
        /// </summary>
        [ObservableProperty] private decimal _xChannels;
        /// <summary>
        /// Gets or sets the number of Y channels for this IC.
        /// </summary>
        [ObservableProperty] private decimal _yChannels;

        /// <summary>
        /// Event raised before an X or Y channel property is changed.
        /// </summary>
        public event EventHandler<CascadeChannelChangingEventArgs>? ChannelChanging;

        /// <summary>
        /// Initializes a new instance of the <see cref="CascadeIcSetting"/> class.
        /// </summary>
        /// <param name="icIndex">The index of the IC.</param>
        /// <param name="xChannels">The initial number of X channels.</param>
        /// <param name="yChannels">The initial number of Y channels.</param>
        public CascadeIcSetting(int icIndex, int xChannels, int yChannels)
        {
            _icIndex = icIndex;
            _xChannels = xChannels;
            _yChannels = yChannels;
        }

        /// <summary>
        /// Partial method hook invoked before the <see cref="XChannels"/> property changes.
        /// Raises the <see cref="ChannelChanging"/> event.
        /// </summary>
        /// <param name="value">The new value for <see cref="XChannels"/>.</param>
        partial void OnXChannelsChanging(decimal value)
        {
            ChannelChanging?.Invoke(this, new CascadeChannelChangingEventArgs(nameof(XChannels), XChannels, value));
        }

        /// <summary>
        /// Partial method hook invoked before the <see cref="YChannels"/> property changes.
        /// Raises the <see cref="ChannelChanging"/> event.
        /// </summary>
        /// <param name="value">The new value for <see cref="YChannels"/>.</param>
        partial void OnYChannelsChanging(decimal value)
        {
            ChannelChanging?.Invoke(this, new CascadeChannelChangingEventArgs(nameof(YChannels), YChannels, value));
        }
    }

    /// <summary>
    /// Provides event data for when a channel count within a <see cref="CascadeIcSetting"/> is changing.
    /// </summary>
    public sealed class CascadeChannelChangingEventArgs : EventArgs
    {
        public CascadeChannelChangingEventArgs(string propertyName, decimal oldValue, decimal newValue)
        {
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string PropertyName { get; }

        public decimal OldValue { get; }

        public decimal NewValue { get; }
    }
}
