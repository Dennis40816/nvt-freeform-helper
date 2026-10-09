using Avalonia.Media;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class UndoSuppressionTests
{
    [Fact]
    public void FreshInstance_IsInactive()
    {
        var suppression = new UndoSuppression();

        Assert.False(suppression.IsActive);
    }

    [Fact]
    public void Enter_NestedScopes_KeepsOuterActiveUntilLastDispose()
    {
        var suppression = new UndoSuppression();

        using (suppression.Enter())
        {
            Assert.True(suppression.IsActive);
            using (suppression.Enter())
            {
                Assert.True(suppression.IsActive);
            }

            Assert.True(suppression.IsActive);
        }

        Assert.False(suppression.IsActive);
    }

    [Fact]
    public void Enter_Exception_ReleasesScope()
    {
        var suppression = new UndoSuppression();

        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var scope = suppression.Enter();
            Assert.True(suppression.IsActive);
            throw new InvalidOperationException("Programmatic change failed.");
        }));

        Assert.False(suppression.IsActive);
    }

    [Fact]
    public void Scope_DoubleDispose_DecrementsOnce()
    {
        var suppression = new UndoSuppression();
        var outer = suppression.Enter();
        var inner = suppression.Enter();

        inner.Dispose();
        inner.Dispose();
        Assert.True(suppression.IsActive);

        outer.Dispose();
        outer.Dispose();
        Assert.False(suppression.IsActive);

        using (suppression.Enter())
        {
            Assert.True(suppression.IsActive);
        }

        Assert.False(suppression.IsActive);
    }

    [Fact]
    public void Enter_NonLifoDisposal_EndsInactive()
    {
        var suppression = new UndoSuppression();
        var first = suppression.Enter();
        var second = suppression.Enter();

        first.Dispose();
        Assert.True(suppression.IsActive);

        second.Dispose();
        Assert.False(suppression.IsActive);
    }
}

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void Undo_InsideOuterSuppression_KeepsRecordingSuppressed()
    {
        var vm = new FreeformHelperViewModel();
        var suppression = GetPrivateField<UndoSuppression>(vm, "_undoSuppression");
        var original = vm.ShowCad;
        vm.ShowCad = !original;
        Assert.True(vm.CanUndo);

        using (suppression.Enter())
        {
            vm.Undo();
            Assert.Equal(original, vm.ShowCad);
            Assert.False(vm.CanUndo);
            Assert.True(suppression.IsActive);

            vm.ShowCad = !original;
            Assert.False(vm.CanUndo);
        }

        Assert.False(suppression.IsActive);
        vm.ShowCad = original;
        Assert.True(vm.CanUndo);
    }

    [Fact]
    public void Undo_OutsideSuppression_DoesNotRecordItsActionAndRestoresRecording()
    {
        var vm = new FreeformHelperViewModel();
        var suppression = GetPrivateField<UndoSuppression>(vm, "_undoSuppression");
        var original = vm.ShowCad;
        vm.ShowCad = !original;
        Assert.True(vm.CanUndo);

        vm.Undo();

        Assert.Equal(original, vm.ShowCad);
        Assert.False(vm.CanUndo);
        Assert.False(suppression.IsActive);

        vm.ShowCad = !original;
        Assert.True(vm.CanUndo);
        vm.Undo();
        Assert.Equal(original, vm.ShowCad);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void ProgrammaticChanges_NestedCallbacks_DoNotRecordUndo()
    {
        var vm = new FreeformHelperViewModel();
        var originalShowCad = vm.ShowCad;
        var nestedChangeObserved = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.CadLineColorHex))
            {
                vm.RegularLineColor = Color.FromRgb(0x44, 0x55, 0x66);
                vm.ShowCad = !originalShowCad;
                nestedChangeObserved = true;
            }
        };

        vm.CadLineColor = Color.FromRgb(0x11, 0x22, 0x33);

        Assert.True(nestedChangeObserved);
        Assert.Equal("#112233", vm.CadLineColorHex);
        Assert.Equal("#445566", vm.RegularLineColorHex);
        Assert.Equal(!originalShowCad, vm.ShowCad);
        Assert.False(vm.CanUndo);

        vm.ShowCad = originalShowCad;
        Assert.True(vm.CanUndo);
        vm.Undo();
        Assert.False(vm.CanUndo);
    }
}
