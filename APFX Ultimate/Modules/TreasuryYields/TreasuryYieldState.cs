namespace cAlgo.Robots;

public sealed class TreasuryYieldState
{
    private readonly Dictionary<string, TreasuryYield> _yields = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TreasuryYield> Yields => _yields;
    public string Status { get; private set; } = "Treasury yields not loaded";
    public int Version { get; private set; }

    public void SetYield(TreasuryYield yield)
    {
        _yields[yield.Maturity] = yield;
        Version++;
    }

    public void SetStatus(string status)
    {
        Status = status;
        Version++;
    }
}

public readonly record struct TreasuryYield(string Maturity, DateTime Date, double Value, double? PreviousValue);

public readonly record struct TreasuryYieldConfiguration(string ApiKey, int RefreshMinutes);
