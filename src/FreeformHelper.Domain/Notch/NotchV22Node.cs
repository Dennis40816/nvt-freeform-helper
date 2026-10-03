namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Strongly-typed v2.2 notch node payload.
/// </summary>
public sealed record NotchV22Node(
    int AnchorDiffIndex,
    int CombinePercent,
    int TargetDiffIndex1,
    int TargetRatioPercent1,
    int TargetDiffIndex2,
    int TargetRatioPercent2,
    int Flags)
{
    public int[] ToValues()
    {
        return new[]
        {
            AnchorDiffIndex,
            CombinePercent,
            TargetDiffIndex1,
            TargetRatioPercent1,
            TargetDiffIndex2,
            TargetRatioPercent2,
            Flags
        };
    }

    public static NotchV22Node FromValues(IReadOnlyList<int> values)
    {
        if (values.Count < 7)
        {
            throw new ArgumentException("v2.2 node requires at least 7 values.", nameof(values));
        }

        return new NotchV22Node(
            AnchorDiffIndex: values[0],
            CombinePercent: values[1],
            TargetDiffIndex1: values[2],
            TargetRatioPercent1: values[3],
            TargetDiffIndex2: values[4],
            TargetRatioPercent2: values[5],
            Flags: values[6]);
    }
}
