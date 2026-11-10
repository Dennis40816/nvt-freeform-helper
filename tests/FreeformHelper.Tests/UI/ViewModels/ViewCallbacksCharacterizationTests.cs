// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ViewCallbacksCharacterizationTests
{
    private static readonly (string Name, Type DeclaredType)[] _expectedCallbacks =
    [
        ("PickOpenDxfPathAsync", typeof(Func<Task<string?>>)),
        ("PickOpenRegularVisibilityMaskPathAsync", typeof(Func<Task<string?>>)),
        ("PickLoadProjectPathAsync", typeof(Func<Task<string?>>)),
        ("PickSaveProjectPathAsync", typeof(Func<Task<string?>>)),
        ("PickSaveNotchPathAsync", typeof(Func<string, string, Task<string?>>)),
        ("PickSaveExportPathAsync", typeof(Func<string, string, Task<string?>>)),
        ("ConfirmEmbedDxfAsync", typeof(Func<Task<bool>>)),
        ("ShowWarningAsync", typeof(Func<string, string, Task>)),
        ("EnsureNoPendingEdits", typeof(Func<bool>)),
        ("OpenNotchDetailAsync", typeof(Func<NotchDetailViewModel, Task>)),
        ("OpenNotchExportSelectionAsync", typeof(Func<NotchExportSelectionViewModel, Task<NotchTable?>>)),
        ("GetCurrentSimulationSafetyAudit", typeof(Func<SimulationSafetyAuditResult?>)),
        ("OpenDxfEditChangeListAsync", typeof(Func<DxfEditChangeListViewModel, Task>)),
        ("OpenIndexMappingReportAsync", typeof(Func<IndexMappingReportViewModel, Task>)),
        ("OpenDxfOverlapReportAsync", typeof(Func<DxfOverlapReportViewModel, Task>)),
        ("OpenDxfLayerImageExportAsync", typeof(Func<LayerImageExportContext, Task<LayerImageExportRequest?>>)),
    ];

    [Fact]
    public void CallbacksExposeTheExpectedPublicNamesAndDeclaredTypesWithPublicGettersAndSetters()
    {
        Assert.All(_expectedCallbacks, expected =>
        {
            var property = GetPublicCallbackProperty(expected.Name);
            Assert.Equal(expected.DeclaredType, property.PropertyType);
            Assert.NotNull(property.GetGetMethod());
            Assert.NotNull(property.GetSetMethod());
        });
    }

    [AvaloniaFact]
    public void NewViewModelHasNoCallbacks()
    {
        var vm = new FreeformHelperViewModel();

        Assert.All(_expectedCallbacks, expected => Assert.Null(GetPublicCallbackProperty(expected.Name).GetValue(vm)));
    }

    [AvaloniaFact]
    public void CallbacksRoundTripAndClearWithoutChangingOtherProperties()
    {
        var vm = new FreeformHelperViewModel();
        var callbacks = CreateCallbacks();
        var expected = new Delegate?[_expectedCallbacks.Length];

        for (var index = 0; index < _expectedCallbacks.Length; index++)
        {
            GetPublicCallbackProperty(_expectedCallbacks[index].Name).SetValue(vm, callbacks[index]);
            expected[index] = callbacks[index];
            AssertCallbacks(vm, expected);
        }

        for (var index = 0; index < _expectedCallbacks.Length; index++)
        {
            GetPublicCallbackProperty(_expectedCallbacks[index].Name).SetValue(vm, null);
            expected[index] = null;
            AssertCallbacks(vm, expected);
        }
    }

    [AvaloniaFact]
    public void CallbacksAreNotSharedBetweenViewModels()
    {
        var first = new FreeformHelperViewModel();
        var second = new FreeformHelperViewModel();
        var callbacks = CreateCallbacks();

        for (var index = 0; index < _expectedCallbacks.Length; index++)
        {
            var property = GetPublicCallbackProperty(_expectedCallbacks[index].Name);
            property.SetValue(first, callbacks[index]);
        }

        AssertCallbacks(first, callbacks);
        Assert.All(_expectedCallbacks, expected => Assert.Null(GetPublicCallbackProperty(expected.Name).GetValue(second)));
    }

    [AvaloniaFact]
    public void CallbackCanBeAssignedByObjectInitializer()
    {
        Func<Task<string?>> pickAsync = static () => Task.FromResult<string?>("initializer.dxf");
        var vm = new FreeformHelperViewModel()
        {
            PickOpenDxfPathAsync = pickAsync,
        };

        Assert.Same(pickAsync, vm.PickOpenDxfPathAsync);
    }

    [AvaloniaFact]
    public async Task OpenDxfCommandCallsTheAssignedPickerOnceAsync()
    {
        var path = TestFiles.NewTempFilePath("view-callback-missing-", ".dxf");
        var calls = 0;
        var vm = new FreeformHelperViewModel()
        {
            PickOpenDxfPathAsync = () =>
            {
                calls++;
                return Task.FromResult<string?>(path);
            },
        };

        await vm.OpenDxfCommand.ExecuteAsync(null);

        Assert.Equal(1, calls);
        Assert.Equal("DXF import failed: file not found.", vm.StatusText);
        Assert.Empty(vm.CadPads);
    }

    private static PropertyInfo GetPublicCallbackProperty(string name)
    {
        var property = typeof(FreeformHelperViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property;
    }

    private static void AssertCallbacks(FreeformHelperViewModel vm, Delegate?[] expected)
    {
        for (var index = 0; index < _expectedCallbacks.Length; index++)
        {
            var actual = GetPublicCallbackProperty(_expectedCallbacks[index].Name).GetValue(vm);
            if (expected[index] is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.Same(expected[index], actual);
            }
        }
    }

    private static Delegate[] CreateCallbacks() =>
    [
        (Func<Task<string?>>)(static () => Task.FromResult<string?>("open.dxf")),
        (Func<Task<string?>>)(static () => Task.FromResult<string?>("mask.csv")),
        (Func<Task<string?>>)(static () => Task.FromResult<string?>("load.json")),
        (Func<Task<string?>>)(static () => Task.FromResult<string?>("save.json")),
        (Func<string, string, Task<string?>>)(static (name, extension) => Task.FromResult<string?>($"notch-{name}.{extension}")),
        (Func<string, string, Task<string?>>)(static (name, extension) => Task.FromResult<string?>($"export-{name}.{extension}")),
        (Func<Task<bool>>)(static () => Task.FromResult(true)),
        (Func<string, string, Task>)(static (_, _) => Task.CompletedTask),
        (Func<bool>)(static () => true),
        (Func<NotchDetailViewModel, Task>)(static _ => Task.CompletedTask),
        (Func<NotchExportSelectionViewModel, Task<NotchTable?>>)(static _ => Task.FromResult<NotchTable?>(null)),
        (Func<SimulationSafetyAuditResult?>)(static () => null),
        (Func<DxfEditChangeListViewModel, Task>)(static _ => Task.CompletedTask),
        (Func<IndexMappingReportViewModel, Task>)(static _ => Task.CompletedTask),
        (Func<DxfOverlapReportViewModel, Task>)(static _ => Task.CompletedTask),
        (Func<LayerImageExportContext, Task<LayerImageExportRequest?>>)(static _ => Task.FromResult<LayerImageExportRequest?>(null)),
    ];
}
