using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the CAD and regular pad info view models show after construction, for the multi
/// selection, the single pad with an inspector snapshot and the single pad built from callbacks.
/// Percent texts are compared without the space some runtimes insert before the percent sign.
/// </summary>
public sealed class PadInfoViewModelConstructionTests
{
    private const string OwnerDiagnostics =
        "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=31|32, ownerShare=50/50, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)";

    [Fact]
    public void CadPadInfo_MultiSelection_ShowsMixedAndRangeTexts()
    {
        var pads = new List<CadPad>
        {
            TestGeometryFactory.CreateCadPad(10, "L1", 0, 0, 10, 5, "A"),
            TestGeometryFactory.CreateCadPad(11, "L2", 20, 0, 26, 4, "B"),
        };
        var vm = new CadPadInfoViewModel(
            pads,
            getDxfIndex: static id => id == 10 ? 5 : 9,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static id => id,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null);

        Assert.Equal(_expectedCadMulti, DescribeCad(vm));
    }

    [Fact]
    public void CadPadInfo_SingleWithInspectorSnapshot_UsesSnapshotTexts()
    {
        var pad = TestGeometryFactory.CreateCadPad(20, "L3", 0, 0, 10, 8, "C");
        var vm = CreateCadViewModel(pad, snapshot: CreateCadSnapshot(pad));

        Assert.Equal(_expectedCadSnapshot, DescribeCad(vm));
    }

    [Fact]
    public void CadPadInfo_SingleFromCallbacks_UsesMatchLinksAndCompensationPreview()
    {
        var pad = TestGeometryFactory.CreateCadPad(21, "L3", 0, 0, 10, 8, "C");
        var vm = CreateCadViewModel(pad, snapshot: null);

        Assert.Equal(_expectedCadCallbacks, DescribeCad(vm));
    }

    [Fact]
    public void RegularPadInfo_MultiSelection_ShowsMixedAndRangeTexts()
    {
        var pads = new List<RegularPad>
        {
            CreateRegular(0, 0, 100, 0, 0, 10, 8, diffIndex: 3, icIndex: 0),
            CreateRegular(1, 2, 101, 20, 8, 32, 20, diffIndex: 9, icIndex: 1),
        };
        var vm = new RegularPadInfoViewModel(
            pads,
            toDisplayRow: static row => row + 10,
            getDxfIndex: null,
            getMatchedCadPadIds: null,
            getMatchedCadLinks: null,
            applyPadSize: null,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null);

        Assert.Equal(_expectedRegularMulti, DescribeRegular(vm));
    }

    [Fact]
    public void RegularPadInfo_SingleWithInspectorSnapshot_UsesSnapshotTexts()
    {
        var pad = CreateRegular(1, 3, 102, 0, 0, 10, 8, diffIndex: 55, icIndex: 1);
        var vm = CreateRegularViewModel(pad, snapshot: CreateRegularSnapshot(pad));

        Assert.Equal(_expectedRegularSnapshot, DescribeRegular(vm));
    }

    [Fact]
    public void RegularPadInfo_SingleFromCallbacks_UsesMatchLinksOwnersAndNotchRows()
    {
        var pad = CreateRegular(1, 3, 102, 0, 0, 10, 8, diffIndex: 55, icIndex: 1);
        var vm = CreateRegularViewModel(pad, snapshot: null);

        Assert.Equal(_expectedRegularCallbacks, DescribeRegular(vm));
    }

    private static RegularPad CreateRegular(int row, int col, int id, double minX, double minY, double maxX, double maxY, int diffIndex, int icIndex)
    {
        var pad = TestGeometryFactory.CreateRegularPad(row, col, id, minX, minY, maxX, maxY, diffIndex, icIndex);
        pad.Freeform = FreeformType.XWay;
        return pad;
    }

    private static CadPadInfoViewModel CreateCadViewModel(CadPad pad, PadInspectorSnapshot? snapshot)
    {
        return new CadPadInfoViewModel(
            [pad],
            getDxfIndex: static _ => 7,
            getCadOutputFwDiffOverride: static _ => 12,
            isDxfIndexAnchor: static _ => true,
            getCustomValue: static _ => 3,
            applyCustomValue: static (_, _) => { },
            setCadOutputFwDiffOverride: static (_, _) => { },
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: static _ => { },
            clearDxfIndexAnchor: null,
            closePadInfo: null,
            notchDetailCommand: null,
            selectAreaBucket: static () => { },
            getMatchedRegularPadIds: static _ => [4809, 4808, 4809],
            getMatchedRegularLinks: static id =>
            [
                new PadMatchLink(id, 4808, 4.0, 0.4, 0.5),
                new PadMatchLink(id, 4809, 6.0, 0.6, 0.75),
            ],
            getRegularPadIcDiff: static regularId => (regularId - 4807, 40 + regularId - 4808),
            focusCadMatches: static _ => { },
            highlightCadMatches: static _ => { },
            highlightCadOwnerPads: static _ => { },
            highlightRegularPads: static _ => { },
            getCadV22CompensationPreview: static _ => (0.5, 1.25, 1.5, true),
            getCadV22CompensationDiagnostics: static _ => OwnerDiagnostics,
            getCadV22TargetAllocationSummary: null,
            inspectorSnapshot: snapshot);
    }

    private static RegularPadInfoViewModel CreateRegularViewModel(RegularPad pad, PadInspectorSnapshot? snapshot)
    {
        return new RegularPadInfoViewModel(
            [pad],
            toDisplayRow: static row => row + 10,
            getDxfIndex: static cadId => 100 + cadId,
            getMatchedCadPadIds: static _ => [32, 31, 32],
            getMatchedCadLinks: static id =>
            [
                new PadMatchLink(31, id, 4.0, 0.4, 0.5),
                new PadMatchLink(32, id, 6.0, 0.6, 0.75),
            ],
            applyPadSize: static (_, _, _, _) => { },
            resetPadSize: static (_, _) => { },
            setFreeform: static (_, _) => { },
            getCrossIcOwnerShares: static _ =>
            [
                (31, 10.0),
                (32, 20.5),
                (33, 5.0),
                (34, 40.0),
                (35, 1.0),
            ],
            getNotchRowsByRegularPad: static id => Enumerable.Range(1, 9)
                .Select(n => new FreeformHelperViewModel.NotchRowPreview(
                    10 - n,
                    n % 2 == 0 ? NotchAlgorithmVersion.V22 : NotchAlgorithmVersion.V21,
                    n % 3,
                    50 + n,
                    id,
                    n == 4 ? null : 30 + n,
                    "values",
                    "comment"))
                .ToList(),
            highlightCadOwnerPads: static _ => { },
            focusRegularMatches: static _ => { },
            highlightRegularMatches: static _ => { },
            closePadInfo: null,
            inspectorSnapshot: snapshot);
    }

    private static PadInspectorSnapshot CreateCadSnapshot(CadPad pad)
    {
        return new PadInspectorSnapshot(
            "cad",
            new CadPadInspectorSnapshot(
                CadPadId: pad.Id,
                Name: pad.Name,
                Layer: pad.Layer,
                Area: pad.Area,
                Bounds: pad.Bounds,
                Centroid: pad.Centroid,
                Vertices: pad.Polygon.Vertices.Length,
                DxfIndex: 112,
                CadOutputFwDiffOverride: 120,
                IsDxfIndexAnchor: true,
                DxfIndexDisplayText: "112",
                CadOutputFwDiffAssignmentModeText: "Auto",
                IcIndex: 2,
                MatchText: "IC3/diff112",
                MatchDetailsText: "IC3/diff112 (best)",
                MatchedRegularPadIds: [4808],
                MatchedRegularDetails: [new PadInspectorMatchedRegularSnapshot(4808, 2, 112, 0.61, 0.54)],
                IsNotchRowEligible: true,
                NotchRowSummary: "Notch rows: 1 (v2.2)",
                Notch: new PadInspectorNotchSnapshot(
                    ToRegularRatio: 0.941,
                    ToFullRatio: 1.556,
                    CombinedRatio: 1.464,
                    IsToFullEnabled: true,
                    Stage3Area: 12.3,
                    StrictAreaThreshold: 0.1,
                    Targets:
                    [
                        new PadInspectorNotchTargetSnapshot(2, 112, 8.0, 0.65, 65, true, true, 1, 1, [new PadInspectorNotchTargetRegularAreaSnapshot(4808, 8.0)], [4808]),
                    ],
                    Diagnostics: OwnerDiagnostics),
                RuleTrace: [new PadInspectorRuleTraceEntry("Sequence", "Adjusted", "IC-row sequence applied")],
                MatchConfidence: 0.61),
            Regular: null);
    }

    private static PadInspectorSnapshot CreateRegularSnapshot(RegularPad pad)
    {
        return new PadInspectorSnapshot(
            "regular",
            Cad: null,
            Regular: new RegularPadInspectorSnapshot(
                RegularPadId: pad.RegularPadId,
                Index: pad.Index,
                Row: pad.Row,
                Col: pad.Col,
                DisplayRow: 7,
                IcIndex: pad.IcIndex,
                DiffIndex: 99,
                Area: pad.Area,
                Bounds: pad.Bounds,
                Centroid: pad.Centroid,
                Freeform: FreeformType.XWay,
                MatchText: "CAD 31",
                MatchDetailsText: "CAD 31 (best)",
                MatchedCadPadIds: [31],
                MatchedCadDetails: [new PadInspectorMatchedCadSnapshot(31, 112, 0.5, 0.4)],
                RuleTrace: [new PadInspectorRuleTraceEntry("Freeform", "Applied", "Source: Step 2 auto-detect")],
                MatchConfidence: 0.4,
                DiffSource: "Auto")
            {
                FreeformSource = "Step 2 auto-detect",
            });
    }

    private static string Fmt(object? value)
    {
        return Normalize(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "<null>");
    }

    private static string Normalize(string value)
    {
        return value.Replace(" %", "%", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string[] DescribeCad(CadPadInfoViewModel vm)
    {
        var lines = new List<string>
        {
            $"IsMulti={Fmt(vm.IsMulti)} SelectedCount={Fmt(vm.SelectedCount)} Title={Fmt(vm.Title)} Subtitle={Fmt(vm.Subtitle)}",
            $"Layer={Fmt(vm.LayerText)} Name={Fmt(vm.NameText)} CadId={Fmt(vm.CadIdText)} DxfIndex={Fmt(vm.DxfIndexText)}",
            $"Centroid={Fmt(vm.CentroidText)} Area={Fmt(vm.AreaText)} Vertex={Fmt(vm.VertexText)} Identity={Fmt(vm.IdentityText)}",
            $"Match={Fmt(vm.MatchText)} Details={Fmt(vm.MatchDetailsText)} Confidence={Fmt(vm.MatchConfidenceText)} Seed={Fmt(vm.GeometrySeedValueText)}",
            $"NotchRows={Fmt(vm.NotchRowSummaryText)} ToRegular={Fmt(vm.ToRegularRatioText)} ToFull={Fmt(vm.ToFullRatioText)}",
            $"Values={Fmt(vm.ToRegularValueText)} | {Fmt(vm.ToFullValueText)} | {Fmt(vm.CombinedValueText)}",
            $"Reason={Fmt(vm.ToFullReasonShortText)} | {Fmt(vm.ToFullReasonFullText)}",
            $"Stage3={Fmt(vm.Stage3AreaText)} Targets={Fmt(vm.TargetAllocationSummaryText)} Owners={Fmt(vm.OwnerSummaryText)}",
            $"Diagnostics={Fmt(vm.ToFullDiagnosticsText)}",
            $"AllocationLines=[{string.Join(" ; ", vm.TargetAllocationLines.Select(Fmt))}]",
            $"AllocationItems=[{string.Join(" ; ", vm.TargetAllocationItems.Select(static item => Fmt(item.RoleText) + "/" + Fmt(item.TargetText) + "/" + Fmt(item.DiffText) + "/" + Fmt(item.AreaText) + "/" + Fmt(item.RatioText) + "/" + Fmt(item.HasHighlightCommand)))}]",
            $"OwnerLinks=[{string.Join(" ; ", vm.NotchOwnerLinks.Select(static link => Fmt(link.Text)))}]",
            $"RuleTrace=[{string.Join(" ; ", vm.RuleTraceLines.Select(Fmt))}]",
            $"Actions focus={Fmt(vm.HasFocusMatchAction)} highlight={Fmt(vm.HasHighlightMatchAction)} overrideEditor={Fmt(vm.HasCadOutputFwDiffOverrideEditor)} anchorEditor={Fmt(vm.HasDxfIndexAnchorEditor)} areaBucket={Fmt(vm.HasSelectAreaBucketCommand)} notchDetail={Fmt(vm.NotchDetailCommand is not null)}",
            $"Override has={Fmt(vm.HasCadOutputFwDiffOverride)} value={Fmt(vm.CadOutputFwDiffOverride)} anchor={Fmt(vm.IsDxfIndexAnchor)} mode={Fmt(vm.CadOutputFwDiffAssignmentModeText)}",
            $"Custom value={Fmt(vm.CustomValue)} pending={Fmt(vm.HasPendingChanges)} details={Fmt(vm.ShowGeometryDetails)}",
        };
        return [.. lines];
    }

    private static string[] DescribeRegular(RegularPadInfoViewModel vm)
    {
        var lines = new List<string>
        {
            $"IsMulti={Fmt(vm.IsMulti)} SelectedCount={Fmt(vm.SelectedCount)} Title={Fmt(vm.Title)} Subtitle={Fmt(vm.Subtitle)}",
            $"Axis={Fmt(vm.HeaderAxisXText)} {Fmt(vm.HeaderAxisYText)} Identity={Fmt(vm.IdentityText)}",
            $"Row={Fmt(vm.RowText)} Col={Fmt(vm.ColText)} Index={Fmt(vm.IndexText)} RowCol={Fmt(vm.RowColText)} RowRange={Fmt(vm.RowRangeText)} ColRange={Fmt(vm.ColRangeText)}",
            $"Ic={Fmt(vm.IcIndexText)} | {Fmt(vm.IcText)} Diff={Fmt(vm.DiffIndexText)} RegularId={Fmt(vm.RegularPadIdText)}",
            $"Bounds={Fmt(vm.BoundsText)} Centroid={Fmt(vm.CentroidText)} Area={Fmt(vm.AreaText)}",
            $"Match={Fmt(vm.MatchText)} Details={Fmt(vm.MatchDetailsText)} Confidence={Fmt(vm.MatchConfidenceText)}",
            $"Freeform={Fmt(vm.FreeformText)} MatchFreeform={Fmt(vm.MatchFreeformText)} DiffSource={Fmt(vm.DiffSourceText)}",
            $"Owners={Fmt(vm.OwnerSummaryText)} NotchRows={Fmt(vm.NotchRowsSummaryText)}",
            $"MatchCards=[{string.Join(" ; ", vm.MatchAreaCards.Select(static card => Fmt(card.TitleText) + "/" + Fmt(card.RoleText) + "/" + Fmt(card.MetaText) + "/" + Fmt(card.AreaText) + "/" + Fmt(card.RatioText) + "/" + Fmt(card.RawText) + "/" + Fmt(card.HasHighlightCommand)))}]",
            $"NotchCards=[{string.Join(" ; ", vm.NotchRowFlowCards.Select(static card => Fmt(card.TitleText) + "/" + Fmt(card.RoleText) + "/" + Fmt(card.MetaText) + "/" + Fmt(card.AreaText) + "/" + Fmt(card.RatioText) + "/" + Fmt(card.HasHighlightCommand)))}]",
            $"RuleTrace=[{string.Join(" ; ", vm.RuleTraceLines.Select(Fmt))}]",
            $"Actions navigation={Fmt(vm.HasMatchNavigation)} highlight={Fmt(vm.HasHighlightMatchAction)}",
            $"Size widthMixed={Fmt(vm.IsPadWidthMixed)} heightMixed={Fmt(vm.IsPadHeightMixed)} width={Fmt(vm.PadWidth)} height={Fmt(vm.PadHeight)} pending={Fmt(vm.HasPendingChanges)} details={Fmt(vm.ShowGeometryDetails)}",
        };
        return [.. lines];
    }

    private static readonly string[] _expectedCadMulti =
    [
        "IsMulti=True SelectedCount=2 Title=CAD pads Subtitle=Selected: 2 pads",
        "Layer=L1, L2 (+) Name=A, B (+) CadId=10 - 11 DxfIndex=5 - 9",
        "Centroid=Mixed Area=24 - 50 mm\u00b2 Vertex=4 Identity=CAD pads \u00b7 Selected: 2 pads",
        "Match=Mixed Details=Mixed Confidence=- Seed=Mixed",
        "NotchRows=Notch rows: - ToRegular=Undo NF (To Regular): - ToFull=To Full: -",
        "Values=- | - | -",
        "Reason= | ",
        "Stage3=Stage3 area: - Targets=Targets: - Owners=",
        "Diagnostics=",
        "AllocationLines=[]",
        "AllocationItems=[]",
        "OwnerLinks=[]",
        "RuleTrace=[]",
        "Actions focus=False highlight=False overrideEditor=False anchorEditor=False areaBucket=False notchDetail=False",
        "Override has=False value=5 anchor=False mode=Auto (geometry seed)",
        "Custom value=0 pending=False details=False",
    ];

    private static readonly string[] _expectedCadSnapshot =
    [
        "IsMulti=False SelectedCount=1 Title=CAD pad Subtitle=Single pad",
        "Layer=L3 Name=C CadId=20 DxfIndex=112",
        "Centroid=5, 4 Area=80 mm\u00b2 Vertex=4 Identity=CAD 20 \u00b7 IC 3 \u00b7 Diff idx 112",
        "Match=IC3/diff112 Details=IC3/diff112 (best) Confidence=61% Seed=IC3/diff112",
        "NotchRows=Notch rows: 1 (v2.2) ToRegular=Undo NF (To Regular): 94.10% ToFull=To Full: 155.60% (enabled)",
        "Values=94.1% | 155.6% (Enabled) | 146.40%",
        "Reason=Expansion applied on clear path. | Expansion applied on clear path. (EXPAND_CLEAR_PATH)",
        "Stage3=Stage3 area: 12.3 mm\u00b2 Targets=Targets: 0 effective / 1 total Owners=Owners 2: CAD 31, CAD 32",
        "Diagnostics=IC3/diff112  REG4808\nArea  ov=10, src=10, blk=0, reach=10\nOwners 2 (31, 32)\nShare  50/50\nFlags  boundary=Y, candidate=Y, tofull=Y, blocker=0\nRule   Expansion applied on clear path. (EXPAND_CLEAR_PATH)",
        "AllocationLines=[]",
        "AllocationItems=[Self before To Full/REG 4808/IC3/diff112/8 mm\u00b2/65%/True]",
        "OwnerLinks=[CAD 31 (1 reg) ; CAD 32 (1 reg)]",
        "RuleTrace=[Sequence  Adjusted\nIC-row sequence applied]",
        "Actions focus=True highlight=True overrideEditor=True anchorEditor=True areaBucket=True notchDetail=False",
        "Override has=True value=120 anchor=True mode=Manual override",
        "Custom value=3 pending=False details=False",
    ];

    private static readonly string[] _expectedCadCallbacks =
    [
        "IsMulti=False SelectedCount=1 Title=CAD pad Subtitle=Single pad",
        "Layer=L3 Name=C CadId=21 DxfIndex=7",
        "Centroid=5, 4 Area=80 mm\u00b2 Vertex=4 Identity=CAD 21 \u00b7 Diff idx 7",
        "Match=IC3/diff41 (+1) Details=IC3/diff41 (regId 4809): 75%\nIC2/diff40 (regId 4808): 50% Confidence=- Seed=IC3/diff41 (+1)",
        "NotchRows=Notch rows: unknown (open from selection inspector) ToRegular=Undo NF (To Regular): 50.00% ToFull=To Full: 125.00% (enabled)",
        "Values=50.0% | 125.0% (Enabled) | 150.00%",
        "Reason=Expansion applied on clear path. | Expansion applied on clear path. (EXPAND_CLEAR_PATH)",
        "Stage3=Stage3 area: - Targets=Targets: - Owners=Owners 2: CAD 31, CAD 32",
        "Diagnostics=IC3/diff112  REG4808\nArea  ov=10, src=10, blk=0, reach=10\nOwners 2 (31, 32)\nShare  50/50\nFlags  boundary=Y, candidate=Y, tofull=Y, blocker=0\nRule   Expansion applied on clear path. (EXPAND_CLEAR_PATH)",
        "AllocationLines=[]",
        "AllocationItems=[]",
        "OwnerLinks=[CAD 31 (1 reg) ; CAD 32 (1 reg)]",
        "RuleTrace=[]",
        "Actions focus=True highlight=True overrideEditor=True anchorEditor=True areaBucket=True notchDetail=False",
        "Override has=True value=12 anchor=True mode=Manual override",
        "Custom value=3 pending=False details=False",
    ];

    private static readonly string[] _expectedRegularMulti =
    [
        "IsMulti=True SelectedCount=2 Title=Regular pads Subtitle=Selected: 2 pads",
        "Axis=- - Identity=Regular pads \u00b7 Selected: 2 pads",
        "Row=- Col=- Index=- RowCol=- RowRange=10-11 ColRange=0, 2",
        "Ic=1 - 2 | 1 - 2 Diff=3 - 9 RegularId=100 - 101",
        "Bounds=X 0 - 32\nY 0 - 20 Centroid=Mixed Area=80 - 144 mm\u00b2",
        "Match=Mixed Details=Mixed Confidence=-",
        "Freeform=XWay MatchFreeform=- DiffSource=-",
        "Owners=Owners: - NotchRows=Notch rows: generate Step 5 first.",
        "MatchCards=[]",
        "NotchCards=[]",
        "RuleTrace=[]",
        "Actions navigation=False highlight=False",
        "Size widthMixed=True heightMixed=True width=0 height=0 pending=False details=False",
    ];

    private static readonly string[] _expectedRegularSnapshot =
    [
        "IsMulti=False SelectedCount=1 Title=Regular pad Subtitle=Single pad",
        "Axis=X 4 Y 8 Identity=REG 102 \u00b7 IC 2 \u00b7 Diff idx 99",
        "Row=7 Col=3 Index=102 RowCol=Row 7, Col 3 RowRange=7 ColRange=3",
        "Ic=2 | 2 Diff=99 RegularId=102",
        "Bounds=X 0 - 10\nY 0 - 8 Centroid=5, 4 Area=80 mm\u00b2",
        "Match=CAD 31 Details=CAD 31 (best) Confidence=40%",
        "Freeform=XWay (Step 2 auto-detect) MatchFreeform=Match CAD 31, Freeform XWay (Step 2 auto-detect) DiffSource=Auto",
        "Owners=CAD 34 40%, CAD 32 20.5%, CAD 31 10%, CAD 33 5% (+1) NotchRows=Notch rows: 9 (REG 102)",
        "MatchCards=[CAD 32/Overlap/CAD Output FW diff 132/6 mm\u00b2/REG 60% / CAD 75%/CAD 32 overlap 6 mm\u00b2; regular coverage 60.00%; CAD coverage 75.00%./True ; CAD 31/Overlap/CAD Output FW diff 131/4 mm\u00b2/REG 40% / CAD 50%/CAD 31 overlap 4 mm\u00b2; regular coverage 40.00%; CAD coverage 50.00%./True]",
        "NotchCards=[#1 v2.1 IC1 diff59 CAD 39/Row/FW diff 59/Step 5/v2.1/True ; #2 v2.2 IC3 diff58 CAD 38/Row/FW diff 58/Step 5/v2.2/True ; #3 v2.1 IC2 diff57 CAD 37/Row/FW diff 57/Step 5/v2.1/True ; #4 v2.2 IC1 diff56 CAD 36/Row/FW diff 56/Step 5/v2.2/True ; #5 v2.1 IC3 diff55 CAD 35/Row/FW diff 55/Step 5/v2.1/True ; #6 v2.2 IC2 diff54 CAD -/Row/FW diff 54/Step 5/v2.2/False ; #7 v2.1 IC1 diff53 CAD 33/Row/FW diff 53/Step 5/v2.1/True ; #8 v2.2 IC3 diff52 CAD 32/Row/FW diff 52/Step 5/v2.2/True]",
        "RuleTrace=[Freeform  Applied\nSource: Step 2 auto-detect]",
        "Actions navigation=True highlight=True",
        "Size widthMixed=False heightMixed=False width=10 height=8 pending=False details=False",
    ];

    private static readonly string[] _expectedRegularCallbacks =
    [
        "IsMulti=False SelectedCount=1 Title=Regular pad Subtitle=Single pad",
        "Axis=X 4 Y 12 Identity=REG 102 \u00b7 IC 2 \u00b7 Diff idx 55",
        "Row=11 Col=3 Index=102 RowCol=Row 11, Col 3 RowRange=11 ColRange=3",
        "Ic=2 | 2 Diff=55 RegularId=102",
        "Bounds=X 0 - 10\nY 0 - 8 Centroid=5, 4 Area=80 mm\u00b2",
        "Match=CAD idx 131, 132 (CAD id 31, 32) Details=CAD idx 132: 75%\nCAD idx 131: 50% Confidence=-",
        "Freeform=XWay MatchFreeform=Match CAD idx 131, 132 (CAD id 31, 32), Freeform XWay DiffSource=-",
        "Owners=CAD 34 40%, CAD 32 20.5%, CAD 31 10%, CAD 33 5% (+1) NotchRows=Notch rows: 9 (REG 102)",
        "MatchCards=[CAD 32/Overlap/CAD Output FW diff 132/6 mm\u00b2/REG 60% / CAD 75%/CAD 32 overlap 6 mm\u00b2; regular coverage 60.00%; CAD coverage 75.00%./True ; CAD 31/Overlap/CAD Output FW diff 131/4 mm\u00b2/REG 40% / CAD 50%/CAD 31 overlap 4 mm\u00b2; regular coverage 40.00%; CAD coverage 50.00%./True]",
        "NotchCards=[#1 v2.1 IC1 diff59 CAD 39/Row/FW diff 59/Step 5/v2.1/True ; #2 v2.2 IC3 diff58 CAD 38/Row/FW diff 58/Step 5/v2.2/True ; #3 v2.1 IC2 diff57 CAD 37/Row/FW diff 57/Step 5/v2.1/True ; #4 v2.2 IC1 diff56 CAD 36/Row/FW diff 56/Step 5/v2.2/True ; #5 v2.1 IC3 diff55 CAD 35/Row/FW diff 55/Step 5/v2.1/True ; #6 v2.2 IC2 diff54 CAD -/Row/FW diff 54/Step 5/v2.2/False ; #7 v2.1 IC1 diff53 CAD 33/Row/FW diff 53/Step 5/v2.1/True ; #8 v2.2 IC3 diff52 CAD 32/Row/FW diff 52/Step 5/v2.2/True]",
        "RuleTrace=[]",
        "Actions navigation=True highlight=True",
        "Size widthMixed=False heightMixed=False width=10 height=8 pending=False details=False",
    ];
}
