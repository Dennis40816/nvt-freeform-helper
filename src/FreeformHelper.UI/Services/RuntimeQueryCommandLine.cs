// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text.Json;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal static class RuntimeQueryCommandLine
{
    private static readonly string[] SupportedCommands =
    {
        "help",
        "status",
        "selection",
        "terminal",
        "terminal-links",
        "pad",
        "notch",
        "multi-owner",
        "notch-stage",
        "notch-validation",
        "load-project",
        "run-step",
        "clear-step",
        "select-cad",
        "select-regular",
        "clear-selection",
        "set-tofull",
        "simulation",
        "export-notch",
    };

    public static bool TryHandleQueryCommand(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], "query", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseCommandLine(args, out var command, out var commandArgs, out var timeoutMs, out var prettyJson, out var parseError))
        {
            var error = RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS", parseError ?? "Invalid arguments.");
            Console.WriteLine(JsonSerializer.Serialize(error, RuntimeQueryProtocol.PrettyJsonOptions));
            exitCode = 2;
            return true;
        }

        var request = new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: command,
            Args: commandArgs.Count == 0 ? null : commandArgs);
        var response = SendRequest(request, timeoutMs);
        var options = prettyJson ? RuntimeQueryProtocol.PrettyJsonOptions : RuntimeQueryProtocol.CompactJsonOptions;
        Console.WriteLine(JsonSerializer.Serialize(response, options));
        exitCode = response.Ok ? 0 : 1;
        return true;
    }

    internal static RuntimeQueryResponseEnvelope SendRequest(RuntimeQueryRequest request, int timeoutMs)
    {
        return RuntimeQueryIpcClient.SendRequest(
            RuntimeQueryProtocol.PipeName, request, timeoutMs, RuntimeQueryErrorMapper.Map);
    }

    private static bool TryParseCommandLine(
        string[] args,
        out string command,
        out Dictionary<string, string> commandArgs,
        out int timeoutMs,
        out bool prettyJson,
        out string? error)
    {
        command = string.Empty;
        commandArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        timeoutMs = 1500;
        prettyJson = true;
        error = null;

        if (args.Length < 2)
        {
            error = $"Usage: query <{string.Join("|", SupportedCommands)}> [--key value]";
            return false;
        }

        command = args[1].Trim().ToLowerInvariant();
        if (!SupportedCommands.Contains(command, StringComparer.Ordinal))
        {
            error = $"Unsupported query command '{command}'. Supported: {string.Join(", ", SupportedCommands)}.";
            return false;
        }

        for (var i = 2; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--json-compact", StringComparison.OrdinalIgnoreCase))
            {
                prettyJson = false;
                continue;
            }

            if (string.Equals(token, "--json-pretty", StringComparison.OrdinalIgnoreCase))
            {
                prettyJson = true;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected token '{token}'. Options must start with '--'.";
                return false;
            }

            string key;
            string value;
            var equalIndex = token.IndexOf('=');
            if (equalIndex > 2)
            {
                key = token.Substring(2, equalIndex - 2);
                value = token[(equalIndex + 1)..];
            }
            else
            {
                key = token[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }
                else
                {
                    value = "true";
                }
            }

            if (string.Equals(key, "timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout) ||
                    parsedTimeout <= 0 ||
                    parsedTimeout > 120000)
                {
                    error = "--timeout-ms must be in [1, 120000].";
                    return false;
                }

                timeoutMs = parsedTimeout;
                continue;
            }

            commandArgs[key] = value;
        }

        return true;
    }
}
