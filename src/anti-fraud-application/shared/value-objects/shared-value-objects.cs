

namespace AntiFraud.Application.Shared.ValueObjects;

public sealed class DatasetReadinessState
{
    private volatile bool _isReady = false;

    public bool IsReady => _isReady;

    public void MarkAsReady() => _isReady = true;
}