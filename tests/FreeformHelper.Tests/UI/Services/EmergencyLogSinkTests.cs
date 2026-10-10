// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text;
using FreeformHelper.UI.Logging;
using NLog;
using NLog.Common;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class EmergencyLogSinkTests
{
    [Fact]
    public void Report_WriterThrows_DoesNotThrow()
    {
        var oldWriter = InternalLogger.LogWriter;
        var oldLevel = InternalLogger.LogLevel;
        var oldConsoleError = InternalLogger.LogToConsoleError;
        var writer = new FailingEmergencyWriter();
        try
        {
            InternalLogger.LogWriter = writer;
            InternalLogger.LogLevel = LogLevel.Error;
            InternalLogger.LogToConsoleError = false;

            var exception = Record.Exception(() => EmergencyLogSink.Report(
                "DxfOverlap.CopyAll", new InvalidOperationException("clipboard unavailable")));

            Assert.Null(exception);
            Assert.True(writer.Attempts > 0);
        }
        finally
        {
            InternalLogger.LogWriter = oldWriter;
            InternalLogger.LogLevel = oldLevel;
            InternalLogger.LogToConsoleError = oldConsoleError;
        }
    }

    [Fact]
    public void Report_InternalLoggerOff_WritesOperationAndExceptionToStandardError()
    {
        var oldError = Console.Error;
        var oldLevel = InternalLogger.LogLevel;
        using var writer = new StringWriter();
        try
        {
            Console.SetError(writer);
            InternalLogger.LogLevel = LogLevel.Off;

            EmergencyLogSink.Report("DxfOverlap.CopyAll", new InvalidOperationException("clipboard unavailable"));

            Assert.Equal("UI event DxfOverlap.CopyAll failed: System.InvalidOperationException: clipboard unavailable" +
                writer.NewLine, writer.ToString());
        }
        finally
        {
            Console.SetError(oldError);
            InternalLogger.LogLevel = oldLevel;
        }
    }

    [Fact]
    public void Report_StandardErrorWriterThrows_DoesNotThrow()
    {
        var oldError = Console.Error;
        using var writer = new FailingEmergencyWriter();
        try
        {
            Console.SetError(TextWriter.Synchronized(writer));

            var exception = Record.Exception(() => EmergencyLogSink.Report(
                "DxfOverlap.CopyAll", new InvalidOperationException("clipboard unavailable")));

            Assert.Null(exception);
            Assert.Equal(1, writer.Attempts);
        }
        finally
        {
            Console.SetError(oldError);
        }
    }

    private sealed class FailingEmergencyWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public int Attempts { get; private set; }

        public override void WriteLine(string? value)
        {
            Attempts++;
            throw new InvalidOperationException("emergency writer unavailable");
        }
    }
}
