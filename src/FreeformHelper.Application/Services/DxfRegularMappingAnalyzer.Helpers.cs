using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class DxfRegularMappingAnalyzer
{
    private static class ScoreBuilder
    {
        public static double Compute(CadPad cad, RegularPad reg, IndexMappingSettings settings)
        {
            var inter = Polygon2.IntersectionAreaWithRect(cad.Polygon, reg.Bounds);
            var union = cad.Area + reg.Area - inter;
            var iou = union > 1e-12 ? inter / union : 0.0;

            var dist = cad.Centroid.DistanceTo(reg.Centroid);
            var dx = reg.Bounds.Width;
            var dy = reg.Bounds.Height;
            var diag = Math.Sqrt(dx * dx + dy * dy);
            if (diag <= 1e-12)
            {
                diag = 1.0;
            }

            var distScore = 1.0 - Math.Clamp(dist / diag, 0.0, 1.0);

            var areaMax = Math.Max(cad.Area, reg.Area);
            var areaRatio = areaMax > 1e-12 ? Math.Min(cad.Area, reg.Area) / areaMax : 0.0;

            var wIou = settings.WeightIou;
            var wDist = settings.WeightCentroidDistance;
            var wArea = settings.WeightAreaRatio;
            var wSum = wIou + wDist + wArea;
            if (wSum <= 1e-12)
            {
                wSum = 1.0;
            }

            var score = (wIou * iou + wDist * distScore + wArea * areaRatio) / wSum;
            return Math.Clamp(score, 0.0, 1.0);
        }
    }

    private static class ManualOverrideApplier
    {
        public static void Apply(
            IReadOnlyDictionary<int, int>? manualOverrides,
            IReadOnlyList<CadPad> cadPads,
            IReadOnlyList<RegularPad> regPads,
            IndexMappingSettings settings,
            int[] cadToReg,
            int[] regToCad,
            double[] cadAssignedScore)
        {
            if (manualOverrides is null || manualOverrides.Count == 0)
            {
                return;
            }

            var cadById = new Dictionary<int, int>(cadPads.Count);
            for (var i = 0; i < cadPads.Count; i++)
            {
                cadById[cadPads[i].Id] = i;
            }

            var regularByIndex = new Dictionary<int, int>(regPads.Count);
            for (var i = 0; i < regPads.Count; i++)
            {
                regularByIndex[regPads[i].RegularPadId] = i;
            }

            foreach (var entry in manualOverrides.OrderBy(k => k.Key))
            {
                if (!cadById.TryGetValue(entry.Key, out var cadIndex))
                {
                    continue;
                }

                if (!regularByIndex.TryGetValue(entry.Value, out var regularListIndex))
                {
                    continue;
                }

                if (cadToReg[cadIndex] >= 0 || regToCad[regularListIndex] >= 0)
                {
                    continue;
                }

                cadToReg[cadIndex] = regularListIndex;
                regToCad[regularListIndex] = cadIndex;
                cadAssignedScore[cadIndex] = ScoreBuilder.Compute(cadPads[cadIndex], regPads[regularListIndex], settings);
            }
        }
    }

    private static class CandidateDiagnosticsBuilder
    {
        public static void UpdateTopCandidates(
            List<CandidatePair> topCandidates,
            int cadIndex,
            int regularPadIndex,
            double score,
            int limit)
        {
            if (limit <= 0 || score <= 0)
            {
                return;
            }

            var insertIndex = topCandidates.Count;
            for (var i = 0; i < topCandidates.Count; i++)
            {
                if (score > topCandidates[i].Score)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < topCandidates.Count)
            {
                topCandidates.Insert(insertIndex, new CandidatePair(cadIndex, regularPadIndex, score));
            }
            else if (topCandidates.Count < limit)
            {
                topCandidates.Add(new CandidatePair(cadIndex, regularPadIndex, score));
            }

            if (topCandidates.Count > limit)
            {
                topCandidates.RemoveAt(topCandidates.Count - 1);
            }
        }

        public static List<DxfRegularMappingCandidate> BuildCandidateDiagnostics(
            IReadOnlyList<CandidatePair> topCandidates,
            IReadOnlyList<RegularPad> regPads)
        {
            if (topCandidates.Count == 0)
            {
                return new List<DxfRegularMappingCandidate>(0);
            }

            var result = new List<DxfRegularMappingCandidate>(topCandidates.Count);
            for (var i = 0; i < topCandidates.Count; i++)
            {
                var candidate = topCandidates[i];
                var reg = regPads[candidate.RegularPadIndex];
                result.Add(new DxfRegularMappingCandidate(
                    RegularPadIndex: reg.RegularPadId,
                    IcIndex: reg.IcIndex,
                    DiffIndex: reg.DiffIndex,
                    Score: candidate.Score));
            }

            return result;
        }
    }

    private readonly record struct CandidatePair(int CadIndex, int RegularPadIndex, double Score);
}
