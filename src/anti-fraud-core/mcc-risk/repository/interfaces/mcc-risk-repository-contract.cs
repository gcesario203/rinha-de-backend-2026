namespace AntiFraud.Core.MCC.Repository;

public interface IMCCRepository
{
    public Task<float> GetAverageAmountByMCC(string mcc, float defaultValue = 0.5f);
}