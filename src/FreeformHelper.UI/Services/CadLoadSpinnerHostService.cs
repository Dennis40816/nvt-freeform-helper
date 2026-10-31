using Avalonia.Controls;

namespace FreeformHelper.UI.Services;

internal static class CadLoadSpinnerHostService
{
    private static readonly CadLoadSpinnerProcessHost _sharedInnerHost = new();
    private static readonly ICadLoadSpinnerHost _sharedHostInstance = new SharedCadLoadSpinnerHost(_sharedInnerHost);

    public static ICadLoadSpinnerHost SharedHost => _sharedHostInstance;

    public static void WarmupSharedHost()
    {
        _sharedInnerHost.Warmup();
    }

    public static void ShutdownSharedHost()
    {
        _sharedInnerHost.Dispose();
    }

    private sealed class SharedCadLoadSpinnerHost(CadLoadSpinnerProcessHost innerHost) : ICadLoadSpinnerHost
    {
        public void Warmup()
        {
            innerHost.Warmup();
        }

        public void Show(Window owner)
        {
            innerHost.Show(owner);
        }

        public void Hide()
        {
            innerHost.Hide();
        }

        public void Dispose()
        {
            // Shared lifetime is owned by App exit, not by individual views.
        }
    }
}
