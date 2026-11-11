using FreeformHelper.Application.Settings;

namespace FreeformHelper.Application.CadImport;

/// <summary>Builds the existing source-layer guidance from accepted inputs.</summary>
public static class CadRegularSourceRules
{
    public static string BuildHint(RegularSourceMode mode, bool hasCad, string? name, int count,
        decimal xChannels, decimal yChannels, decimal cascadeNum)
    {
        if (mode != RegularSourceMode.FromDxfLayer) return string.Empty;
        if (!hasCad) return "DXF regular source: load DXF first.";
        if (string.IsNullOrWhiteSpace(name)) return "DXF regular source: select a regular layer.";
        if (count == 0) return $"Regular layer '{name}' has no visible pads.";
        var x = Math.Max(1, (int)Math.Round(xChannels));
        var y = Math.Max(1, (int)Math.Round(yChannels));
        if (count == x * y) return $"Regular layer '{name}': pads={count}, matches X*Y={x}*{y}.";
        return BuildDxfRegularSourceMismatchHint(count, x, y, Math.Max(1, (int)Math.Round(cascadeNum)));
    }

    public static string BuildDxfRegularSourceMismatchHint(int padCount, int x, int y, int cascade)
    {
        var expected = x * y;
        var hints = new List<string>
        {
            $"Regular layer pads={padCount}, current X*Y={x}*{y}={expected}.",
        };
        int? keepYSuggestedX = null;
        if (y > 0 && padCount % y == 0)
        {
            keepYSuggestedX = padCount / y;
            hints.Add($"Keep Y={y} -> set X={keepYSuggestedX.Value}.");
        }
        int? keepXSuggestedY = null;
        if (x > 0 && padCount % x == 0)
        {
            keepXSuggestedY = padCount / x;
            hints.Add($"Keep X={x} -> set Y={keepXSuggestedY.Value}.");
        }
        var (bestX, bestY) = FindClosestFactorPair(padCount, x, y);
        if (keepYSuggestedX != bestX || keepXSuggestedY != bestY)
        {
            hints.Add($"Closest exact pair: X={bestX}, Y={bestY}.");
        }
        var hintXForCascade = keepYSuggestedX ?? bestX;
        if (cascade > hintXForCascade)
        {
            hints.Add($"Cascade={cascade} exceeds suggested X={hintXForCascade}; set Cascade <= {hintXForCascade}.");
        }
        else if (cascade > 1)
        {
            var perIc = SplitPerIcColumns(hintXForCascade, cascade);
            hints.Add($"With Cascade={cascade}, suggested per-IC X: {string.Join("/", perIc)}.");
        }
        return string.Join(" ", hints);
    }
    private static (int X, int Y) FindClosestFactorPair(int product, int preferX, int preferY)
    {
        if (product <= 0)
        {
            return (Math.Max(1, preferX), Math.Max(1, preferY));
        }
        var bestX = product;
        var bestY = 1;
        var bestDistance = int.MaxValue;
        var bestAspectDistance = double.MaxValue;
        var targetAspect = preferY > 0 ? (double)preferX / preferY : 1.0;
        for (var d = 1; d * d <= product; d++)
        {
            if (product % d != 0)
            {
                continue;
            }
            var candidates = new[]
            {
                (X: product / d, Y: d),
                (X: d, Y: product / d),
            };
            foreach (var candidate in candidates)
            {
                var distance = Math.Abs(candidate.X - preferX) + Math.Abs(candidate.Y - preferY);
                var aspect = candidate.Y > 0 ? (double)candidate.X / candidate.Y : double.MaxValue;
                var aspectDistance = Math.Abs(aspect - targetAspect);
                if (distance < bestDistance ||
                    (distance == bestDistance && aspectDistance < bestAspectDistance))
                {
                    bestDistance = distance;
                    bestAspectDistance = aspectDistance;
                    bestX = candidate.X;
                    bestY = candidate.Y;
                }
            }
        }
        return (bestX, bestY);
    }
    private static List<int> SplitPerIcColumns(int totalX, int cascade)
    {
        var safeCascade = Math.Clamp(cascade, 1, Math.Max(1, totalX));
        var baseCols = totalX / safeCascade;
        var remainder = totalX % safeCascade;
        var result = new List<int>(safeCascade);
        for (var ic = 0; ic < safeCascade; ic++)
        {
            result.Add(baseCols + (ic < remainder ? 1 : 0));
        }
        return result;
    }
}
