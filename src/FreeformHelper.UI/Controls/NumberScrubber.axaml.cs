using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Controls;

public sealed partial class NumberScrubber : UserControl
{
    private Border? _rootBorder;
    private Border? _scrubArea;
    private TextBox? _inputBox;
    private bool _isEditing;
    private bool _isScrubbing;
    private Point _scrubStart;
    private decimal _scrubStartValue;

    public NumberScrubber()
    {
        InitializeComponent();
        AttachControls();
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble);
    }
}
