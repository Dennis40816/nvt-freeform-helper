using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed class IndexMappingCandidateViewModel
{
    public IndexMappingCandidateViewModel(int rank, DxfRegularMappingCandidate candidate)
    {
        Rank = rank;
        RegularPadIndex = candidate.RegularPadIndex;
        IcIndex = candidate.IcIndex;
        DiffIndex = candidate.DiffIndex;
        Score = candidate.Score;
    }

    public int Rank { get; }
    public int RegularPadIndex { get; }
    public int IcIndex { get; }
    public int DiffIndex { get; }
    public double Score { get; }
    public string Display => $"#{Rank} diff{DiffIndex} (IC{IcIndex}, regId {RegularPadIndex}), score={Score:0.###}";
}
