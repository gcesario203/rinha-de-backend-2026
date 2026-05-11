using AntiFraud.Core.MCC.Repository;

namespace AntiFraud.Infrastructure.MCC.Repository;

public sealed class MCCInMemoryRiskRepository : IMCCRepository
{
    private readonly Dictionary<string, float> _mccAverageAmounts;

    public MCCInMemoryRiskRepository(Dictionary<string, float> mccAverageAmounts)
    {
        _mccAverageAmounts = mccAverageAmounts;
    }

    public float GetAverageAmountByMCC(string mcc, float defaultValue = 0.5f)
        => _mccAverageAmounts.TryGetValue(mcc, out var v) ? v : defaultValue;
}
