using AntiFraud.Core.MCC.Repository;

namespace AntiFraud.Infrastructure.MCC.Repository;

public class MCCInMemoryRiskRepository : IMCCRepository
{
    private readonly Dictionary<string, float> _mccAverageAmounts;

    public MCCInMemoryRiskRepository(Dictionary<string, float> mccAverageAmounts)
    {
        _mccAverageAmounts = mccAverageAmounts;
    }

    public Task<float> GetAverageAmountByMCC(string mcc, float defaultValue = 0.5f)
    {
        if (_mccAverageAmounts.TryGetValue(mcc, out var averageAmount))
        {
            return Task.FromResult(averageAmount);
        }
        else
        {
            return Task.FromResult(defaultValue);
        }
    }
}