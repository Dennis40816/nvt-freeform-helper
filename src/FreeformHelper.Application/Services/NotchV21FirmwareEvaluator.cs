namespace FreeformHelper.Application.Services;

internal static class NotchV21FirmwareEvaluator
{
    internal static NotchV21FirmwareEvaluation Apply(
        NotchV21FirmwareProjection projection,
        IReadOnlyDictionary<NotchDiffKey, double> beforeByDiff,
        ICollection<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(beforeByDiff);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var buffer = beforeByDiff.ToDictionary(
            static item => item.Key,
            static item => QuantizeToFirmwareInt16(item.Value));
        var firmwareBaseline = buffer.ToDictionary(
            static item => item.Key,
            static item => (double)item.Value);

        for (var icIndex = 0; icIndex < projection.NodesByIc.Count; icIndex++)
        {
            foreach (var node in projection.NodesByIc[icIndex])
            {
                var destination = new NotchDiffKey(
                    icIndex,
                    node.DestinationDiffIndex);
                if (!buffer.TryGetValue(destination, out var current))
                {
                    AppendMissingDestinationDiagnostic(destination, diagnostics);
                    continue;
                }

                var percent = node.ReguToFullPercent;
                buffer[destination] = percent == 0
                    ? (short)0
                    : unchecked((short)((current * percent) / 100));
            }
        }

        var pendingOffsets = new List<(NotchDiffKey Destination, short Offset)>();
        for (var icIndex = 0; icIndex < projection.NodesByIc.Count; icIndex++)
        {
            foreach (var node in projection.NodesByIc[icIndex])
            {
                var destination = new NotchDiffKey(icIndex, node.DestinationDiffIndex);
                if (!buffer.ContainsKey(destination))
                {
                    continue;
                }

                short sum = 0;
                AddTerm(node.FirstDiffIndex, node.FirstType, node.FirstRatioQ7);
                AddTerm(node.SecondDiffIndex, node.SecondType, node.SecondRatioQ7);
                pendingOffsets.Add((destination, sum));

                void AddTerm(int refDiffIndex, int type, int ratioQ7)
                {
                    if (type == NotchV21Q7Codec.TypeNone)
                    {
                        return;
                    }

                    var source = new NotchDiffKey(icIndex, refDiffIndex);
                    if (!buffer.TryGetValue(source, out var sourceValue))
                    {
                        diagnostics.Add(
                            $"Missing v2.1 firmware source diff IC{icIndex + 1}/diff{refDiffIndex} in projected grid.");
                        return;
                    }

                    var scaled = NotchV21Q7Codec.ScaleSigned(sourceValue, type, ratioQ7);
                    sum = unchecked((short)(sum + scaled));
                }
            }
        }

        foreach (var pending in pendingOffsets)
        {
            buffer[pending.Destination] = unchecked((short)(buffer[pending.Destination] + pending.Offset));
        }

        return new NotchV21FirmwareEvaluation(
            firmwareBaseline,
            buffer.ToDictionary(static item => item.Key, static item => (double)item.Value));
    }

    internal static short QuantizeToFirmwareInt16(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        var integral = Math.Truncate(value);
        return (short)Math.Clamp(integral, short.MinValue, short.MaxValue);
    }

    private static void AppendMissingDestinationDiagnostic(
        NotchDiffKey destination,
        ICollection<string> diagnostics)
    {
        diagnostics.Add(
            $"Missing v2.1 firmware destination diff IC{destination.IcIndex + 1}/diff{destination.DiffIndex} in projected grid.");
    }
}

internal sealed record NotchV21FirmwareEvaluation(
    IReadOnlyDictionary<NotchDiffKey, double> BeforeByDiff,
    Dictionary<NotchDiffKey, double> AfterByDiff);
