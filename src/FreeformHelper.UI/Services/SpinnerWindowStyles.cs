namespace FreeformHelper.UI.Services;

/// <summary>
/// Win32 style and position flags that <c>CadLoadSpinnerWindow</c> uses to make the spinner a click-through tool window.
/// </summary>
internal static class SpinnerWindowStyles
{
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;

    internal const long WsCaption = 0x00C00000L;
    internal const long WsThickFrame = 0x00040000L;
    internal const long WsBorder = 0x00800000L;
    internal const long WsDlgFrame = 0x00400000L;
    internal const long WsSysMenu = 0x00080000L;
    internal const long WsMinimizeBox = 0x00020000L;
    internal const long WsMaximizeBox = 0x00010000L;

    internal const long WsExLayered = 0x00080000L;
    internal const long WsExTransparent = 0x00000020L;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExNoActivate = 0x08000000L;

    internal const uint SwpNosize = 0x0001;
    internal const uint SwpNomove = 0x0002;
    internal const uint SwpNozorder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
}
