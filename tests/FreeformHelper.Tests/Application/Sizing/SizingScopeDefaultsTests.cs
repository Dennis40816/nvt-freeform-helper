using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SizingScopeDefaultsTests
{
    [Fact]
    public void FreeformHelperViewModel_DefaultSizingScope_IsLocal()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.UseLocalSizing);
        Assert.True(vm.IsWidthRowLocal);
        Assert.True(vm.IsHeightColumnLocal);
    }
}
