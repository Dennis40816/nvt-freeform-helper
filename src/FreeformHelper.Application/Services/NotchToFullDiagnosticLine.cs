namespace FreeformHelper.Application.Services;

/// <summary>
/// The one pattern of a To Full diagnostics line. The generator writes the line, and the notch details and the
/// pad inspector trace both parse it with this pattern, so a new field cannot be added to only one parser.
/// </summary>
public static class NotchToFullDiagnosticLine
{
    public const string Pattern =
        @"IC(?<ic>\d+)\/diff(?<diff>-?\d+)\s+reg(?<reg>\d+)\s*:\s*" +
        @"ov=(?<ov>[-\d.]+),\s*" +
        @"src=(?<src>[-\d.]+),\s*" +
        @"blk=(?<blk>[-\d.]+),\s*" +
        @"reach=(?<reach>[-\d.]+),\s*" +
        @"blocker=(?<blocker>\d+)," +
        @"(?:\s*owners=(?<owners>[^,]+),)?" +
        @"(?:\s*ownerCnt=\d+,)?" +
        @"(?:\s*ownerShare=(?<ownershare>[^,]+),)?" +
        @"\s*boundary=(?<boundary>[YN])," +
        @"(?:\s*candidate=(?<candidate>[YN]),)?" +
        @"\s*tofull=(?<tofull>[YN])" +
        @"(?:\s*\((?<rule>[A-Z_]+)\))?";
}
