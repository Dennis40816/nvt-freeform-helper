// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using Avalonia.Platform.Storage;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Records picker requests without implementing Avalonia's restricted storage interfaces.</summary>
public class RecordingFilePickerProvider : DispatchProxy
{
    public RecordingFilePickerProvider()
    {
    }

    public List<FilePickerOpenOptions> OpenRequests { get; } = [];
    public List<FilePickerSaveOptions> SaveRequests { get; } = [];
    public Exception? Failure { get; set; }

    public static IStorageProvider Create(out RecordingFilePickerProvider recorder)
    {
        var provider = DispatchProxy.Create<IStorageProvider, RecordingFilePickerProvider>();
        recorder = (RecordingFilePickerProvider)provider;
        return provider;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IStorageProvider.OpenFilePickerAsync):
                OpenRequests.Add((FilePickerOpenOptions)args![0]!);
                return Failure is { } openFailure
                    ? Task.FromException<IReadOnlyList<IStorageFile>>(openFailure)
                    : Task.FromResult<IReadOnlyList<IStorageFile>>([]);
            case nameof(IStorageProvider.SaveFilePickerAsync):
                SaveRequests.Add((FilePickerSaveOptions)args![0]!);
                return Failure is { } saveFailure
                    ? Task.FromException<IStorageFile?>(saveFailure)
                    : Task.FromResult<IStorageFile?>(null);
            default:
                throw new NotSupportedException($"Unexpected storage operation: {targetMethod?.Name}");
        }
    }
}

internal static class FilePickerRequestAssertions
{
    public static void Open(
        RecordingFilePickerProvider recorder, string title, params (string Name, string Pattern)[] filters)
    {
        var options = Assert.Single(recorder.OpenRequests);
        Assert.Empty(recorder.SaveRequests);
        Assert.Equal(title, options.Title);
        Assert.False(options.AllowMultiple);
        Assert.Null(options.SuggestedFileName);
        Filters(options.FileTypeFilter, filters);
    }

    public static void Save(
        RecordingFilePickerProvider recorder, string title, string extension, string fileName,
        params (string Name, string Pattern)[] filters)
    {
        var options = Assert.Single(recorder.SaveRequests);
        Assert.Empty(recorder.OpenRequests);
        Assert.Equal(title, options.Title);
        Assert.Equal(extension, options.DefaultExtension);
        Assert.Equal(fileName, options.SuggestedFileName);
        Filters(options.FileTypeChoices, filters);
    }

    private static void Filters(
        IReadOnlyList<FilePickerFileType>? actual, (string Name, string Pattern)[] expected)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].Pattern, Assert.Single(actual[i].Patterns ?? []));
        }
    }
}
