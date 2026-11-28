// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls.Notifications;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies save shortcut toast messages and notification precedence.
/// </summary>
public sealed class SaveShortcutToastPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Resolve_SuccessWithoutStatus_UsesDefaultMessage(string? statusText)
    {
        var toast = SaveShortcutToastPolicy.Resolve(true, statusText);

        Assert.Equal("Project saved.", toast.Message);
        Assert.Equal(NotificationType.Success, toast.Type);
    }

    [Fact]
    public void Resolve_SuccessWithStatus_PreservesMessage()
    {
        const string statusText = "  Saved current project.  ";

        var toast = SaveShortcutToastPolicy.Resolve(true, statusText);

        Assert.Equal(statusText, toast.Message);
        Assert.Equal(NotificationType.Success, toast.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Resolve_FailureWithoutStatus_UsesDefaultMessage(string? statusText)
    {
        var toast = SaveShortcutToastPolicy.Resolve(false, statusText);

        Assert.Equal("Save project failed.", toast.Message);
        Assert.Equal(NotificationType.Warning, toast.Type);
    }

    [Fact]
    public void Resolve_FailureWithStatus_PreservesMessage()
    {
        const string statusText = "  Project is read-only.  ";

        var toast = SaveShortcutToastPolicy.Resolve(false, statusText);

        Assert.Equal(statusText, toast.Message);
        Assert.Equal(NotificationType.Warning, toast.Type);
    }

    [Fact]
    public void Resolve_FailureWithCanceledStatus_UsesInformation()
    {
        const string statusText = "Canceled by user";

        var toast = SaveShortcutToastPolicy.Resolve(false, statusText);

        Assert.Equal(statusText, toast.Message);
        Assert.Equal(NotificationType.Information, toast.Type);
    }

    [Fact]
    public void Resolve_SuccessWithCancelInStatus_StillUsesSuccess()
    {
        const string statusText = "Saved after a canceled attempt.";

        var toast = SaveShortcutToastPolicy.Resolve(true, statusText);

        Assert.Equal(statusText, toast.Message);
        Assert.Equal(NotificationType.Success, toast.Type);
    }
}
