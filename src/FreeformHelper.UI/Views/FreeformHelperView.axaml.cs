using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Interaction logic for the FreeformHelperView.axaml user control.
/// This class handles the view-specific logic, UI element interactions,
/// and delegates tasks to its ViewModel (<see cref="ViewModels.FreeformHelperViewModel"/>).
/// </summary>
public sealed partial class FreeformHelperView : UserControl
{
    private UiEventRunner? _uiEvents => (DataContext as FreeformHelperViewModel)?.UiEvents;

    private const int ConsoleFallbackTailLines = 4000;

    // References to key UI controls, initialized after InitializeComponent.
    private PadCanvas? _canvas;
    private Canvas? _padInfoOverlay;
    private PadInfoPopover? _padInfoPopover;
    private Avalonia.Controls.Shapes.Path? _padInfoLink;
    private Avalonia.Controls.Shapes.Ellipse? _padInfoAnchor;
    private RowDefinition? _consoleRow;
    private ConsolePanel? _consolePanel;
    private TextEditor? _consoleEditor;
    private Border? _consoleBorder;
    private bool _consoleHasFocus;
    private IDisposable? _consoleFocusWithinSubscription;
    private ViewModels.ShellViewModel? _shellViewModel;
    private TopLevel? _topLevel;
    private Menu? _mainMenu;
    private Window? _initialFitWindow;
    private EventHandler? _initialFitHandler;
    private SettingsWindow? _settingsWindow;
    private NotchExportSelectionWindow? _notchExportSelectionWindow;
    private DxfEditChangeListWindow? _dxfEditChangeListWindow;
    private IndexMappingReportWindow? _indexMappingReportWindow;
    private Window? _notchExportSelectionOwner;
    private bool _isNotchExportSelectionHidden;
    private Border? _notchExportRestoreHint;
    private bool _isViewAttached;
    private bool _isCadLoadSpinnerHostVisible;
    private bool _isPadInfoLayoutUpdateQueued;
    private FreeformHelperViewModel? _observedViewModel;
    private ICadLoadSpinnerHost? _cadLoadSpinnerHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeformHelperView"/> class.
    /// </summary>
    public FreeformHelperView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        SetLeftPanelVisible(true);
        SetRightPanelVisible(true);

        _canvas = FindCanvas();
        WireCanvasEvents(_canvas);
        _notchExportRestoreHint = this.FindControl<Border>("NotchExportRestoreHintBorder");

        _consoleRow = this.FindControl<Grid>("CanvasGrid")?.RowDefinitions.ElementAtOrDefault(2);
        ResolveConsoleShellControls();
        _mainMenu = this.FindControl<Menu>("MainMenu");

        if (_consoleBorder is not null)
        {
            _consoleFocusWithinSubscription = _consoleBorder
                .GetObservable(InputElement.IsKeyboardFocusWithinProperty)
                .Subscribe(focused => SetConsoleFocus(focused));
        }

        UpdateConsoleFocusVisual();
        UpdateNotchExportRestoreHintVisibility();
    }
}
