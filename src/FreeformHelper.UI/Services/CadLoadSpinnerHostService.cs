using Avalonia.Controls;

namespace FreeformHelper.UI.Services;

internal static class CadLoadSpinnerHostService
{
    private static readonly CadLoadSpinnerProcessHost SharedInnerHost = new();
    private static readonly ICadLoadSpinnerHost SharedHostInstance = new SharedCadLoadSpinnerHost(SharedInnerHost);

    public static ICadLoadSpinnerHost SharedHost => SharedHostInstance;

    public static void WarmupSharedHost()
    {
        SharedInnerHost.Warmup();
    }

    public static void ShutdownSharedHost()
    {
        SharedInnerHost.Dispose();
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
