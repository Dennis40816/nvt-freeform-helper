using System.Globalization;
using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Export;

internal static class NotchFirmwareCExporter
{
    private const string NotchDiffNoneLiteral = "NHC_DIFF_NONE";
    private const string NullPointerLiteral = "((const ST_PRI_NHC_TABLE_NODE_INFO*)0)";
    private const string NullFwMaskPointerLiteral = "((const UINT16*)0)";

    public static string Export(
        NotchTable table,
        RegularGrid grid,
        ProjectSettings settings,
        NotchExportProfile profile,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);
        var nullValue = settings.Notch.NullValue;

        var v22Rows = table.Rows
            .Where(static row => row.Version == NotchAlgorithmVersion.V22)
            .ToList();
        var orderedV21Rows = table.Rows
            .Where(static row => row.Version == NotchAlgorithmVersion.V21)
            .OrderBy(static row => row.IcIndex)
            .ThenBy(static row => row.DiffIndex)
            .ToList();

        if (v22Rows.Count > 0 && orderedV21Rows.Count > 0)
        {
            throw new InvalidOperationException(
                "C export is version-specific. Select C v2.1 or C v2.2 so the generated FW file has one table layout.");
        }

        if (v22Rows.Count > 0)
        {
            return ExportV22FwFile(v22Rows, grid, settings, nullValue, profile, activeRegularPadIds);
        }

        if (orderedV21Rows.Count > 0)
        {
            return ExportV21FwFile(orderedV21Rows, grid, settings, nullValue, profile, activeRegularPadIds);
        }

        return "// Notch table contains no supported v2.1/v2.2 rows.\n";
    }

    private static string ExportV21FwFile(
        IReadOnlyList<NotchTableRow> rows,
        RegularGrid grid,
        ProjectSettings settings,
        int nullValue,
        NotchExportProfile profile,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        var icCount = ResolveIcCount(rows, grid);
        var emitFwBaseMask = ShouldEmitFwBaseMask(profile);
        IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc = emitFwBaseMask
            ? BuildFwBaseMaskByIc(grid, icCount, activeRegularPadIds)
            : Array.Empty<FwBaseMaskIc>();
        var projection = NotchV21FirmwareProjector.Project(
            rows,
            nullValue,
            icCount,
            settings.Notch.ComputationMode);
        var groupedNodes = projection.NodesByIc;

        var sb = new StringBuilder();
        AppendFwFileHeader(sb, "Notch v2.1 compensation export");
        AppendV21TypeBlock(sb, nullValue, icCount, groupedNodes, fwBaseMaskByIc, emitFwBaseMask);
        AppendGenerationMetadata(
            sb,
            settings,
            profile,
            fwBaseMaskByIc,
            emitFwBaseMask,
            v21Count: groupedNodes.Sum(static nodes => nodes.Count),
            v22Count: 0);
        AppendV21Tables(sb, groupedNodes, nullValue);
        if (emitFwBaseMask)
        {
            AppendFwBaseMaskTables(sb, fwBaseMaskByIc);
        }

        AppendV21FunctionBlock(sb, emitFwBaseMask);
        return sb.ToString();
    }

    private static string ExportV22FwFile(
        IReadOnlyList<NotchTableRow> rows,
        RegularGrid grid,
        ProjectSettings settings,
        int nullValue,
        NotchExportProfile profile,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        var icCount = ResolveIcCount(rows, grid);
        var emitFwBaseMask = ShouldEmitFwBaseMask(profile);
        IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc = emitFwBaseMask
            ? BuildFwBaseMaskByIc(grid, icCount, activeRegularPadIds)
            : Array.Empty<FwBaseMaskIc>();
        var projection = NotchV22FirmwareProjector.Project(rows, nullValue, icCount);
        var groupedNodes = profile == NotchExportProfile.Release
            ? NotchV22FirmwareProjector.OrderNodesByIc(
                projection.SourceRows.Where(static node => !IsNoOpNode(node)).ToList(),
                icCount)
            : projection.NodesByIc;

        var sb = new StringBuilder();
        AppendFwFileHeader(sb, "Notch v2.2 compensation export");
        AppendV22TypeBlock(sb, nullValue, icCount, groupedNodes, fwBaseMaskByIc, emitFwBaseMask);
        AppendGenerationMetadata(
            sb,
            settings,
            profile,
            fwBaseMaskByIc,
            emitFwBaseMask,
            v21Count: 0,
            v22Count: groupedNodes.Sum(static nodes => nodes.Count));
        AppendV22Tables(sb, groupedNodes);
        if (emitFwBaseMask)
        {
            AppendFwBaseMaskTables(sb, fwBaseMaskByIc);
        }

        AppendV22FunctionBlock(sb, emitFwBaseMask);
        return sb.ToString();
    }

    private static void AppendFwFileHeader(StringBuilder sb, string note)
    {
        sb.AppendLine("/**");
        sb.AppendLine("    FUNCTION Layer.");
        sb.AppendLine();
        sb.AppendLine("    Notch Function");
        sb.AppendLine();
        sb.AppendLine("    @file       func_notch.c");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    @note       {note}; generated by FreeformHelper.");
        sb.AppendLine();
        sb.AppendLine("    Copyright   Novatek Microelectronics Corp. 2014.  All rights reserved.");
        sb.AppendLine("*/");
        sb.AppendLine();
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Including Files                                                             */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("#include \"notch.h\"");
        sb.AppendLine();
        sb.AppendLine("#if (USER_SWITCH_NOTCH_COMPENSATION == FUNC_ENABLE)");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Local Function Prototype                                                    */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine();
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Local Global Variables                                                      */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine();
    }

    private static void AppendV21TypeBlock(
        StringBuilder sb,
        int nullValue,
        int icCount,
        IReadOnlyList<IReadOnlyList<NotchV21FirmwareNode>> nodesByIc,
        IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc,
        bool emitFwBaseMask)
    {
        var totalCount = nodesByIc.Sum(static nodes => nodes.Count);
        var maxCount = Math.Max(1, nodesByIc.Select(static nodes => nodes.Count).DefaultIfEmpty(0).Max());
        sb.AppendLine("// Requires FW platform typedefs from notch.h / ap_gvariable.h: UINT8, UINT16, INT16.");
        sb.AppendLine("// v2.1 table shape follows codebase v2.0.0 ST_PRI_NHC_TABLE_NODE_INFO.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_IC_NUM          ({Math.Max(1, icCount)}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM        ({totalCount}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM_MAX    ({maxCount}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define NHC_DIFF_NONE            (0x{Math.Clamp(nullValue, 0, ushort.MaxValue):X4}u)");
        sb.AppendLine("#ifndef NHC_TYPE_NONE");
        sb.AppendLine("#define NHC_TYPE_NONE            (0u)");
        sb.AppendLine("#endif");
        sb.AppendLine("#ifndef NHC_TYPE_ADD");
        sb.AppendLine("#define NHC_TYPE_ADD             (1u)");
        sb.AppendLine("#endif");
        sb.AppendLine("#ifndef NHC_TYPE_SUB");
        sb.AppendLine("#define NHC_TYPE_SUB             (2u)");
        sb.AppendLine("#endif");
        sb.AppendLine();
        for (var icIndex = 0; icIndex < nodesByIc.Count; icIndex++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM_IC{icIndex + 1} ({nodesByIc[icIndex].Count}u)");
        }

        if (emitFwBaseMask)
        {
            AppendFwBaseMaskDefines(sb, fwBaseMaskByIc);
        }

        sb.AppendLine("#define castNHC_TABLE           castNHC_TABLE_IC1");
        sb.AppendLine();
        sb.AppendLine("typedef struct");
        sb.AppendLine("{");
        sb.AppendLine("    UINT16 NHC_IDX;");
        sb.AppendLine("    UINT8  NHC_REGULAR;");
        sb.AppendLine("    UINT8  NHC_REGU_TO_FULL;");
        sb.AppendLine("    UINT16 NHC_1ST_IDX;");
        sb.AppendLine("    UINT8  NHC_1ST_TYPE;");
        sb.AppendLine("    UINT8  NHC_1ST_RATIO;");
        sb.AppendLine("    UINT16 NHC_2ND_IDX;");
        sb.AppendLine("    UINT8  NHC_2ND_TYPE;");
        sb.AppendLine("    UINT8  NHC_2ND_RATIO;");
        sb.AppendLine("} ST_PRI_NHC_TABLE_NODE_INFO;");
        sb.AppendLine();
        AppendIcTableInfoType(sb);
        if (emitFwBaseMask)
        {
            AppendFwBaseMaskInfoType(sb);
        }
    }

    private static void AppendV22TypeBlock(
        StringBuilder sb,
        int nullValue,
        int icCount,
        IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> nodesByIc,
        IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc,
        bool emitFwBaseMask)
    {
        var totalCount = nodesByIc.Sum(static nodes => nodes.Count);
        var maxCount = Math.Max(1, nodesByIc.Select(static nodes => nodes.Count).DefaultIfEmpty(0).Max());
        sb.AppendLine("// Requires FW platform typedefs from notch.h / ap_gvariable.h: UINT8, UINT16, INT8, INT16.");
        sb.AppendLine("// v2.2 is source-oriented: NHC_IDX is the FW source diff, targets are redistributed legs.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_IC_NUM          ({Math.Max(1, icCount)}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM        ({totalCount}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM_MAX    ({maxCount}u)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define NHC_DIFF_NONE            (0x{Math.Clamp(nullValue, 0, ushort.MaxValue):X4}u)");
        sb.AppendLine("#define NHC_FLAG_NONE            (0u)");
        sb.AppendLine("#define NHC_FLAG_CONTINUATION    (1u << 0)");
        sb.AppendLine("#define NHC_FLAG_ANCHOR_OVERRIDE (1u << 1)");
        sb.AppendLine();
        for (var icIndex = 0; icIndex < nodesByIc.Count; icIndex++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_NODE_NUM_IC{icIndex + 1} ({nodesByIc[icIndex].Count}u)");
        }

        if (emitFwBaseMask)
        {
            AppendFwBaseMaskDefines(sb, fwBaseMaskByIc);
        }

        sb.AppendLine("#define castNHC_TABLE           castNHC_TABLE_IC1");
        sb.AppendLine();
        sb.AppendLine("typedef struct");
        sb.AppendLine("{");
        sb.AppendLine("    UINT16 NHC_IDX;");
        sb.AppendLine("    UINT8  NHC_COMBINE;");
        sb.AppendLine("    UINT16 NHC_1ST_IDX;");
        sb.AppendLine("    INT8   NHC_1ST_RATIO;");
        sb.AppendLine("    UINT16 NHC_2ND_IDX;");
        sb.AppendLine("    INT8   NHC_2ND_RATIO;");
        sb.AppendLine("    UINT8  NHC_FLAGS;");
        sb.AppendLine("} ST_PRI_NHC_TABLE_NODE_INFO;");
        sb.AppendLine();
        AppendIcTableInfoType(sb);
        if (emitFwBaseMask)
        {
            AppendFwBaseMaskInfoType(sb);
        }
    }

    private static void AppendIcTableInfoType(StringBuilder sb)
    {
        sb.AppendLine("typedef struct");
        sb.AppendLine("{");
        sb.AppendLine("    const ST_PRI_NHC_TABLE_NODE_INFO* pstTable;");
        sb.AppendLine("    UINT16 u16NodeNum;");
        sb.AppendLine("} ST_PRI_NHC_IC_TABLE_INFO;");
        sb.AppendLine();
    }

    private static void AppendFwBaseMaskDefines(StringBuilder sb, IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc)
    {
        var maxMaskCount = Math.Max(1, fwBaseMaskByIc.Select(static mask => mask.ActiveDiffIndexes.Count).DefaultIfEmpty(0).Max());
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_SIM_BASE_VALUE  (400)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_FW_MASK_NUM_MAX ({maxMaskCount}u)");
        for (var icIndex = 0; icIndex < fwBaseMaskByIc.Count; icIndex++)
        {
            var mask = fwBaseMaskByIc[icIndex];
            sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_FW_MASK_NUM_IC{icIndex + 1}  ({mask.ActiveDiffIndexes.Count}u)");
            sb.AppendLine(CultureInfo.InvariantCulture, $"#define USER_NHC_FW_MASK_SPAN_IC{icIndex + 1} ({mask.DiffSpan}u)");
        }

        sb.AppendLine();
    }

    private static void AppendFwBaseMaskInfoType(StringBuilder sb)
    {
        sb.AppendLine("typedef struct");
        sb.AppendLine("{");
        sb.AppendLine("    const UINT16* pu16ActiveDiffs;");
        sb.AppendLine("    UINT16 u16ActiveDiffNum;");
        sb.AppendLine("    UINT16 u16DiffSpan;");
        sb.AppendLine("} ST_PRI_NHC_FW_BASE_MASK_INFO;");
        sb.AppendLine();
    }

    private static void AppendGenerationMetadata(
        StringBuilder sb,
        ProjectSettings settings,
        NotchExportProfile profile,
        IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc,
        bool emitFwBaseMask,
        int v21Count,
        int v22Count)
    {
        var notch = settings.Notch;
        var (enableToRegular, enableToFull) = notch.ResolveStep3Switches();

        sb.AppendLine("// Generation metadata");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   ComputationMode: {notch.ComputationMode}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   CompensationModel: {notch.CompensationModel}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   SourceCombineRule: {DescribeSourceCombineRule(notch.CompensationModel)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   TargetAllocationRule: {NotchV22TargetAllocationPolicy.DescribeRule(notch.CompensationModel)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   EffectiveToRegular: {ToOnOff(enableToRegular)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   EffectiveToFull: {ToOnOff(enableToFull)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   ToFullRuleEngine: {ToOnOff(notch.EnableToFullRuleEngine)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   ToFullRuleTrace: {ToOnOff(notch.EnableToFullRuleTrace)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   BoundaryVirtualAreaCap: {ToOnOff(notch.EnableBoundaryVirtualAreaCap)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   BoundaryVirtualAreaCapRatio: {NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(notch.BoundaryVirtualAreaCapRatio):0.####}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   TargetCoverageGuard: {ToOnOff(notch.EnableTargetCoverageGuard)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   TargetCoverageCapPercent: {Math.Clamp(notch.TargetCoverageCapPercent, 0, 255)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   ExportProfile: {profile}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"//   Sections: v2.1={(v21Count > 0 ? "present" : "empty")}, v2.2={(v22Count > 0 ? "present" : "empty")}");
        if (emitFwBaseMask)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"//   FwBaseMask: active FW diffs={fwBaseMaskByIc.Sum(static mask => mask.ActiveDiffIndexes.Count)}, span={fwBaseMaskByIc.Sum(static mask => mask.DiffSpan)}");
            sb.AppendLine("//   FwBaseMaskRule: FUNC_NHC_SimulationLoadFwBaseMaskByIc clears the IC diff span to 0, then sets active regular FW diffs to the requested base value.");
        }

        sb.AppendLine();
    }

    private static void AppendV21Tables(
        StringBuilder sb,
        IReadOnlyList<IReadOnlyList<NotchV21FirmwareNode>> nodesByIc,
        int nullValue)
    {
        for (var icIndex = 0; icIndex < nodesByIc.Count; icIndex++)
        {
            var symbolName = GetIcTableSymbol(icIndex);
            var nodes = nodesByIc[icIndex];
            if (nodes.Count == 0)
            {
                continue;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"static const ST_PRI_NHC_TABLE_NODE_INFO {symbolName}[USER_NHC_NODE_NUM_IC{icIndex + 1}] =");
            sb.AppendLine("{");
            foreach (var node in nodes)
            {
                sb.AppendLine(FormatV21NodeLine(node, nullValue));
            }

            sb.AppendLine("};");
            sb.AppendLine();
        }

        AppendIcDispatchTable(sb, nodesByIc.Select(static nodes => nodes.Count).ToArray());
    }

    private static void AppendV22Tables(
        StringBuilder sb,
        IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> nodesByIc)
    {
        for (var icIndex = 0; icIndex < nodesByIc.Count; icIndex++)
        {
            var symbolName = GetIcTableSymbol(icIndex);
            var nodes = nodesByIc[icIndex];
            if (nodes.Count == 0)
            {
                continue;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"static const ST_PRI_NHC_TABLE_NODE_INFO {symbolName}[USER_NHC_NODE_NUM_IC{icIndex + 1}] =");
            sb.AppendLine("{");
            foreach (var node in nodes)
            {
                sb.AppendLine(FormatV22NodeLine(node));
            }

            sb.AppendLine("};");
            sb.AppendLine();
        }

        AppendIcDispatchTable(sb, nodesByIc.Select(static nodes => nodes.Count).ToArray());
    }

    private static void AppendIcDispatchTable(StringBuilder sb, int[] countByIc)
    {
        sb.AppendLine("static const ST_PRI_NHC_IC_TABLE_INFO castNHC_TABLE_BY_IC[USER_NHC_IC_NUM] =");
        sb.AppendLine("{");
        for (var icIndex = 0; icIndex < countByIc.Length; icIndex++)
        {
            var pointer = countByIc[icIndex] > 0 ? GetIcTableSymbol(icIndex) : NullPointerLiteral;
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {{ {pointer}, USER_NHC_NODE_NUM_IC{icIndex + 1} }}, // IC{icIndex + 1}");
        }

        sb.AppendLine("};");
        sb.AppendLine();
    }

    private static void AppendFwBaseMaskTables(StringBuilder sb, IReadOnlyList<FwBaseMaskIc> fwBaseMaskByIc)
    {
        sb.AppendLine("// FW simulation base mask");
        sb.AppendLine("// Active regular FW diffs are initialized to the requested base value; all other slots in the IC span are forced to 0.");
        for (var icIndex = 0; icIndex < fwBaseMaskByIc.Count; icIndex++)
        {
            var mask = fwBaseMaskByIc[icIndex];
            if (mask.ActiveDiffIndexes.Count == 0)
            {
                continue;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"static const UINT16 cau16NHC_FW_BASE_MASK_IC{icIndex + 1}[USER_NHC_FW_MASK_NUM_IC{icIndex + 1}] =");
            sb.AppendLine("{");
            AppendUint16InitializerValues(sb, mask.ActiveDiffIndexes);
            sb.AppendLine("};");
            sb.AppendLine();
        }

        sb.AppendLine("static const ST_PRI_NHC_FW_BASE_MASK_INFO castNHC_FW_BASE_MASK_BY_IC[USER_NHC_IC_NUM] =");
        sb.AppendLine("{");
        for (var icIndex = 0; icIndex < fwBaseMaskByIc.Count; icIndex++)
        {
            var mask = fwBaseMaskByIc[icIndex];
            var pointer = mask.ActiveDiffIndexes.Count > 0
                ? $"cau16NHC_FW_BASE_MASK_IC{icIndex + 1}"
                : NullFwMaskPointerLiteral;
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    {{ {pointer}, USER_NHC_FW_MASK_NUM_IC{icIndex + 1}, USER_NHC_FW_MASK_SPAN_IC{icIndex + 1} }}, // IC{icIndex + 1}");
        }

        sb.AppendLine("};");
        sb.AppendLine();
    }

    private static void AppendUint16InitializerValues(StringBuilder sb, IReadOnlyList<int> values)
    {
        const int valuesPerLine = 12;
        for (var index = 0; index < values.Count; index += valuesPerLine)
        {
            var lineValues = values
                .Skip(index)
                .Take(valuesPerLine)
                .Select(static value => value.ToString(CultureInfo.InvariantCulture));
            sb.Append("    ");
            sb.Append(string.Join(", ", lineValues));
            if (index + valuesPerLine < values.Count)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }
    }

    private static void AppendV21FunctionBlock(StringBuilder sb, bool emitFwBaseMask)
    {
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Interface Functions                                                         */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine();
        sb.AppendLine("static void FUNC_NHC_DiffCompensationOneTable(const ST_PRI_NHC_TABLE_NODE_INFO* pstTable, UINT16 u16NodeNum)");
        sb.AppendLine("{");
        sb.AppendLine("    UINT16 u16j;");
        sb.AppendLine("    INT16 s16Sum;");
        sb.AppendLine("    INT16 s16DiffOffsetBk[USER_NHC_NODE_NUM_MAX] = { 0 };");
        sb.AppendLine();
        sb.AppendLine("    if (pstTable == ((const ST_PRI_NHC_TABLE_NODE_INFO*)0))");
        sb.AppendLine("    {");
        sb.AppendLine("        return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (u16j = 0u; u16j < u16NodeNum; u16j++)");
        sb.AppendLine("    {");
        sb.AppendLine("        // MUL");
        sb.AppendLine("        if (pstTable[u16j].NHC_REGU_TO_FULL != 0u)");
        sb.AppendLine("        {");
        sb.AppendLine("            S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX] =");
        sb.AppendLine("                S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX] * pstTable[u16j].NHC_REGU_TO_FULL / 100;");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            // To prevent from the diff of merged pad is negative after CNC");
        sb.AppendLine("            S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX] = 0;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (u16j = 0u; u16j < u16NodeNum; u16j++)");
        sb.AppendLine("    {");
        sb.AppendLine("        s16Sum = 0;");
        sb.AppendLine("        if (pstTable[u16j].NHC_1ST_TYPE == NHC_TYPE_ADD)");
        sb.AppendLine("        {");
        sb.AppendLine("            s16Sum += (S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_1ST_IDX] * pstTable[u16j].NHC_1ST_RATIO) >> 7;");
        sb.AppendLine("        }");
        sb.AppendLine("        else if (pstTable[u16j].NHC_1ST_TYPE == NHC_TYPE_SUB)");
        sb.AppendLine("        {");
        sb.AppendLine("            s16Sum -= (S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_1ST_IDX] * pstTable[u16j].NHC_1ST_RATIO) >> 7;");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            ; // Comment for MISRA C Check");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (pstTable[u16j].NHC_2ND_TYPE == NHC_TYPE_ADD)");
        sb.AppendLine("        {");
        sb.AppendLine("            s16Sum += (S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_2ND_IDX] * pstTable[u16j].NHC_2ND_RATIO) >> 7;");
        sb.AppendLine("        }");
        sb.AppendLine("        else if (pstTable[u16j].NHC_2ND_TYPE == NHC_TYPE_SUB)");
        sb.AppendLine("        {");
        sb.AppendLine("            s16Sum -= (S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_2ND_IDX] * pstTable[u16j].NHC_2ND_RATIO) >> 7;");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            ; // Comment for MISRA C Check");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        s16DiffOffsetBk[u16j] = s16Sum;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (u16j = 0u; u16j < u16NodeNum; u16j++)");
        sb.AppendLine("    {");
        sb.AppendLine("        S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX] += s16DiffOffsetBk[u16j];");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        AppendCommonDispatchFunctions(sb, emitFwBaseMask);
    }

    private static void AppendV22FunctionBlock(StringBuilder sb, bool emitFwBaseMask)
    {
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Interface Functions                                                         */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine();
        sb.AppendLine("static INT16 FUNC_NHC_ScalePercent(INT16 s16Value, INT16 s16Percent)");
        sb.AppendLine("{");
        sb.AppendLine("    return (INT16)((s16Value * s16Percent) / 100);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static void FUNC_NHC_ApplyTarget(INT16 s16Source, UINT16 u16TargetIdx, INT8 s8Ratio)");
        sb.AppendLine("{");
        sb.AppendLine("    if ((u16TargetIdx != NHC_DIFF_NONE) && (s8Ratio != 0))");
        sb.AppendLine("    {");
        sb.AppendLine("        S_2D_DIFFAFTER->as16Buf[u16TargetIdx] += FUNC_NHC_ScalePercent(s16Source, (INT16)s8Ratio);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("static void FUNC_NHC_DiffCompensationOneTable(const ST_PRI_NHC_TABLE_NODE_INFO* pstTable, UINT16 u16NodeNum)");
        sb.AppendLine("{");
        sb.AppendLine("    UINT16 u16j;");
        sb.AppendLine("    INT16 s16SourceBk[USER_NHC_NODE_NUM_MAX] = { 0 };");
        sb.AppendLine("    INT16 s16RetainPct;");
        sb.AppendLine("    INT16 s16SourceDeltaPct;");
        sb.AppendLine();
        sb.AppendLine("    if (pstTable == ((const ST_PRI_NHC_TABLE_NODE_INFO*)0))");
        sb.AppendLine("    {");
        sb.AppendLine("        return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (u16j = 0u; u16j < u16NodeNum; u16j++)");
        sb.AppendLine("    {");
        sb.AppendLine("        s16SourceBk[u16j] = S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX];");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (u16j = 0u; u16j < u16NodeNum; u16j++)");
        sb.AppendLine("    {");
        sb.AppendLine("        if ((pstTable[u16j].NHC_FLAGS & NHC_FLAG_CONTINUATION) == 0u)");
        sb.AppendLine("        {");
        sb.AppendLine("            s16RetainPct = (INT16)pstTable[u16j].NHC_COMBINE;");
        sb.AppendLine("            s16RetainPct -= (INT16)pstTable[u16j].NHC_1ST_RATIO;");
        sb.AppendLine("            s16RetainPct -= (INT16)pstTable[u16j].NHC_2ND_RATIO;");
        sb.AppendLine("            if (s16RetainPct < 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                s16RetainPct = 0;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            s16SourceDeltaPct = s16RetainPct - 100;");
        sb.AppendLine("            S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_IDX] +=");
        sb.AppendLine("                FUNC_NHC_ScalePercent(s16SourceBk[u16j], s16SourceDeltaPct);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        FUNC_NHC_ApplyTarget(s16SourceBk[u16j], pstTable[u16j].NHC_1ST_IDX, pstTable[u16j].NHC_1ST_RATIO);");
        sb.AppendLine("        FUNC_NHC_ApplyTarget(s16SourceBk[u16j], pstTable[u16j].NHC_2ND_IDX, pstTable[u16j].NHC_2ND_RATIO);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        AppendCommonDispatchFunctions(sb, emitFwBaseMask);
    }

    private static void AppendCommonDispatchFunctions(StringBuilder sb, bool emitFwBaseMask)
    {
        if (emitFwBaseMask)
        {
            sb.AppendLine("void FUNC_NHC_SimulationLoadFwBaseMaskByIc(UINT8 u8Ic, INT16 s16BaseValue)");
            sb.AppendLine("{");
            sb.AppendLine("    UINT16 u16j;");
            sb.AppendLine("    const ST_PRI_NHC_FW_BASE_MASK_INFO* pstMaskInfo;");
            sb.AppendLine();
            sb.AppendLine("    if (u8Ic >= USER_NHC_IC_NUM)");
            sb.AppendLine("    {");
            sb.AppendLine("        return;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    pstMaskInfo = &castNHC_FW_BASE_MASK_BY_IC[u8Ic];");
            sb.AppendLine("    for (u16j = 0u; u16j < pstMaskInfo->u16DiffSpan; u16j++)");
            sb.AppendLine("    {");
            sb.AppendLine("        S_2D_DIFFAFTER->as16Buf[u16j] = 0;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    if (pstMaskInfo->pu16ActiveDiffs == ((const UINT16*)0))");
            sb.AppendLine("    {");
            sb.AppendLine("        return;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    for (u16j = 0u; u16j < pstMaskInfo->u16ActiveDiffNum; u16j++)");
            sb.AppendLine("    {");
            sb.AppendLine("        S_2D_DIFFAFTER->as16Buf[pstMaskInfo->pu16ActiveDiffs[u16j]] = s16BaseValue;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("void FUNC_NHC_SimulationLoadFwBaseMask(void)");
            sb.AppendLine("{");
            sb.AppendLine("    FUNC_NHC_SimulationLoadFwBaseMaskByIc(0u, USER_NHC_SIM_BASE_VALUE);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)");
        sb.AppendLine("{");
        sb.AppendLine("    if (u8Ic >= USER_NHC_IC_NUM)");
        sb.AppendLine("    {");
        sb.AppendLine("        return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    FUNC_NHC_DiffCompensationOneTable(castNHC_TABLE_BY_IC[u8Ic].pstTable, castNHC_TABLE_BY_IC[u8Ic].u16NodeNum);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/****************************************************************************************************");
        sb.AppendLine(" *");
        sb.AppendLine(" *    FUNC_NHC_DiffCompensation");
        sb.AppendLine(" *");
        sb.AppendLine(" *****************************************************************************************************/");
        sb.AppendLine("void FUNC_NHC_DiffCompensation(void)");
        sb.AppendLine("{");
        sb.AppendLine("    FUNC_NHC_DiffCompensationByIc(0u);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Local Functions                                                             */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine();
        sb.AppendLine("#endif");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
        sb.AppendLine("/* Debug Functions                                                             */");
        sb.AppendLine("/*-----------------------------------------------------------------------------*/");
    }

    private static bool ShouldEmitFwBaseMask(NotchExportProfile profile) => profile == NotchExportProfile.Debug;

    private static int ResolveIcCount(IReadOnlyList<NotchTableRow> rows, RegularGrid grid)
    {
        var maxIcIndex = -1;
        if (rows.Count > 0)
        {
            maxIcIndex = Math.Max(maxIcIndex, rows.Max(static row => row.IcIndex));
        }

        if (grid.Pads.Count > 0)
        {
            maxIcIndex = Math.Max(maxIcIndex, grid.Pads.Max(static pad => pad.IcIndex));
        }

        return Math.Max(1, maxIcIndex + 1);
    }

    private static List<FwBaseMaskIc> BuildFwBaseMaskByIc(
        RegularGrid grid,
        int icCount,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        var count = Math.Max(1, icCount);
        var maskByIc = Enumerable.Range(0, count)
            .Select(static icIndex => new List<int>())
            .ToArray();
        var maxDiffByIc = Enumerable.Repeat(-1, count).ToArray();
        var activeRegularSet = activeRegularPadIds is null
            ? null
            : new HashSet<int>(activeRegularPadIds);

        foreach (var pad in grid.Pads)
        {
            if (pad.IcIndex < 0 || pad.IcIndex >= count || pad.DiffIndex < 0)
            {
                continue;
            }

            maxDiffByIc[pad.IcIndex] = Math.Max(maxDiffByIc[pad.IcIndex], pad.DiffIndex);
            if (activeRegularSet is not null && !activeRegularSet.Contains(pad.RegularPadId))
            {
                continue;
            }

            maskByIc[pad.IcIndex].Add(pad.DiffIndex);
        }

        var result = new List<FwBaseMaskIc>(count);
        for (var icIndex = 0; icIndex < count; icIndex++)
        {
            var activeDiffs = maskByIc[icIndex]
                .Distinct()
                .Where(static diff => diff <= ushort.MaxValue)
                .OrderBy(static diff => diff)
                .ToArray();
            var span = Math.Clamp(maxDiffByIc[icIndex] + 1, 0, ushort.MaxValue);
            result.Add(new FwBaseMaskIc(icIndex, span, activeDiffs));
        }

        return result;
    }

    private static string DescribeSourceCombineRule(NotchCompensationModel model)
    {
        return model switch
        {
            NotchCompensationModel.CurrentGain => "C = sum(target regular coverage); ToFull uses Stage3 effective area",
            NotchCompensationModel.ConservativeNoGain => "C = sum(target regular coverage); ToFull excludes source gain",
            NotchCompensationModel.Disabled => "C = 100",
            _ => "C = sum(target regular coverage)",
        };
    }

    private static string ToOnOff(bool value) => value ? "ON" : "OFF";

    private static bool IsNoOpNode(NotchV22FirmwareRow node)
    {
        return node.CombinePercent == 100 &&
               node.FirstTargetDiffIndex is null &&
               node.FirstRatioPercent == 0 &&
               node.SecondTargetDiffIndex is null &&
               node.SecondRatioPercent == 0;
    }

    private static string GetIcTableSymbol(int icIndex) => string.Format(CultureInfo.InvariantCulture, "castNHC_TABLE_IC{0}", icIndex + 1);

    private static string FormatV21NodeLine(NotchV21FirmwareNode node, int nullValue)
    {
        var firstType = FormatV21TypeLiteral(node.FirstType);
        var secondType = FormatV21TypeLiteral(node.SecondType);
        return string.Format(
            CultureInfo.InvariantCulture,
            "    {{ {0,5}, {1,3}, {2,3}, {3,13}, {4,12}, {5,4}, {6,13}, {7,12}, {8,4} }}, // {9}",
            node.DestinationDiffIndex,
            node.RegularPercent,
            node.ReguToFullPercent,
            FormatDiffLiteral(node.FirstDiffIndex, nullValue),
            firstType,
            node.FirstRatioQ7,
            FormatDiffLiteral(node.SecondDiffIndex, nullValue),
            secondType,
            node.SecondRatioQ7,
            node.Comment);
    }

    private static string FormatV22NodeLine(NotchV22FirmwareRow node)
    {
        var movePercent = node.FirstRatioPercent + node.SecondRatioPercent;
        var keepPercent = node.CombinePercent - movePercent;
        return string.Format(
            CultureInfo.InvariantCulture,
            "    {{ {0,5}, {1,3}, {2,13}, {3,4}, {4,13}, {5,4}, {6} }}, // {7}; NODE KEEP={8}% MOVE={9}%",
            node.SourceDiffIndex,
            node.CombinePercent,
            FormatDiffLiteral(node.FirstTargetDiffIndex),
            node.FirstRatioPercent,
            FormatDiffLiteral(node.SecondTargetDiffIndex),
            node.SecondRatioPercent,
            NotchV22FirmwareProjector.FormatFlagsLiteral(node.Flags),
            node.Comment,
            keepPercent,
            movePercent);
    }

    private static string FormatDiffLiteral(int value, int nullValue)
    {
        return value == nullValue
            ? NotchDiffNoneLiteral
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDiffLiteral(int? value) => value.HasValue
        ? value.Value.ToString(CultureInfo.InvariantCulture)
        : NotchDiffNoneLiteral;

    private static string FormatV21TypeLiteral(int type)
    {
        return type switch
        {
            NotchV21Q7Codec.TypeAdd => "NHC_TYPE_ADD",
            NotchV21Q7Codec.TypeSub => "NHC_TYPE_SUB",
            _ => "NHC_TYPE_NONE",
        };
    }

    private sealed record FwBaseMaskIc(
        int IcIndex,
        int DiffSpan,
        IReadOnlyList<int> ActiveDiffIndexes);
}
