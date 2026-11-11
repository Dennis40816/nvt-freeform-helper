using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.CadImport;

public enum CadLoadSource
{
    Path,
    Embedded,
}

public sealed record CadLoadOutcome(
    CadPadSet Cad,
    CadLoadSource Source,
    string? SourcePath,
    CadPadExactDuplicateSanitizationResult DuplicateSanitization,
    string? FallbackWarning = null);
