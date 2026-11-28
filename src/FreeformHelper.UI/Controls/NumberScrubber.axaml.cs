using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Controls;

public sealed partial class NumberScrubber : UserControl
{
    // Required parts. The constructor fails when the XAML does not declare one of them.
    private readonly Border _rootBorder;
    private readonly Border _scrubArea;
    private readonly TextBox _inputBox;
    private bool _isEditing;
    private bool _isScrubbing;
    private Point _scrubStart;
    private decimal _scrubStartValue;

    public NumberScrubber()
    {
        InitializeComponent();
        _rootBorder = RequirePart<Border>("RootBorder");
        _scrubArea = RequirePart<Border>("ScrubArea");
        _inputBox = RequirePart<TextBox>("InputBox");
        AttachControls();
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Bubble);
    }

    private TPart RequirePart<TPart>(string name)
        where TPart : Control =>
        this.FindControl<TPart>(name) ?? throw new InvalidOperationException($"NumberScrubber requires the '{name}' part.");
}
