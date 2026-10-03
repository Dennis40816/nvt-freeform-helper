using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides a service layer for importing DXF data into <see cref="CadPadSet"/> objects.
/// This service acts as a wrapper around the <see cref="DxfPadImporter"/> from the infrastructure layer.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed class DxfImportService
{
    // Private instance of the DXF pad importer.
    private readonly DxfPadImporter _importer = new();

    internal DxfLayerCatalog ImportedCatalog { get; private set; } = DxfLayerCatalog.Empty;

    /// <summary>
    /// Imports CAD pads from a DXF file specified by its file path.
    /// </summary>
    /// <param name="path">The file path to the DXF file.</param>
    /// <param name="options">The <see cref="DxfImportOptions"/> to apply during import.</param>
    /// <returns>A <see cref="CadPadSet"/> containing the imported CAD pads.</returns>
    public CadPadSet ImportFromPath(string path, DxfImportOptions options)
    {
        var pads = DxfPadImporter.Import(path, out var catalog, options);
        ImportedCatalog = catalog;
        return pads;
    }

    /// <summary>
    /// Imports CAD pads from a DXF file provided as a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing the DXF data.</param>
    /// <param name="options">The <see cref="DxfImportOptions"/> to apply during import.</param>
    /// <returns>A <see cref="CadPadSet"/> containing the imported CAD pads.</returns>
    public CadPadSet ImportFromStream(Stream stream, DxfImportOptions options)
    {
        return DxfPadImporter.Import(stream, options);
    }
}
