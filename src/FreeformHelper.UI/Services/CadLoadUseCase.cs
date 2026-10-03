using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Loads CAD data from paths or embedded bytes using consistent rules.
/// </summary>
public sealed class CadLoadUseCase
{
    private readonly DxfImportService _dxfImportService;

    public CadLoadUseCase(DxfImportService dxfImportService)
    {
        _dxfImportService = dxfImportService ?? throw new ArgumentNullException(nameof(dxfImportService));
    }

    public CadLoadOutcome? TryLoadFromPath(string? path, DxfImportOptions options)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var cad = _dxfImportService.ImportFromPath(path, options);
        return BuildOutcome(cad, CadLoadSource.Path, path);
    }

    public CadLoadOutcome? TryLoadFromEmbedded(byte[]? data, DxfImportOptions options)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        using var ms = new MemoryStream(data);
        var cad = _dxfImportService.ImportFromStream(ms, options);
        return BuildOutcome(cad, CadLoadSource.Embedded, null);
    }

    private static CadLoadOutcome BuildOutcome(CadPadSet cad, CadLoadSource source, string? sourcePath)
    {
        var duplicateSanitization = CadPadExactDuplicateSanitizer.Sanitize(cad);
        return new CadLoadOutcome(cad, source, sourcePath, duplicateSanitization);
    }

    public CadLoadOutcome? TryLoadFromProject(string? lastPath, byte[]? embedded, DxfImportOptions options)
    {
        Exception? pathLoadException = null;
        if (!string.IsNullOrWhiteSpace(lastPath) && File.Exists(lastPath))
        {
            try
            {
                return TryLoadFromPath(lastPath, options);
            }
            catch (Exception ex)
            {
                pathLoadException = ex;
            }
        }

        try
        {
            var embeddedOutcome = TryLoadFromEmbedded(embedded, options);
            if (embeddedOutcome is not null)
            {
                return embeddedOutcome;
            }
        }
        catch (Exception ex)
        {
            if (pathLoadException is not null)
            {
                throw new AggregateException("Failed to load CAD from both path and embedded DXF.", pathLoadException, ex);
            }

            throw;
        }

        if (pathLoadException is not null)
        {
            throw pathLoadException;
        }

        return null;
    }
}

public enum CadLoadSource
{
    Path,
    Embedded,
}

public sealed record CadLoadOutcome(
    CadPadSet Cad,
    CadLoadSource Source,
    string? SourcePath,
    CadPadExactDuplicateSanitizationResult DuplicateSanitization);
