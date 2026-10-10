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
