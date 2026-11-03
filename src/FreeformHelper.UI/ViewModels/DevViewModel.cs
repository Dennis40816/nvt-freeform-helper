using CommunityToolkit.Mvvm.ComponentModel;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// ViewModel for the Dev page used to preview controls, tokens, and icon sizing.
/// </summary>
public sealed partial class DevViewModel : ObservableObject
{
    internal UiEventRunner? UiEvents { get; init; }

    [ObservableProperty]
    private string _status = "Dev preview";
}
