using System.Globalization;
using System.Text;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Export;

public sealed class NotchTableExporter
{
    private const int CsvPayloadColumnCount = 9;

    public static string ExportAsCsv(NotchTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var orderedRows = table.Rows
            .OrderBy(r => r.IcIndex)
            .ThenBy(r => r.DiffIndex)
            .ThenBy(r => (int)r.Version)
            .ThenBy(r => r.RegularPadIndex)
            .ThenBy(r => r.CadPadId ?? int.MaxValue)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("row_number,version,version_code,ic_index,ic_number,fw_diff_idx,regular_pad_id,cad_pad_id,payload_width");
        for (var index = 0; index < CsvPayloadColumnCount; index++)
        {
            sb.Append(CultureInfo.InvariantCulture, $",payload_{index + 1:00}");
        }

        sb.AppendLine(",comment");

        for (var rowIndex = 0; rowIndex < orderedRows.Count; rowIndex++)
        {
            var row = orderedRows[rowIndex];
            var values = row.Values ?? Array.Empty<int>();
            sb.Append(rowIndex + 1).Append(',')
              .Append(ToCsvVersionLabel(row.Version)).Append(',')
              .Append((int)row.Version).Append(',')
              .Append(row.IcIndex).Append(',')
              .Append(row.IcIndex + 1).Append(',')
              .Append(row.DiffIndex).Append(',')
              .Append(row.RegularPadIndex).Append(',')
              .Append(row.CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "")
              .Append(',')
              .Append(values.Length);

            for (var index = 0; index < CsvPayloadColumnCount; index++)
            {
                sb.Append(',');
                if (index < values.Length)
                {
                    sb.Append(values[index]);
                }
            }

            sb.Append(',')
              .Append('"').Append(EscapeCsv(row.Comment)).Append('"')
              .AppendLine();
        }

        return sb.ToString();
    }

    public static string ExportAsCInitializer(
        NotchTable table,
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        NotchExportProfile profile = NotchExportProfile.Release,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);

        if (table.Rows.Count == 0)
        {
            return "// Notch table is empty.\n";
        }

        return NotchFirmwareCExporter.Export(table, grid, settings, profile, activeRegularPadIds);
    }

    private static string EscapeCsv(string s)
    {
        // RFC4180-ish: escape quotes by doubling them.
        // We already wrap the field with quotes.
        s ??= string.Empty;
        return s.Replace("\"", "\"\"");
    }

    private static string ToCsvVersionLabel(NotchAlgorithmVersion version)
    {
        var display = version.ToDisplayLabel();
        return display.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? display[1..]
            : display;
    }
}
